/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AsyncExclusiveResource.cs 
*
* AsyncExclusiveResource.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Provides a base class for resources that must be obtained exclusively in a multi-threaded environment
    /// but allow state update operations (and their exceptions) to be deferred to the next accessor.
    /// </summary>
    /// <typeparam name="TState">The state parameter type passed during updates</typeparam>
    public abstract class AsyncExclusiveResource<TState> : VnDisposeable, IWaitHandle, IAsyncWaitHandle
    {
        /// <summary>
        /// Main mutli-threading lock used for primary access synchronization
        /// </summary>
        protected SemaphoreSlim MainLock { get; } = new (1, 1);       

        private Task? LastUpdate;

        /// <summary>
        /// <inheritdoc/>
        /// <br></br>
        /// <br></br>
        /// If the previous call to <see cref="UpdateAndRelease"/> resulted in an asynchronous update, and exceptions occurred, an <see cref="AsyncUpdateException"/>
        /// will be thrown enclosing the exception
        /// </summary>
        /// <param name="millisecondsTimeout">Time in milliseconds to wait for exclusive access to the resource</param>
        /// <exception cref="AsyncUpdateException"></exception>
        /// <inheritdoc/>
        public virtual bool WaitOne(int millisecondsTimeout)
        {
            //First wait for main lock
            if (MainLock.Wait(millisecondsTimeout))
            {
                //Main lock has been taken
                try
                {
                    //Wait for async update if there is one pending(will throw exceptions if any occurred)
                    LastUpdate?.Wait();
                    return true;
                }
                catch (AggregateException ae) when (ae.InnerException != null)
                {
                    //Release the main lock and re-throw the inner exception
                    _ = MainLock.Release();
                    //Throw a new async update exception
                    throw new AsyncUpdateException(ae.InnerException);
                }
                catch
                {
                    //Release the main lock and re-throw the exception
                    _ = MainLock.Release();
                    throw;
                }
            }
            return false;
        }

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public virtual async Task WaitOneAsync(CancellationToken token = default)
        {
            //Wait for main lock
            await MainLock.WaitAsync(token).ConfigureAwait(true);
            //if the last update completed synchronously, return true
            if (LastUpdate == null)
            {
                return;
            }
            try
            {
                //Await the last update task and catch its exceptions
                await LastUpdate.ConfigureAwait(false);
            }
            catch
            {
                //Release the main lock and re-throw the exception
                _ = MainLock.Release();
                throw;
            }
        }

        /// <summary>
        /// Requests a resource update and releases the exclusive lock on this resource. If a deferred update operation has any 
        /// exceptions during its last operation, they will be thrown here.  
        /// </summary>
        /// <param name="defer">Specifies whether the update should be deferred or awaited on the current call</param>
        /// <param name="state">A state parameter to be passed to the update function</param>
        /// <exception cref="ObjectDisposedException"></exception>
        public async ValueTask UpdateAndRelease(bool defer, TState state)
        {
            //Otherwise wait and update on the current thread
            try
            {
                //Dispose the update task
                LastUpdate?.Dispose();
                //Remove the reference
                LastUpdate = null;
                //Run update on the current thread
                LastUpdate = await UpdateResource(defer, state).ConfigureAwait(true);
                //If the update is not deferred, await the results 
                if (!defer && LastUpdate != null)
                {
                    await LastUpdate.ConfigureAwait(true);
                }
            }
            finally
            {
                //Release the main lock
                _ = MainLock.Release();
            }
        }

        /// <summary>
        /// <para>
        /// When overridden in a derived class, is responsible for updating the state of the instance if necessary.
        /// </para>
        /// <para>
        /// If the result of the update returns a <see cref="Task"/> that represents the deferred update, the next call to <see cref="WaitOne"/> will 
        /// block until the operation completes and will throw any exceptions that occurred
        /// </para>
        /// </summary>
        /// <param name="defer">true if the caller expects a resource update to be deferred, false if the caller expects the result of the update to be awaited</param>
        /// <param name="state">State parameter passed when releasing</param>
        /// <returns>A <see cref="Task"/> representing the async state update operation, or null if no async state update operation needs to be monitored</returns>
        protected abstract ValueTask<Task?> UpdateResource(bool defer, TState state);

        ///<inheritdoc/>
        protected override void Free()
        {
            //Dispose lock
            MainLock.Dispose();

            //Try to cleanup the last update
            if (LastUpdate != null && LastUpdate.IsCompletedSuccessfully)
            {
                LastUpdate.Dispose();
            }

            LastUpdate = null;
        }

    }
}