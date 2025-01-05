/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ServerLogBuilder.cs 
*
* ServerLogBuilder.cs is part of VNLib.WebServer which is part of the larger 
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

using Serilog;

using VNLib.WebServer.Config;
using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer.RuntimeLoading
{
    internal sealed class ServerLogBuilder
    {
        public LoggerConfiguration SysLogConfig { get; } = new();

        public LoggerConfiguration AppLogConfig { get; } = new();

        public LoggerConfiguration? DebugConfig { get; }


        public ServerLogBuilder BuildForConsole(ProcessArguments args)
        {
            InitConsoleLog(args, AppLogConfig, "Application");
            InitConsoleLog(args, SysLogConfig, "System");
            return this;
        }

        public ServerLogBuilder BuildFromConfig(IServerConfig config)
        {
            InitSingleLog(config, "logs::app_log", "Application", AppLogConfig);
            InitSingleLog(config, "logs::sys_log", "System", SysLogConfig);
            return this;
        }

        public ServerLogger GetLogger()
        {
            //Return logger
            return new (
                new(AppLogConfig),
                new(SysLogConfig),
                DebugConfig == null ? null : new(DebugConfig)
            );
        }

        private static void InitConsoleLog(ProcessArguments args, LoggerConfiguration conf, string logName)
        {
            //Set verbosity level, defaul to informational
            if (args.Verbose)
            {
                conf.MinimumLevel.Verbose();
            }
            else if (args.Debug)
            {
                conf.MinimumLevel.Debug();
            }
            else
            {
                conf.MinimumLevel.Information();
            }

            //Setup loggers to write to console unless the -s silent arg is set
            if (!args.Silent)
            {
                string template = $"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}} [{{Level:u3}}] {logName} {{Message:lj}}{{NewLine}}{{Exception}}";
                _ = conf.WriteTo.Console(outputTemplate: template);
            }
        }

        private static void InitSingleLog(IServerConfig config, string elPath, string logName, LoggerConfiguration logConfig)
        {
            LogConfig? conf = config.GetConfigProperty<LogConfig>(elPath);
            if(conf is null || !conf.Enabled)
            {
                return;
            }

            //Default path if the user did not set one or set it to null
            conf.Path ??= Path.Combine(Environment.CurrentDirectory, $"{elPath}.txt");
           
            conf.Template ??= $"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}} [{{Level:u3}}] {logName} {{Message:lj}}{{NewLine}}{{Exception}}";

            //Configure the log file writer
            logConfig.WriteTo.File(
                path: conf.Path,
                buffered: true,
                retainedFileCountLimit: conf.RetainedFiles,
                formatProvider: null,
                fileSizeLimitBytes: conf.FileSizeLimit,
                rollingInterval: Enum.Parse<RollingInterval>(conf.Interval, ignoreCase: true),
                outputTemplate: conf.Template,
                flushToDiskInterval: TimeSpan.FromSeconds(conf.FlushIntervalSeconds)
            );

            //If the log element is not specified in config, do not write log files
        }
    }
}
