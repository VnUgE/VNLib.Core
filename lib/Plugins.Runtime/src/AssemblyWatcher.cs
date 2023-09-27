/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: AssemblyWatcher.cs 
*
* AssemblyWatcher.cs is part of VNLib.Plugins.Runtime which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime. If not, see http://www.gnu.org/licenses/.
*/

using System.IO;
using System.Threading;
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Runtime
{
    internal sealed class AssemblyWatcher : IPluginAssemblyWatcher
    {
        private readonly object _lock = new ();
        private readonly Dictionary<IPluginReloadEventHandler, AsmFileWatcher> _watchers;

        public AssemblyWatcher()
        {
            _watchers = new();
        }

        ///<inheritdoc/>
        public void StopWatching(IPluginReloadEventHandler handler)
        {
            lock (_lock)
            {
                //Find old watcher by its handler, then dispose it
                if (_watchers.Remove(handler, out AsmFileWatcher? watcher))
                {
                    //dispose the watcher
                    watcher.Dispose();
                }
            }
        }

        ///<inheritdoc/>
        public void WatchAssembly(IPluginReloadEventHandler handler, IPluginAssemblyLoader loader)
        {
            lock(_lock)
            {
                if(_watchers.Remove(handler, out AsmFileWatcher? watcher))
                {
                    //dispose the watcher
                    watcher.Dispose();
                }

                //Queue up a new watcher
                watcher = new(loader, handler);

                //Store watcher
                _watchers.Add(handler, watcher);
            }
        }

        private sealed class AsmFileWatcher : VnDisposeable
        {
            public IPluginReloadEventHandler Handler { get; }

            private readonly IPluginAssemblyLoader _loaderSource;
            private readonly Timer _delayTimer;
            private readonly FileSystemWatcher _watcher;

            private bool _pause;

            public AsmFileWatcher(IPluginAssemblyLoader LoaderSource, IPluginReloadEventHandler handler)
            {
                Handler = handler;
                _loaderSource = LoaderSource;

                string dir = Path.GetDirectoryName(LoaderSource.Config.AssemblyFile)!;

                //Configure watcher to notify only when the assembly file changes
                _watcher = new FileSystemWatcher(dir)
                {
                    Filter = "*.dll",
                    EnableRaisingEvents = false,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite,
                };

                //Configure listener
                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;

                _watcher.EnableRaisingEvents = true;

                //setup delay timer to wait on the config
                _delayTimer = new(OnTimeout, this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            void OnFileChanged(object sender, FileSystemEventArgs e)
            {
                //if were already waiting to process an event, we dont need to stage another
                if (_pause)
                {
                    return;
                }

                //Set pause flag
                _pause = true;

                //Restart the timer to trigger reload event on elapsed
                _delayTimer.Restart(_loaderSource.Config.ReloadDelay);
            }

            private void OnTimeout(object? state)
            {
                _delayTimer.Stop();

                //Fire event, let exception crash app
                Handler.OnPluginUnloaded(_loaderSource);

                //Clear pause flag
                _pause = false;
            }

            protected override void Free()
            {
                _delayTimer.Dispose();

                //Detach event handler and dispose watcher
                _watcher.Changed -= OnFileChanged;
                _watcher.Dispose();
            }
        }
    }
}
