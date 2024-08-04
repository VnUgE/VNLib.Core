/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ConnectionLogMiddleware.cs 
*
* ConnectionLogMiddleware.cs is part of VNLib.WebServer which is part of the larger 
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

/*
 *  Provides an Nginx-style log of incoming connections.
 */

using System.Threading.Tasks;
using System.Security.Authentication;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.WebServer.Middlewares
{
    internal sealed class ConnectionLogMiddleware(ILogProvider Log) : IHttpMiddleware
    {
        const string template = @"{ip} - {usr} [{local_time}] {tls} '{method} {url} {http_version}' {hostname} '{refer}' '{user_agent}' '{forwarded_for}'";

        ///<inheritdoc/>
        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity)
        {
            if (Log.IsEnabled(LogLevel.Information))
            {
                string userId = string.Empty;
                if (entity.Session.IsSet)
                {
                    userId = entity.Session.UserID;
                }

                Log.Information(template,
                    entity.TrustedRemoteIp,
                    userId,
                    entity.RequestedTimeUtc.ToLocalTime().ToString("dd/MMM/yyyy:HH:mm:ss zzz", null),
                    GetTlsInfo(entity),
                    entity.Server.Method,
                    entity.Server.RequestUri.PathAndQuery,
                    GetProtocolVersionString(entity),
                    entity.RequestedRoot.Hostname,
                    entity.Server.Referer,
                    entity.Server.UserAgent,
                    entity.Server.Headers["X-Forwarded-For"] ?? string.Empty
                );
            }

            return ValueTask.FromResult(FileProcessArgs.Continue);
        }

        static string GetProtocolVersionString(HttpEntity entity)
        {
            return entity.Server.ProtocolVersion switch
            {
                HttpVersion.Http09 => "HTTP/0.9",
                HttpVersion.Http1 => "HTTP/1.0",
                HttpVersion.Http11 => "HTTP/1.1",
                HttpVersion.Http2 => "HTTP/2.0",
                HttpVersion.Http3 => "HTTP/3.0",
                _ => "HTTP/1.1"
            };
        }

        static string GetTlsInfo(HttpEntity entity)
        {
            ref readonly TransportSecurityInfo? secInfo = ref entity.Server.GetTransportSecurityInfo();

            if(!secInfo.HasValue)
            {
                return string.Empty;
            }

#pragma warning disable CA5398, CA5397, SYSLIB0039 // Avoid hardcoded SslProtocols values

            return secInfo.Value.SslProtocol switch
            {
                SslProtocols.Tls => "TLSv1.0",
                SslProtocols.Tls11 => "TLSv1.1",
                SslProtocols.Tls12 => "TLSv1.2",
                SslProtocols.Tls13 => "TLSv1.3",
                _ => "Unknown"
            };

#pragma warning restore CA5397, CA5398, SYSLIB0039 // Do not use deprecated SslProtocols values
        }
    }
}