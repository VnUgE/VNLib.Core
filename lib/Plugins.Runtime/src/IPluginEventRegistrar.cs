/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginEventRegistrar.cs 
*
* IPluginEventRegistrar.cs is part of VNLib.Plugins.Runtime which is
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

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Represents a type that accepts <see cref="IPluginEventListener"/>
    /// event handlers and allow them to unload events
    /// </summary>
    public interface IPluginEventRegistrar
    {
        /// <summary>
        /// Registers a plugin event listener
        /// </summary>
        /// <param name="listener">The event handler instance to register</param>
        /// <param name="state">An optional state paremeter to pass to the event handler</param>
        void Register(IPluginEventListener listener, object? state = null);

        /// <summary>
        /// Unregisters the event listener
        /// </summary>
        /// <param name="listener">The event handler instance to unregister</param>
        /// <returns>A value that indicates if the event handler was successfully unregistered</returns>
        bool Unregister(IPluginEventListener listener);
    }
}
