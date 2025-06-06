/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: LogLevel.cs 
*
* LogLevel.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Utils.Logging
{
    /// <summary>
    /// Specifies the logging levels for logging operations
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Verbose logging level for detailed diagnostic information
        /// </summary>
        Verbose, 
        /// <summary>
        /// Debug logging level for debug information during development
        /// </summary>
        Debug, 
        /// <summary>
        /// Information logging level for general informational messages
        /// </summary>
        Information, 
        /// <summary>
        /// Warning logging level for potentially harmful situations
        /// </summary>
        Warning, 
        /// <summary>
        /// Error logging level for error events that might still allow the application to continue
        /// </summary>
        Error, 
        /// <summary>
        /// Fatal logging level for very severe error events that will presumably lead to application abort
        /// </summary>
        Fatal
    }
}
