/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.PluginBase
* File: IPluginTaskObserver.cs 
*
* IPluginTaskObserver.cs is part of VNLib.Plugins.PluginBase which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.PluginBase is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.PluginBase is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.PluginBase. If not, see http://www.gnu.org/licenses/.
*/

using System.Threading.Tasks;

namespace VNLib.Plugins
{
    /// <summary>
    /// Represents an plugin task observer to observe background operations
    /// </summary>
    public interface IPluginTaskObserver
    {
        /// <summary>
        /// Adds a task to the observation list
        /// </summary>
        /// <param name="task">The task to observe</param>
        void ObserveTask(Task task);

        /// <summary>
        /// Removes a task from the task observation list
        /// </summary>
        /// <param name="task">The task to remove</param>
        void RemoveObservedTask(Task task);
    }
}