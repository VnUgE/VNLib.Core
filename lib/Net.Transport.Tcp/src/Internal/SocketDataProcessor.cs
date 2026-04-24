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
using System.Diagnostics;
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

        // Used to guard the shutdown behavior
        private bool _isShutDown;

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
            // clear shutdown flag
            _isShutDown = false;

            _receiver.Prepare();
            _sender.Prepare();

            NetworkStream.ReadTimeout = Timeout.Infinite;
            NetworkStream.WriteTimeout = Timeout.Infinite;
        }

        /// <inheritdoc/>
        public bool Release()
        {
            Debug.Assert(_isShutDown, "Expected pipeline to be shutdown before release");

            // Use bitwise and to ensure both release methods are called even if the first one returns false
            return _receiver.Release() & _sender.Release();
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

        /*
         * In normal operation. The network stream is used and expected to be disposed by the consumer.
         * Previously this was a no-op, changed to allow for better control flow. If consumers dispose the 
         * stream it means they are no longer using the pipeline, so we can eagerly complete the workers 
         * instead of deferring until the transport is disconnected/returned to the pool.
         * 
         * The shutdown method is still exposed here incase the consumer does not properly call 
         * dispose on the stream as a safetynet as the workers will not completed otherwise causing the 
         * worker tasks to wait indefinitely. 
         * 
         * Also concurrency note. It's know that the shutdown/close functions _should_ be called from 
         * a single thread context. It's more expensive to make it thread safe so it's a best effort.
         */

        /// <summary>
        /// Must be called when the pipeline is requested to be closed
        /// </summary>
        internal void ShutDownClientPipe()
        {
            if (_isShutDown)
            {
                return;
            }

            /*
            * Completing the pipelines will close the consumer side of the 
            * receiving loop, which should cause the receive loop to exit as completed
            * and complete the producer side of the sending loop, which should cause 
            * the send loop to exit as completed
            */

            _isShutDown = true;

            _receiver.CompletePipeline();
            _sender.CompletePipeline();
        }

        ///<inheritdoc/>
        void ITransportInterface.Close() => ShutDownClientPipe();

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
