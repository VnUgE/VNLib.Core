/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: FilePathCache.cs 
*
* FilePathCache.cs is part of VNLib.Plugins.Essentials which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace VNLib.Plugins.Essentials
{

    /// <summary>
    /// Represents a cache store for translated file paths to avoid 
    /// path probing and file system syscalls
    /// </summary>
    internal abstract class FilePathCache
    {

        public abstract bool TryGetMappedPath(string filePath, [NotNullWhen(true)] out string? cachedPath);

        /// <summary>
        /// Attempts to store a path mapping in the cache store
        /// </summary>
        /// <param name="requestPath">The requested input path</param>
        /// <param name="filePath">The filesystem path this requested path maps to</param>
        public abstract void StorePathMapping(string requestPath, string filePath);

        /// <summary>
        /// Creates a new cache store with the specified max age. If max age is zero, the 
        /// cache store will be disabled.
        /// </summary>
        /// <param name="maxAge">The max time to store the cahced path reecord</param>
        /// <returns>The cache store</returns>
        public static FilePathCache GetCacheStore(TimeSpan maxAge)
        {
            return maxAge == TimeSpan.Zero 
                ? new DisabledCacheStore() 
                : new DictBackedFilePathCache(maxAge);
        }

        /*
         * A very basic dictionary cache that stores translated paths
         * from a request input path to a filesystem path.
         * 
         * This must be thread safe as it's called in a multithreaded context.
         */
        private sealed class DictBackedFilePathCache(TimeSpan maxAge) : FilePathCache
        {
            private readonly ConcurrentDictionary<string, CachedPath> _pathCache = new(StringComparer.OrdinalIgnoreCase);

            ///<inheritdoc/>
            public override bool TryGetMappedPath(string filePath, [NotNullWhen(true)] out string? cachedPath)
            {
                if (_pathCache.TryGetValue(filePath, out CachedPath cp))
                {
                    //TODO: Implement a cache eviction policy
                    cachedPath = cp.Path;
                    return true;
                }

                cachedPath = null;
                return false;
            }

            ///<inheritdoc/>
            public override void StorePathMapping(string requestPath, string filePath)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);

                //Cache path is an internal assignment. Should never be null
                Debug.Assert(filePath is not null);

                //TODO: Implement a cache eviction policy
                _pathCache[requestPath] = new CachedPath { Path = filePath, LastStored = DateTime.MinValue.Ticks };
            }

            private struct CachedPath
            {
                public required string Path;
                public required long LastStored;
            }
        }

        /*
         * A cache store that does nothing, it always misses and will 
         * cause a normal file fetch 
         */
        private sealed class DisabledCacheStore : FilePathCache
        {
            ///<inheritdoc/>
            public override void StorePathMapping(string requestPath, string filePath)
            { }

            ///<inheritdoc/>
            public override bool TryGetMappedPath(string filePath, [NotNullWhen(true)] out string? cachedPath)
            {
                cachedPath = null;
                return false;
            }
        }
    }
}
