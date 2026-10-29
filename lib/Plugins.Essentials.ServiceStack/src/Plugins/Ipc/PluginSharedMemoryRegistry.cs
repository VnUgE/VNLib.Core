/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryRegistry.cs
*
* PluginSharedMemoryRegistry.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Plugins.Ipc.SharedMemory;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc
{

    /// <summary>
    /// Manages the lifetime of shared memory regions allocated by plugins.
    /// Regions are registered by owner plugins during pre-load and revoked when
    /// the owning plugin unloads. Accessor plugins receive deferred handles that
    /// connect on first access.
    /// </summary>
    internal sealed class PluginSharedMemoryRegistry(PluginSharedMemoryConfig config) : VnDisposeable
    {
        private readonly PluginSharedMemoryConfig config = config ?? throw new ArgumentNullException(nameof(config));

        // Use to synchronize access to internal state of the registry
        private readonly object _syncLock = new();

        // The collection of all currently mapped regions, keyed by region name.
        // Access to this table is protected by _syncLock
        private readonly Dictionary<string, IpcRegionEntry> _mappedRegions = new(StringComparer.OrdinalIgnoreCase);

        // Maintains a list of all "early" accessors. Accessors waiting for their region to be mapped
        // Access to this list is protected by _syncLock.
        private readonly LinkedList<IpcRegionAccessor> _pendingAccessors = [];

        // Used to signal pending accessors waiting on a task that the registry is being disposed
        private readonly CancellationTokenSource _accessorCancelToken = new();

        /*
         * Used by MapRegion to atomically create a new region mapping
         * from the allocator.
         */
        private IpcRegionEntry CreateRegionMapping(string regionName, int size)
        {
            return new()
            {
                RegionName      = regionName,
                Region          = config.Allocator.Alloc(regionName, size)
            };
        }

        protected override void Free()
        {
            IpcRegionEntry[] entries;
            IpcRegionAccessor[] accessors;

            lock (_syncLock)
            {
                // Extract all entries from the table and clear it inside the lock
                entries = _mappedRegions.Values.ToArray();
                _mappedRegions.Clear();

                accessors = _pendingAccessors.ToArray();
                _pendingAccessors.Clear();
            }

            foreach (IpcRegionEntry entry in entries)
            {
                config.Allocator.Free(entry.Region);
            }

            // Cancel all pending accessors that were never mapped
            _accessorCancelToken.Cancel(throwOnFirstException: false);
            _accessorCancelToken.Dispose();
        }

        /// <summary>
        /// Maps a new region with the supplied name with the desired size and notifies
        /// any pending accessors.
        /// <para>
        /// Holders must call <see cref="ReleaseHandle(object)"/> when the
        /// region is no longer in use.
        /// </para>
        /// </summary>
        /// <param name="regionName">The name of the shared memory region</param>
        /// <param name="size">The size of the region in bytes</param>
        /// <returns>
        /// A handle that tracks the owner of the region.
        /// </returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        internal IIpcRegionOwner MapRegion(string regionName, int size)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(regionName);
            ArgumentOutOfRangeException.ThrowIfLessThan(size, config.MinRegionSize);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(size, config.MaxRegionSize);

            Check();

            /*
             * TODO: Currently this implementation atomically creates a new region OR gets an
             * existing mapping. It does not protect against multiple producers or producer
             * unloads — whoever maps the name first wins and sets the region size. A second
             * caller mapping the same name with a different size receives the pre-existing
             * region and a size-mismatch exception is thrown. Callers should verify
             * region.Length after mapping if exact sizing is critical.
             * 
             * The guard was decided to be acceptable because region size is a compile time constant
             * so hot-reload of changing region size is developer-only issue. Otherwise it's the same 
             * bug of duplicate allocation detection. 
             */

            IpcRegionEntry? entry;

            lock (_syncLock)
            {
                if (!_mappedRegions.TryGetValue(regionName, out entry))
                {
                    // Create new mapping for region
                    entry = CreateRegionMapping(regionName, size);

                    // Add mapping to table
                    _mappedRegions.Add(regionName, entry);
                }
                else if (entry.Region.Length != size)
                {
                    // Reject a second producer mapping the same name with a different size
                    throw new InvalidOperationException(
                        $"Region '{regionName}' is already mapped with size {entry.Region.Length} " +
                        $"but a second producer requested size {size}."
                    );
                }

                // Increment reference count to account for new owner
                entry.AddReader();

                // search for pending accessors and notify matches
                LinkedListNode<IpcRegionAccessor>? node = _pendingAccessors.First;

                while (node is not null)
                {
                    // Capture next before potentially removing current
                    LinkedListNode<IpcRegionAccessor>? next = node.Next;
                    
                    IpcRegionAccessor accessor = node.Value;

                    if (string.Equals(regionName, accessor.RegionName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Region matches invoke handler
                        accessor.OnRegionMapped(entry.Region);

                        // increment reference count
                        entry.AddReader();

                        //Remove from pending list
                        _pendingAccessors.Remove(node);
                    }

                    node = next;
                }
            }

            return new IpcRegionOwner(regionName, entry.Region);
        }

        /// <summary>
        /// Registers a new accessor for a region and gets a handle that
        /// will capture the region if/when it becomes available to the
        /// accessor.
        /// </summary>
        /// <param name="regionName">The name of the shared plugin memory region</param>
        /// <returns>An accessor handle that may acquire the region when available</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        internal IPluginMemoryRegionAccessor AddReader(string regionName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(regionName);

            Check();

            lock (_syncLock)
            {
                // See if the region is already mapped
                if (_mappedRegions.TryGetValue(regionName, out IpcRegionEntry? existingEntry))
                {
                    // Increment reference count
                    existingEntry.AddReader();

                    // It's already mapped so create the new active accessor wrapper
                    return new IpcRegionAccessor(existingEntry);
                }
                // Defer pending accessor to wait for new region
                else
                {
                    IpcRegionAccessor accessor = new(regionName, _accessorCancelToken.Token);

                    _pendingAccessors.AddLast(accessor);

                    return accessor;
                }
            }
        }

        /// <summary>
        /// Frees a previously registered accessor or owner from the region mapping. If all accessors and
        /// owners have been freed from the region, the region is unmapped and returned to the allocator
        /// </summary>
        /// <param name="handle">The previously registered accessor for a region</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        internal void ReleaseHandle(object handle)
        {
            ArgumentNullException.ThrowIfNull(handle);
            Check();

            IpcRegionEntry? entry;

            lock (_syncLock)
            {
                string regionName;

                if (handle is IpcRegionAccessor accessor)
                {
                    if (accessor.Invalid)
                    {
                        throw new ArgumentException("This accessor handle has already been used to release a region. Invalid handle");
                    }

                    // Set the handle as invalid to attempt to track double frees
                    accessor.Invalid = true;

                    // Remove pending accessor that was never mapped before freed.
                    if (_pendingAccessors.Remove(accessor))
                    {
                        // A region was never mapped, so it never incremented the count
                        // so there should be nothing to do
                        return;
                    }

                    regionName = accessor.RegionName;
                }
                else if (handle is IpcRegionOwner owner)
                {
                    if (owner.Invalid)
                    {
                        throw new ArgumentException("This owner handle has already been used to unmap a region. Invalid handle");
                    }

                    // Set the handle as invalid to attempt to track double frees
                    owner.Invalid = true;

                    regionName = owner.RegionName;
                }
                else
                {
                    throw new ArgumentException("Cannot perform unmap operation on invalid handle type.");
                }

                if (_mappedRegions.TryGetValue(regionName, out entry))
                {
                    /*
                     * The reference count always starts a 1 when created to account for
                     * the owner. All accessors will increment the reference count when they
                     * acquire the handle for the first time.
                     *
                     * If all accessors have exited, the count should be 1, when the owner
                     * decrements, it should drop to 0 and then be freed.
                     *
                     * Value will be > 0 if any accessors are left. If so, the accessor will
                     * become responsible for freeing the block.
                     */

                    if (entry.RemoveReader() == 0)
                    {
                        bool removed = _mappedRegions.Remove(entry.RegionName);

                        Debug.Assert(removed, "Failed to remove existing shared memory mapping from mapping table");

                        goto FreeRegion;
                    }

                    // Accessors remain, keep region alive
                    return;
                }
            }

            Debug.Fail("Shared region owner double free detected");

            // TODO: create better exception type
            throw new InvalidOperationException("Failed to unmap region: region does not exist or is already freed.");

        FreeRegion:
            // Free memory region back to pool outside the lock
            config.Allocator.Free(entry.Region);
            return;
        }

        private sealed class IpcRegionEntry
        {
            /// <summary>
            /// The name of the region this entry is holding memory for
            /// </summary>
            public required string RegionName { get; init; }

            /// <summary>
            /// The underlying memory handle from the allocator
            /// </summary>
            public required IPluginMemoryRegion Region { get; init; }

            /// <summary>
            /// Tracks the number of active accessors of the handle
            /// </summary>
            internal int ReaderCount { get; private set; }

            /// <summary>
            /// Increments the reference count and returns the new value
            /// </summary>
            /// <returns>The incremented reference count</returns>
            internal int AddReader() => ++ReaderCount;

            /// <summary>
            /// Decrements the reference count and returns the new value
            /// </summary>
            /// <returns>The decremented reference count</returns>
            internal int RemoveReader() => --ReaderCount;
        }

        private sealed class IpcRegionOwner(string regionName, IPluginMemoryRegion region) : IIpcRegionOwner
        {
            /// <summary>
            /// The name of the region this owner holds memory for
            /// </summary>
            public string RegionName { get; } = regionName;

            /// <summary>
            /// The shared memory region handle
            /// </summary>
            public IPluginMemoryRegion Region { get; } = region;

            /// <summary>
            /// Value tracked by the registry that helps prevent double frees
            /// </summary>
            internal bool Invalid;
        }

        private sealed class IpcRegionAccessor : IPluginMemoryRegionAccessor
        {
            private readonly string _regionName;
            private readonly Task<IPluginMemoryRegion> _loadTask;
            private IPluginMemoryRegion? _region;

            /// <summary>
            /// Value tracked by the registry that helps prevent double frees
            /// </summary>
            internal bool Invalid;

            /// <summary>
            /// Creates a new <see cref="IpcRegionAccessor"/> wrapper around an
            /// existing region. (active accessor)
            /// </summary>
            /// <param name="entry">The existing region</param>
            internal IpcRegionAccessor(IpcRegionEntry entry)
            {
                _regionName = entry.RegionName;
                _region = entry.Region;

                // Completed task since we already have the region
                _loadTask = Task.FromResult(entry.Region);
            }

            /// <summary>
            /// Creates a new accessor for pending accessors before a region is mapped. Registry
            /// should ensure accessor is mapped as a pending accessor.
            /// <para>
            /// Creates a pending accessor handle that must wait to be notified when the region
            /// is created.
            /// </para>
            /// </summary>
            /// <param name="regionName">The name of the memory region we wish to access</param>
            /// <param name="closeToken">
            /// A cancellation token that signals to waiting accessors that the region will never map 
            /// and the system is closing.
            /// </param>
            internal IpcRegionAccessor(string regionName, CancellationToken closeToken)
            {
                _regionName = regionName;

                /*
                 * Task that can be scheduled to send notification to waiting user on async
                 * loading.
                 */
                _loadTask = new(
                    function: GetRegion,
                    cancellationToken: closeToken,
                    creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
                );
            }

            internal void OnRegionMapped(IPluginMemoryRegion region)
            {
                _region = region;

                /*
                * Guard against double-start: two producers publishing the same region name
                * would both fire this handler and call Start() on an already-running task.
                * Only start if the task hasn't been scheduled yet.
                */
                if (_loadTask.Status == TaskStatus.Created)
                {
                    _loadTask.Start(TaskScheduler.Default);
                }
            }

            /// <inheritdoc/>
            public string RegionName => _regionName;

            /// <inheritdoc/>
            /// <remarks>
            /// This method also serves as the cold task delegate for pending accessors — when
            /// <see cref="OnRegionMapped"/> starts the task, this runs on the thread pool and
            /// its return value completes <see cref="WaitAsync"/>.
            /// </remarks>
            public IPluginMemoryRegion GetRegion()
                => _region ?? throw new InvalidOperationException("Region has not been mapped yet");

            /// <inheritdoc/>
            public bool IsValid() => _region != null;

            /// <inheritdoc/>
            public Task<IPluginMemoryRegion> WaitAsync() => _loadTask;

        }
    }
}
