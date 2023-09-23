/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IVirtualHostBuilder.cs 
*
* IVirtualHostBuilder.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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


namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Allows for configuring a single virtual host
    /// </summary>
    public interface IVirtualHostBuilder
    {
        /// <summary>
        /// Allows configuring a virtual host options
        /// </summary>
        /// <param name="configCallback">A callback function used to modify the virtual host configuration</param>
        /// <returns></returns>
        IVirtualHostBuilder WithOption(Action<VirtualHostConfiguration> configCallback);
    }
}
