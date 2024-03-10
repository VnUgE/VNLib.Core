/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpEvent.cs 
*
* IHttpEvent.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
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
using System.Collections.Generic;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Contains an http request and session information.
    /// </summary>
    public interface IHttpEvent
    {
        /// <summary>
        /// Current connection information. (Like "$_SERVER" superglobal in PHP)
        /// </summary>
        IConnectionInfo Server { get; }

        /// <summary>
        /// The <see cref="HttpServer"/> that this connection originated from
        /// </summary>
        IHttpServer OriginServer { get; }

        /// <summary>
        /// If the request has query arguments they are stored in key value format
        /// </summary>
        /// <remarks>Keys are case-insensitive</remarks>
        IReadOnlyDictionary<string, string> QueryArgs { get; }

        /// <summary>
        /// If the request body has form data or url encoded arguments they are stored in key value format
        /// </summary>
        IReadOnlyDictionary<string, string> RequestArgs { get; }

        /// <summary>
        /// Contains all files upladed with current request
        /// </summary>
        /// <remarks>Keys are case-insensitive</remarks>
        IReadOnlyList<FileUpload> Files { get; }

        /// <summary>
        /// Complete the session and respond to user
        /// </summary>
        /// <param name="code">Status code of operation</param>
        /// <exception cref="InvalidOperationException"></exception>
        void CloseResponse(HttpStatusCode code);
        
        /// <summary>
        /// Responds to a client with a <see cref="Stream"/> containing data to be sent to user of a given contentType.
        /// Runtime will dispose of the stream during closing event
        /// </summary>
        /// <param name="code">Response status code</param>
        /// <param name="type">MIME ContentType of data</param>
        /// <param name="stream">Data to be sent to client</param>
        /// <param name="length">Length of data to read from the stream and send to client</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void CloseResponse(HttpStatusCode code, ContentType type, Stream stream, long length);

        /// <summary>
        /// Responds to a client with an in-memory <see cref="IMemoryResponseReader"/> containing data 
        /// to be sent to user of a given contentType.
        /// </summary>
        /// <param name="code">The status code to set</param>
        /// <param name="type">The entity content-type</param>
        /// <param name="entity">The in-memory response data</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void CloseResponse(HttpStatusCode code, ContentType type, IMemoryResponseReader entity);

        /// <summary>
        /// Responds to a client with an <see cref="IHttpStreamResponse"/> containing data to be sent 
        /// to user of a given contentType.
        /// </summary>
        /// <param name="code">The http status code</param>
        /// <param name="type">The entity content type</param>
        /// <param name="entity">The entity body to stream to the client</param>
        /// <param name="length">The length in bytes of the stream data</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void CloseResponse(HttpStatusCode code, ContentType type, IHttpStreamResponse entity, long length);

        /// <summary>
        /// Configures the server to change protocols from HTTP to the specified 
        /// custom protocol handler. 
        /// </summary>
        /// <param name="protocolHandler">The custom protocol handler</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void DangerousChangeProtocol(IAlternateProtocol protocolHandler);        

        /// <summary>
        /// Sets an http server control mask to be applied to the current event flow
        /// </summary>
        void SetControlFlag(ulong mask);
       
    }
}