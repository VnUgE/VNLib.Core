/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: SocketDataProcessor.cs 
*
* SocketDataProcessor.cs is part of VNLib.Net.Transport.Tcp which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.Tcp is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.Tcp is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp.Internal
{

    /// <summary>
    /// A reusable type for transferring data in a full duplex to a networking channel
    /// using pipelines.
    /// </summary>
    internal sealed class SocketDataProcessor : ITransportInterface, IReusable
    {
        public readonly ReusableNetworkStream NetworkStream;
        private readonly SocketSendStrategy _sender;
        private readonly SocketReceiveStrategy _receiver;

        /// <summary>
        /// Gets the send strategy for this pipeline, which is responsible for
        /// writing data to the socket and flushing it
        /// </summary>
        internal SocketSendStrategy Sender => _sender;

        /// <summary>
        /// Gets the receive strategy for this pipeline, which is responsible for
        /// reading data from the socket and publishing it to the receive pipe for consumption
        /// </summary>
        internal SocketReceiveStrategy Receiver => _receiver;

        /// <summary>
        /// Initializes a new reusable socket pipeline worker
        /// </summary>
        /// <param name="pipeOptions"></param>
        public SocketDataProcessor(PipeOptions pipeOptions)
        {
            //Init pipes
            _sender = new(pipeOptions);
            _receiver = new(pipeOptions);  

            //Init reusable network stream
            NetworkStream = new(this);
        }

        /// <inheritdoc/>
        public void Prepare()
        {
            _receiver.Prepare();
            _sender.Prepare();

            NetworkStream.ReadTimeout = Timeout.Infinite;
            NetworkStream.WriteTimeout = Timeout.Infinite;
        }

        /// <inheritdoc/>
        public bool Release()
        {
            // Use bitwise and to ensure both release methods are called even if the first one returns false
            return _receiver.Release() &
                _sender.Release();
        }

        /// <summary>
        /// The internal cleanup/dispose method to be called
        /// when the pipeline is no longer needed
        /// </summary>
        public void DisposeInternal()
        {
            _receiver.Dispose();
            _sender.Dispose();
        }

        /// <summary>
        /// Must be called when the pipeline is requested to be closed
        /// </summary>
        internal void ShutDownClientPipe()
        {
            /*
             * Completing the pipelines will close the consumer side of the 
             * receiving loop, which should cause the receive loop to exit as completed
             * and complete the producer side of the sending loop, which should cause 
             * the send loop to exit as completed
             */

            _receiver.CompletePipeline();
            _sender.CompletePipeline();
        }

        ///<inheritdoc/>
        IBufferWriter<byte> ITransportInterface.SendBuffer => _sender.SendBuffer;
        
        ///<inheritdoc/>
        public ValueTask FlushSendAsync(int timeout, CancellationToken cancellation) 
            => _sender.FlushAsync(timeout, cancellation);

        ///<inheritdoc/>
        void ITransportInterface.Send(ReadOnlySpan<byte> data, int timeout)
        {
            _sender.WriteData(data);
            
            ValueTask result = _sender.FlushAsync(timeout, CancellationToken.None);         

            //If the task is completed, then it was sync, so get the result
            if (result.IsCompleted)
            {
                result.GetAwaiter().GetResult();
            }
            //Otherwise convert to task then await it
            else
            {
                result.AsTask().GetAwaiter().GetResult();
            }
        }

        ///<inheritdoc/>
        ValueTask ITransportInterface.SendAsync(ReadOnlyMemory<byte> data, int timeout, CancellationToken cancellation)
        {
            _sender.WriteData(data.Span);
            return _sender.FlushAsync(timeout, cancellation);          
        }

        ///<inheritdoc/>
        ValueTask<int> ITransportInterface.RecvAsync(Memory<byte> buffer, int timeout, CancellationToken cancellation) 
            => _receiver.ReceiveAsync(buffer, timeout, cancellation);

        ///<inheritdoc/>
        int ITransportInterface.Recv(Span<byte> buffer, int timeout)
            => _receiver.Receive(buffer, timeout);               
    }
}
