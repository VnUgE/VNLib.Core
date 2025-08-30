/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: LoggerExtensions.cs 
*
* LoggerExtensions.cs is part of VNLib.Utils which is part of the larger 
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

#pragma warning disable CA1062 // Validate arguments of public methods

using System;

namespace VNLib.Utils.Logging
{
    /// <summary>
    /// Extension helper methods for writing logs to a <see cref="ILogProvider"/>
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Writes a debug level log entry with exception information
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="exp">The exception to log</param>
        /// <param name="value">Additional message text</param>
        public static void Debug(this ILogProvider log, Exception exp, string value = "") 
            => log.Write(LogLevel.Debug, exp, value);
        
        /// <summary>
        /// Writes a debug level log entry with a simple message
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="value">The message to log</param>
        public static void Debug(this ILogProvider log, string value) 
            => log.Write(LogLevel.Debug, value);
        
        /// <summary>
        /// Writes a debug level log entry with formatted message and object parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The parameters for formatting</param>
        public static void Debug(this ILogProvider log, string format, params object?[] args) 
            => log.Write(LogLevel.Debug, format, args);
        
        /// <summary>
        /// Writes a debug level log entry with formatted message and value type parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The value type parameters for formatting</param>
        public static void Debug(this ILogProvider log, string format, params ValueType[] args) 
            => log.Write(LogLevel.Debug, format, args);
        
        /// <summary>
        /// Writes an error level log entry with exception information
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="exp">The exception to log</param>
        /// <param name="value">Additional message text</param>
        public static void Error(this ILogProvider log, Exception exp, string value = "") 
            => log.Write(LogLevel.Error, exp, value);
        
        /// <summary>
        /// Writes an error level log entry with a simple message
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="value">The message to log</param>
        public static void Error(this ILogProvider log, string value) 
            => log.Write(LogLevel.Error, value);
        
        /// <summary>
        /// Writes an error level log entry with formatted message and object parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The parameters for formatting</param>
        public static void Error(this ILogProvider log, string format, params object?[] args) 
            => log.Write(LogLevel.Error, format, args);
        
        /// <summary>
        /// Writes a fatal level log entry with exception information
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="exp">The exception to log</param>
        /// <param name="value">Additional message text</param>
        public static void Fatal(this ILogProvider log, Exception exp, string value = "") 
            => log.Write(LogLevel.Fatal, exp, value);
        
        /// <summary>
        /// Writes a fatal level log entry with a simple message
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="value">The message to log</param>
        public static void Fatal(this ILogProvider log, string value) 
            => log.Write(LogLevel.Fatal, value);
        
        /// <summary>
        /// Writes a fatal level log entry with formatted message and object parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The parameters for formatting</param>
        public static void Fatal(this ILogProvider log, string format, params object?[] args) 
            => log.Write(LogLevel.Fatal, format, args);
        
        /// <summary>
        /// Writes a fatal level log entry with formatted message and value type parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The value type parameters for formatting</param>
        public static void Fatal(this ILogProvider log, string format, params ValueType[] args) 
            => log.Write(LogLevel.Fatal, format, args);
        
        /// <summary>
        /// Writes an information level log entry with exception information
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="exp">The exception to log</param>
        /// <param name="value">Additional message text</param>
        public static void Information(this ILogProvider log, Exception exp, string value = "") 
            => log.Write(LogLevel.Information, exp, value);
        
        /// <summary>
        /// Writes an information level log entry with a simple message
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="value">The message to log</param>
        public static void Information(this ILogProvider log, string value) 
            => log.Write(LogLevel.Information, value);
        
        /// <summary>
        /// Writes an information level log entry with formatted message and object parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The parameters for formatting</param>
        public static void Information(this ILogProvider log, string format, params object?[] args) 
            => log.Write(LogLevel.Information, format, args);
        
        /// <summary>
        /// Writes an information level log entry with formatted message and value type parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The value type parameters for formatting</param>
        public static void Information(this ILogProvider log, string format, params ValueType[] args) 
            => log.Write(LogLevel.Information, format, args);
        
        /// <summary>
        /// Writes a verbose level log entry with exception information
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="exp">The exception to log</param>
        /// <param name="value">Additional message text</param>
        public static void Verbose(this ILogProvider log, Exception exp, string value = "") 
            => log.Write(LogLevel.Verbose, exp, value);
        
        /// <summary>
        /// Writes a verbose level log entry with a simple message
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="value">The message to log</param>
        public static void Verbose(this ILogProvider log, string value) 
            => log.Write(LogLevel.Verbose, value);
        
        /// <summary>
        /// Writes a verbose level log entry with formatted message and object parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The parameters for formatting</param>
        public static void Verbose(this ILogProvider log, string format, params object?[] args) 
            => log.Write(LogLevel.Verbose, format, args);
        
        /// <summary>
        /// Writes a verbose level log entry with formatted message and value type parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The value type parameters for formatting</param>
        public static void Verbose(this ILogProvider log, string format, params ValueType[] args) 
            => log.Write(LogLevel.Verbose, format, args);
        
        /// <summary>
        /// Writes a warning level log entry with exception information
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="exp">The exception to log</param>
        /// <param name="value">Additional message text</param>
        public static void Warn(this ILogProvider log, Exception exp, string value = "") 
            => log.Write(LogLevel.Warning, exp, value);
        
        /// <summary>
        /// Writes a warning level log entry with a simple message
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="value">The message to log</param>
        public static void Warn(this ILogProvider log, string value) 
            => log.Write(LogLevel.Warning, value);
        
        /// <summary>
        /// Writes a warning level log entry with formatted message and object parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The parameters for formatting</param>
        public static void Warn(this ILogProvider log, string format, params object?[] args) 
            => log.Write(LogLevel.Warning, format, args);
        
        /// <summary>
        /// Writes a warning level log entry with formatted message and value type parameters
        /// </summary>
        /// <param name="log">The log provider instance</param>
        /// <param name="format">The format string</param>
        /// <param name="args">The value type parameters for formatting</param>
        public static void Warn(this ILogProvider log, string format, params ValueType[] args) 
            => log.Write(LogLevel.Warning, format, args);
    }
}
