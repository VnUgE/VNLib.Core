/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ServerLogger.cs 
*
* ServerLogger.cs is part of VNLib.WebServer which is part of the larger 
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

using VNLib.Utils;

namespace VNLib.WebServer.RuntimeLoading
{
    internal sealed class ServerLogger(VLogProvider applog, VLogProvider syslog, VLogProvider? debuglog) : VnDisposeable
    {

        public VLogProvider AppLog { get; } = applog;

        public VLogProvider SysLog { get; } = syslog;

        public VLogProvider? DebugLog { get; } = debuglog;

        protected override void Free()
        {
            AppLog.Dispose();
            SysLog.Dispose();
            DebugLog?.Dispose();
        }
    }
}
