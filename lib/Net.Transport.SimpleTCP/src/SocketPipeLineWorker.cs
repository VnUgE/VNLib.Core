/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: SocketPipeLineWorker.cs 
*
* SocketPipeLineWorker.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.SimpleTCP is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.SimpleTCP is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.IO.Pipelines;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Caching;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Transport.Tcp
{

    /// <summary>
    /// A reuseable socket pipeline provider, that marshals data from a network stream 
    /// to a connected socket.
    /// </summary>
    internal sealed class SocketPipeLineWorker : ITransportInterface, IReusable
    {
        public readonly ReusableNetworkStream NetworkStream;
        private readonly Pipe SendPipe;
        private readonly Pipe RecvPipe;
        private readonly Timer RecvTimer;
        private readonly Timer SendTimer;
        private readonly Stream RecvStream;

        /// <summary>
        /// Initalizes a new reusable socket pipeline worker
        /// </summary>
        /// <param name="pipeOptions"></param>
        public SocketPipeLineWorker(PipeOptions pipeOptions)
        {
            //Init pipes
            SendPipe = new(pipeOptions);
            RecvPipe = new(pipeOptions);

            RecvStream = RecvPipe.Reader.AsStream(true);

            //Init timers to infinite
            RecvTimer = new(OnRecvTimerElapsed, state: this, Timeout.Infinite, Timeout.Infinite);
            SendTimer = new(OnSendTimerElapsed, state: this, Timeout.Infinite, Timeout.Infinite);

            //Init reusable network stream
            NetworkStream = new(this);
        }

        public void Prepare()
        {
            NetworkStream.ReadTimeout = Timeout.Infinite;
            NetworkStream.WriteTimeout = Timeout.Infinite;
        }      

        public bool Release()
        {
            _sysSocketBufferSize = 0;

            //Reset pipes for use
            SendPipe.Reset();
            RecvPipe.Reset();

            return true;
        }

        /// <summary>
        /// Gets a buffer used during a socket accept operation
        /// </summary>
        /// <param name="bufferSize">The size hint of the buffer to get</param>
        /// <returns>A memory structure of the specified size</returns>
        public Memory<byte> GetMemory(int bufferSize) => RecvPipe.Writer.GetMemory(bufferSize);

        /*
         * NOTES
         * 
         * Timers used to maintain resource exhuastion independent 
         * of the actual socket pipeline, so to preserve the state 
         * of the pipelines until the writer is closed.
         * 
         * This choice was made to allow the api consumer to decide how to 
         * process a timeout without affecting the state of the pipelines
         * or socket until the close event.
         */

        private void OnRecvTimerElapsed(object? state)
        {
            //cancel pending read on recv pipe when timout expires
            RecvPipe.Reader.CancelPendingRead();
        }

        private void OnSendTimerElapsed(object? state)
        {
            //Cancel pending flush
            SendPipe.Writer.CancelPendingFlush();
        }

        /*
         * Pipeline worker tasks. Listen for data on the socket, 
         * and listen for data on the pipe to marshal data between 
         * the pipes and the socket
         */

        private ReadResult _sendReadRes;
        private int _sysSocketBufferSize;

        public async Task SendDoWorkAsync<TIO>(TIO sock, int sendBufferSize)
            where TIO : ISocketIo
        {
            Exception? errCause = null;
            ReadOnlySequence<byte>.Enumerator enumerator;
            ForwardOnlyMemoryReader<byte> segmentReader;

            try
            {
                _sysSocketBufferSize = sendBufferSize;

                //Enter work loop
                while (true)
                {
                    //wait for data from the write pipe and write it to the socket
                    _sendReadRes = await SendPipe.Reader.ReadAsync(CancellationToken.None);

                    //Catch error/cancel conditions and break the loop
                    if (_sendReadRes.IsCanceled || _sendReadRes.Buffer.IsEmpty)
                    {
                        break;
                    }

                    /*
                     * Even if the pipe was completed, and if the buffer is not empty, then 
                     * there is still data to be written to the socket, so we must continue
                     */

                    //Get enumerator to write memory segments
                   enumerator = _sendReadRes.Buffer.GetEnumerator();

                    //Begin enumerator
                    while (enumerator.MoveNext())
                    {

                        /*
                         * Using a foward only reader allows the following loop
                         * to track the ammount of data written to the socket
                         * until the entire segment has been sent or if it has
                         * move to the next segment
                         */

                        segmentReader = new(enumerator.Current);
                         
                        while(segmentReader.WindowSize > 0)
                        {
                            //Write segment to socket, and upate written data
                            int written = await sock.SendAsync(segmentReader.Window, SocketFlags.None);

                            if(written < 0)
                            {
                                goto ExitOnSocketErr;
                            }

                            if(written == segmentReader.WindowSize)
                            {
                                //All data was written
                                break;
                            }

                            //Advance unread window to end of the written data
                            segmentReader.Advance(written);
                        } 
                        //Advance to next window/segment
                    }
                  
                    //Advance pipe
                    SendPipe.Reader.AdvanceTo(_sendReadRes.Buffer.End);
                    
                    //Pipe has been completed and all data was written
                    if (_sendReadRes.IsCompleted)
                    {
                        break;
                    }
                }

            ExitOnSocketErr:
                ;

            }
            catch (Exception ex)
            {
                errCause = ex;   
            }
            finally
            {
                _sendReadRes = default;

                //Complete the send pipe reader
                await SendPipe.Reader.CompleteAsync(errCause);
            }
        }


        private FlushResult _recvFlushRes;

        public async Task RecvDoWorkAsync<TIO>(TIO sock, int bytesTransferred, int recvBufferSize)
            where TIO : ISocketIo
        {            
            Exception? cause = null;
            Memory<byte> buffer;

            try
            {
                //If initial data was buffered, it needs to be published to the reader
                if (bytesTransferred > 0)
                {
                    //Advance the write to written data from accept
                    RecvPipe.Writer.Advance(bytesTransferred);

                    //Flush initial data
                    _recvFlushRes = await RecvPipe.Writer.FlushAsync(CancellationToken.None);

                    //Check flush result for error/cancel
                    if (IsPipeClosedAfterFlush(ref _recvFlushRes))
                    {
                        //Exit
                        return;
                    }
                }

                //Enter work loop
                while (true)
                {
                    //Get buffer from pipe writer
                    buffer = RecvPipe.Writer.GetMemory(recvBufferSize);
                    
                    //Wait for data or error from socket
                    int count = await sock.ReceiveAsync(buffer, SocketFlags.None);

                    if(count <= 0)
                    {
                        //Connection is softly closing, exit
                        break;
                    }

                    //Advance/notify the pipe
                    RecvPipe.Writer.Advance(count);

                    //Publish read data
                    _recvFlushRes = await RecvPipe.Writer.FlushAsync(CancellationToken.None);

                    //Writing has completed, time to exit
                    if (IsPipeClosedAfterFlush(ref _recvFlushRes))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                cause = ex;

                //Cancel sending reader task because the socket has an error and cannot be used
                SendPipe.Reader.CancelPendingRead();
            }
            finally
            {
                _recvFlushRes = default;

                //Stop timer incase exception
                RecvTimer.Stop();

                //Cleanup and complete the writer
                await RecvPipe.Writer.CompleteAsync(cause);
                //The recv reader is completed by the network stream
            }
        }


        private static bool IsPipeClosedAfterFlush(ref FlushResult result) => result.IsCanceled || result.IsCompleted;


        /// <summary>
        /// The internal cleanup/dispose method to be called
        /// when the pipeline is no longer needed
        /// </summary>
        public void DisposeInternal()
        {
            RecvTimer.Dispose();
            SendTimer.Dispose();
        }

        /// <summary>
        /// Must be called when the pipeline is requested to be closed
        /// </summary>
        /// <returns>A value task that complets when the piepline is completed</returns>
        internal async ValueTask ShutDownClientPipeAsync()
        {
            //Complete the data input so sending completes
            await SendPipe.Writer.CompleteAsync();
            await RecvPipe.Reader.CompleteAsync();
        }


        private static async Task AwaitFlushTask<TTimer>(ValueTask<FlushResult> valueTask, TTimer timer)
            where TTimer : INetTimer
        {
            try
            {
                FlushResult result = await valueTask.ConfigureAwait(false);
                ThrowHelpers.ThrowIfWriterCanceled(result.IsCanceled);
            }
            finally
            {
                timer.Stop();
            }
        }

        private ValueTask SendWithTimerInternalAsync<TTimer>(in TTimer timer, CancellationToken cancellation)
            where TTimer : INetTimer
        {
            //Start send timer
            timer.Start();
            try
            {
                //Send the segment
                ValueTask<FlushResult> result = SendPipe.Writer.FlushAsync(cancellation);

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
            catch
            {
                //Stop timer on exception
                timer.Stop();
                throw;
            }
        }

        private ValueTask SendAsync(ReadOnlySpan<byte> data, int timeout, CancellationToken cancellation)
        {
            //Publish send data to send pipe
            CopyAndPublishDataOnSendPipe(data, _sysSocketBufferSize, SendPipe.Writer);

            //See if timer is required
            if (timeout < 1)
            {
                NoOpTimerWrapper noOpTimer = default;

                //no timer
                return SendWithTimerInternalAsync(in noOpTimer, cancellation);
            }
            else
            {
                TpTimerWrapper sendTimer = new(SendTimer, timeout);

                //Pass new send timer to send method
                return SendWithTimerInternalAsync(in sendTimer, cancellation);
            }
        }

        ValueTask ITransportInterface.SendAsync(ReadOnlyMemory<byte> data, int timeout, CancellationToken cancellation)
        {
            return SendAsync(data.Span, timeout, cancellation);
        }

        private static void CopyAndPublishDataOnSendPipe<TWriter>(ReadOnlySpan<byte> src, int bufferSize, TWriter writer)
            where TWriter: IBufferWriter<byte>
        {
            Debug.Assert(bufferSize > 0, "A call to CopyAndPublishDataOnSendPipe was made before a socket was connected");

            ref byte srcRef = ref MemoryMarshal.GetReference(src);

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
                Span<byte> dest = writer.GetSpan(dataToCopy);
                ref byte destRef = ref MemoryMarshal.GetReference(dest);

                //Copy data to the buffer at the new position (attempt to use hardware acceleration)
                MemoryUtil.AcceleratedMemmove(ref srcRef, written, ref destRef, 0, (uint)dataToCopy);

                //Advance the writer by the number of bytes written
                writer.Advance(dataToCopy);

                //Increment the written count
                written += (uint)dataToCopy;
            }
        }       

        void ITransportInterface.Send(ReadOnlySpan<byte> data, int timeout)
        {
            //Call async send and wait for completion
            ValueTask result = SendAsync(data, timeout, CancellationToken.None);

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

        private ValueTask<int> RecvWithTimeoutAsync<TTimer>(Memory<byte> data, in TTimer timer, CancellationToken cancellation)
            where TTimer : INetTimer
        {
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
                    RecvTimer.Stop();

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
        }

        ValueTask<int> ITransportInterface.RecvAsync(Memory<byte> buffer, int timeout, CancellationToken cancellation)
        {
            //See if timer is required
            if (timeout < 1)
            {
                NoOpTimerWrapper noOpTimer = default;

                //no timer
                return RecvWithTimeoutAsync(buffer, in noOpTimer, cancellation);
            }
            else
            {
                TpTimerWrapper recvTimer = new(RecvTimer, timeout);

                //Pass new send timer to send method
                return RecvWithTimeoutAsync(buffer, in recvTimer, cancellation);
            }
        }

        int ITransportInterface.Recv(Span<byte> buffer, int timeout)
        {
            //Restart timer
            RecvTimer.Restart(timeout);
            try
            {
                return RecvStream.Read(buffer);
            }
            finally
            {
                RecvTimer.Stop();
            }
        }
      

        private static class ThrowHelpers
        {            
            public static void ThrowIfWriterCanceled(bool isCancelled) 
            {
                if(isCancelled)
                {
                    throw new OperationCanceledException("The write operation was canceled by the underlying PipeWriter");
                }               
            }
        }

        private interface INetTimer
        {
            void Start();

            void Stop();
        }

        private readonly struct TpTimerWrapper : INetTimer
        {
            private readonly Timer _timer;
            private readonly int _timeout;

            public TpTimerWrapper(Timer timer, int timeout)
            {
                _timer = timer;
                _timeout = timeout;
            }

            public readonly void Start() => _timer.Restart(_timeout);

            public readonly void Stop() => _timer.Stop();
        }

        private readonly struct NoOpTimerWrapper : INetTimer
        {
            public readonly void Start() { }

            public readonly void Stop() { }
        }
    }
}
