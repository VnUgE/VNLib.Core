/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: Entry.cs 
*
* Entry.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Hashing;
using VNLib.Hashing.Native.MonoCypher;

using VNLib.WebServer.Config;
using VNLib.WebServer.Bootstrap;
using VNLib.WebServer.RuntimeLoading;

namespace VNLib.WebServer
{

    static class Entry
    {
        const string STARTUP_MESSAGE =
@"VNLib.Webserver - runtime host Copyright (C) Vaughn Nugent
This program comes with ABSOLUTELY NO WARRANTY.
Licensing for this software and other libraries can be found at https://www.vaughnnugent.com/resources/software
Starting...
";

        private static readonly DirectoryInfo EXE_DIR = new(Environment.CurrentDirectory);

        private const string DEFAULT_CONFIG_PATH = "config.json";
        internal const string SESSION_TIMEOUT_PROP_NAME = "max_execution_time_ms";
        internal const string TCP_CONF_PROP_NAME = "tcp";
        internal const string LOAD_DEFAULT_HOSTNAME_VALUE = "[system]";
        internal const string PLUGINS_CONFIG_PROP_NAME = "plugins";


        static int Main(string[] args)
        {
            ProcessArguments procArgs = new(args);

            //Print the help menu
            if (args.Length == 0 || procArgs.HasArgument("-h") || procArgs.HasArgument("--help"))
            {
                PrintHelpMenu();
                return 0;
            }

            Console.WriteLine(STARTUP_MESSAGE);

            //Init log config builder
            ServerLogBuilder logBuilder = new();
            logBuilder.BuildForConsole(procArgs);

            //try to load the json configuration file
            IServerConfig? config = LoadConfig(procArgs);
            if (config is null)
            {
                logBuilder.AppLogConfig.CreateLogger().Error("No configuration file was found");
                return -1;
            }

            //Build logs from config
            logBuilder.BuildFromConfig(config.GetDocumentRoot());

            //Create the logger
            using ServerLogger logger = logBuilder.GetLogger();

            //Dump config to console
            if (procArgs.HasArgument("--dump-config"))
            {
                DumpConfig(config.GetDocumentRoot(), logger);
            }

            //Setup the app-domain listener
            InitAppDomainListener(procArgs, logger.AppLog);

#if !DEBUG
            if (procArgs.LogHttp)
            {
                logger.AppLog.Warn("HTTP Logging is only enabled in builds compiled with DEBUG symbols");
            }
#endif

            if (procArgs.ZeroAllocations && !MemoryUtil.Shared.CreationFlags.HasFlag(HeapCreation.GlobalZero))
            {
                logger.AppLog.Debug("Zero allocation flag was set, but the shared heap was not created with the GlobalZero flag, consider enabling zero allocations globally");
            }

            using WebserverBase server = GetWebserver(logger, config, procArgs);

            try
            {
                logger.AppLog.Information("Building service stack, populating service domain...");

                server.Configure();
            }
            catch (ServerConfigurationException sce) when (sce.InnerException is not null)
            {
                logger.AppLog.Fatal("Failed to configure server. Reason: {sce}", sce.InnerException.Message);
                return -1;
            }
            catch (ServerConfigurationException sce)
            {
                logger.AppLog.Fatal("Failed to configure server. Reason: {sce}", sce.Message);
                return -1;
            }
            catch (Exception ex) when (ex.InnerException is ServerConfigurationException sce)
            {
                logger.AppLog.Fatal("Failed to configure server. Reason: {sce}", sce.Message);
                return -1;
            }
            catch (Exception ex)
            {
                logger.AppLog.Fatal(ex, "Failed to configure server");
                return -1;
            }

            logger.AppLog.Verbose("Server configuration stage complete");

            using ManualResetEvent ShutdownEvent = new(false);

            try
            {
                logger.AppLog.Information("Starting services...");

                server.Start();

                logger.AppLog.Information("Service stack started, servers are listening.");

                //Register console cancel to cause cleanup
                Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
                {
                    e.Cancel = true;
                    ShutdownEvent.Set();
                };

                /*
                 * Optional background thread to listen for commands on stdin which 
                 * can also request a server shutdown. 
                 * 
                 * The loop runs in a background thread and will not block the main thread
                 * The loop can request a server shutdown by setting the shutdown event
                 */

                if (!procArgs.HasArgument("--input-off"))
                {
                    CommandListener cmdLoop = new(ShutdownEvent, server, logger.AppLog);

                    Thread consoleListener = new(() => cmdLoop.ListenForCommands(Console.In, Console.Out, name: "stdin"))
                    {
                        IsBackground = true
                    };

                    consoleListener.Start();
                }

                logger.AppLog.Information("Main thread waiting for exit signal, press ctrl + c to exit");

                //Wait for user signal to exit
                ShutdownEvent.WaitOne();

                logger.AppLog.Information("Stopping service stack");

                server.Stop();

                //Wait for all plugins to unload and cleanup (temporary)
                Thread.Sleep(500);

                return 0;
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                logger.AppLog.Fatal("Failed to start servers, address already in use");
                return (int)se.SocketErrorCode;
            }
            catch (SocketException se)
            {
                logger.AppLog.Fatal(se, "Failed to start servers due to a socket exception");
                return (int)se.SocketErrorCode;
            }
            catch (Exception ex)
            {
                logger.AppLog.Fatal(ex, "Failed to start web servers");
            }

            return -1;
        }

        static void PrintHelpMenu()
        {
            const string TEMPLATE =
@$"
    VNLib.Webserver Copyright (C) 2024 Vaughn Nugent

    A high-performance, cross-platform, single process, reference webserver built on the .NET 8.0 Core runtime.

    Option flags:
        --config         <path>     - Specifies the path to the configuration file (relative or absolute)
        --input-off                 - Disables the STDIN listener, no runtime commands will be processed
        --inline-scheduler          - Enables inline scheduling for TCP transport IO processing (not available when using TLS)
        --no-plugins                - Disables loading of dynamic plugins
        --log-http                  - Enables logging of HTTP request and response headers to the system logger (debug builds only)
        --log-transport             - Enables logging of transport events to the system logger (debug builds only)
        --dump-config               - Dumps the JSON configuration to the console during loading
        --compression-off           - Disables dynamic response compression
        --zero-alloc                - Forces all http/tcp memory pool allocations to be zeroed before use (reduced performance)
        --sequential-load           - Loads all plugins sequentially (default is concurrently)
        --no-reuse-socket           - Disables socket reuse for TCP connections (Windows only)
        --reuse-address             - Enables address reuse for TCP connections
        -h, --help                  - Prints this help menu
        -t, --threads    <num>      - Specifies the number of socket accept threads. Defaults to processor count
        -s, --silent                - Disables all console logging
        -v, --verbose               - Enables verbose logging
        -d, --debug                 - Enables debug logging for the process and all plugins
        -vv                         - Enables very verbose logging (attaches listeners for app-domain events and logs them to the output)

    Your configuration file must be a JSON or YAML encoded file and be readable to the process. You may consider keeping it in a safe 
    location outside the application and only readable to this process.

    You should disable hot-reload for production environments, for security and performance reasons.

    You may consider using the --input-off flag to disable STDIN listening for production environments for security reasons.

    Optional environment variables:
        {MemoryUtil.SHARED_HEAP_FILE_PATH} - Specifies the path to the native heap allocator library
        {MemoryUtil.SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV} - Enables heap diagnostics for the shared heap 1 = enabled, 0 = disabled
        {MemoryUtil.SHARED_HEAP_GLOBAL_ZERO} - Enables zeroing of all allocations from the shared heap 1 = enabled, 0 = disabled
        {MemoryUtil.SHARED_HEAP_RAW_FLAGS} - Raw flags to pass to the shared heap allocator's HeapCreate function, hexadeciaml encoded
        {VnArgon2.ARGON2_LIB_ENVIRONMENT_VAR_NAME} - Specifies the path to the Argon2 native library
        {MonoCypherLibrary.MONOCYPHER_LIB_ENVIRONMENT_VAR_NAME} - Specifies the path to the Monocypher native library

    Usage:
        VNLib.Webserver --config <path> ... (other options)     #Starts the server from the configuration (basic usage)

";
            Console.WriteLine(TEMPLATE);
        }

        #region config

        /// <summary>
        /// Initializes the configuration DOM from the specified cmd args 
        /// or the default configuration path
        /// </summary>
        /// <param name="args">The command-line-arguments</param>
        /// <returns>A new <see cref="JsonDocument"/> that contains the application configuration</returns>
        private static IServerConfig? LoadConfig(ProcessArguments args)
        {
            //Get the config path or default config
            string configPath = args.GetArgument("--config") ?? Path.Combine(EXE_DIR.FullName, DEFAULT_CONFIG_PATH);

            return JsonServerConfig.FromFile(configPath);
        }

        private static WebserverBase GetWebserver(ServerLogger logger, IServerConfig config, ProcessArguments procArgs)
        {
            logger.AppLog.Information("Configuring production webserver");
            return new ReleaseWebserver(logger, config, procArgs);
        }

        private static void DumpConfig(JsonElement doc, ServerLogger logger)
        {
            //Dump the config to the console
            using VnMemoryStream ms = new();
            using (Utf8JsonWriter writer = new(ms, new() { Indented = true }))
            {
                doc.WriteTo(writer);
            }

            string json = Encoding.UTF8.GetString(ms.AsSpan());
            logger.AppLog.Information("Dumping configuration to console...\n{c}", json);
        }

        #endregion

        private static void InitAppDomainListener(ProcessArguments args, ILogProvider log)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                log.Fatal("UNHANDLED APPDOMAIN EXCEPTION \n {e}", e);
            };
            //If double verbose is specified, log app-domain messages
            if (args.DoubleVerbose)
            {
                log.Verbose("Double verbose mode enabled, registering app-domain listeners");

                currentDomain.FirstChanceException += delegate (object? sender, FirstChanceExceptionEventArgs e)
                {
                    log.Verbose(e.Exception, "Exception occured in app-domain ");
                };
                currentDomain.AssemblyLoad += delegate (object? sender, AssemblyLoadEventArgs args)
                {
                    log.Verbose(
                        "Assembly loaded {asm} to appdomain {domain} from\n{location}",
                        args.LoadedAssembly.FullName,
                        currentDomain.FriendlyName,
                        args.LoadedAssembly.Location
                    );
                };
                currentDomain.DomainUnload += delegate (object? sender, EventArgs e)
                {
                    log.Verbose("Domain {domain} unloaded", currentDomain.FriendlyName);
                };
            }
        }

    }
}
