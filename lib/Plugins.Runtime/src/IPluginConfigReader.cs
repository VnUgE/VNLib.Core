/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginConfigReader.cs 
*
* IPluginConfigReader.cs is part of VNLib.Plugins.Runtime which is part of the larger 
* VNLib collection of libraries and utilities.
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

using System.IO;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Represents an object that gets configuration data for the desired assembly configuration
    /// and writes that conifguration to the output stream.
    /// </summary>
    public interface IPluginConfigReader
    {
        /// <summary>
        /// Gets the configuration data for the desired assembly configuration and writes that
        /// configuration to the output stream.
        /// </summary>
        /// <param name="asmConfig">The assembly configuration to get the configuration data for</param>
        /// <param name="outputStream">The stream to write the configuration file data to</param>
        void ReadPluginConfigData(IPluginAssemblyLoadConfig asmConfig, Stream outputStream);
    }
}
