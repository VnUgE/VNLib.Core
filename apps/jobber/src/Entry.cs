/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: Entry.cs
*
* Entry.cs is part of jobber which is part of the larger 
* VNLib collection of libraries and utilities.
 *
* jobber is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* jobber is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with jobber. If not, see http://www.gnu.org/licenses/.
 */


using System;
using System.Threading;

using Jobber.Config;
using Jobber.Runtime;

using Serilog;
using Serilog.Core;

namespace Jobber
{

    internal static class Entry
    {

        private const string STARTUP =
@"jobber - lightweight multi-service supervisor
Copyright (c) 2025 Vaughn Nugent
Starting...
";

        private const string HELP_TEXT = @"
jobber usage:
    --config <path>        Path to config file (.json | .yaml/.yml)
    --quiet | -q           Suppress console output from services (still tees to files)
    --wait <service>       Override primary service to wait on
    --stop-timeout <sec>   Override stop_timeout_sec
    --list                 List services and exit
    --dry-run              Parse and validate only
    -v / --verbose         Verbose logging
    -d / --debug           Debug logging
    -h / --help            Help
";

        internal static ILogger Logger { get; private set; } = null!;

        internal static ProcessArguments Args { get; private set; } = null!;

        internal static int Main(string[] args)
        {
            Args = new ProcessArguments(args);

            if (args.Length == 0 || Args.HasArgument("-h") || Args.HasArgument("--help"))
            {
                Console.WriteLine(HELP_TEXT);
                return 0;
            }

            Console.WriteLine(STARTUP);

            Logger = CreateLogger(Args);

            JsonJobberConfig? provider = JsonJobberConfig.FromFile(Args.ConfigPath ?? "jobber.json");
            if (provider == null)
            {
                Logger.Fatal("Failed to load configuration");
                return -1;
            }

            if (Args.List)
            {
                
                return 0;
            }

            using CancellationTokenSource cts = new ();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                //TODO implement service handler
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Fatal error");
                return -1;
            }
            finally
            {
                Logger.Information("Shutting down logging");
                if (Logger is IDisposable disp)
                {
                    disp.Dispose();
                }
            }
        }

        private static Logger CreateLogger(ProcessArguments args)
        {
            // Configure Serilog
            LoggerConfiguration cfg = new ();

            if (Args.Verbose)
            {
                cfg.MinimumLevel.Verbose();
            }
            else if (Args.Debug)
            {
                cfg.MinimumLevel.Debug();
            }
            else
            {
                cfg.MinimumLevel.Information();
            }

            // Console sink may not be present in every environment; guard it.
            _ = cfg.WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] jobber {Message:lj}{NewLine}{Exception}");

            return cfg.CreateLogger();
        }

    }
}