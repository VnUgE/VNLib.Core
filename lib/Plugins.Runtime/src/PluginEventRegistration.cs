/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginEventRegistration.cs 
*
* PluginEventRegistration.cs is part of VNLib.Plugins.Runtime which is 
* part of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Holds a registration for events to a given <see cref="IPluginEventRegistrar"/>.
    /// The event listener is unregistered from events when this registration is disposed.
    /// </summary>
    public readonly record struct PluginEventRegistration : IDisposable
    {
        private readonly IPluginEventRegistrar _registrar;
        private readonly IPluginEventListener _listener;

        internal PluginEventRegistration(IPluginEventRegistrar container, IPluginEventListener listener)
        {
            _listener = listener;
            _registrar = container;
        }

        /// <summary>
        /// Unreigsers the listner and releases held resources
        /// </summary>
        public readonly void Dispose()
        {
            _ = _registrar?.Unregister(_listener);
        }

        /// <summary>
        /// Unregisters a previously registered <see cref="IPluginEventListener"/>
        /// from the <see cref="PluginController"/> it was registered to
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public readonly void Unregister()
        {
            if (_registrar == null)
            {
                return;
            }
            if (!_registrar.Unregister(_listener))
            {
                throw new InvalidOperationException("The listner has already been unregistered");
            }
        }
    }
}
