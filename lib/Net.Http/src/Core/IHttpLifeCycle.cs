/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpLifeCycle.cs 
*
* IHttpLifeCycle.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// Represents a interface of lifecycle hooks that correspond
    /// with HTTP lifecycle events.
    /// </summary>
    internal interface IHttpLifeCycle
    {
        /// <summary>
        /// Raised when the context is being prepare for reuse,
        /// "revived from storage"
        /// </summary>
        void OnPrepare();

        /// <summary>
        /// Raised when the context is being released back to the pool
        /// for reuse at a later time
        /// </summary>
        void OnRelease();

        /// <summary>
        /// Raised when a new request is about to be processed
        /// on the current context
        /// </summary>
        void OnNewRequest();

        /// <summary>
        /// Raised when the request has been processed and the
        /// response has been sent. Used to perform per-request
        /// cleanup/reset for another request.
        /// </summary>
        /// <remarks>
        /// This method is guarunteed to be called regardless of an http error, this 
        /// method should not throw exceptions
        /// </remarks>
        void OnComplete();
    }
}