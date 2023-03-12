/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AsyncAccessSerializer.cs 
*
* AsyncAccessSerializer.cs is part of VNLib.Utils which is part of
* the larger VNLib collection of libraries and utilities.
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Memory.Caching;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Creates a base concrete implementation of an <see cref="IAsyncAccessSerializer{TMoniker}"/>
    /// </summary>
    /// <typeparam name="TMoniker">The moniker (key) type</typeparam>
    public class AsyncAccessSerializer<TMoniker> : IAsyncAccessSerializer<TMoniker>, ICacheHolder where TMoniker : notnull
    {
        /// <summary>
        /// The mutual exclusion monitor locking object
        /// </summary>
        protected object StoreLock { get; }

        /// <summary>
        /// A cache pool for <see cref="WaitEntry"/>
        /// </summary>
        protected Stack<WaitEntry> EntryPool { get; }

        /// <summary>
        /// The table containing all active waiters
        /// </summary>
        protected Dictionary<TMoniker, WaitEntry> WaitTable { get; }

        /// <summary>
        /// The maxium number of elements allowed in the internal entry cache pool
        /// </summary>
        protected int MaxPoolSize { get; }

        /// <summary>
        /// Initializes a new <see cref="AsyncAccessSerializer{TMoniker}"/> with the desired 
        /// caching pool size and initial capacity
        /// </summary>
        /// <param name="maxPoolSize">The maxium number of cached wait entry objects</param>
        /// <param name="initialCapacity">The initial capacity of the wait table</param>
        /// <param name="keyComparer">The moniker key comparer</param>
        public AsyncAccessSerializer(int maxPoolSize, int initialCapacity, IEqualityComparer<TMoniker>? keyComparer)
        {
            MaxPoolSize = maxPoolSize;
            StoreLock = new();
            EntryPool = new Stack<WaitEntry>(maxPoolSize);
            WaitTable = new(initialCapacity, keyComparer);
        }

        ///<inheritdoc/>
        public virtual Task WaitAsync(TMoniker moniker, CancellationToken cancellation = default)
        {
            //Token must not be cancelled 
            cancellation.ThrowIfCancellationRequested();

            WaitEnterToken token;

            lock (StoreLock)
            {
                //See if the entry already exists, otherwise get a new wait entry
                if (!WaitTable.TryGetValue(moniker, out WaitEntry? wait))
                {
                    GetWaitEntry(ref wait, moniker);

                    //Add entry to store
                    WaitTable[moniker] = wait;
                }

                //Get waiter before leaving lock
                token = wait.GetWaiter();
            }

            return token.EnterWaitAsync(cancellation);
        }

        ///<inheritdoc/>
        public virtual void Release(TMoniker moniker)
        {
            /*
             * When releasing a lock on a moniker, we store entires in an internal table. Wait entires also require mutual
             * exclustion to properly track waiters. This happens inside a single lock for lower entry times/complexity. 
             * The wait's internal semaphore may also cause longer waits within the lock, so wait entires are "prepared"
             * by using tokens to access the wait/release mechanisms with proper tracking.
             * 
             * Tokens can be used to control the wait because the call to release may cause thread yielding (if internal 
             * WaitHandle is being used), so we don't want to block other callers.
             * 
             * When there are no more waiters for a moniker at the time the lock was entered, the WaitEntry is released
             * back to the pool.
             */

            WaitReleaseToken releaser;

            lock (StoreLock)
            {
                WaitEntry entry = WaitTable[moniker];

                //Call release while holding store lock
                if (entry.Release(out releaser) == 0)
                {
                    //No more waiters
                    WaitTable.Remove(moniker);

                    /*
                     * We must release the semaphore before returning to pool, 
                     * its safe because there are no more waiters
                     */
                    releaser.Release();

                    ReturnEntry(entry);

                    //already released
                    releaser = default;
                }
            }
            //Release sem outside of lock
            releaser.Release();
        }


        /// <summary>
        /// Gets a <see cref="WaitEntry"/> from the pool, or initializes a new one
        /// and stores the moniker referrence
        /// </summary>
        /// <param name="wait">The <see cref="WaitEntry"/> referrence to initialize</param>
        /// <param name="moniker">The moniker referrence to initialize</param>
        protected virtual void GetWaitEntry([NotNull] ref WaitEntry? wait, TMoniker moniker)
        {
            //Try to get wait from pool
            if (!EntryPool.TryPop(out wait))
            {
                wait = new();
            }

            //Init wait with session
            wait.Prepare(moniker);
        }

        /// <summary>
        /// Returns an empty <see cref="WaitEntry"/> back to the pool for reuse
        /// </summary>
        /// <param name="entry">The entry to return to the pool</param>
        protected virtual void ReturnEntry(WaitEntry entry)
        {
            //Remove session ref
            entry.Prepare(default);

            if (EntryPool.Count < MaxPoolSize)
            {
                EntryPool.Push(entry);
            }
            else
            {
                //Dispose entry since were not storing it
                entry.Dispose();
            }
        }

        /// <summary>
        /// NOOP
        /// </summary>
        public void CacheClear()
        { }

        ///<inheritdoc/>
        public void CacheHardClear()
        {
            //Take lock to remove the stored wait entires to dispose of them
            WaitEntry[] pooled;

            lock (StoreLock)
            {
                pooled = EntryPool.ToArray();
                EntryPool.Clear();

                //Cleanup the wait store
                WaitTable.TrimExcess();
            }

            //Dispose entires
            Array.ForEach(pooled, static pooled => pooled.Dispose());
        }

        /// <summary>
        /// An entry within the wait table representing a serializer entry 
        /// for a given moniker
        /// </summary>
        protected class WaitEntry : VnDisposeable
        {
            private uint _waitCount;
            private readonly SemaphoreSlim _waitHandle;

            /// <summary>
            /// A stored referrnece to the moniker while the wait exists
            /// </summary>
            public TMoniker? Moniker { get; private set; }

            /// <summary>
            /// Initializes a new <see cref="WaitEntry"/>
            /// </summary>
            public WaitEntry()
            {
                _waitHandle = new(1, 1);
                Moniker = default!;
            }

            /// <summary>
            /// Gets a token used to enter the lock which may block, or yield async
            /// outside of a nested lock
            /// </summary>
            /// <returns>The waiter used to enter a wait on the moniker</returns>
            public WaitEnterToken GetWaiter()
            {
                /*
                 * Increment wait count before entering the lock
                 * A cancellation is the only way out, so cover that 
                 * during the async, only if the token is cancelable
                 */
                _ = Interlocked.Increment(ref _waitCount);
                return new(this);
            }

            /// <summary>
            /// Prepares a release 
            /// </summary>
            /// <param name="releaser">
            /// The token that should be used to release the exclusive lock held on 
            /// a moniker
            /// </param>
            /// <returns>The number of remaining waiters</returns>
            public uint Release(out WaitReleaseToken releaser)
            {
                releaser = new(_waitHandle);

                //Decrement release count before leaving
                return Interlocked.Decrement(ref _waitCount);
            }

            /// <summary>
            /// Prepres a new <see cref="WaitEntry"/> for 
            /// its new session.
            /// </summary>
            /// <param name="moniker">The referrence to the moniker to hold</param>
            public void Prepare(TMoniker? moniker)
            {
                Moniker = moniker;
                _waitCount = 0;
            }

            /*
             * Called by WaitEnterToken to enter the lock 
             * outside a nested lock
             */

            internal Task WaitAsync(CancellationToken cancellation)
            {

                //See if lock can be entered synchronously
                if (_waitHandle.Wait(0, CancellationToken.None))
                {
                    //Lock was entered successfully without async yield
                    return Task.CompletedTask;
                }

                //Lock must be entered async

                //Check to confirm cancellation may happen
                if (cancellation.CanBeCanceled)
                {
                    //Task may be cancelled, so we need to monitor the results to properly set waiting count
                    Task wait = _waitHandle.WaitAsync(cancellation);
                    return WaitForLockEntryWithCancellationAsync(wait);
                }
                else
                {
                    //Task cannot be canceled, so we dont need to monitor the results
                    return _waitHandle.WaitAsync(CancellationToken.None);
                }
            }

            private async Task WaitForLockEntryWithCancellationAsync(Task wait)
            {
                try
                {
                    await wait.ConfigureAwait(false);
                }
                catch
                {
                    //Decrement wait count on error entering lock async
                    _ = Interlocked.Decrement(ref _waitCount);
                    throw;
                }
            }

            ///<inheritdoc/>
            protected override void Free()
            {
                _waitHandle.Dispose();
            }
        }

        /// <summary>
        /// A token used to safely release an exclusive lock inside the 
        /// <see cref="WaitEntry"/>
        /// </summary>
        protected readonly ref struct WaitReleaseToken
        {
            private readonly SemaphoreSlim? _sem;

            internal WaitReleaseToken(SemaphoreSlim sem) => _sem = sem;

            /// <summary>
            /// Releases the exclusive lock held by the token. NOTE:
            /// this method may only be called ONCE after a wait has been
            /// released
            /// </summary>
            public readonly void Release() => _sem?.Release();
        }

        /// <summary>
        /// A token used to safely enter a wait for exclusive access to a <see cref="WaitEntry"/>
        /// </summary>
        protected readonly ref struct WaitEnterToken
        {
            private readonly WaitEntry _entry;

            internal WaitEnterToken(WaitEntry entry) => _entry = entry;

            /// <summary>
            /// Enters the wait for the WaitEntry. This method may not block
            /// or yield (IE Return <see cref="Task.CompletedTask"/>)
            /// </summary>
            /// <param name="cancellation">A token to cancel the wait for the resource</param>
            /// <returns></returns>
            public Task EnterWaitAsync(CancellationToken cancellation) => _entry.WaitAsync(cancellation);
        }
    }
}