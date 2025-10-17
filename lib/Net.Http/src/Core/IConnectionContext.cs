/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IConnectionContext.cs 
*
* IConnectionContext.cs is part of VNLib.Net.Http which is part of the larger 
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

using System.Threading.Tasks;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// A request-response stream oriented connection state
    /// </summary>
    internal interface IConnectionContext
    {
        /// <summary>
        /// Initializes the context to work with the specified 
        /// transport context
        /// </summary>
        /// <param name="tranpsort">A referrence to the transport context to use</param>
        void InitializeContext(ITransportContext tranpsort);

        /// <summary>
        /// Signals the context that it should prepare to process a new request 
        /// for the current transport
        /// </summary>
        void BeginRequest();

        /// <summary>
        /// Flushes and pending data associated with the request to the transport
        /// </summary>
        /// <returns>A task that represents the flush operation</returns>
        Task FlushTransportAsync();

        /// <summary>
        /// Signals to the context that it will release any request specific
        /// resources
        /// </summary>
        void EndRequest();
    }
}