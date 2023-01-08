/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IServiceHost.cs 
*
* IServiceHost.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents a host that exposes a processor for host events
    /// </summary>
    public interface IServiceHost
    {
        /// <summary>
        /// The <see cref="EventProcessor"/> to process 
        /// incoming HTTP connections
        /// </summary>
        EventProcessor Processor { get; }
        /// <summary>
        /// The host's transport infomration
        /// </summary>
        IHostTransportInfo TransportInfo { get; }
    }
}
