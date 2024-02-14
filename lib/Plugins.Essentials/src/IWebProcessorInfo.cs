/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IWebProcessor.cs 
*
* IWebProcessor.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Accounts;

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// Abstractions for methods and information for processors
    /// </summary>
    public interface IWebProcessor : IWebRoot
    {
        /// <summary>
        /// Gets the EP processing options
        /// </summary>
        EventProcessorConfig Options { get; }

        /// <summary>
        /// Gets the account security provider
        /// </summary>
        IAccountSecurityProvider? AccountSecurity { get; }
       
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
        /// Finds the file specified by the request and the server root the user has requested.
        /// Determines if it exists, has permissions to access it, and allowed file attributes.
        /// Also finds default files and files without extensions
        /// </summary>
        bool FindResourceInRoot(string resourcePath, bool fullyQualified, out string path);

        /// <summary>
        /// Determines if a requested resource exists within the <see cref="EventProcessor"/> and is allowed to be accessed.
        /// </summary>
        /// <param name="resourcePath">The path to the resource</param>
        /// <param name="path">An out parameter that is set to the absolute path to the existing and accessable resource</param>
        /// <returns>True if the resource exists and is allowed to be accessed</returns>
        bool FindResourceInRoot(string resourcePath, out string path);
    }
}