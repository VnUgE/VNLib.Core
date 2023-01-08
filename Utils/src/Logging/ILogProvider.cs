/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ILogProvider.cs 
*
* ILogProvider.cs is part of VNLib.Utils which is part of the larger 
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
    /// Self-contained logging interface that allows for applications events to be written to an
    /// output source
    /// </summary>
    public interface ILogProvider
    {
        /// <summary>
        /// Flushes any buffers to the output source
        /// </summary>
        abstract void Flush();

        /// <summary>
        /// Writes the string to the log with the specified priority log level
        /// </summary>
        /// <param name="level">The log priority level</param>
        /// <param name="value">The message to print</param>
        void Write(LogLevel level, string value);
        /// <summary>
        /// Writes the exception and optional string to the log with the specified priority log level
        /// </summary>
        /// <param name="level">The log priority level</param>
        /// <param name="exception">An exception object to write</param>
        /// <param name="value">The message to print</param>
        void Write(LogLevel level, Exception exception, string value = "");
        /// <summary>
        /// Writes the template string and params arguments to the log with the specified priority log level
        /// </summary>
        ///  <param name="level">The log priority level</param>
        /// <param name="value">The log template string</param>
        /// <param name="args">Variable length array of objects to log with the specified templatre</param>
        void Write(LogLevel level, string value, params object?[] args);
        /// <summary>
        /// Writes the template string and params arguments to the log with the specified priority log level
        /// </summary>
        ///  <param name="level">The log priority level</param>
        /// <param name="value">The log template string</param>
        /// <param name="args">Variable length array of objects to log with the specified templatre</param>
        void Write(LogLevel level, string value, params ValueType[] args);
        
        /// <summary>
        /// Gets the underlying log source
        /// </summary>
        /// <returns>The underlying log source</returns>
        object GetLogProvider();
        /// <summary>
        /// Gets the underlying log source
        /// </summary>
        /// <returns>The underlying log source</returns>
        public virtual T GetLogProvider<T>() => (T)GetLogProvider();
    }
}
