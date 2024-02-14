/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: FileWatcher.cs 
*
* FileWatcher.cs is part of VNLib.Utils which is part of the larger 
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

using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// A static class that provides simple ways to listen for changes to files in 
    /// the filesystem. This class is thread-safe and can be used to listen for
    /// changes to multiple files at once.
    /// </summary>
    public static class FileWatcher
    {
        private static readonly ConcurrentDictionary<string, DirWatcher> Watchers = new();

        /// <summary>
        /// Starts listening for changes to a file at the specified path. If the file is already being
        /// watched, the handler is added to the list of subscribers for that file.
        /// </summary>
        /// <param name="path">The path of the file to start listening for changes on</param>
        /// <param name="handler">The file event handler</param>
        public static void Subscribe(string path, IFSChangeHandler handler)
        {
            //Make sure file is fully qualified
            path = Path.GetFullPath(path);

            //Get an existing watcher or create a new one while the lock is held
            DirWatcher watcher = Watchers.GetOrAdd(path, static p => new(p));

            lock (watcher)
            {
                watcher.AddHandler(path, handler);
            }
        }

        /// <summary>
        /// Stops listening for changes to a file at the specified path. If the file is not being 
        /// watched, this method does nothing.
        /// </summary>
        /// <param name="path">The path to the file to stop listening for</param>
        /// <param name="handler">The event handler to unsubscribe</param>
        public static void Unsubscribe(string path, IFSChangeHandler handler)
        {
            if (Watchers.TryGetValue(path, out DirWatcher? watcher))
            {
                //Syncronize access to the watcher to be completely thread-safe
                lock (watcher)
                {
                    if (watcher.RemoveHandler(path, handler))
                    {
                        Watchers.TryRemove(path, out _);
                        watcher.Dispose();
                    }
                }
            }
        }

        private sealed class DirWatcher : VnDisposeable
        {
            private readonly ConcurrentDictionary<string, SingleFileSubPool> Handlers = new();
            private readonly FileSystemWatcher Watcher;

            public DirWatcher(string path)
            {
                //Setup new watcher
                Watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!)
                {
                    Filter = "*.*",
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false,  //We only care about top-level files
                    InternalBufferSize = 8192,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.Security
                };

                Watcher.Changed += Watcher_Changed;
                Watcher.Created += Watcher_Changed;
                Watcher.Deleted += Watcher_Changed;
                Watcher.Renamed += Watcher_Changed;
            }

            public bool RemoveHandler(string path, IFSChangeHandler handler)
            {
                if(Handlers.TryGetValue(Path.GetFileName(path), out SingleFileSubPool? watcher))
                {
                    //Remove the single handler
                    if(watcher.RemoveHandler(handler))
                    {
                        //Handler watcher is empty, remove it from handlers store
                        _ = Handlers.TryRemove(Path.GetFileName(path), out _);
                    }
                }

                return Handlers.IsEmpty;
            }

            public void AddHandler(string path, IFSChangeHandler handler)
            {
                //Get the file name only
                string fileName = Path.GetFileName(path);

                //Get existing watcher or create a new one
                if (Handlers.TryGetValue(fileName, out SingleFileSubPool? watcher))
                {
                    watcher.AddHandler(handler);
                }
                else
                {
                    watcher = new SingleFileSubPool(path);
                    watcher.AddHandler(handler);
                    Handlers.TryAdd(fileName, watcher);
                }
            }

            private void Watcher_Changed(object sender, FileSystemEventArgs e)
            {
                //Only invoke subscribers if the specific file is being watched
                if (e.Name is not null && Handlers.TryGetValue(e.Name, out SingleFileSubPool? watcher))
                {
                    watcher.OnFileChanged(e);
                }
            }

            protected override void Free()
            {
                Handlers.Clear();
                Watcher.Dispose();
            }

            private sealed class SingleFileSubPool(string path)
            {
                private readonly List<IFSChangeHandler> Handlers = new();

                public string FileName { get; } = Path.GetFileName(path);

                public bool RemoveHandler(IFSChangeHandler handler)
                {
                    Handlers.Remove(handler);
                    return Handlers.Count == 0;
                }

                public void AddHandler(IFSChangeHandler handler) => Handlers.Add(handler);

                public void OnFileChanged(FileSystemEventArgs e) => Handlers.ForEach(h => h.OnFileChanged(e));
            }
        }
    }
}