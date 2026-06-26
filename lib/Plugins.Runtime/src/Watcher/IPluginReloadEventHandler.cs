/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginReloadEventHandler.cs 
*
* IPluginReloadEventHandler.cs is part of VNLib.Plugins.Runtime which 
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

namespace VNLib.Plugins.Runtime.Watcher
{
    internal interface IPluginReloadEventHandler
    {
        /// <summary>
        /// Called every time a watched <see cref="IPluginAssemblyLoadConfig"/> assembly file changes 
        /// on disk and a reload should be triggered.
        /// </summary>
        void OnAssemblyFileChanged();
    }
}
