/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Net.Sockets;
using System.IO.Pipelines;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

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
        public void Prepare()
        {}

        public bool Release()
        {
            /*
             * If the pipeline has been started, then the pipes 
             * will be completed by the worker threads (or by the streams)
             * and when release is called, there will no longer be 
             * an observer for the result, which means the pipes 
             * may be safely reset for reuse
             */
            if (_recvTask != null)
            {                 
                SendPipe.Reset();
                RecvPipe.Reset();
            }
            /*
             * If socket had an error and was not started,
             * it means there may be data written to the 
             * recv pipe from the accept operation, that 
             * needs to be cleared
             */
            else
            {
                //Complete the recvpipe then reset it to discard buffered data
                RecvPipe.Reader.Complete();
                RecvPipe.Writer.Complete();
                //now reset it
                RecvPipe.Reset();
            }
           
            //Cleanup tasks
            _recvTask = null;
            _sendTask = null;
            
            //Cleanup cts
            _cts?.Dispose();
            _cts = null;

            return true;
        }

        private Task? _recvTask;
        private Task? _sendTask;
      
        private CancellationTokenSource? _cts;
        
        public readonly ReusableNetworkStream NetworkStream;

        private readonly Pipe SendPipe;
        private readonly Pipe RecvPipe;
        private readonly Timer RecvTimer;
        private readonly Timer SendTimer;
        private readonly Stream RecvStream;

        ///<inheritdoc/>
        public int SendTimeoutMs { get; set; }

        ///<inheritdoc/>
        public int RecvTimeoutMs { get; set; }


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

            SendTimeoutMs = Timeout.Infinite;
            RecvTimeoutMs = Timeout.Infinite;
        }

        /// <summary>
        /// Gets a buffer used during a socket accept operation
        /// </summary>
        /// <param name="bufferSize">The size hint of the buffer to get</param>
        /// <returns>A memory structure of the specified size</returns>
        public Memory<byte> GetMemory(int bufferSize) => RecvPipe.Writer.GetMemory(bufferSize);

        /// <summary>
        /// Begins async work to receive and send data on a connected socket
        /// </summary>
        /// <param name="client">The socket to read/write from</param>
        /// <param name="bytesTransferred">The number of bytes to be commited</param>
        public void Start(Socket client, int bytesTransferred)
        {
            //Advance writer
            RecvPipe.Writer.Advance(bytesTransferred);
            //begin recv tasks, and pass inital data to be flushed flag
            _recvTask = RecvDoWorkAsync(client, bytesTransferred > 0);
            _sendTask = SendDoWorkAsync(client);
        }


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

        private async Task SendDoWorkAsync(Socket sock)
        {
            Exception? cause = null;
            try
            {
                //Enter work loop
                while (true)
                {
                    //wait for data from the write pipe and write it to the socket
                    _sendReadRes = await SendPipe.Reader.ReadAsync(CancellationToken.None);

                    //Catch error/cancel conditions and break the loop
                    if (_sendReadRes.IsCanceled || !sock.Connected || _sendReadRes.Buffer.IsEmpty)
                    {
                        break;
                    }

                    /*
                     * Even if the pipe was completed, and if the buffer is not empty, then 
                     * there is still data to be written to the socket, so we must continue
                     */

                    //Get enumerator to write memory segments
                    ReadOnlySequence<byte>.Enumerator enumerator = _sendReadRes.Buffer.GetEnumerator();

                    //Begin enumerator
                    while (enumerator.MoveNext())
                    {

                        /*
                         * Using a foward only reader allows the following loop
                         * to track the ammount of data written to the socket
                         * until the entire segment has been sent or if it has
                         * move to the next segment
                         */

                        ForwardOnlyMemoryReader<byte> reader = new(enumerator.Current);
                         
                        while(reader.WindowSize > 0)
                        {
                            //Write segment to socket, and upate written data
                            int written = await sock.SendAsync(reader.Window, SocketFlags.None);

                            if(written >= reader.WindowSize)
                            {
                                //All data was written
                                break;
                            }

                            //Advance unread window to end of the written data
                            reader.Advance(written);
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
            }
            catch (Exception ex)
            {
                cause = ex;
            }
            finally
            {
                _sendReadRes = default;

                //Complete the send pipe writer
                await SendPipe.Reader.CompleteAsync(cause);

                //Cancel the recv task
                _cts!.Cancel();
            }
        }

        private FlushResult _recvFlushRes;

        private async Task RecvDoWorkAsync(Socket sock, bool initialData)
        {
            //init new cts
            _cts = new();
            
            Exception? cause = null;
            try
            {
                //Avoid syscall?
                int bufferSize = sock.ReceiveBufferSize;

                //If initial data was buffered, it needs to be published to the reader
                if (initialData)
                {
                    //Flush initial data
                    FlushResult res = await RecvPipe.Writer.FlushAsync(CancellationToken.None);

                    if (res.IsCompleted || res.IsCanceled)
                    {
                        //Exit
                        return;
                    }
                }

                //Enter work loop
                while (true)
                {
                    //Get buffer from pipe writer
                    Memory<byte> buffer = RecvPipe.Writer.GetMemory(bufferSize);
                    
                    //Wait for data or error from socket
                    int count = await sock.ReceiveAsync(buffer, SocketFlags.None, _cts.Token);

                    //socket returned emtpy data
                    if (count == 0 || !sock.Connected)
                    {
                        break;
                    }

                    //Advance/notify the pipe
                    RecvPipe.Writer.Advance(count);

                    //Publish read data
                    _recvFlushRes = await RecvPipe.Writer.FlushAsync(CancellationToken.None);

                    //Writing has completed, time to exit
                    if (_recvFlushRes.IsCompleted || _recvFlushRes.IsCanceled)
                    {
                        break;
                    }
                }
            }
            //Normal exit
            catch (OperationCanceledException)
            {}
            catch (SocketException se)
            {
                cause = se;
                //Cancel sending reader task because the socket has an error and cannot be used
                SendPipe.Reader.CancelPendingRead();
            }
            catch (Exception ex)
            {
                cause = ex;
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

        /// <summary>
        /// The internal cleanup/dispose method to be called
        /// when the pipeline is no longer needed
        /// </summary>
        public void DisposeInternal()
        {
            RecvTimer.Dispose();
            SendTimer.Dispose();

            //Perform some managed cleanup
            
            //Cleanup tasks
            _recvTask = null;
            _sendTask = null;

            //Cleanup cts
            _cts?.Dispose();
            _cts = null;
        }
       

        private static async Task AwaitFlushTask(ValueTask<FlushResult> valueTask, Timer? sendTimer)
        {
            try
            {
                FlushResult result = await valueTask.ConfigureAwait(false);

                if (result.IsCanceled)
                {
                    ThrowHelpers.ThrowWriterCanceled();
                }
            }
            finally
            {
                sendTimer?.Stop();
            }
        }

        private ValueTask SendWithTimerInternalAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation)
        {
            //Start send timer
            SendTimer.Restart(SendTimeoutMs);
            try
            {
                //Send the segment
                ValueTask<FlushResult> result = SendPipe.Writer.WriteAsync(data, cancellation);

                //Task completed successfully, so 
                if (result.IsCompleted)
                {
                    //Stop timer
                    SendTimer.Stop();

                    //safe to get the flush result sync, may throw, so preserve the call stack
                    FlushResult fr = result.GetAwaiter().GetResult();
                    
                    //Check for canceled and throw
                    return fr.IsCanceled
                        ? throw new OperationCanceledException("The write operation was canceled by the underlying PipeWriter")
                        : ValueTask.CompletedTask;
                }
                else
                {
                    //Wrap the task in a ValueTask since it must be awaited, and will happen on background thread
                    return new(AwaitFlushTask(result, SendTimer));
                }
            }
            catch
            {
                //Stop timer on exception
                SendTimer.Stop();
                throw;
            }
        }

        private ValueTask SendWithoutTimerInternalAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation)
        {
            //Send the segment
            ValueTask<FlushResult> result = SendPipe.Writer.WriteAsync(data, cancellation);

            //Task completed successfully, so 
            if (result.IsCompleted)
            {
                /*
                 * We can get the flush result synchronously, it may throw
                 * so preserve the call stack
                 */
                FlushResult fr = result.GetAwaiter().GetResult();
                
                //Check for canceled and throw
                return fr.IsCanceled
                    ? throw new OperationCanceledException("The write operation was canceled by the underlying PipeWriter")
                    : ValueTask.CompletedTask;
            }
            else
            {
                //Wrap the task in a ValueTask since it must be awaited, and will happen on background thread
                return new(AwaitFlushTask(result, null));
            }
        }

        ValueTask ITransportInterface.SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation)
        {
            //Use timer if timeout is set, dont otherwise
            return SendTimeoutMs < 1 ? SendWithoutTimerInternalAsync(data, cancellation) : SendWithTimerInternalAsync(data, cancellation);
        }
      
        void ITransportInterface.Send(ReadOnlySpan<byte> data)
        {
            //Determine if the send timer should be used
            Timer? _timer = SendTimeoutMs < 1 ? null : SendTimer;
            
            //Write data directly to the writer buffer
            SendPipe.Writer.Write(data);

            //Start send timer
            _timer?.Restart(SendTimeoutMs);
            
            try
            {
                //Send the segment
                ValueTask<FlushResult> result = SendPipe.Writer.FlushAsync(CancellationToken.None);

                //Await the result synchronously
                FlushResult fr = result.ConfigureAwait(false).GetAwaiter().GetResult();

                if (fr.IsCanceled)
                {
                    ThrowHelpers.ThrowWriterCanceled();
                }
            }
            finally
            {
                //Stop timer
                _timer?.Stop();                
            }
        }


        ValueTask<int> ITransportInterface.RecvAsync(Memory<byte> buffer, CancellationToken cancellation)
        {
            static async Task<int> AwaitAsyncRead(ValueTask<int> task, Timer recvTimer)
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

            //Restart recv timer
            RecvTimer.Restart(RecvTimeoutMs);
            try
            {
                //Read async and get the value task
                ValueTask<int> result = RecvStream.ReadAsync(buffer, cancellation);

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
                    return new(AwaitAsyncRead(result, RecvTimer));
                }
            }
            catch
            {
                RecvTimer.Stop();
                throw;
            }
        }

        int ITransportInterface.Recv(Span<byte> buffer)
        {
            //Restart timer
            RecvTimer.Restart(RecvTimeoutMs);
            try
            {
                return RecvStream.Read(buffer);
            }
            finally
            {
                RecvTimer.Stop();
            }
        }

        Task ITransportInterface.CloseAsync()
        {
            //Complete the send pipe writer since stream is closed
            ValueTask vt = SendPipe.Writer.CompleteAsync();
            //Complete the recv pipe reader since its no longer used
            ValueTask rv = RecvPipe.Reader.CompleteAsync();
            //Join worker tasks, no alloc if completed sync, otherwise alloc anyway
            return Task.WhenAll(vt.AsTask(), rv.AsTask(), _recvTask!, _sendTask!);
        }
      

        private static class ThrowHelpers
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ThrowWriterCanceled() 
            {
                throw new OperationCanceledException("The write operation was canceled by the underlying PipeWriter");
            }
        }
    }
}
