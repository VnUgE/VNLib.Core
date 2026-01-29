/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Diagnostics;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Utils.IO;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Server
{

    /// <summary>
    /// Represents an FBM request response container.
    /// </summary>
    public sealed class FBMResponseMessage : IReusable, IFBMMessage
    {
        internal FBMResponseMessage(int internalBufferSize, Encoding headerEncoding, IFBMMemoryManager manager)
        {
            _headerAccumulator = new HeaderDataAccumulator(internalBufferSize, manager);
            _headerEncoding = headerEncoding;
            _messageEnumerator = new(this);
        }

        private readonly MessageSegmentEnumerator _messageEnumerator;
        private readonly HeaderDataAccumulator _headerAccumulator;
        private readonly Encoding _headerEncoding;

        private IAsyncMessageBody? MessageBody;

        ///<inheritdoc/>
        public int MessageId { get; private set; }

        void IReusable.Prepare()
        {
            _headerAccumulator!.Prepare();
        }

        bool IReusable.Release()
        {
            //Release header accumulator
            _headerAccumulator.Close();
           
            MessageBody = null;

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
           
            MessageId = messageId;

            //Write the message id to the beginning of the headers buffer
            Helpers.WriteMessageid(_headerAccumulator, messageId);
        }

        ///<inheritdoc/>
        public void WriteHeader(HeaderCommand header, ReadOnlySpan<char> value) 
            => WriteHeader((byte)header, value);
        
        ///<inheritdoc/>
        public void WriteHeader(byte header, ReadOnlySpan<char> value) 
            => Helpers.WriteHeader(_headerAccumulator, header, value, _headerEncoding);

        ///<inheritdoc/>
        public void WriteBody(ReadOnlySpan<byte> body, ContentType contentType = ContentType.Binary)
        {
            //Append content type header
            WriteHeader(HeaderCommand.ContentType, HttpHelpers.GetContentTypeString(contentType));
            //end header segment
            Helpers.WriteTermination(_headerAccumulator);
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
            ArgumentNullException.ThrowIfNull(messageBody);

            if(MessageBody != null)
            {
                throw new InvalidOperationException("The message body is already set");
            }
            
            //Append message content type header
            WriteHeader(HeaderCommand.ContentType, HttpHelpers.GetContentTypeString(messageBody.ContentType));
            
            //end header segment
            Helpers.WriteTermination(_headerAccumulator);
            
            //Store message body
            MessageBody = messageBody;
        }

        /// <summary>
        /// Gets the internal message body enumerator and prepares the message for sending
        /// </summary>
        /// <returns>A value task that returns the message body enumerator</returns>
        internal IAsyncMessageReader GetResponseData() => _messageEnumerator;

        private sealed class MessageSegmentEnumerator(FBMResponseMessage message) : IAsyncMessageReader
        {
            private readonly ISlindingWindowBuffer<byte> _accumulator = message._headerAccumulator;

            bool HeadersRead;

            ///<inheritdoc/>
            public ReadOnlyMemory<byte> Current => _accumulator.AccumulatedBuffer;

            ///<inheritdoc/>
            public bool DataRemaining => message.MessageBody?.RemainingSize > 0;
           
            ///<inheritdoc/>
            public async ValueTask<bool> MoveNextAsync()
            {
                //Attempt to read header segment first
                if (!HeadersRead)
                {
                    /*
                     * If headers have not been read yet, we can attempt to buffer as much 
                     * of the message body into the header accumulator buffer as possible. This will 
                     * reduce message fragmentation.
                     */
                    if (DataRemaining && _accumulator.RemainingSize > 0)
                    {
                        //Message body must be set when data is remaining
                        Debug.Assert(message.MessageBody != null);

                        int read = await message.MessageBody
                            .ReadAsync(_accumulator.RemainingBuffer)
                            .ConfigureAwait(false);

                        //Advance accumulator to the read bytes
                        _accumulator.Advance(read);
                    }

                    //Set headers read flag
                    HeadersRead = true;
                    
                    return true;
                }
                else if (DataRemaining)
                {
                    //Reset the accumulator so we can read another segment
                    _accumulator.Reset();

                    //Message body must be set when data is remaining
                    Debug.Assert(message.MessageBody != null);

                    //Read body segment
                    int read = await message.MessageBody.ReadAsync(_accumulator.RemainingBuffer);

                    //Advance accumulator to the read bytes
                    _accumulator.Advance(read);

                    return read > 0;
                }
                return false;
            }

            ///<inheritdoc/>
            public ValueTask DisposeAsync()
            {
                //Reset headers read flag
                HeadersRead = false;

                //Dispose the message body if set
                return message.MessageBody != null ? message.MessageBody.DisposeAsync() : ValueTask.CompletedTask;
            }
        }
    }
    
}
