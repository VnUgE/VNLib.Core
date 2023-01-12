/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.PluginBase
* File: VLogProvider.cs 
*
* VLogProvider.cs is part of VNLib.Plugins.PluginBase which is part of the larger 
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
using System.Linq;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using VNLib.Utils;
using VNLib.Utils.Logging;

namespace VNLib.Plugins
{
    /// <summary>
    /// Provides a concrete <see cref="ILogProvider"/> instance for writing events to a <see cref="Log"/> sink
    /// </summary>
    public class VLogProvider : VnDisposeable, ILogProvider
    {
        private readonly Logger LogCore;

        /// <summary>
        /// Creates a new <see cref="ILogger"/> from the specified <see cref="LoggerConfiguration"/>
        /// </summary>
        /// <param name="config">Configuration to generate the logger from</param>
        public VLogProvider(LoggerConfiguration config) => LogCore = config.CreateLogger();
        ///<inheritdoc/>
        public void Flush() { }
        ///<inheritdoc/>
        public object GetLogProvider() => LogCore;
        ///<inheritdoc/>
        public void Write(LogLevel level, string value)
        {
            LogCore.Write((LogEventLevel)level, value);
        }
        ///<inheritdoc/>
        public void Write(LogLevel level, Exception exception, string value = "")
        {
            LogCore.Write((LogEventLevel)level, exception, value);
        }
        ///<inheritdoc/>
        public void Write(LogLevel level, string value, params object[] args)
        {
            LogCore.Write((LogEventLevel)level, value, args);
        }
        ///<inheritdoc/>
        public void Write(LogLevel level, string value, params ValueType[] args)
        {
            //Since call with box values, only call if the log level is enabled
            if (LogCore.IsEnabled((LogEventLevel)level))
            {
                LogCore.Write((LogEventLevel)level, value, args.Select(static s => (object)s).ToArray());
            }
        }
        ///<inheritdoc/>
        protected override void Free()
        {
            LogCore.Dispose();
        }

    }
}