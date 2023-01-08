/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMResponseMessage.cs 
*
* FBMResponseMessage.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Utils.IO;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Caching;
using VNLib.Net.Messaging.FBM.Client;

namespace VNLib.Net.Messaging.FBM.Server
{

    /// <summary>
    /// Represents an FBM request response container.
    /// </summary>
    public sealed class FBMResponseMessage : IReusable, IFBMMessage
    {
        internal FBMResponseMessage(int internalBufferSize, Encoding headerEncoding)
        {
            _headerAccumulator = new HeaderDataAccumulator(internalBufferSize);
            _headerEncoding = headerEncoding;
            _messageEnumerator = new(this);
        }

        private readonly MessageSegmentEnumerator _messageEnumerator;
        private readonly ISlindingWindowBuffer<byte> _headerAccumulator;
        private readonly Encoding _headerEncoding;

        private IAsyncMessageBody? _messageBody;

        ///<inheritdoc/>
        public int MessageId { get; private set; }

        void IReusable.Prepare()
        {
            (_headerAccumulator as HeaderDataAccumulator)!.Prepare();
        }

        bool IReusable.Release()
        {
            //Release header accumulator
            _headerAccumulator.Close();
           
            _messageBody = null;

            MessageId = 0;
           
            return true;
        }

        /// <summary>
        /// Initializes the response message with the specified message-id 
        /// to respond with
        /// </summary>
        /// <param name="messageId">The message id of the context to respond to</param>
        internal void Prepare(int messageId)
        {
            //Reset accumulator when message id is written
            _headerAccumulator.Reset();
            //Write the messageid to the begining of the headers buffer
            MessageId = messageId;
            _headerAccumulator.Append((byte)HeaderCommand.MessageId);
            _headerAccumulator.Append(messageId);
            _headerAccumulator.WriteTermination();
        }

        ///<inheritdoc/>
        public void WriteHeader(HeaderCommand header, ReadOnlySpan<char> value)
        {
            WriteHeader((byte)header, value);
        }
        ///<inheritdoc/>
        public void WriteHeader(byte header, ReadOnlySpan<char> value)
        {
            _headerAccumulator.WriteHeader(header, value, _headerEncoding);
        }
      
        ///<inheritdoc/>
        public void WriteBody(ReadOnlySpan<byte> body, ContentType contentType = ContentType.Binary)
        {
            //Append content type header
            WriteHeader(HeaderCommand.ContentType, HttpHelpers.GetContentTypeString(contentType));
            //end header segment
            _headerAccumulator.WriteTermination();
            //Write message body
            _headerAccumulator.Append(body);
        }

        /// <summary>
        /// Sets the response message body
        /// </summary>
        /// <param name="messageBody">The <see cref="IAsyncMessageBody"/> to stream data from</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddMessageBody(IAsyncMessageBody messageBody)
        {
            if(_messageBody != null)
            {
                throw new InvalidOperationException("The message body is already set");
            }
            
            //Append message content type header
            WriteHeader(HeaderCommand.ContentType, HttpHelpers.GetContentTypeString(messageBody.ContentType));
            
            //end header segment
            _headerAccumulator.WriteTermination();
            
            //Store message body
            _messageBody = messageBody;

        }

        /// <summary>
        /// Gets the internal message body enumerator and prepares the message for sending
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A value task that returns the message body enumerator</returns>
        internal async ValueTask<IAsyncMessageReader> GetResponseDataAsync(CancellationToken cancellationToken)
        {
            //try to buffer as much data in the header segment first
            if(_messageBody?.RemainingSize > 0 && _headerAccumulator.RemainingSize > 0)
            {
                //Read data from the message
                int read = await _messageBody.ReadAsync(_headerAccumulator.RemainingBuffer, cancellationToken);
                //Advance accumulator to the read bytes
                _headerAccumulator.Advance(read);
            }
            //return reusable enumerator
            return _messageEnumerator;
        }

        private class MessageSegmentEnumerator : IAsyncMessageReader
        {
            private readonly FBMResponseMessage _message;

            bool HeadersRead;

            public MessageSegmentEnumerator(FBMResponseMessage message)
            {
                _message = message;
            }

            public ReadOnlyMemory<byte> Current { get; private set; }

            public bool DataRemaining { get; private set; }
           
            public async ValueTask<bool> MoveNextAsync()
            {
                //Attempt to read header segment first
                if (!HeadersRead)
                {
                    //Set the accumulated buffer
                    Current = _message._headerAccumulator.AccumulatedBuffer;

                    //Update data remaining flag
                    DataRemaining = _message._messageBody?.RemainingSize > 0;

                    //Set headers read flag
                    HeadersRead = true;
                    
                    return true;
                }
                else if (_message._messageBody?.RemainingSize > 0)
                {
                    //Use the header buffer as the buffer for the message body
                    Memory<byte> buffer = _message._headerAccumulator.Buffer;

                    //Read body segment
                    int read = await _message._messageBody.ReadAsync(buffer);

                    //Update data remaining flag
                    DataRemaining = _message._messageBody.RemainingSize > 0;

                    if (read > 0)
                    {
                        //Store the read segment
                        Current = buffer[..read];
                        return true;
                    }
                }
                return false;
            }

            public async ValueTask DisposeAsync()
            {
                //Clear current segment
                Current = default;

                //Reset headers read flag
                HeadersRead = false;
                
                //Dispose the message body if set
                if (_message._messageBody != null)
                {
                    await _message._messageBody.DisposeAsync();
                }
            }
        }
    }
    
}
