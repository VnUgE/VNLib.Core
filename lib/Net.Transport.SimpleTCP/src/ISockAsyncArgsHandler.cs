/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: ISockAsyncArgsHandler.cs 
*
* ISockAsyncArgsHandler.cs is part of VNLib.Net.Transport.SimpleTCP which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.SimpleTCP is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.SimpleTCP is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

namespace VNLib.Net.Transport.Tcp
{
    internal interface ISockAsyncArgsHandler
    {
        /// <summary>
        /// Called when an asynchronous accept operation has completed
        /// </summary>
        /// <param name="args">The arguments that completed the accept operation</param>
        void OnSocketAccepted(VnSocketAsyncArgs args);

        /// <summary>
        /// Called when an asynchronous disconnect operation has completed
        /// </summary>
        /// <param name="args">The args that are disconnecting</param>
        void OnSocketDisconnected(VnSocketAsyncArgs args);
    }
}
