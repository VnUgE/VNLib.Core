/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpTransportBinding.cs 
*
* HttpTransportBinding.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Collections.Generic;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Presents a one-to-many relationship between a transport provider and it's virtual hosts
    /// </summary>
    /// <param name="Transport">The transport to listen for incomming connections on</param>
    /// <param name="Roots">The enumeration of web roots that will route connections</param>
    /// <remarks>
    /// An HTTP server accepts a collection of these bindings to allow for a many-to-many 
    /// relationship between transport providers and virtual hosts.
    /// </remarks>
    public sealed record HttpTransportBinding(ITransportProvider Transport, IEnumerable<IWebRoot> Roots);
}