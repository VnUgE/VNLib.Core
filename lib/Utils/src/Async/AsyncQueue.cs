/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AsyncQueue.cs 
*
* AsyncQueue.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Diagnostics.CodeAnalysis;

namespace VNLib.Utils.Async
{

    /// <summary>
    /// Provides a <see cref="Channel{T}"/> based asynchronous queue
    /// </summary>
    /// <typeparam name="T">The event object type</typeparam>
    public class AsyncQueue<T> : IAsyncQueue<T>
    {
        private readonly Channel<T> _channel;

        /// <summary>
        /// Initalizes a new multi-threaded bound channel queue, that accepts 
        /// the <paramref name="capacity"/> number of items before it will 
        /// return asynchronously, or fail to enqueue items
        /// </summary>
        /// <param name="capacity">The maxium number of items to allow in the queue</param>
        public AsyncQueue(int capacity) : this(false, false, capacity)
        { }

        /// <summary>
        /// Initalizes a new multi-threaded unbound channel queue
        /// </summary>
        public AsyncQueue() : this(false, false)
        { }

        /// <summary>
        /// Initalizes a new queue that allows specifying concurrency requirements 
        /// and a bound/unbound channel capacity
        /// </summary>
        /// <param name="singleWriter">A value that specifies only a single thread be enqueing items?</param>
        /// <param name="singleReader">A value that specifies only a single thread will be dequeing</param>
        /// <param name="capacity">
        /// The maxium number of items to enque without failing. If set to <see cref="int.MaxValue"/> maximum is disabled
        /// </param>
        public AsyncQueue(bool singleWriter, bool singleReader, int capacity = int.MaxValue)
        {
            if (capacity == int.MaxValue)
            {
                //Create unbounded
                UnboundedChannelOptions opt = new()
                {
                    SingleReader = singleReader,
                    SingleWriter = singleWriter,
                    AllowSynchronousContinuations = true,
                };
                _channel = Channel.CreateUnbounded<T>(opt);
            }
            else
            {
                //Create bounded
                BoundedChannelOptions opt = new(capacity)
                {
                    SingleReader = singleReader,
                    SingleWriter = singleWriter,
                    AllowSynchronousContinuations = true,
                    //Default wait for space
                    FullMode = BoundedChannelFullMode.Wait
                };
                _channel = Channel.CreateBounded<T>(opt);
            }
        }

        /// <summary>
        /// Initalizes a new unbound channel based queue
        /// </summary>
        /// <param name="ubOptions">Channel options</param>
        public AsyncQueue(UnboundedChannelOptions ubOptions) 
            => _channel = Channel.CreateUnbounded<T>(ubOptions);

        /// <summary>
        /// Initalizes a new bound channel based queue
        /// </summary>
        /// <param name="options">Channel options</param>
        public AsyncQueue(BoundedChannelOptions options) 
            => _channel = Channel.CreateBounded<T>(options);

        /// <inheritdoc/>
        public bool TryEnqueue(T item) => _channel.Writer.TryWrite(item);

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default) => _channel.Writer.WriteAsync(item, cancellationToken);

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default) => _channel.Reader.ReadAsync(cancellationToken);

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public bool TryDequeue([MaybeNullWhen(false)] out T result) => _channel.Reader.TryRead(out result);

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public bool TryPeek([MaybeNullWhen(false)] out T result) => _channel.Reader.TryPeek(out result);
    }
}
