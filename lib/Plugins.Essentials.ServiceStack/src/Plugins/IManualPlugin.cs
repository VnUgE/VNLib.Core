/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IManualPlugin.cs 
*
* IManualPlugin.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.ComponentModel.Design;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{
    /// <summary>
    /// Represents a plugin that may be added to a service stack in user-code
    /// instead of the conventional runtime plugin loading system
    /// </summary>
    public interface IManualPlugin : IDisposable
    {
        /// <summary>
        /// The name of the plugin
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Collects all exported services for use within the service stack
        /// </summary>
        /// <param name="container">The container to add services to</param>
        void GetAllExportedServices(IServiceContainer container);

        /// <summary>
        /// Initializes the plugin, called before accessing any other methods
        /// </summary>
        void Initialize();

        /// <summary>
        /// Loads the plugin, called after initialization but before getting 
        /// endpoints or services to allow for the plugin to configure itself
        /// and perform initial setup
        /// </summary>
        void Load();

        /// <summary>
        /// Called when an unload was requested, either manually by the plugin controller
        /// or when the service stack is unloading
        /// </summary>
        void Unload();

        /// <summary>
        /// Passes a console command to the plugin
        /// </summary>
        /// <param name="command">The raw command text to pass to the plugin from the console</param>
        void OnConsoleCommand(string command);
    }
}
