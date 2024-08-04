/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: MainServerMiddlware.cs 
*
* MainServerMiddlware.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.WebServer.Middlewares
{

    /// <summary>
    /// Provides required/essential server functionality as a middelware processor
    /// </summary>
    /// <param name="Log"></param>
    /// <param name="VirtualHostOptions"></param>
    internal sealed class MainServerMiddlware(ILogProvider Log, VirtualHostConfig VirtualHostOptions, bool forcePorts) : IHttpMiddleware
    {
        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity)
        {
            //Set special server header
            VirtualHostOptions.TrySetSpecialHeader(entity.Server, SpecialHeaders.Server);

            //Block websocket requests
            if (entity.Server.IsWebSocketRequest)
            {
                Log.Verbose("Client {ip} made a websocket request", entity.TrustedRemoteIp);
            }

            ref readonly TransportSecurityInfo? tlsSecInfo = ref entity.Server.GetTransportSecurityInfo();

            //Check transport security if set
            if (tlsSecInfo.HasValue)
            {

            }

            //If not behind upstream server, uri ports and server ports must match
            bool enforcePortCheck = !entity.IsBehindDownStreamServer && forcePorts;
           
            if (enforcePortCheck && !entity.Server.EnpointPortsMatch())
            {
                Log.Debug("Connection {ip} received on port {p} but the client host port did not match at {pp}",
                    entity.TrustedRemoteIp,
                    entity.Server.LocalEndpoint.Port,
                    entity.Server.RequestUri.Port
                );

                return ValueTask.FromResult(FileProcessArgs.Deny);
            }

            /*
             * downstream server will handle the transport security,
             * if the connection is not from an downstream server 
             * and is using transport security then we can specify HSTS
             */
            if (entity.IsSecure)
            {
                VirtualHostOptions.TrySetSpecialHeader(entity.Server, SpecialHeaders.Hsts);
            }

            //Add response headers from vh config
            for (int i = 0; i < VirtualHostOptions.AdditionalHeaders.Length; i++)
            {
                //Get and append the client header value
                ref KeyValuePair<string, string> header = ref VirtualHostOptions.AdditionalHeaders[i];

                entity.Server.Headers.Append(header.Key, header.Value);
            }

            return ValueTask.FromResult(FileProcessArgs.Continue);
        }
    }
}