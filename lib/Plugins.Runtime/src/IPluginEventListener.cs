/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginEventListener.cs 
*
* IPluginEventListener.cs is part of VNLib.Plugins.Runtime which
* is part of the larger VNLib collection of libraries and utilities.
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
    /// Represents a plugin event consumer.
    /// </summary>
    public interface IPluginEventListener
    {
        /// <summary>
        /// Called by the registered <see cref="PluginController"/>
        /// to notify this listener that the plugins within the collection
        /// have been initialized and loaded
        /// </summary>
        /// <param name="controller">The collection on which the load event occured</param>
        /// <param name="state">The registration state parameter</param>
        void OnPluginLoaded(PluginController controller, object? state);
        /// <summary>
        /// Called by the registered <see cref="PluginController"/>
        /// to notify this listener that this plugins within the 
        /// collection have been unloaded
        /// </summary>
        /// <param name="controller">The controller that is reloading</param>
        /// <param name="state">The registration state parameter</param>
        void OnPluginUnloaded(PluginController controller, object? state);
    }
}
