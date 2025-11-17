/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMContext.cs 
*
* FBMContext.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Text;

using VNLib.Utils.IO;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Server
{
    /// <summary>
    /// A request/response pair message context
    /// </summary>
    public sealed class FBMContext : IReusable
    {
        private readonly Encoding _headerEncoding;

        private readonly IReusable _request;
        private readonly IReusable _response;
        
        /// <summary>
        /// The request message to process
        /// </summary>
        public FBMRequestMessage Request { get; }
        /// <summary>
        /// The response message
        /// </summary>
        public FBMResponseMessage Response { get; }
        /// <summary>
        /// Creates a new reusable <see cref="FBMContext"/>
        /// for use within a <see cref="ObjectRental{T}"/> 
        /// cache
        /// </summary>
        /// <param name="requestHeaderBufferSize">The size in characters of the request header buffer</param>
        /// <param name="responseBufferSize">The size in characters of the response header buffer</param>
        /// <param name="headerEncoding">The message header encoding instance</param>
        /// <param name="manager">The context memory manager</param>
        public FBMContext(int requestHeaderBufferSize, int responseBufferSize, Encoding headerEncoding, IFBMMemoryManager manager)
        {
            _request = Request = new(requestHeaderBufferSize, manager);
            _response = Response = new(responseBufferSize, headerEncoding, manager);
            _headerEncoding = headerEncoding;
        }

        /// <summary>
        /// Initializes the context with the buffered request data
        /// </summary>
        /// <param name="requestData">The request data buffer positioned at the beginning of the request data</param>
        /// <param name="connectionId">The unique id of the connection</param>
        internal void Prepare(VnMemoryStream requestData, string connectionId)
        {
            Request.Prepare(requestData, connectionId, _headerEncoding);
            //Message id is set after the request parses the incoming message
            Response.Prepare(Request.MessageId);
        }

        void IReusable.Prepare()
        {
            _request.Prepare();
            _response.Prepare();
        }

        bool IReusable.Release()
        {
            return _request.Release() & _response.Release();
        }
    }
}
