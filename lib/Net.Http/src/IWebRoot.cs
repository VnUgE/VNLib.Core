/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IWebRoot.cs 
*
* IWebRoot.cs is part of VNLib.Net.Http which is part of the larger 
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

using System;
using System.Threading.Tasks;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents a root identifying the main endpoints of the server, and the primary processing actions
    /// for requests to this endpoint
    /// </summary>
    public interface IWebRoot
    {

        /// <summary>
        /// The hostname the server will listen for, and the hostname that will identify this root when a connection requests it
        /// </summary>
        string Hostname { get; }

        /// <summary>
        /// <para>
        /// The main event handler for user code to process a request 
        /// </para>
        /// <para>
        /// NOTE: This function must be thread-safe!
        /// </para>
        /// </summary>
        /// <param name="httpEvent">An active, unprocessed event capturing the request infomration into a standard format</param>
        /// <returns>A <see cref="ValueTask"/> that the processor will await until the entity has been processed</returns>
        ValueTask ClientConnectedAsync(IHttpEvent httpEvent);
    }
}