/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IVirtualHostHooks.cs 
*
* IVirtualHostHooks.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System.Net;

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    /// <summary>
    /// Represents a type that will handle http events for a virtual host
    /// </summary>
    public interface IVirtualHostHooks
    {
        /// <summary>
        /// <para>
        /// Called when the server intends to process a file and requires translation from a 
        /// uri path to a usable filesystem path 
        /// </para>
        /// <para>
        /// NOTE: This function must be thread-safe!
        /// </para>
        /// </summary>
        /// <param name="requestPath">The path requested by the request </param>
        /// <returns>The translated and filtered filesystem path used to identify the file resource</returns>
        string TranslateResourcePath(string requestPath);

        /// <summary>
        /// <para>
        /// When an error occurs and is handled by the library, this event is invoked 
        /// </para>
        /// <para>
        /// NOTE: This function must be thread-safe! And should not throw exceptions
        /// </para>
        /// </summary>
        /// <param name="errorCode">The error code that was created during processing</param>
        /// <param name="entity">The active IHttpEvent representing the faulted request</param>
        /// <returns>A value indicating if the entity was proccsed by this call</returns>
        bool ErrorHandler(HttpStatusCode errorCode, IHttpEvent entity);

        /// <summary>
        /// For pre-processing a request entity before all endpoint lookups are performed
        /// </summary>
        /// <param name="entity">The http entity to process</param>
        /// <param name="args">The results to return to the file processor, or <see cref="FileProcessArgs.Continue"/> if processing should continue</param>
        /// <returns></returns>
        void PreProcessEntityAsync(HttpEntity entity, out FileProcessArgs args);

        /// <summary>
        /// Allows for post processing of a selected <see cref="FileProcessArgs"/> for the given entity.
        /// <para>
        /// This method may mutate the <paramref name="chosenRoutine"/> argument reference to change the
        /// the routine that will be used to process the file.
        /// </para>
        /// </summary>
        /// <param name="entity">The http entity to process</param>
        /// <param name="chosenRoutine">The selected file processing routine for the given request</param>
        void PostProcessFile(HttpEntity entity, ref FileProcessArgs chosenRoutine);
    }
}
