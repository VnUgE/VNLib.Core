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

using System.Collections.Generic;

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
    }
}
