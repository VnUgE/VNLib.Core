/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: HttpEntity.cs 
*
* HttpEntity.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;

/*
 * HttpEntity was converted to an object as during profiling
 * it was almost always heap allcated due to async opertaions
 * or other object tracking issues. So to reduce the number of
 * allocations (at the cost of larger objects) basic profiling 
 * showed less GC load and less collections when SessionInfo 
 * remained a value type
 */
#pragma warning disable CA1051 // Do not declare visible instance fields

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// A container for an <see cref="HttpEvent"/> with its attached session.
    /// This class cannot be inherited.
    /// </summary>
    public sealed class HttpEntity : IHttpEvent
    {

        /// <summary>
        /// The connection event entity
        /// </summary>
        private readonly IHttpEvent Entity;

        private readonly CancellationTokenSource EventCts;

        public HttpEntity(IHttpEvent entity, IWebProcessor root)
        {
            Entity = entity;
            RequestedRoot = root;
            //Init event cts
            EventCts = new(root.Options.ExecutionTimeout);
          
            //See if the connection is coming from an downstream server
            IsBehindDownStreamServer = root.Options.DownStreamServers.Contains(entity.Server.RemoteEndpoint.Address);
            /*
            * If the connection was behind a trusted downstream server, 
            * we can trust the x-forwarded-for header,
            * otherwise use the remote ep ip address
            */
            TrustedRemoteIp = entity.Server.GetTrustedIp(IsBehindDownStreamServer);
            //Local connection
            IsLocalConnection = entity.Server.LocalEndpoint.Address.IsLocalSubnet(TrustedRemoteIp);
            //Cache value
            IsSecure = entity.Server.IsSecure(IsBehindDownStreamServer);

            //Cache current time
            RequestedTimeUtc = DateTimeOffset.UtcNow;
        }

        private SessionInfo _session;
        internal FileProcessArgs EventArgs;
        internal SessionHandle EventSessionHandle;

        /// <summary>
        /// Internal call to attach a new session to the entity from the 
        /// internal session handle
        /// </summary>
        internal void AttachSession()
        {
            if (EventSessionHandle.IsSet)
            {
                _session = new(EventSessionHandle.SessionData!, Entity.Server, TrustedRemoteIp);
            }
        }

        /// <summary>
        /// Internal call to cleanup any internal resources
        /// </summary>
        internal void Dispose()
        {
            EventCts.Dispose();
        }

        /// <summary>
        /// A token that has a scheduled timeout to signal the cancellation of the entity event
        /// </summary>
        public CancellationToken EventCancellation => EventCts.Token;

        /// <summary>
        /// The session associated with the event
        /// </summary>
        public ref readonly SessionInfo Session => ref _session;

        /// <summary>
        /// A value that indicates if the connecion came from a trusted downstream server
        /// </summary>
        public readonly bool IsBehindDownStreamServer;

        /// <summary>
        /// Determines if the connection came from the local network to the current server
        /// </summary>
        public readonly bool IsLocalConnection;

        /// <summary>
        /// Gets a value that determines if the connection is using tls, locally 
        /// or behind a trusted downstream server that is using tls.
        /// </summary>
        public readonly bool IsSecure;

        /// <summary>
        /// Caches a <see cref="DateTimeOffset"/> that was created when the connection was created.
        /// The approximate current UTC time
        /// </summary>
        public readonly DateTimeOffset RequestedTimeUtc;

        /// <summary>
        /// The connection info object assocated with the entity
        /// </summary>
        public IConnectionInfo Server => Entity.Server;

        /// <summary>
        /// User's ip. If the connection is behind a local proxy, returns the users actual IP. Otherwise returns the connection ip. 
        /// </summary>
        public readonly IPAddress TrustedRemoteIp;

        /// <summary>
        /// The requested web root. Provides additional site information
        /// </summary>
        public readonly IWebProcessor RequestedRoot;

        /// <summary>
        /// If the request has query arguments they are stored in key value format
        /// </summary>
        public IReadOnlyDictionary<string, string> QueryArgs => Entity.QueryArgs;

        /// <summary>
        /// If the request body has form data or url encoded arguments they are stored in key value format
        /// </summary>
        public IReadOnlyDictionary<string, string> RequestArgs => Entity.RequestArgs;

        /// <summary>
        /// Contains all files upladed with current request
        /// </summary>
        public IReadOnlyList<FileUpload> Files => Entity.Files;

        ///<inheritdoc/>
        HttpServer IHttpEvent.OriginServer => Entity.OriginServer;

        /// <summary>
        /// Complete the session and respond to user
        /// </summary>
        /// <param name="code">Status code of operation</param>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseResponse(HttpStatusCode code) => Entity.CloseResponse(code);

        ///<inheritdoc/>
        ///<exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseResponse(HttpStatusCode code, ContentType type, Stream stream)
        {
            Entity.CloseResponse(code, type, stream);
            //Verify content type matches
            if (!Server.Accepts(type))
            {
                throw new ContentTypeUnacceptableException("The client does not accept the content type of the response");
            }
        }

        ///<inheritdoc/>
        ///<exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseResponse(HttpStatusCode code, ContentType type, IMemoryResponseReader entity)
        {
            //Verify content type matches
            if (!Server.Accepts(type))
            {
                throw new ContentTypeUnacceptableException("The client does not accept the content type of the response");
            }

            Entity.CloseResponse(code, type, entity);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisableCompression() => Entity.DisableCompression();

        /*
         * Do not directly expose dangerous methods, but allow them to be called
         */

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IHttpEvent.DangerousChangeProtocol(IAlternateProtocol protocolHandler) => Entity.DangerousChangeProtocol(protocolHandler);
    }
}
