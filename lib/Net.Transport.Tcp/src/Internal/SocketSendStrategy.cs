/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: SocketSendStrategy.cs 
*
* SocketSendStrategy.cs is part of VNLib.Net.Transport.Tcp which is part of the larger 
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
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.IO.Pipelines;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;

namespace VNLib.Net.Transport.Tcp.Internal
{
    internal sealed class SocketSendStrategy(PipeOptions pipeOptions) : SocketStrategyBase(pipeOptions)
    {      
        private int _sysSocketSendBufSizeHint = 0;

        /// <summary>
        /// Gets an <see cref="IBufferWriter{T}"/> for writing data to the send pipe.
        /// </summary>
        public IBufferWriter<byte> SendBuffer => Pipe.Writer;

        /*
         * Fired when the send timer has expired. For sending, we need to 
         * cancel the pending flush and let the caller return.
         */
        protected override void OnTimeoutExpired(object? state)
            => Pipe.Writer.CancelPendingFlush();

        private ValueTask FlushWithTimerInternalAsync<TTimer>(in TTimer timer, CancellationToken cancellation)
            where TTimer : INetTimer
        {
            //Start send timer
            timer.Start();
            try
            {
                //Send the segment
                ValueTask<FlushResult> result = Pipe.Writer.FlushAsync(cancellation);

                //Task completed successfully, so 
                if (result.IsCompleted)
                {
                    //Stop timer
                    timer.Stop();

                    //safe to get the flush result sync, may throw, so preserve the call stack
                    FlushResult fr = result.GetAwaiter().GetResult();

                    //Check for canceled and throw
                    return fr.IsCanceled
                        ? ValueTask.FromException(new OperationCanceledException("The write operation was canceled by the underlying PipeWriter"))
                        : ValueTask.CompletedTask;
                }
                else
                {
                    //Wrap the task in a ValueTask since it must be awaited, and will happen on background thread
                    return new(AwaitFlushTask(result, timer));
                }
            }
            catch (Exception ex)
            {
                //Stop timer on exception
                timer.Stop();
                return ValueTask.FromException(ex);
            }

            static async Task AwaitFlushTask(ValueTask<FlushResult> valueTask, TTimer timer)
            {
                try
                {
                    FlushResult result = await valueTask.ConfigureAwait(false);
                    if (result.IsCanceled)
                    {
                        throw new OperationCanceledException("The write operation was canceled by the underlying PipeWriter");
                    }
                }
                finally
                {
                    timer.Stop();
                }
            }
        }

        /// <summary>
        /// Entrypoint for the send pipeline worker task. Starts the send loop that waits for data on the 
        /// send pipe and flushes it to the socket until the pipe is completed or an error occurs.
        /// </summary>
        /// <typeparam name="TIO">The type of the socket I/O interface</typeparam>
        /// <param name="sock">The socket to which data will be sent</param>
        /// <param name="sendBufferSize">The size of the send buffer</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This function will never raise an exception on the task. Any exceptions that occur during 
        /// normal operation will be propagated to the pipeline.
        /// </remarks>
        public async Task DoWorkAsync<TIO>(TIO sock, int sendBufferSize)
            where TIO : ISocketIo
        {
            Exception? errCause = null;
            ReadResult sendReadRes;
            ReadOnlySequence<byte>.Enumerator sendEnum;
            ForwardOnlyMemoryReader<byte> segmentReader;

            IsStarted |= true;

            try
            {
                _sysSocketSendBufSizeHint = sendBufferSize;

                //Enter work loop
                do
                {
                    // wait indefinitely for data from the write pipe and write it to the socket
                    // or until the pipe is completed or cancelled
                    sendReadRes = await Pipe.Reader.ReadAsync(CancellationToken.None)
                        .ConfigureAwait(false);

                    //Catch error/cancel conditions and break the loop
                    if (sendReadRes.IsCanceled || sendReadRes.Buffer.IsEmpty)
                    {
                        break;
                    }

                    /*
                     * Even if the pipe was completed, and if the buffer is not empty, then 
                     * there is still data to be written to the socket, so we must continue
                     */

                    //Get enumerator to write memory segments
                    sendEnum = sendReadRes.Buffer.GetEnumerator();

                    while (sendEnum.MoveNext())
                    {

                        /*
                         * Using a forward only reader allows the following loop
                         * to track the amount of data written to the socket
                         * until the entire segment has been sent or if it has
                         * move to the next segment
                         */

                        segmentReader = new(sendEnum.Current);

                        while (segmentReader.WindowSize > 0)
                        {
                            //Write segment to socket, and update written data
                            int written = await sock.SendAsync(segmentReader.Window, SocketFlags.None)
                                                .ConfigureAwait(false);

                            if (written < 0)
                            {
                                goto ExitOnSocketErr;
                            }

                            if (written == segmentReader.WindowSize)
                            {
                                //All data was written
                                break;
                            }

                            //Advance unread window to end of the written data
                            segmentReader.Advance(written);
                        }
                        //Advance to next window/segment
                    }

                    // Advance pipe
                    Pipe.Reader.AdvanceTo(sendReadRes.Buffer.End);

                    
                // Continue loop so long as pipeline was not completed
                } while (!sendReadRes.IsCompleted);

            ExitOnSocketErr:
                ;

            }
            catch (Exception ex)
            {
                errCause = ex;
            }
            finally
            {
                /*
                 * Signals to producers that the consumer is no longer processing data, and that any pending 
                 * or future data will not be processed. 
                 */
                Pipe.Reader.Complete(errCause);                  
            }
        }

        /*
         * On the send pipe, it's the callers responsibility to complete
         * the producer side of the pipe, to signal no more data will be written
         */

        /// <inheritdoc/>
        public override void CompletePipeline() 
            => Pipe.Writer.Complete(); 

        /// <summary>
        /// Waits for data written to the internal send pipe to be processed by
        /// the worker task and flushed to the socket, with an optional timeout. If the flush
        /// is not completed before the timeout expires, the flush will be canceled and an 
        /// OperationCanceledException will be thrown.
        /// </summary>
        /// <param name="timeout">The time in milliseconds to wait for the flush</param>
        /// <param name="cancellation">A cancellation token to observe while waiting for the flush</param>
        /// <returns>A ValueTask representing the asynchronous flush operation</returns>
        public ValueTask FlushAsync(int timeout, CancellationToken cancellation)
        {
            //See if timer is required
            if (timeout < 1)
            {
                NoOpTimerWrapper noOpTimer = default;

                //no timer
                return FlushWithTimerInternalAsync(in noOpTimer, cancellation);
            }
            else
            {
                TpTimerWrapper sendTimer = new(OpTimer, timeout);

                //Pass new send timer to send method
                return FlushWithTimerInternalAsync(in sendTimer, cancellation);
            }
        }

        /// <summary>
        /// Copies all of the supplied data into the internal pipewriter buffers, and advances the writer accordingly, 
        /// so that the data can be flushed to the socket by the worker task.
        /// </summary>
        /// <param name="src">The source data to prepare in the buffer</param>
        public void WriteData(ReadOnlySpan<byte> src)
        {
            int bufferSize = _sysSocketSendBufSizeHint;

            Debug.Assert(bufferSize > 0, "A call to CopyAndPublishDataOnSendPipe was made before a socket was connected");

            ref readonly byte srcRef = ref MemoryMarshal.GetReference(src);

            /*
             * Only publish blocks up to the size of the socket buffer
             * If blocks are larger than the socket buffer, they will 
             * be published in chunks up to the size of the socket buffer
             */
            uint written = 0;
            while (written < src.Length)
            {
                //Clamp the data to copy to the size of the socket buffer
                int dataToCopy = (int)Math.Min(bufferSize, src.Length - written);

                //Get a new buffer span, as large as the data to copy
                Span<byte> dest = Pipe.Writer.GetSpan(dataToCopy);

                //Copy data to the buffer at the new position (attempt to use hardware acceleration)
                MemoryUtil.AcceleratedMemmove(
                    src: in srcRef,
                    srcOffset: written,
                    dst: ref MemoryMarshal.GetReference(dest),
                    dstOffset: 0,
                    elementCount: (uint)dataToCopy
                );

                //Advance the writer by the number of bytes written
                Pipe.Writer.Advance(dataToCopy);

                //Increment the written count
                written += (uint)dataToCopy;
            }
        }

        /// <summary>
        /// Triggers a cancellation of the pending flush operation on the send pipe, which will cause the 
        /// flush to complete with a canceled state.
        /// </summary>
        public void CancelPendingFlush() => Pipe.Writer.CancelPendingFlush();
    }
}
