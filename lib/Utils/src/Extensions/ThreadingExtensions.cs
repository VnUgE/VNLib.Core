﻿/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ThreadingExtensions.cs 
*
* ThreadingExtensions.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Async;
using VNLib.Utils.Resources;

namespace VNLib.Utils.Extensions
{

    /// <summary>
    /// Provides extension methods to common threading and TPL library operations
    /// </summary>
    public static class ThreadingExtensions
    {
        /// <summary>
        /// Waits for exlcusive access to the resource identified by the given moniker
        /// and returns a handle that will release the lock when disposed.
        /// </summary>
        /// <typeparam name="TMoniker"></typeparam>
        /// <param name="serialzer"></param>
        /// <param name="moniker">The moniker used to identify the lock</param>
        /// <param name="cancellation">A token to cancel the wait operation</param>
        /// <returns>A task that resolves a handle that holds the lock information and releases the lock when disposed</returns>
        public static Task<SerializerHandle<TMoniker>> GetHandleAsync<TMoniker>(
           this IAsyncAccessSerializer<TMoniker> serialzer,
           TMoniker moniker,
           CancellationToken cancellation = default
           )
        {
            //Wait async get handle
            static async Task<SerializerHandle<TMoniker>> AwaitHandle(Task wait, IAsyncAccessSerializer<TMoniker> serialzer, TMoniker moniker)
            {
                await wait.ConfigureAwait(false);
                return new SerializerHandle<TMoniker>(moniker, serialzer);
            }
         
            //Enter the lock async
            Task wait = serialzer.WaitAsync(moniker, cancellation);

            if (wait.IsCompleted)
            {
                //Allow throwing the exception if cancel or error

#pragma warning disable CA1849 // Call async methods when in an async method
                wait.GetAwaiter().GetResult();
#pragma warning restore CA1849 // Call async methods when in an async method

                //return the new handle
                return Task.FromResult(new SerializerHandle<TMoniker>(moniker, serialzer));
            }

            //Wait async
            return AwaitHandle(wait, serialzer, moniker);
        }

        /// <summary>
        /// Asynchronously waits to enter the <see cref="SemaphoreSlim"/> while observing a <see cref="CancellationToken"/>
        /// and getting a releaser handle
        /// </summary>
        /// <param name="semaphore"></param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A releaser handle that may be disposed to release the semaphore</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        public static async Task<SemSlimReleaser> GetReleaserAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            return new SemSlimReleaser(semaphore);
        }

        /// <summary>
        /// Asynchronously waits to enter the <see cref="SemaphoreSlim"/> using a 32-bit signed integer to measure the time intervale
        /// and getting a releaser handle
        /// </summary>
        /// <param name="semaphore"></param>
        /// <param name="timeout">A the maximum amount of time in milliseconds to wait to enter the semaphore</param>
        /// <returns>A releaser handle that may be disposed to release the semaphore</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static async Task<SemSlimReleaser> GetReleaserAsync(this SemaphoreSlim semaphore, int timeout)
        {
            if (await semaphore.WaitAsync(timeout))
            {
                return new SemSlimReleaser(semaphore);
            }
            throw new TimeoutException("Failed to enter the semaphore before the specified timeout period");
        }

        /// <summary>
        /// Blocks the current thread until it can enter the <see cref="SemaphoreSlim"/>
        /// </summary>
        /// <param name="semaphore"></param>
        /// <returns>A releaser handler that releases the semaphore when disposed</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static SemSlimReleaser GetReleaser(this SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new SemSlimReleaser(semaphore);
        }

        /// <summary>
        /// Blocks the current thread until it can enter the <see cref="SemaphoreSlim"/>
        /// </summary>
        /// <param name="semaphore"></param>
        /// <param name="timeout">A the maximum amount of time in milliseconds to wait to enter the semaphore</param>
        /// <returns>A releaser handler that releases the semaphore when disposed</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static SemSlimReleaser GetReleaser(this SemaphoreSlim semaphore, int timeout)
        {
            if (semaphore.Wait(timeout))
            {
                return new SemSlimReleaser(semaphore);
            }
            throw new TimeoutException("Failed to enter the semaphore before the specified timeout period");
        }

        /// <summary>
        /// Blocks the current thread until it can enter the <see cref="Mutex"/>
        /// </summary>
        /// <param name="mutex"></param>
        /// <returns>A releaser handler that releases the semaphore when disposed</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="AbandonedMutexException"></exception>
        public static MutexReleaser Enter(this Mutex mutex)
        {
            mutex.WaitOne();
            return new MutexReleaser(mutex);
        }

        /// <summary>
        /// Blocks the current thread until it can enter the <see cref="SemaphoreSlim"/>
        /// </summary>
        /// <param name="mutex"></param>
        /// <param name="timeout">A the maximum amount of time in milliseconds to wait to enter the semaphore</param>
        /// <returns>A releaser handler that releases the semaphore when disposed</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static MutexReleaser Enter(this Mutex mutex, int timeout)
        {
            if (mutex.WaitOne(timeout))
            {
                return new MutexReleaser(mutex);
            }
            throw new TimeoutException("Failed to enter the semaphore before the specified timeout period");
        }

        private static readonly Task<bool> TrueCompleted = Task.FromResult(true);
        private static readonly Task<bool> FalseCompleted = Task.FromResult(false);

        /// <summary>
        /// Asynchronously waits for a the <see cref="WaitHandle"/> to receive a signal. This method spins until 
        /// a thread yield will occur, then asynchronously yields.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="timeoutMs">The timeout interval in milliseconds</param>
        /// <returns>
        /// A task that compeletes when the wait handle receives a signal or times-out,
        /// the result of the awaited task will be <c>true</c> if the signal is received, or 
        /// <c>false</c> if the timeout interval expires
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Task<bool> WaitAsync(this WaitHandle handle, int timeoutMs = Timeout.Infinite)
        {
            ArgumentNullException.ThrowIfNull(handle);

            //test non-blocking handle state
            if (handle.WaitOne(0))
            {
                return TrueCompleted;
            }
            //When timeout is 0, wh will block, return false
            else if(timeoutMs == 0)
            {
                return FalseCompleted;
            }
            //Init short lived spinwait
            SpinWait sw = new();
            //Spin until yield occurs
            while (!sw.NextSpinWillYield)
            {
                sw.SpinOnce();
                //Check handle state
                if (handle.WaitOne(0))
                {
                    return TrueCompleted;
                }
            }

            return NoSpinWaitAsync(handle, timeoutMs);
        }

        /// <summary>
        /// Asynchronously waits for a the <see cref="WaitHandle"/> to receive a signal. This method spins until 
        /// a thread yield will occur, then asynchronously yields.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="timeoutMs">The timeout interval in milliseconds</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynct wait event</param>
        /// <returns>
        /// A task that compeletes when the wait handle receives a signal or times-out,
        /// the result of the awaited task will be <c>true</c> if the signal is received, or 
        /// <c>false</c> if the timeout interval expires
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Task<bool> WaitAsync(this WaitHandle handle, int timeoutMs, CancellationToken cancellation = default)
        {
            Task<bool> withoutToken = WaitAsync(handle, timeoutMs);

            return withoutToken.IsCompleted 
                ? withoutToken 
                : withoutToken.WaitAsync(cancellation);
        }

        /// <summary>
        /// Asynchronously waits for a the <see cref="WaitHandle"/> to receive a signal, without checking 
        /// current state or spinning. This function always returns a new task that will complete when the
        /// handle is signaled or the timeout interval expires.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="timeoutMs">Time (in ms)</param>
        /// <returns></returns>
        public static Task<bool> NoSpinWaitAsync(this WaitHandle handle, int timeoutMs)
        {            
            //Completion source used to signal the awaiter when the wait handle is signaled
            TaskCompletionSource<bool> completion = new(TaskCreationOptions.None);
            
            //Register wait on threadpool to complete the task source
            RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject(
                handle, 
                TaskCompletionCallback, 
                completion, 
                timeoutMs, executeOnlyOnce: true
            );

            //Register continuation to cleanup
            _ = completion.Task.ContinueWith(
                CleanupContinuation, 
                registration, 
                CancellationToken.None, 
                TaskContinuationOptions.ExecuteSynchronously, 
                TaskScheduler.Default
            ).ConfigureAwait(false);
            
            return completion.Task;
        }

        private static void CleanupContinuation(Task<bool> task, object? taskCompletion)
        {
            RegisteredWaitHandle registration = (taskCompletion as RegisteredWaitHandle)!;
            registration.Unregister(null);
            task.Dispose();
        }
        private static void TaskCompletionCallback(object? tcsState, bool timedOut)
        {
            TaskCompletionSource<bool> completion = (tcsState as TaskCompletionSource<bool>)!;
            //Set the result of the wait handle timeout
            _ = completion.TrySetResult(!timedOut);
        }
      

        /// <summary>
        /// Registers a callback method that will be called when the token has been cancelled.
        /// This method waits indefinitely for the token to be cancelled. 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="callback">The callback method to invoke when the token has been cancelled</param>
        /// <returns>A task that may be unobserved, that completes when the token has been cancelled</returns>
        public static Task RegisterUnobserved(this CancellationToken token, Action callback)
        {
            //Call callback when the wait handle is set
            return token.WaitHandle.WaitAsync()
                .ContinueWith(static (t, callback) => (callback as Action)!.Invoke(), 
                    callback, 
                    CancellationToken.None, 
                    TaskContinuationOptions.ExecuteSynchronously, 
                    TaskScheduler.Default
                );
        }
    }
}