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
using System.Diagnostics;
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
            EntryPool = new(maxPoolSize);
            WaitTable = new(initialCapacity, keyComparer);
        }

        ///<inheritdoc/>
        public virtual Task WaitAsync(TMoniker moniker, CancellationToken cancellation = default)
        {
            //Token must not be cancelled before entering wait 
            cancellation.ThrowIfCancellationRequested();

            WaitEnterToken token;
            WaitEntry? wait;

            if (cancellation.CanBeCanceled)
            {
                lock (StoreLock)
                {
                    //See if the entry already exists, otherwise get a new wait entry
                    if (!WaitTable.TryGetValue(moniker, out wait))
                    {
                        GetWaitEntry(ref wait, moniker);

                        //Add entry to store
                        WaitTable[moniker] = wait;
                    }

                    //Get waiter before leaving lock
                    wait.ScheduleWait(cancellation, out token);
                }
               
                //Enter wait and setup cancellation continuation
                return EnterCancellableWait(in token, wait);
            }
            else
            {
                lock (StoreLock)
                {
                    //See if the entry already exists, otherwise get a new wait entry
                    if (!WaitTable.TryGetValue(moniker, out wait))
                    {
                        GetWaitEntry(ref wait, moniker);

                        //Add entry to store
                        WaitTable[moniker] = wait;
                    }

                    //Get waiter before leaving lock
                    wait.ScheduleWait(out token);
                }

                //Enter the waiter without any cancellation support
                return token.EnterWaitAsync();
            }
        }
       
        /// <summary>
        /// Enters a cancellable wait and sets up a continuation to release the wait entry
        /// if a cancellation occurs
        /// </summary>
        /// <param name="token"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        protected Task EnterCancellableWait(in WaitEnterToken token, WaitEntry entry)
        {
            //Inspect for a task that is already completed
            if (token.MayYield)
            {
                Task awaitable = token.EnterWaitAsync();
                _ = awaitable.ContinueWith(OnCancellableTaskContinuation, entry, TaskScheduler.Default);
                return awaitable;
            }
            else
            {
                return token.EnterWaitAsync();
            }
        }

        private void OnCancellableTaskContinuation(Task task, object? state)
        {
            if (!task.IsCompletedSuccessfully)
            {
                Debug.Assert(task.IsCanceled, "A wait task did not complete successfully but was not cancelled, this is an unexpected condition");

                //store lock must be held during wait entry transition
                lock (StoreLock)
                {
                    //Release the wait entry
                    (state as WaitEntry)!.OnCancelled(task);
                }
            }
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
             * 
             * Since tasks are cancellable by another thread at any time, it is possible that the canceled task will be
             * dequeued as the next task to transition. This condition is guarded by the release token by returning a boolean
             * signalling the transition failure, if so, must repeat the release process, until a valid release occurs
             * or the final release is issued.
             */        

            WaitReleaseToken releaser;

            do
            {
                lock (StoreLock)
                {
                    WaitEntry entry = WaitTable[moniker];

                    //Call release while holding store lock
                    if (entry.ExitWait(out releaser) == 0)
                    {
                        //No more waiters
                        WaitTable.Remove(moniker);

                        /*
                         * We must release the semaphore before returning to pool, 
                         * its safe because there are no more waiters
                         */

                        Debug.Assert(!releaser.WillTransition, "The wait entry referrence count was 0 but a release token was issued that would cause a lock transision");

                        releaser.Release();

                        ReturnEntry(entry);

                        return;
                    }
                }

            /*
             * If the releaser fails to transition the next task, we need to repeat the 
             * release process to ensure that at least one waiter is properly released
             */
            } while (!releaser.Release());
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

            //Init wait with moniker
            wait.Prepare(moniker);
        }

        /// <summary>
        /// Returns an empty <see cref="WaitEntry"/> back to the pool for reuse 
        /// (does not hold the store lock)
        /// </summary>
        /// <param name="entry">The entry to return to the pool</param>
        protected virtual void ReturnEntry(WaitEntry entry)
        {
            //Remove ref
            entry.Prepare(default);

            if (EntryPool.Count < MaxPoolSize)
            {
                EntryPool.Push(entry);
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
        }

        /// <summary>
        /// An entry within the wait table representing a serializer entry 
        /// for a given moniker
        /// </summary>
        protected class WaitEntry 
        {
            private uint _waitCount;

            /*
             * Head of the waiting task queue
             */
            private TaskNode? _head;

            /// <summary>
            /// A stored referrnece to the moniker while the wait exists
            /// </summary>
            public TMoniker? Moniker { get; private set; }         

            /// <summary>
            /// Gets a token used to enter the lock which may block, or yield async
            /// outside of a nested lock
            /// <para>This method and release method are not thread safe</para>
            /// </summary>
            /// <param name="enterToken">A referrence to the wait entry token</param>
            /// <returns>
            /// The incremented reference count.
            /// </returns>
            public uint ScheduleWait(out WaitEnterToken enterToken)
            {
                /*
                 * Increment wait count before entering the lock
                 * A cancellation is the only way out, so cover that 
                 * during the async, only if the token is cancelable
                 */

                _waitCount++;

                if (_waitCount != 1)
                {
                    TaskNode waiter = InitAndEnqueueWaiter(default);
                    enterToken = new(waiter);
                    return _waitCount;
                }
                else
                {
                    enterToken = default;
                    return _waitCount;
                }
            }

            /// <summary>
            /// Gets a token used to enter the lock which may block, or yield async
            /// outside of a nested lock, with cancellation support
            /// <para>This method and release method are not thread safe</para>
            /// </summary>
            /// <param name="cancellation"></param>
            /// <param name="enterToken">A referrence to the wait entry token</param>
            /// <returns>
            /// The incremented reference count.
            /// </returns>
            public uint ScheduleWait(CancellationToken cancellation, out WaitEnterToken enterToken)
            {
                /*
                 * Increment wait count before entering the lock
                 * A cancellation is the only way out, so cover that 
                 * during the async, only if the token is cancelable
                 */

                _waitCount++;

                if (_waitCount != 1)
                {
                    TaskNode waiter = InitAndEnqueueWaiter(cancellation);
                    enterToken = new(waiter);
                    return _waitCount;
                }
                else
                {
                    enterToken = default;
                    return _waitCount;
                }
            }

            /// <summary>
            /// Prepares a release token and atomically decrements the waiter count
            /// and returns the remaining number of waiters.
            /// <para>This method and enter method are not thread safe</para>
            /// </summary>
            /// <param name="releaser">
            /// The token that should be used to release the exclusive lock held on 
            /// a moniker
            /// </param>
            /// <returns>The number of remaining waiters</returns>
            public uint ExitWait(out WaitReleaseToken releaser)
            {
                //Decrement release count before leaving
                --_waitCount;

                TaskNode? next = _head;

                if(next != null)
                {
                    //Remove task from queue
                    _head = next.Next;
                }

                //Init releaser
                releaser = new(next);
           
                return _waitCount;
            }

            /// <summary>
            /// Prepres a new <see cref="WaitEntry"/> for 
            /// its new moniker object.
            /// </summary>
            /// <param name="moniker">The referrence to the moniker to hold</param>
            public void Prepare(TMoniker? moniker)
            {
                Moniker = moniker;
                
                //Wait count should be 0 on calls to prepare, its a bug if not
                Debug.Assert(_waitCount == 0, "Async serializer wait count should have been reset before pooling");
            }

            /*
             * Called by WaitEnterToken to enter the lock 
             * outside a nested lock
             */


            private TaskNode InitAndEnqueueWaiter(CancellationToken cancellation)
            {
                TaskNode newNode = new(OnTaskCompleted, this, cancellation);

                //find the tail
                TaskNode? tail = _head;
                if (tail == null)
                {
                    _head = newNode;
                }
                else
                {
                    //Find end of queue
                    while (tail.Next != null)
                    {
                        tail = tail.Next;
                    }
                    //Store new tail
                    tail.Next = newNode;
                }
                return newNode;
            }

            private void RemoveTask(TaskNode task)
            {
                //search entire queue for task
                TaskNode? node = _head;
                while (node != null)
                {
                    if (node.Next == task)
                    {
                        //Remove task from queue
                        node.Next = task.Next;
                        break;
                    }
                    node = node.Next;
                }
            }

            internal uint OnCancelled(Task instance)
            {
                RemoveTask((instance as TaskNode)!);

                //Decrement release count before leaving
                return --_waitCount;
            }

            private static void OnTaskCompleted(object? state)
            { }
         

            /*
            * A linked list style task node that is used to store the 
            * next task in the queue and be awaitable as a task
            */

            private sealed class TaskNode : Task
            {
                public TaskNode(Action<object?> callback, object item, CancellationToken cancellation) : base(callback, item, cancellation)
                { }

                public TaskNode? Next { get; set; }
            
            }
        }

        /// <summary>
        /// A token used to safely release an exclusive lock inside the 
        /// <see cref="WaitEntry"/>
        /// </summary>
        protected readonly ref struct WaitReleaseToken
        {
            private readonly Task? _nextWaiter;

            /// <summary>
            /// Indicates if releasing the lock will cause scheduling of another thread
            /// </summary>
            public readonly bool WillTransition => _nextWaiter != null;

            internal WaitReleaseToken(Task? nextWaiter) => _nextWaiter = nextWaiter;

            /// <summary>
            /// Releases the exclusive lock held by the token. NOTE:
            /// this method may only be called ONCE after a wait has been
            /// released. 
            /// <para>
            /// If <see cref="WillTransition"/> is true, this method may cause a waiting 
            /// task to transition. The result must be examined to determine if the
            /// transition was successful.If a transition is not successful, then a deadlock may occur if 
            /// another waiter is not selected.
            /// </para>
            /// </summary>
            /// <returns>A value that indicates if the task was transition successfully</returns>
            public readonly bool Release()
            {
                //return success if no next waiter
                if(_nextWaiter == null)
                {
                    return true;
                }

                /*
                 * Guard against the next waiter being cancelled,
                 * this thread could be suspended after this check
                 * but for now should be good enough. An exception
                 * will be thrown if this doesnt work
                 */

                switch (_nextWaiter.Status)
                {
                    case TaskStatus.Canceled:
                        return false;
                    case TaskStatus.Created:
                        _nextWaiter.Start();
                        return true;
                }

                Debug.Fail($"Next waiting task is in an invalid state: {_nextWaiter.Status}");
                return false;
            }
        }

        /// <summary>
        /// A token used to safely enter a wait for exclusive access to a <see cref="WaitEntry"/>
        /// </summary>
        protected readonly ref struct WaitEnterToken
        {
            private readonly Task? _waiter;
           
            /// <summary>
            /// Indicates if a call to EnterWaitAsync will cause an awaiter to yield
            /// </summary>
            public bool MayYield => _waiter != null;

            internal WaitEnterToken(Task wait) => _waiter = wait;

            /// <summary>
            /// Enters the wait for the WaitEntry. This method may not block
            /// or yield (IE Return <see cref="Task.CompletedTask"/>)
            /// </summary>
            /// <returns></returns>
            public readonly Task EnterWaitAsync() => _waiter ?? Task.CompletedTask;
        }
    }
}