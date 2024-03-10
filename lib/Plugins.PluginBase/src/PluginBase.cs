/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.PluginBase
* File: PluginBase.cs 
*
* PluginBase.cs is part of VNLib.Plugins.PluginBase which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.PluginBase is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.PluginBase is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.PluginBase. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Serilog;

using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Attributes;

namespace VNLib.Plugins
{

    /// <summary>
    /// Provides a concrete base class for <see cref="IPlugin"/> instances using the Serilog logging provider.
    /// Accepts the standard plugin <see cref="JsonDocument"/> configuration constructors
    /// </summary>
    public abstract class PluginBase : MarshalByRefObject, IPluginTaskObserver, IPlugin
    {
        /*
         * CTS exists for the life of the plugin, its resources are never disposed
         * such not to disturb late running tasks that depend on the cts's state
         */
        private readonly CancellationTokenSource Cts = new();

        private readonly LinkedList<Task> DeferredTasks = new();
        private readonly LinkedList<ServiceExport> _services = new();

        /// <summary>
        /// A cancellation token that is cancelled when the plugin has been unloaded
        /// </summary>
        public CancellationToken UnloadToken => Cts.Token;

        /// <summary>
        /// The property name of the host/global configuration element in the plugin
        /// runtime supplied configuration object.
        /// </summary>
        protected virtual string GlobalConfigDomPropertyName => "host";

        /// <summary>
        /// The property name of the plugin configuration element in the plugin
        /// runtime supplied configuration object.
        /// </summary>
        protected virtual string PluginConfigDomPropertyName => "plugin";

        /// <summary>
        /// The logging instance
        /// </summary>
        public ILogProvider Log { get; private set; }

        /// <summary>
        /// If passed by the host application, the configuration file of the host application and plugin
        /// </summary>
        protected JsonDocument Configuration { get; private set; }

        /// <summary>
        /// The configuration data from the host application
        /// </summary>
        public JsonElement HostConfig => Configuration.RootElement.GetProperty(GlobalConfigDomPropertyName);

        /// <summary>
        /// The configuration data from the plugin's config file passed by the host application
        /// </summary>
        public JsonElement PluginConfig => Configuration.RootElement.GetProperty(PluginConfigDomPropertyName);

        /// <summary>
        /// The collection of exported services that will be published to the host 
        /// application
        /// </summary>
        public ICollection<ServiceExport> Services => _services;

        /// <inheritdoc/>
        public abstract string PluginName { get; }

        /// <summary>
        /// The file/console log template 
        /// </summary>
        protected virtual string LogTemplate => $"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}} [{{Level:u3}}] {PluginName}: {{Message:lj}}{{NewLine}}{{Exception}}";

        /// <summary>
        /// Arguments passed to the plugin by the host application
        /// </summary>
        public ArgumentList HostArgs { get; private set; }

        /// <summary>
        /// The host application may invoke this method when the assembly is loaded and this plugin is constructed to pass
        /// a configuration object to the instance. This method populates the configuration objects if applicable.
        /// </summary>
        [ConfigurationInitalizer]
        public virtual void InitConfig(ReadOnlySpan<byte> config)
        {
            if (config.IsEmpty)
            {
                throw new ArgumentNullException(nameof(config));
            }

            //reader for the config value
            Utf8JsonReader reader = new(config);
            
            //Parse the config
            Configuration = JsonDocument.ParseValue(ref reader);
        }
       
        /// <summary>
        /// Responsible for initalizing the log provider. The host should invoke this method
        /// directly after the configuration is initialized
        /// </summary>
        /// <param name="cmdArgs"></param>
        [LogInitializer]
        public virtual void InitLog(string[] cmdArgs)
        {
            HostArgs = new(cmdArgs);
            //Open new logger config
            LoggerConfiguration logConfig = new();
            //Check for verbose
            if (HostArgs.HasArgument("-v") || HostArgs.HasArgument("--verbose"))
            {
                logConfig.MinimumLevel.Verbose();
            }
            //Check for debug mode
            else if (HostArgs.HasArgument("-d") || HostArgs.HasArgument("--debug"))
            {
                logConfig.MinimumLevel.Debug();
            }
            //Default to information
            else
            {
                logConfig.MinimumLevel.Information();
            }

            //Init console log
            InitConsoleLog(logConfig);

            //Init file log
            InitFileLog(logConfig);

            //Open logger
            Log = new VLogProvider(logConfig);
        }

        private void InitConsoleLog(LoggerConfiguration logConfig)
        {
            //If silent arg is not specified, open log to console
            if (!(HostArgs.HasArgument("--silent") || HostArgs.HasArgument("-s")))
            {
                _ = logConfig.WriteTo.Console(outputTemplate: LogTemplate, formatProvider:null);
            }
        }

        private void InitFileLog(LoggerConfiguration logConfig)
        {
            string filePath = null;
            string template = null;

            TimeSpan flushInterval = TimeSpan.FromSeconds(10);
            
            int retainedLogs = 31;
            //Default to 500mb log file size
            int fileSizeLimit = 500 * 1000 * 1024;
            RollingInterval interval = RollingInterval.Infinite;

            /*
             * Try to get login configuration from the plugin configuration, otherwise fall
             * back to the application logger
             */
            if (PluginConfig.TryGetProperty("log", out JsonElement logEl) ||
                HostConfig.TryGetProperty("app_log", out logEl)
            )
            {
                //User may want to disable plugin logging
                if (logEl.TryGetProperty("disabled", out JsonElement disEl) && disEl.GetBoolean())
                {
                    return;
                }

                IReadOnlyDictionary<string, JsonElement> conf = logEl.EnumerateObject().ToDictionary(static s => s.Name, static s => s.Value);

                filePath = conf.GetPropString("path");
                template = conf.GetPropString("template");

                if (conf.TryGetValue("flush_sec", out JsonElement flushEl))
                {
                    flushInterval = flushEl.GetTimeSpan(TimeParseType.Seconds);
                }

                if (conf.TryGetValue("retained_files", out JsonElement retainedEl))
                {
                    retainedLogs = retainedEl.GetInt32();
                }

                if (conf.TryGetValue("file_size_limit", out JsonElement sizeEl))
                {
                    fileSizeLimit = sizeEl.GetInt32();
                }

                if (conf.TryGetValue("interval", out JsonElement intervalEl))
                {
                    interval = Enum.Parse<RollingInterval>(intervalEl.GetString()!, true);
                }

                /*
                 * If the user specified a log file path, replace the name of the log file with the 
                 * plugin name, leave the directory and file extension the same
                 */
                if(filePath != null)
                {                   
                    string appLogName = Path.GetFileNameWithoutExtension(filePath);

                    filePath = filePath.Replace(appLogName, PluginName, StringComparison.Ordinal);
                }

                //Default to exe dir if not set
                filePath ??= Path.Combine(Environment.CurrentDirectory, $"{PluginName}.txt");
                template ??= LogTemplate;

                //Configure the log file writer
                logConfig.WriteTo.File(filePath,
                    buffered: true,
                    retainedFileCountLimit: retainedLogs,
                    formatProvider: null,
                    fileSizeLimitBytes: fileSizeLimit,
                    rollingInterval: interval,
                    outputTemplate: template,
                    flushToDiskInterval: flushInterval
                );
            }
        }

        /// <summary>
        /// When overriden handles a console command
        /// </summary>
        /// <param name="cmd"></param>
        [ConsoleEventHandler]
        public void HandleCommand(string cmd)
        {
            try
            {
                ProcessHostCommand(cmd);
            }
            catch(Exception ex)
            {
                Log.Error(ex);
            }
        }

        /// <summary>
        /// Invoked when the host process has a command message to send 
        /// </summary>
        /// <param name="cmd">The command message</param>
        protected abstract void ProcessHostCommand(string cmd);

        ///<inheritdoc/>
        void IPlugin.PublishServices(IPluginServicePool pool) => OnPublishServices(pool);

        ///<inheritdoc/>
        void IPlugin.Load()
        {
            //Setup empty log if not specified
            Log ??= new VLogProvider(new());
            //Default logger before loading
            Configuration ??= JsonDocument.Parse("{}");
            try
            {
                //Try to load the plugin and cleanup since the plugin failed to load
                OnLoad();
            }
            catch
            {
                //Cancel the token
                Cts.Cancel();
                
                //Cleanup
                CleanupPlugin();
                
                throw;
            }
        }

        ///<inheritdoc/>
        void IPlugin.Unload()
        {
            try 
            {
                //Cancel the token
                Cts.Cancel();
                
                //Call unload impl
                OnUnLoad();

                //Wait for bg tasks
                WaitForTasks();
            }
            finally
            {
                CleanupPlugin();
            }
        }

        private void CleanupPlugin()
        {
            //Dispose the config document
            Configuration?.Dispose();
            //dispose the log
            (Log as IDisposable)?.Dispose();
            //Remove any services
            _services.Clear();
            //empty deffered array
            DeferredTasks.Clear();
        }

        private void WaitForTasks()
        {
            const int WARNING_INTERVAL = 1500;

            void OnTimerElapsed(object state)
            {
                //Write time errors to log
                Log.Warn("One or more deferred background tasks are taking a long time to complete");
            }

            if(DeferredTasks.Count > 0)
            {
                //Startup timer to warn if tasks are taking a long time to complete
                using Timer t = new(OnTimerElapsed, this, WARNING_INTERVAL, WARNING_INTERVAL);
                
                Task[] tasks;
                lock (DeferredTasks)
                {
                    //Copy tasks to array
                    tasks = DeferredTasks.ToArray();
                }
                
                //Wait for all tasks to complete for a maxium of 10 seconds
                if(!Task.WaitAll(tasks, TimeSpan.FromSeconds(10)))
                {
                    Log.Error("Tasks failed to complete in the allotted timeout period");
                }
            }
        }

        ///<inheritdoc/>
        public void ObserveTask(Task task)
        {
            lock (DeferredTasks)
            {
                DeferredTasks.AddFirst(task);
            }
        }

        ///<inheritdoc/>
        public void RemoveObservedTask(Task task)
        {
            lock (DeferredTasks)
            {
                DeferredTasks.Remove(task);
            }
        }

        /// <summary>
        /// <para>
        /// Invoked when the host loads the plugin instance
        /// </para>
        /// <para>
        /// All endpoints must be routed before this method returns
        /// </para>
        /// </summary>
        protected abstract void OnLoad();
        
        /// <summary>
        /// Invoked when all endpoints have been removed from service. All managed and unmanged resources should be released.
        /// </summary>
        protected abstract void OnUnLoad();

        /// <summary>
        /// Invoked when the host requests the plugin to publish services
        /// <para>
        /// If overriden, the base implementation must be called to 
        /// publish all services in the internal service collection
        /// </para>
        /// </summary>
        /// <param name="pool">The pool to publish services to</param>
        protected virtual void OnPublishServices(IPluginServicePool pool)
        {
            //Publish all services then cleanup
            foreach (ServiceExport export in _services)
            {
                pool.ExportService(export.ServiceType, export.Service, export.Flags);
            }
        }
    }
}