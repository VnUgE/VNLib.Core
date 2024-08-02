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
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

using Serilog;

using VNLib.Utils.Extensions;

namespace VNLib.WebServer.RuntimeLoading
{
    internal sealed class ServerLogBuilder
    {
        public LoggerConfiguration SysLogConfig { get; }
        public LoggerConfiguration AppLogConfig { get; }
        public LoggerConfiguration? DebugConfig { get; }

        public ServerLogBuilder()
        {
            AppLogConfig = new();
            SysLogConfig = new();
        }

        public ServerLogBuilder BuildForConsole(ProcessArguments args)
        {
            InitConsoleLog(args, AppLogConfig, "Application");
            InitConsoleLog(args, SysLogConfig, "System");
            return this;
        }

        public ServerLogBuilder BuildFromConfig(JsonElement logEl)
        {
            InitSingleLog(logEl, "app_log", "Application", AppLogConfig);
            InitSingleLog(logEl, "sys_log", "System", SysLogConfig);
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

        private static void InitSingleLog(JsonElement el, string elPath, string logName, LoggerConfiguration logConfig)
        {
            string? filePath = null;
            string? template = null;

            TimeSpan flushInterval = TimeSpan.FromSeconds(10);
            int retainedLogs = 31;
            //Default to 500mb log file size
            int fileSizeLimit = 500 * 1000 * 1024;
            RollingInterval interval = RollingInterval.Infinite;

            //try to get the log config object
            if (el.TryGetProperty(elPath, out JsonElement logEl))
            {
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

                //Set default objects
                filePath ??= Path.Combine(Environment.CurrentDirectory, $"{elPath}.txt");
                template ??= $"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}} [{{Level:u3}}] {logName} {{Message:lj}}{{NewLine}}{{Exception}}";

                //Configure the log file writer
                logConfig.WriteTo.File(filePath,
                    buffered: true,
                    retainedFileCountLimit: retainedLogs,
                    formatProvider:null,
                    fileSizeLimitBytes: fileSizeLimit,
                    rollingInterval: interval,
                    outputTemplate: template,
                    flushToDiskInterval: flushInterval);
            }

            //If the log element is not specified in config, do not write log files
        }
    }
}
