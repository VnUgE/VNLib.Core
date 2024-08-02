/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: VLogProvider.cs 
*
* VLogProvider.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Linq;
using System.Runtime.CompilerServices;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using VNLib.Utils;
using VNLib.Utils.Logging;

namespace VNLib.WebServer
{
    internal sealed class VLogProvider : VnDisposeable, ILogProvider
    {
        private readonly Logger LogCore;

        public VLogProvider(LoggerConfiguration config)
        {
            LogCore = config.CreateLogger();
        }
        public void Flush() { }

        public object GetLogProvider() => LogCore;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEnabled(LogLevel level) => LogCore.IsEnabled((LogEventLevel)level);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(LogLevel level, string value)
        {
            LogCore.Write((LogEventLevel)level, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(LogLevel level, Exception exception, string value = "")
        {
            LogCore.Write((LogEventLevel)level, exception, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(LogLevel level, string value, params object[] args)
        {
            LogCore.Write((LogEventLevel)level, value, args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(LogLevel level, string value, params ValueType[] args)
        {
            //Serilog logger supports passing valuetypes to avoid boxing objects
            if (LogCore.IsEnabled((LogEventLevel)level))
            {
                object[] ar = args.Select(a => (object)a).ToArray();
                LogCore.Write((LogEventLevel)level, value, ar);
            }
        }

        protected override void Free() => LogCore.Dispose();
    }
}
