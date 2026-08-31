/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AsyncLazyExtensions.cs 
*
* AsyncLazyExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils.Async;
using VNLib.Utils.Resources;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Provides extension methods for <see cref="IAsyncLazy{T}"/>.
    /// </summary>
    public static class AsyncLazyExtensions
    {
        /// <summary>
        /// Gets an <see cref="IAsyncLazy{T}"/> wrapper for the specified <see cref="Task{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result produced by the task.</typeparam>
        /// <param name="task">The task to wrap as an asynchronous lazy operation.</param>
        /// <returns>An <see cref="IAsyncLazy{T}"/> wrapper for the specified task.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
        public static IAsyncLazy<T> AsLazy<T>(this Task<T> task)
        {
            ArgumentNullException.ThrowIfNull(task);
            return new AsyncLazy<T>(task);
        }

        /// <summary>
        /// Transforms a lazy operation into another using the specified handler.
        /// </summary>
        /// <typeparam name="T">The source type of the lazy operation.</typeparam>
        /// <typeparam name="TResult">The result type of the transformation.</typeparam>
        /// <param name="lazy">The lazy operation to transform.</param>
        /// <param name="handler">A function that transforms the lazy result into the output type.</param>
        /// <returns>A new <see cref="IAsyncLazy{T}"/> that produces the transformed result.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="lazy"/> or <paramref name="handler"/> is <see langword="null"/>.</exception>
        public static IAsyncLazy<TResult> Transform<T, TResult>(this IAsyncLazy<T> lazy, Func<T, TResult> handler)
        {
            ArgumentNullException.ThrowIfNull(lazy);
            ArgumentNullException.ThrowIfNull(handler);

            static async Task<TResult> OnResult(IAsyncLazy<T> lazy, Func<T, TResult> cb)
            {
                T result = await lazy;
                return cb(result);
            }

            return OnResult(lazy, handler).AsLazy();
        }

        /*
         * Concrete implementation of IAsyncLazy that wraps a Task<T> and provides non-blocking 
         * access to the result.
         */
        private sealed class AsyncLazy<T> : IAsyncLazy<T>
        {
            private readonly Task<T> _task;
            private readonly LazyInitializer<T> _result;

            public AsyncLazy(Task<T> task)
            {
                _task = task ?? throw new ArgumentNullException(nameof(task));
                _result = new(() => _task.Result);
            }

            ///<inheritdoc/>
            public bool Completed => _task.IsCompleted;

            ///<inheritdoc/>
            public T Value
            {
                get
                {
                    /* Lazy initializer provides good concurrency and caching
                     * for the result once it's available. 
                     * 
                     * If the result is already cached, return it. 
                     * If the task is complete but the result is not loaded, then 
                     * trigger a concurrent load of the result.
                     * 
                     * Otherwise the task is faulted or not completed, so handle 
                     * those cases.
                     */

                    if (_result.IsLoaded || _task.IsCompletedSuccessfully)
                    {
                        return _result.Instance;
                    }
                    // Catch all non-successful but completed states to raise exceptions
                    else if (_task.IsCompleted)
                    {
                        // Unwrap and raise exception from result
                        return _task.GetAwaiter().GetResult();
                    }
                    else
                    {
                        throw new InvalidOperationException("The asynchronous operation has not completed.");
                    }
                }
            }

            ///<inheritdoc/>
            public TaskAwaiter<T> GetAwaiter() => _task.GetAwaiter();

            ///<inheritdoc/>
            public Task<T> AsTask() => _task;
        }
    }
}
