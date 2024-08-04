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
    /*
     * Middelware that matches clients real ip addresses against a whitelist
     * and blocks them if they are not on the list
     */
    [MiddlewareImpl(MiddlewareImplOptions.SecurityCritical)]
    internal sealed class IpWhitelistMiddleware(ILogProvider Log, FrozenSet<IPAddress> WhiteList) : IHttpMiddleware
    {
        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity)
        {
            if (!WhiteList.Contains(entity.TrustedRemoteIp))
            {
                Log.Verbose("Client {ip} is not whitelisted, blocked", entity.TrustedRemoteIp);
                return ValueTask.FromResult(FileProcessArgs.Deny);
            }

            return ValueTask.FromResult(FileProcessArgs.Continue);
        }
    }
}