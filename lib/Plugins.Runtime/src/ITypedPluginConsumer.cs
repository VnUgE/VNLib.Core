/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: ITypedPluginConsumer.cs 
*
* ITypedPluginConsumer.cs is part of VNLib.Plugins.Runtime which is
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
    /// An abstraction that represents a consumer of a dynamically loaded type.
    /// </summary>
    /// <typeparam name="T">The service type to consume</typeparam>
    public interface ITypedPluginConsumer<T>
    {
        /// <summary>
        /// Invoked when the instance of the desired type is loaded.
        /// This is a new instance of the desired type
        /// </summary>
        /// <param name="plugin">A new instance of the requested type</param>
        /// <param name="state">An optional user-state that will be passed when this function is invoked</param>
        void OnLoad(T plugin, object? state);

        /// <summary>
        /// Called when the loader that maintains the instance is unloading
        /// the type.
        /// </summary>
        /// <param name="plugin">The instance of the type that is being unloaded</param>
        /// <param name="state">An optional user-state that will be passed when this function is invoked</param>
        void OnUnload(T plugin, object? state);
    }
}
