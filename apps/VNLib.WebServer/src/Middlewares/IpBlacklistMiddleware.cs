/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: WhitelistMiddleware.cs 
*
* WhitelistMiddleware.cs is part of VNLib.WebServer which is part of the larger 
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

using System.Net;
using System.Threading.Tasks;
using System.Collections.Frozen;

using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.WebServer.Middlewares
{
    [MiddlewareImpl(MiddlewareImplOptions.SecurityCritical)]
    internal sealed class IpBlacklistMiddleware(ILogProvider Log, FrozenSet<IPAddress> Blacklist) : IHttpMiddleware
    {
        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity)
        {
            if (Blacklist.Contains(entity.TrustedRemoteIp))
            {
                Log.Verbose("Client {ip} is blacklisted, blocked", entity.TrustedRemoteIp);
                return ValueTask.FromResult(FileProcessArgs.Deny);
            }

            return ValueTask.FromResult(FileProcessArgs.Continue);
        }
    }
}