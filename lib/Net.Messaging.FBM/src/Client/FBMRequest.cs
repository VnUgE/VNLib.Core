/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMRequest.cs 
*
* FBMRequest.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// A reusable Fixed Buffer Message request container. This class is not thread-safe
    /// </summary>
    public sealed class FBMRequest : VnDisposeable, IReusable, IFBMMessage, IStringSerializeable
    {
        private sealed class BufferWriter : IBufferWriter<byte>
        {
            private readonly FBMRequest _request;

            public BufferWriter(FBMRequest request)
            {
                _request = request;
            }

            public void Advance(int count)
            {
                _request.Position += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                return sizeHint > 0 ? _request.RemainingBuffer[0..sizeHint] : _request.RemainingBuffer;
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                return sizeHint > 0 ? _request.RemainingBuffer.Span[0..sizeHint] : _request.RemainingBuffer.Span;
            }
        }

        private readonly IMemoryOwner<byte> HeapBuffer;
      
      
        private readonly BufferWriter _writer;
        private int Position;

        private readonly Encoding HeaderEncoding;
        private readonly int ResponseHeaderBufferSize;
        private readonly List<KeyValuePair<HeaderCommand, ReadOnlyMemory<char>>> ResponseHeaderList = new();
        private char[]? ResponseHeaderBuffer;

        /// <summary>
        /// The size (in bytes) of the request message
        /// </summary>
        public int Length => Position;
        private Memory<byte> RemainingBuffer => HeapBuffer.Memory[Position..];

        /// <summary>
        /// The id of the current request message
        /// </summary>
        public int MessageId { get; }
        /// <summary>
        /// The request message packet
        /// </summary>
        public ReadOnlyMemory<byte> RequestData => HeapBuffer.Memory[..Position];
        /// <summary>
        /// An <see cref="ManualResetEvent"/> to signal request/response
        /// event completion
        /// </summary>
        internal ManualResetEvent ResponseWaitEvent { get; }

        internal VnMemoryStream? Response { get; private set; }
        /// <summary>
        /// Initializes a new <see cref="FBMRequest"/> with the sepcified message buffer size,
        /// and a random messageid
        /// </summary>
        /// <param name="config">The fbm client config storing required config variables</param>
        public FBMRequest(in FBMClientConfig config) : this(Helpers.RandomMessageId, in config)
        { }
        /// <summary>
        /// Initializes a new <see cref="FBMRequest"/> with the sepcified message buffer size and a custom MessageId
        /// </summary>
        /// <param name="messageId">The custom message id</param>
        /// <param name="config">The fbm client config storing required config variables</param>
        public FBMRequest(int messageId, in FBMClientConfig config)
        {
            //Setup response wait handle but make sure the contuation runs async
            ResponseWaitEvent = new(true);
            
            //Alloc the buffer as a memory owner so a memory buffer can be used
            HeapBuffer = config.BufferHeap.DirectAlloc<byte>(config.MessageBufferSize);
            
            MessageId = messageId;

            HeaderEncoding = config.HeaderEncoding;
            ResponseHeaderBufferSize = config.MaxHeaderBufferSize;

            WriteMessageId();
            _writer = new(this);
        }

        /// <summary>
        /// Resets the internal buffer and writes the message-id header to the begining 
        /// of the buffer
        /// </summary>
        private void WriteMessageId()
        {
            //Get writer over buffer
            ForwardOnlyWriter<byte> buffer = new(HeapBuffer.Memory.Span);
            //write messageid header to the buffer
            buffer.Append((byte)HeaderCommand.MessageId);
            buffer.Append(MessageId);
            buffer.WriteTermination();
            //Store intial position
            Position = buffer.Written;
        }

        ///<inheritdoc/>
        public void WriteHeader(HeaderCommand header, ReadOnlySpan<char> value) => WriteHeader((byte)header, value);
        ///<inheritdoc/>
        public void WriteHeader(byte header, ReadOnlySpan<char> value)
        {
            ForwardOnlyWriter<byte> buffer = new(RemainingBuffer.Span);
            buffer.WriteHeader(header, value, Helpers.DefaultEncoding);
            //Update position
            Position += buffer.Written;
        }
        ///<inheritdoc/>
        public void WriteBody(ReadOnlySpan<byte> body, ContentType contentType = ContentType.Binary)
        {
            //Write content type header
            WriteHeader(HeaderCommand.ContentType, HttpHelpers.GetContentTypeString(contentType));
            //Get writer over buffer
            ForwardOnlyWriter<byte> buffer = new(RemainingBuffer.Span);
            //Now safe to write body
            buffer.WriteBody(body);
            //Update position
            Position += buffer.Written;
        }
        /// <summary>
        /// Returns buffer writer for writing the body data to the internal message buffer
        /// </summary>
        /// <returns>A <see cref="BufferWriter"/> to write message body to</returns>
        public IBufferWriter<byte> GetBodyWriter()
        {
            //Write body termination
            Helpers.Termination.CopyTo(RemainingBuffer);
            Position += Helpers.Termination.Length;
            //Return buffer writer
            return _writer;
        }

        /// <summary>
        /// Resets the internal buffer and allows for writing a new message with
        /// the same message-id
        /// </summary>
        public void Reset()
        {
            //Re-writing the message-id will reset the buffer
            WriteMessageId();
        }

        internal void SetResponse(VnMemoryStream? vms)
        {
            Response = vms;
            ResponseWaitEvent.Set();
        }

        internal Task WaitForResponseAsync(CancellationToken token)
        {
            return ResponseWaitEvent.WaitAsync().WaitAsync(token);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            HeapBuffer.Dispose();
            ResponseWaitEvent.Dispose();
            OnResponseDisposed();
        }
        void IReusable.Prepare() => Reset();
        bool IReusable.Release()
        {
            //Clear old response data if error occured
            Response?.Dispose();
            Response = null;
            
            return true;
        }

        /// <summary>
        /// Gets the response of the sent message
        /// </summary>
        /// <returns>The response message for the current request</returns>
        internal FBMResponse GetResponse()
        {
            if (Response != null)
            {
                /*
                 * NOTICE
                 * 
                 * The FBM Client will position the response stream to the start 
                 * of the header section (missing the message-id header)
                 * 
                 * The message id belongs to this request so it cannot be mismatched
                 * 
                 * The headers are read into a list of key-value pairs and the stream
                 * is positioned to the start of the message body
                 */


                //Alloc rseponse buffer
                ResponseHeaderBuffer ??= ArrayPool<char>.Shared.Rent(ResponseHeaderBufferSize);

                //Parse message headers
                HeaderParseError statusFlags = Helpers.ParseHeaders(Response, ResponseHeaderBuffer, ResponseHeaderList, HeaderEncoding);

                //return response structure
                return new(Response, statusFlags, ResponseHeaderList, OnResponseDisposed);
            }
            else
            {
                return new();
            }
        }

        //Called when a response message is disposed to cleanup resources held by the response
        private void OnResponseDisposed()
        {
            //Clear response header list
            ResponseHeaderList.Clear();

            //Clear old response
            Response?.Dispose();
            Response = null;

            if (ResponseHeaderBuffer != null)
            {
                //Free response buffer
                ArrayPool<char>.Shared.Return(ResponseHeaderBuffer!);
                ResponseHeaderBuffer = null;
            }
        }

        ///<inheritdoc/>
        public string Compile()
        {
            int charSize = Helpers.DefaultEncoding.GetCharCount(RequestData.Span);
            using UnsafeMemoryHandle<char> buffer = Memory.UnsafeAlloc<char>(charSize + 128);
            ERRNO count = Compile(buffer.Span);
            return buffer.AsSpan(0, count).ToString();
        }
        ///<inheritdoc/>
        public void Compile(ref ForwardOnlyWriter<char> writer)
        {
            writer.Append("Message ID:");
            writer.Append(MessageId);
            writer.Append(Environment.NewLine);
            Helpers.DefaultEncoding.GetChars(RequestData.Span, ref writer);
        }
        ///<inheritdoc/>
        public ERRNO Compile(in Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }
        ///<inheritdoc/>
        public override string ToString() => Compile();
       
    }
}
