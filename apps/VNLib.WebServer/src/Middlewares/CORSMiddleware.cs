/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: CORSMiddleware.cs 
*
* CORSMiddleware.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Net;
using System.Threading.Tasks;
using System.Collections.Frozen;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Essentials.Middleware;

using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer.Middlewares
{

    /// <summary>
    /// Adds HTTP CORS protection to http servers
    /// </summary>
    /// <param name="Log"></param>
    /// <param name="VirtualHostOptions"></param>
    [MiddlewareImpl(MiddlewareImplOptions.SecurityCritical)]
    internal sealed class CORSMiddleware(ILogProvider Log, CorsSecurityConfig secConfig) : IHttpMiddleware
    {
        private readonly FrozenSet<string> _corsAuthority = secConfig.AllowedCorsAuthority.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity)
        {
            //Check coors enabled
            bool isCors = entity.Server.IsCors();
            bool isCrossSite = entity.Server.IsCrossSite();

            /*
             * Deny/allow cross site/cors requests at the site-level
             */
            if (!secConfig.DenyCorsCons)
            {
                //Confirm the origin is allowed during cors connections
                if (entity.Server.CrossOrigin && _corsAuthority.Count > 0)
                {
                    //If the authority is not allowed, deny the connection
                    if (!_corsAuthority.Contains(entity.Server.Origin!.Authority))
                    {
                        Log.Debug("Denied a connection from a cross-origin site {s}, the origin was not whitelisted", entity.Server.Origin);
                        return ValueTask.FromResult(FileProcessArgs.Deny);
                    }
                }

                if (isCors)
                {
                    //set the allow credentials header
                    entity.Server.Headers["Access-Control-Allow-Credentials"] = "true";

                    //If cross site flag is set, or the connection has cross origin flag set, set explicit origin
                    if (entity.Server.CrossOrigin || isCrossSite && entity.Server.Origin != null)
                    {
                        entity.Server.Headers["Access-Control-Allow-Origin"] = $"{entity.Server.RequestUri.Scheme}://{entity.Server.Origin!.Authority}";
                        //Add origin to the response vary header when setting cors origin
                        entity.Server.Headers.Append(HttpResponseHeader.Vary, "Origin");
                    }
                }

                //Add sec vary headers for cors enabled sites
                entity.Server.Headers.Append(HttpResponseHeader.Vary, "Sec-Fetch-Dest,Sec-Fetch-Mode,Sec-Fetch-Site");
            }
            else if (isCors | isCrossSite)
            {
                Log.Verbose("Denied a cross-site/cors request from {con} because this site does not allow cross-site/cors requests", entity.TrustedRemoteIp);
                return ValueTask.FromResult(FileProcessArgs.Deny);
            }

            //If user-navigation is set and method is get, make sure it does not contain object/embed
            if (entity.Server.IsNavigation() && entity.Server.Method == HttpMethod.GET)
            {
                string? dest = entity.Server.Headers["sec-fetch-dest"];
                if (dest != null && (dest.Contains("object", StringComparison.OrdinalIgnoreCase) || dest.Contains("embed", StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Debug("Denied a browser navigation request from {con} because it contained an object/embed", entity.TrustedRemoteIp);
                    return ValueTask.FromResult(FileProcessArgs.Deny);
                }
            }

            //If the connection is a cross-site, then an origin header must be supplied
            if (isCrossSite && entity.Server.Origin is null)
            {
                Log.Debug("Denied cross-site request because origin header was not supplied");
                return ValueTask.FromResult(FileProcessArgs.Deny);
            }

            //If same origin is supplied, enforce origin header on post/options/put/patch
            if (string.Equals("same-origin", entity.Server.Headers["Sec-Fetch-Site"], StringComparison.OrdinalIgnoreCase))
            {
                //If method is not get/head, then origin is required
                if ((entity.Server.Method & (HttpMethod.GET | HttpMethod.HEAD)) == 0 && entity.Server.Origin == null)
                {
                    Log.Debug("Denied same-origin POST/PUT... request because origin header was not supplied");
                    return ValueTask.FromResult(FileProcessArgs.Deny);
                }
            }

            if(!IsSessionSecured(entity))
            {
                return ValueTask.FromResult(FileProcessArgs.Deny);
            }

            return ValueTask.FromResult(FileProcessArgs.Continue);
        }

        private bool IsSessionSecured(HttpEntity entity)
        {
            ref readonly SessionInfo session = ref entity.Session;

            /*
             * When sessions are created for connections that come from a different 
             * origin, their origin is stored for later. 
             * 
             * If the session was created from a different origin or the current connection
             * is cross origin, then the origin must be allowed by the configuration
             */

            //No session loaded, nothing to check
            if (!session.IsSet)
            {
                return true;
            }

            if (entity.Server.Origin is null)
            {
                return true;
            }          

            if (session.IsNew || session.SessionType != SessionType.Web)
            {
                return true;
            }

            bool sameOrigin = string.Equals(
                entity.Server.Origin.Authority,
                session.SpecifiedOrigin?.Authority,
                StringComparison.OrdinalIgnoreCase
            );

            if (sameOrigin || _corsAuthority.Contains(entity.Server.Origin.Authority))
            {
                return true;
            }

            Log.Debug("Denied connection from {0} because the user's origin {org} changed to {other} and is not whitelisted.",
                entity.TrustedRemoteIp,
                session.SpecifiedOrigin?.Authority,
                entity.Server.Origin.Authority
            );

            return false;
        }
    }
}