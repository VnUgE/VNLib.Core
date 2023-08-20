/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: HttpMiddlewareResult.cs 
*
* HttpMiddlewareResult.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

namespace VNLib.Plugins.Essentials.Middleware
{
    /// <summary>
    /// The result of a <see cref="IHttpMiddleware"/> process.
    /// </summary>
    public enum HttpMiddlewareResult
    {
        /// <summary>
        /// The request has not been completed and should continue to be processed.
        /// </summary>
        Continue,

        /// <summary>
        /// The request has been handled and no further processing should occur.
        /// </summary>
        Complete
    }
}
