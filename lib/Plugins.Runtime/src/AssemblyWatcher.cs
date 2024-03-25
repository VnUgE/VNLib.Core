/*
* Copyright (c) 2024 Vaughn Nugent
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


namespace VNLib.Plugins.Runtime
{    

    internal static class AssemblyWatcher
    {

        internal static IDisposable WatchAssembly(IPluginReloadEventHandler handler, IPluginAssemblyLoader loader)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(loader);

            DebouncedFSEventHandler dbh = new(loader, handler);
            FileWatcher.Subscribe(loader.Config.AssemblyFile, dbh);

            return dbh;
        }

        internal sealed class DebouncedFSEventHandler : VnDisposeable, IFSChangeHandler
        {

            private readonly IPluginReloadEventHandler _handler;
            private readonly IPluginAssemblyLoader _loaderSource;
            private readonly Timer _delayTimer;

            private bool _pause;

            public DebouncedFSEventHandler(IPluginAssemblyLoader loader, IPluginReloadEventHandler handler)
            {
                _handler = handler;
                _loaderSource = loader;

                //setup delay timer to wait on the config
                _delayTimer = new(OnTimeout, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            ///<inheritdoc/>
            void IFSChangeHandler.OnFileChanged(FileSystemEventArgs e)
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
                _handler.OnPluginUnloaded(_loaderSource);

                //Clear pause flag
                _pause = false;
            }

            protected override void Free()
            {
                _delayTimer.Dispose();

                FileWatcher.Unsubscribe(_loaderSource.Config.AssemblyFile, this);
            }
        }
    }
}
