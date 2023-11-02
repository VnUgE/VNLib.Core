/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMRequestMessage.cs 
*
* FBMRequestMessage.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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

using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Server
{
    /// <summary>
    /// Represents a client request message to be serviced
    /// </summary>
    public sealed class FBMRequestMessage : IFBMHeaderBuffer, IReusable
    {
        private readonly List<FBMMessageHeader> _headers;
        private readonly int HeaderBufferSize;
        private readonly IFBMMemoryManager _memoryManager;
        private readonly IFBMSpanOnlyMemoryHandle _memHandle;

        /// <summary>
        /// Creates a new resusable <see cref="FBMRequestMessage"/>
        /// </summary>
        /// <param name="headerBufferSize">The size of the buffer to alloc during initialization</param>
        /// <param name="manager">The memory manager to use for the message</param>
        internal FBMRequestMessage(int headerBufferSize, IFBMMemoryManager manager)
        {
            HeaderBufferSize = headerBufferSize;
            _headers = new();
            _memoryManager = manager;
            _memHandle = _memoryManager.InitSpanOnly();
        }
        
        /// <summary>
        /// The ID of the current message
        /// </summary>
        public int MessageId { get; private set; }
        /// <summary>
        /// Gets the underlying socket-id fot the current connection
        /// </summary>
        public string? ConnectionId { get; private set; }
        /// <summary>
        /// The raw request message, positioned to the body section of the message data
        /// </summary>
        public VnMemoryStream? RequestBody { get; private set; }
        /// <summary>
        /// A collection of headers for the current request
        /// </summary>
        public IReadOnlyList<FBMMessageHeader> Headers => _headers;
        /// <summary>
        /// Status flags set during the message parsing
        /// </summary>
        public HeaderParseError ParseStatus { get; private set; }        
        /// <summary>
        /// The message body data as a <see cref="ReadOnlySpan{T}"/>
        /// </summary>
        public ReadOnlySpan<byte> BodyData => Helpers.GetRemainingData(RequestBody!);

        /// <summary>
        /// Determines if the current message is considered a control frame
        /// </summary>
        public bool IsControlFrame { get; private set; }

        /// <summary>
        /// Prepares the request to be serviced
        /// </summary>
        /// <param name="vms">The request data packet</param>
        /// <param name="socketId">The unique id of the connection</param>
        /// <param name="dataEncoding">The data encoding used to decode header values</param>
        internal void Prepare(VnMemoryStream vms, string socketId, Encoding dataEncoding)
        {
            //Store request body
            RequestBody = vms;
            
            //Store message id
            MessageId = Helpers.GetMessageId(Helpers.ReadLine(vms));

            //Check mid for control frame
            if(MessageId == Helpers.CONTROL_FRAME_MID)
            {
                IsControlFrame = true;
            }
            else if (MessageId < 1)
            {
                ParseStatus |= HeaderParseError.InvalidId;
                return;
            }

            ConnectionId = socketId;          

            //Parse headers
            ParseStatus = Helpers.ParseHeaders(vms, this, _headers, dataEncoding);
        }
      

        void IReusable.Prepare()
        {
            ParseStatus = HeaderParseError.None;
            //Alloc header buffer
            _memoryManager.AllocBuffer(_memHandle, MemoryUtil.ByteCount<char>(HeaderBufferSize));
        }
        
       
        bool IReusable.Release()
        {
            //Dispose the request message
            RequestBody?.Dispose();
            RequestBody = null;
            //Clear headers before freeing buffer
            _headers.Clear();
            //Free header-buffer
            _memoryManager.FreeBuffer(_memHandle);
            ConnectionId = null;
            MessageId = 0;
            IsControlFrame = false;
            return true;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<char> IFBMHeaderBuffer.GetSpan(int offset, int count)
        {
            //Cast to char buffer
            Span<char> chars = MemoryMarshal.Cast<byte, char>(_memHandle.GetSpan());
            //Return the requested span
            return chars.Slice(offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<char> IFBMHeaderBuffer.GetSpan() => MemoryMarshal.Cast<byte, char>(_memHandle.GetSpan());

    }
}
