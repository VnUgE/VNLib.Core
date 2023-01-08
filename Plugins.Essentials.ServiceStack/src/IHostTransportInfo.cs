/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IHostTransportInfo.cs 
*
* IHostTransportInfo.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents the service host's network/transport 
    /// information including the optional certificate and
    /// the endpoint to listen on
    /// </summary>
    public interface IHostTransportInfo
    {
        /// <summary>
        /// Optional TLS certificate to use
        /// </summary>
        X509Certificate? Certificate { get; }

        /// <summary>
        /// The endpoint to listen on
        /// </summary>
        IPEndPoint TransportEndpoint { get; }
    }
}
