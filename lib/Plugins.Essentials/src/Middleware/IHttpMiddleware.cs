﻿/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IHttpMiddleware.cs 
*
* IHttpMiddleware.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using System.Threading.Tasks;

namespace VNLib.Plugins.Essentials.Middleware
{

    /// <summary>
    /// Represents a low level intermediate request processor with high privilages, meant to add 
    /// functionality to entity processing.
    /// </summary>
    public interface IHttpMiddleware
    {
        /// <summary>
        /// Processes the <see cref="HttpEntity"/> and returns a <see cref="FileProcessArgs"/> 
        /// indicating the result of the process operation
        /// </summary>
        /// <param name="entity">The entity to process</param>
        /// <returns>The result of the operation</returns>
        ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity);

        /// <summary>
        /// Post processes an HTTP entity with possible file selection. May optionally mutate the 
        /// current arguments before the event processor completes a response. 
        /// </summary>
        /// <param name="entity">The entity that has been processes and is ready to close</param>
        /// <param name="currentArgs">The current file processor arguments</param>
        /// <remarks>
        /// Generally this function should simply observe results as the entity may already have been 
        /// configured for a response, such as by a virtual routine. You should inspect the current arguments
        /// before mutating the reference.
        /// </remarks>
        virtual void VolatilePostProcess(HttpEntity entity, ref FileProcessArgs currentArgs)
        { }
    }
}
