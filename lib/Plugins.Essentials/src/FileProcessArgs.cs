/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: FileProcessArgs.cs 
*
* FileProcessArgs.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using System;
using System.Net;

#nullable enable

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// Server routine to follow after processing selector 
    /// </summary>
    public enum FpRoutine
    {
        /// <summary>
        /// There was an error during processing and the server should immediatly respond with a <see cref="HttpStatusCode.InternalServerError"/> error code
        /// </summary>
        Error,
        /// <summary>
        /// The server should continue the file read operation with the current information
        /// </summary>
        Continue,
        /// <summary>
        /// The server should redirect the conneciton to an alternate location
        /// </summary>
        Redirect,
        /// <summary>
        /// The server should immediatly respond with a <see cref="HttpStatusCode.Forbidden"/> error code
        /// </summary>
        Deny,
        /// <summary>
        /// The server should fulfill the reqeest by sending the contents of an alternate file location (if it exists) with the existing connection
        /// </summary>
        ServeOther,
        /// <summary>
        /// The server should immediatly respond with a <see cref="HttpStatusCode.NotFound"/> error code
        /// </summary>
        NotFound,
        /// <summary>
        /// Serves another file location that must be a trusted fully qualified location
        /// </summary>
        ServeOtherFQ,
        /// <summary>
        /// The connection does not require a file to be processed
        /// </summary>
        VirtualSkip,
    }

    /// <summary>
    /// Specifies operations the file processor will follow during request handling
    /// </summary>
    /// <param name="Alternate">
    /// The routine the file processor should execute
    /// </param>
    /// <param name="Routine">
    /// An optional alternate path for the given routine
    /// </param>
    public readonly record struct FileProcessArgs(FpRoutine Routine, string Alternate)
    {
        /// <summary>
        /// Signals the file processor should complete with a <see cref="FpRoutine.Deny"/> routine
        /// </summary>
        public static readonly FileProcessArgs Deny = new (FpRoutine.Deny);
        /// <summary>
        /// Signals the file processor should continue with intended/normal processing of the request
        /// </summary>
        public static readonly FileProcessArgs Continue = new (FpRoutine.Continue);
        /// <summary>
        /// Signals the file processor should complete with a <see cref="FpRoutine.Error"/> routine
        /// </summary>
        public static readonly FileProcessArgs Error = new (FpRoutine.Error);
        /// <summary>
        /// Signals the file processor should complete with a <see cref="FpRoutine.NotFound"/> routine
        /// </summary>
        public static readonly FileProcessArgs NotFound = new (FpRoutine.NotFound);
        /// <summary>
        /// Signals the file processor should not process the connection
        /// </summary>
        public static readonly FileProcessArgs VirtualSkip = new (FpRoutine.VirtualSkip);
      

        /// <summary>
        /// Initializes a new <see cref="FileProcessArgs"/> with the specified routine
        /// and empty <see cref="Alternate"/> path
        /// </summary>
        /// <param name="routine">The file processing routine to execute</param>
        public FileProcessArgs(FpRoutine routine):this(routine, string.Empty)
        {
        }
    }
}