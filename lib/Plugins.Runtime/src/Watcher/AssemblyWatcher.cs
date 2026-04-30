/*
* Copyright (c) 2026 Vaughn Nugent
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

using System;
using System.IO;
using System.Threading;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Extensions;


namespace VNLib.Plugins.Runtime.Watcher
{    

    internal static class AssemblyWatcher
    {
        internal static IDisposable WatchAssembly(IPluginReloadEventHandler handler, IPluginAssemblyLoadConfig config)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(config);

            DebouncedFSEventHandler dbh = new(config, handler);
            FileWatcher.Subscribe(config.AssemblyFile, dbh);

            return dbh;
        }

        internal sealed class DebouncedFSEventHandler : VnDisposeable, IFSChangeHandler
        {

            private readonly IPluginReloadEventHandler _handler;
            private readonly IPluginAssemblyLoadConfig _config;
            private readonly Timer _delayTimer;

            private bool _pause;

            public DebouncedFSEventHandler(IPluginAssemblyLoadConfig config, IPluginReloadEventHandler handler)
            {
                _handler = handler;
                _config = config;

                // Setup delay timer to wait on the config
                _delayTimer = new(OnTimeout, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            ///<inheritdoc/>
            void IFSChangeHandler.OnFileChanged(FileSystemEventArgs e)
            {
                // if we're already waiting to process an event, we don't need to stage another
                if (_pause)
                {
                    return;
                }

                // Set pause flag
                _pause = true;

                // Restart the timer to trigger reload event on elapsed
                _delayTimer.Restart(_config.ReloadDelay);
            }

            private void OnTimeout(object? state)
            {
                _delayTimer.Stop();

                // Fire event, let exception crash app
                _handler.OnAssemblyFileChanged();

                // Clear pause flag
                _pause = false;
            }

            protected override void Free()
            {
                _delayTimer.Dispose();

                FileWatcher.Unsubscribe(_config.AssemblyFile, this);
            }
        }
    }
}
