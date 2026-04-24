/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: SocketReceiveStrategy.cs 
*
* SocketReceiveStrategy.cs is part of VNLib.Net.Transport.Tcp which is part of the larger 
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
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

namespace VNLib.Net.Transport.Tcp.Internal
{
    /// <summary>
    /// Implements the receive direction of the socket pipeline. Reads data from the socket
    /// and publishes it to the receive pipe for downstream consumption. Supports optional read
    /// timeouts via the generic <see cref="INetTimer"/> pattern.
    /// </summary>
    internal sealed class SocketReceiveStrategy : SocketStrategyBase
    {
        /// <summary>
        /// Gets the <see cref="Stream"/> view of the receive pipe reader, used to
        /// expose buffered socket data to callers that consume via the <see cref="Stream"/> API.
        /// </summary>
        public Stream RecvStream { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketReceiveStrategy"/> class.
        /// </summary>
        /// <param name="pipeOptions">The options used to configure the internal receive <see cref="System.IO.Pipelines.Pipe"/>.</param>
        public SocketReceiveStrategy(PipeOptions pipeOptions) : base(pipeOptions) 
            => RecvStream = Pipe.Reader.AsStream(true);

        /// <summary>
        /// Gets an <see cref="IBufferWriter{T}"/> that writes directly into the receive pipe.
        /// Used by the accept path to publish pre-read bytes before the pipeline worker task starts.
        /// </summary>
        public IBufferWriter<byte> ReceiveBuffer => Pipe.Writer;

        /*
         * During a receive operation, the caller may specify a timeout. If that 
         * timer expires, it fires here. Cancelling the pending read will cause
         * the blocked reader to return.
         */

        /// <inheritdoc/>
        protected override void OnTimeoutExpired(object? state)
            => Pipe.Reader.CancelPendingRead();

        /// <summary>
        /// Begins the receive loop, which reads data from the socket and publishes it to the receive pipe until 
        /// the socket is closed or an error occurs.
        /// </summary>
        /// <typeparam name="TIO">The socket I/O interface type; generic to avoid virtual dispatch on the hot path.</typeparam>
        /// <param name="sock">The socket from which data will be received.</param>
        /// <param name="recvBufferSize">A hint to the worker for how large to allocate each receive buffer from the pipe writer.</param>
        /// <returns>A task that completes once the pipeline work has finished or been cancelled.</returns>
        /// <remarks>
        /// This function will never raise an exception on the task. Any exceptions that occur during 
        /// normal operation will be propagated to the pipeline.
        /// </remarks>
        public async Task DoWorkAsync<TIO>(TIO sock, int recvBufferSize)
            where TIO : ISocketIo
        {
            Exception? cause = null;
            FlushResult recvFlushRes;
            Memory<byte> recvBuffer;

            Debug.Assert(!IsStarted, "Receive pipeline worker was already started or was not properly reset.");
            IsStarted = true;

            try
            {
                /*
                 * Some platforms allow for publishing data directly onto the ReceiveBuffer
                 * such as WSA on windows during Accept(). Parent socket class will
                 */
                recvFlushRes = await Pipe.Writer.FlushAsync(CancellationToken.None)
                                    .ConfigureAwait(false);

                //Check flush result for error/cancel
                if (IsPipeClosedAfterFlush(ref recvFlushRes))
                {
                    //Exit
                    return;
                }

                //Enter work loop
                while (true)
                {
                    //Get buffer from pipe writer
                    recvBuffer = Pipe.Writer.GetMemory(recvBufferSize);

                    //Wait for data or error from socket
                    int count = await sock.ReceiveAsync(recvBuffer, SocketFlags.None)
                                    .ConfigureAwait(false);

                    // always call advance, even with empty result, to free buffer if needed
                    Pipe.Writer.Advance(count);

                    if (count <= 0)
                    {
                        //Connection is softly closing, exit
                        break;
                    }                   

                    // Publish read data. If pipeline is full, yields to apply backpressure on the socket.
                    recvFlushRes = await Pipe.Writer.FlushAsync(CancellationToken.None)
                                        .ConfigureAwait(false);

                    //Writing has completed, time to exit
                    if (IsPipeClosedAfterFlush(ref recvFlushRes))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                cause = ex;

                // TODO: Old extracted code cancelled the sending pipe since the socket is in an error state
                // will have to think if it was necessary during normal operation, or not
            }
            finally
            {
                //Stop timer incase exception
                OpTimer.Stop();

                //Cleanup and complete the writer
                Pipe.Writer.Complete(cause);
            }

            static bool IsPipeClosedAfterFlush(ref FlushResult result)
                => result.IsCanceled || result.IsCompleted;
        }

        /*
         * The worker task cleans up the writer side of the pipe, so we need to expose
         * a way for consumers to signal their done with the reader side of the pipe, so that 
         * the worker can clean up and exit gracefully
         */

        /// <inheritdoc/>
        public override void CompletePipeline() => Pipe.Reader.Complete();

        private ValueTask<int> RecvWithTimerInternalAsync<TTimer>(Memory<byte> data, in TTimer timer, CancellationToken cancellation)
           where TTimer : INetTimer
        {
            //Restart timer
            timer.Start();
            try
            {
                //Read async and get the value task
                ValueTask<int> result = RecvStream.ReadAsync(data, cancellation);

                if (result.IsCompleted)
                {
                    //Completed sync, may throw, if not return the results
                    int read = result.GetAwaiter().GetResult();

                    //Stop the timer
                    timer.Stop();

                    return ValueTask.FromResult(read);
                }
                else
                {
                    //return async as value task
                    return new(AwaitAsyncRead(result, timer));
                }
            }
            catch
            {
                timer.Stop();
                throw;
            }
            
            /* 
             * In the async path, wraps the value task await by allocating a new Task
             * object removing issues with awaiting, waiting synchronously and 
             * exception handling.
             */
            static async Task<int> AwaitAsyncRead(ValueTask<int> task, TTimer recvTimer)
            {
                try
                {
                    return await task.ConfigureAwait(false);
                }
                finally
                {
                    recvTimer.Stop();
                }
            }
        }

        /// <summary>
        /// Reads a block of published data from the consumer pipe into the supplied buffer, optionally 
        /// with a timeout and task cancellation.
        /// </summary>
        /// <param name="buffer">The buffer to write received data into</param>
        /// <param name="timeout">An optional timeout (in milliseconds) to cancel blocking (async yield) reads. Timer is enabled when > 0</param>
        /// <param name="cancellation">A token to cancel the read operation</param>
        /// <returns>A task that completes with the number of bytes read into the buffer.</returns>
        public ValueTask<int> ReceiveAsync(Memory<byte> buffer, int timeout, CancellationToken cancellation)
        {
            //See if timer is required
            if (timeout < 1)
            {
                NoOpTimerWrapper noOpTimer = default;               
                return RecvWithTimerInternalAsync(buffer, in noOpTimer, cancellation);
            }
            else
            {
                TpTimerWrapper recvTimer = new(OpTimer, timeout);               
                return RecvWithTimerInternalAsync(buffer, in recvTimer, cancellation);
            }
        }

        /// <summary>
        /// Reads a block of data from the consumer path 
        /// into the supplied buffer.
        /// </summary>
        /// <param name="buffer">The buffer to receive data into</param>
        /// <param name="timeout">A timeout value used to cancel the read operation if it blocks. Enabled when > 0 </param>
        /// <returns>The number of bytes read into the buffer</returns>
        public int Receive(Span<byte> buffer, int timeout)
        {
            if (timeout > 0)
            {
                // Start timer before entering read
                OpTimer.Restart(timeout);
                try
                {
                    return RecvStream.Read(buffer);
                }
                finally
                {
                    // Clear timer
                    OpTimer.Stop();
                }
            }
            else
            {
                return RecvStream.Read(buffer);
            }           
        }
    }
}
