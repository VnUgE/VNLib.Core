/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AccessSerializer.cs 
*
* AccessSerializer.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Resources;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Provides access arbitration to an exclusive resouce
    /// </summary>
    /// <typeparam name="TKey">The uinique identifier type for the resource</typeparam>
    /// <typeparam name="TResource">The resource type</typeparam>
    public sealed class AccessSerializer<TKey, TResource> where TResource : IExclusiveResource
    {
        private readonly SemaphoreSlim semaphore;
        private readonly Func<TKey, TResource> Factory;
        private readonly Action CompletedCb;       
        private int WaitingCount;
        /// <summary>
        /// Creates a new <see cref="AccessSerializer{K, T}"/> with the specified factory and completed callback
        /// </summary>
        /// <param name="factory">Factory function to genereate new <typeparamref name="TResource"/> objects from <typeparamref name="TKey"/> keys</param>
        /// <param name="completedCb">Function to be invoked when the encapsulated objected is no longer in use</param>
        /// <exception cref="ArgumentNullException"></exception>
        public AccessSerializer(Func<TKey, TResource> factory, Action completedCb)
        {
            this.Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.CompletedCb = completedCb;
            //Setup semaphore for locking
            this.semaphore = new SemaphoreSlim(1, 1);
            this.WaitingCount = 0;
        }

        /// <summary>
        /// Attempts to obtain an exclusive lock on the object 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="wait">Time to wait for lock</param>
        /// <param name="exObj"></param>
        /// <returns>true if lock was obtained within the timeout, false if the lock was not obtained</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public bool TryWait(TKey key, TimeSpan wait, out ExclusiveResourceHandle<TResource> exObj) 
        {
            //Increase waiting count while we wait
            Interlocked.Increment(ref WaitingCount);
            try
            {
                //Try to obtain the lock
                if (semaphore.Wait(wait))
                {
                    TResource get() => Factory(key);
                    //Create new exclusive lock handle that will generate a new that calls release when freed
                    exObj = new(get, Release);
                    return true;
                }
                //Lock not taken
                exObj = null;
                return false;
            }
            finally
            {
                //Decrease the waiting count since we are no longer waiting
                Interlocked.Decrement(ref WaitingCount);
            }
        }
        /// <summary>
        /// Waits for exclusive access to the resource.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>An <see cref="ExclusiveResourceHandle{T}"/> encapsulating the resource</returns>
        public ExclusiveResourceHandle<TResource> Wait(TKey key)
        {
            try
            {
                //Increase waiting count while we wait
                Interlocked.Increment(ref WaitingCount);
                //Try to obtain the lock
                semaphore.Wait();
                //Local function to generate the output value
                TResource get() => Factory(key);
                //Create new exclusive lock handle that will generate a new that calls release when freed
                return new(get, Release);
            }
            finally
            {
                //Decrease the waiting count since we are no longer waiting
                Interlocked.Decrement(ref WaitingCount);
            }
        }
        /// <summary>
        /// Asynchronously waits for exclusive access to the resource.
        /// </summary>
        /// <returns>An <see cref="ExclusiveResourceHandle{TResource}"/> encapsulating the resource</returns>
        public async Task<ExclusiveResourceHandle<TResource>> WaitAsync(TKey key, CancellationToken cancellationToken = default)
        {
            try
            {
                //Increase waiting count while we wait
                Interlocked.Increment(ref WaitingCount);
                //Try to obtain the lock
                await semaphore.WaitAsync(cancellationToken);
                //Local function to generate the output value
                TResource get() => Factory(key);
                //Create new exclusive lock handle that will generate a new that calls release when freed
                return new(get, Release);
            }
            finally
            {
                //Decrease the waiting count since we are no longer waiting
                Interlocked.Decrement(ref WaitingCount);
            }
        }
        /// <summary>
        /// Releases an exclusive lock that is held on an object
        /// </summary>
        private void Release()
        {
            /*
            * If objects are waiting for the current instance, then we will release 
            * the semaphore and exit, as we no longer have control over the context
            */
            if (WaitingCount > 0)
            {
                this.semaphore.Release();
            }
            else
            {
                //Do not release the sempahore, just dispose of the semaphore
                this.semaphore.Dispose();
                //call the completed function
                CompletedCb?.Invoke();
            }
        }
    }

    /// <summary>
    /// Provides access arbitration to an <see cref="IExclusiveResource"/>
    /// </summary>
    /// <typeparam name="TKey">The uinique identifier type for the resource</typeparam>
    /// <typeparam name="TArg">The type of the optional argument to be passed to the user-implemented factory function</typeparam>
    /// <typeparam name="TResource">The resource type</typeparam>
    public sealed class AccessSerializer<TKey, TArg, TResource> where TResource : IExclusiveResource
    {
        private readonly SemaphoreSlim semaphore;
        private readonly Func<TKey, TArg, TResource> Factory;
        private readonly Action CompletedCb;
        private int WaitingCount;
        /// <summary>
        /// Creates a new <see cref="AccessSerializer{TKey, TArg, TResource}"/> with the specified factory and completed callback
        /// </summary>
        /// <param name="factory">Factory function to genereate new <typeparamref name="TResource"/> objects from <typeparamref name="TKey"/> keys</param>
        /// <param name="completedCb">Function to be invoked when the encapsulated objected is no longer in use</param>
        /// <exception cref="ArgumentNullException"></exception>
        public AccessSerializer(Func<TKey, TArg, TResource> factory, Action completedCb)
        {
            this.Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.CompletedCb = completedCb;
            //Setup semaphore for locking
            this.semaphore = new SemaphoreSlim(1, 1);
            this.WaitingCount = 0;
        }

        /// <summary>
        /// Attempts to obtain an exclusive lock on the object 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="arg">The key identifying the resource</param>
        /// <param name="wait">Time to wait for lock</param>
        /// <param name="exObj"></param>
        /// <returns>true if lock was obtained within the timeout, false if the lock was not obtained</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public bool TryWait(TKey key, TArg arg, TimeSpan wait, out ExclusiveResourceHandle<TResource> exObj)
        {
            //Increase waiting count while we wait
            Interlocked.Increment(ref WaitingCount);
            try
            {
                //Try to obtain the lock
                if (semaphore.Wait(wait))
                {
                    TResource get() => Factory(key, arg);
                    //Create new exclusive lock handle that will generate a new that calls release when freed
                    exObj = new(get, Release);
                    return true;
                }
                //Lock not taken
                exObj = null;
                return false;
            }
            finally
            {
                //Decrease the waiting count since we are no longer waiting
                Interlocked.Decrement(ref WaitingCount);
            }
        }       
        /// <summary>
        /// Waits for exclusive access to the resource.
        /// </summary>
        /// <param name="key">The unique key that identifies the resource</param>
        /// <param name="arg">The state argument to pass to the factory function</param>
        /// <returns>An <see cref="ExclusiveResourceHandle{TResource}"/> encapsulating the resource</returns>
        public ExclusiveResourceHandle<TResource> Wait(TKey key, TArg arg)
        {
            try
            {
                //Increase waiting count while we wait
                Interlocked.Increment(ref WaitingCount);
                //Try to obtain the lock
                semaphore.Wait();
                //Local function to generate the output value
                TResource get() => Factory(key, arg);
                //Create new exclusive lock handle that will generate a new that calls release when freed
                return new(get, Release);
            }
            finally
            {
                //Decrease the waiting count since we are no longer waiting
                Interlocked.Decrement(ref WaitingCount);
            }
        }
        /// <summary>
        /// Asynchronously waits for exclusive access to the resource.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="arg">The state argument to pass to the factory function</param>
        /// <param name="cancellationToken"></param>
        /// <returns>An <see cref="ExclusiveResourceHandle{TResource}"/> encapsulating the resource</returns>
        public async Task<ExclusiveResourceHandle<TResource>> WaitAsync(TKey key, TArg arg, CancellationToken cancellationToken = default)
        {
            try
            {
                //Increase waiting count while we wait
                Interlocked.Increment(ref WaitingCount);
                //Try to obtain the lock
                await semaphore.WaitAsync(cancellationToken);
                //Local function to generate the output value
                TResource get() => Factory(key, arg);
                //Create new exclusive lock handle that will generate a new that calls release when freed
                return new(get, Release);
            }
            finally
            {
                //Decrease the waiting count since we are no longer waiting
                Interlocked.Decrement(ref WaitingCount);
            }
        }       

        /// <summary>
        /// Releases an exclusive lock that is held on an object
        /// </summary>
        private void Release()
        {
            /*
            * If objects are waiting for the current instance, then we will release 
            * the semaphore and exit, as we no longer have control over the context
            */
            if (WaitingCount > 0)
            {
                this.semaphore.Release();
            }
            else
            {
                //Do not release the sempahore, just dispose of the semaphore
                this.semaphore.Dispose();
                //call the completed function
                CompletedCb?.Invoke();
            }
        }
    }
}