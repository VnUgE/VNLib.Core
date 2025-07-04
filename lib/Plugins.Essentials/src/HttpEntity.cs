﻿/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Content;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;

/*
 * HttpEntity was converted to an object as during profiling
 * it was almost always heap allocated due to async operations
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
    public sealed class HttpEntity : IHttpEvent, IDisposable
    {

        /// <summary>
        /// The connection event entity
        /// </summary>
        private readonly IHttpEvent Entity;

        private readonly CancellationTokenSource EventCts;

        /// <summary>
        /// Creates a new <see cref="HttpEntity"/> instance with the optional 
        /// session handle. If the session handle is set, the session will be
        /// attached to the entity
        /// </summary>
        /// <param name="evnt">The event to parse and wrap</param>
        /// <param name="root">The processor the connection has originated from</param>
        /// <param name="session">An optional session handle to attach to the entity</param>
        public HttpEntity(IHttpEvent evnt, IWebProcessor root, ref readonly SessionHandle session)
            : this(evnt, root)
        {
            //Assign optional session and attempt to attach it
            EventSessionHandle = session;
            AttachSession();
        }

        internal HttpEntity(IHttpEvent entity, IWebProcessor root)
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
        /// Cleans up internal resources
        /// </summary>
        public void Dispose() => EventCts.Dispose();

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
        IHttpServer IHttpEvent.OriginServer => Entity.OriginServer;

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
        public void CloseResponse(HttpStatusCode code, ContentType type, Stream stream, long length)
        {
            //Verify content type matches
            if (!Server.Accepts(type))
            {
                throw new ContentTypeUnacceptableException("The client does not accept the content type of the response");
            }

            /*
             * If the underlying stream is actaully a memory stream, 
             * create a wrapper for it to read as a memory response.
             * This is done to avoid a user-space copy since we can 
             * get access to access the internal buffer
             * 
             * Stream length also should not cause an integer overflow,
             * which also mean position is assumed not to overflow 
             * or cause an overflow during reading
             * 
             * Finally not all memory streams allow fetching the internal 
             * buffer, so check that it can be aquired.
             */
            if (
                stream is MemoryStream ms
                && length < int.MaxValue
                && ms.TryGetBuffer(out ArraySegment<byte> arrSeg)
            )
            {
                Entity.CloseResponse(
                    code,
                    type,
                    entity: new MemStreamWrapper(in arrSeg, ms, (int)length)
                );

                return;
            }

            /*
             * Readonly vn streams can also use a shortcut to avoid http buffer allocation and 
             * async streaming. This is done by wrapping the stream in a memory response reader
             * 
             * Allocating a memory manager requires that the stream is readonly
             */
            if (stream is VnMemoryStream vms && length < int.MaxValue)
            {
                Entity.CloseResponse(
                   code,
                   type,
                   entity: new VnStreamWrapper(vms, (int)length)
                );

                return;
            }

            /*
             * Files can have a bit more performance using the RandomAccess library when reading
             * sequential segments without buffering. It avoids a user-space copy and async reading
             * performance without the file being opened as async.
             */
            if (stream is FileStream fs)
            {
                Entity.CloseResponse(
                    code,
                    type,
                    entity: new DirectFileStream(fs.SafeFileHandle),
                    length
                );

                return;
            }

            Entity.CloseResponse(code, type, stream, length);
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
        ///<exception cref="ContentTypeUnacceptableException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseResponse(HttpStatusCode code, ContentType type, IHttpStreamResponse stream, long length)
        {
            //Verify content type matches
            if (!Server.Accepts(type))
            {
                throw new ContentTypeUnacceptableException("The client does not accept the content type of the response");
            }

            Entity.CloseResponse(code, type, stream, length);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetControlFlag(ulong mask) => Entity.SetControlFlag(mask);

        /*
         * Do not directly expose dangerous methods, but allow them to be called
         */

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IHttpEvent.DangerousChangeProtocol(IAlternateProtocol protocolHandler) => Entity.DangerousChangeProtocol(protocolHandler);


        private sealed class VnStreamWrapper(VnMemoryStream memStream, int length) : IMemoryResponseReader
        {
            //Store memory buffer, causes an internal allocation, so avoid calling mutliple times
            readonly ReadOnlyMemory<byte> _memory = memStream.AsMemory();

            readonly int length = length;

            /*
             * Stream may be offset by the caller, it needs 
             * to be respected during streaming.
             */
            int read = (int)memStream.Position;

            ///<inheritdoc/>
            public int Remaining
            {
                get
                {
                    Debug.Assert(length - read >= 0);
                    return length - read;
                }
            }

            ///<inheritdoc/>
            public void Advance(int written) => read += written;

            ///<inheritdoc/>
            public void Close() => memStream.Dispose();

            ///<inheritdoc/>
            public ReadOnlyMemory<byte> GetMemory() => _memory.Slice(read, Remaining);
        }


        private sealed class MemStreamWrapper(ref readonly ArraySegment<byte> data, MemoryStream stream, int length) : IMemoryResponseReader
        {
            readonly ArraySegment<byte> _data = data;
            readonly int length = length;

            /*
             * Stream may be offset by the caller, it needs 
             * to be respected during streaming.
             */
            int read = (int)stream.Position;

            ///<inheritdoc/>
            public int Remaining
            {
                get
                {
                    Debug.Assert(length - read >= 0);
                    return length - read;
                }
            }

            ///<inheritdoc/>
            public void Advance(int written) => read += written;

            ///<inheritdoc/>
            public void Close() => stream.Dispose();

            ///<inheritdoc/>
            public ReadOnlyMemory<byte> GetMemory() => _data.AsMemory(read, Remaining);
        }
    }
}
