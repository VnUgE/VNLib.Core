/*
* Copyright (c) 2022 Vaughn Nugent
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
        public static void Debug(this ILogProvider log, Exception exp, string value = "") => log.Write(LogLevel.Debug, exp, value);
        public static void Debug(this ILogProvider log, string value) => log.Write(LogLevel.Debug, value);
        public static void Debug(this ILogProvider log, string format, params object?[] args) => log.Write(LogLevel.Debug, format, args);
        public static void Debug(this ILogProvider log, string format, params ValueType[] args) => log.Write(LogLevel.Debug, format, args);
        public static void Error(this ILogProvider log, Exception exp, string value = "") => log.Write(LogLevel.Error, exp, value);
        public static void Error(this ILogProvider log, string value) => log.Write(LogLevel.Error, value);
        public static void Error(this ILogProvider log, string format, params object?[] args) => log.Write(LogLevel.Error, format, args);
        public static void Fatal(this ILogProvider log, Exception exp, string value = "") => log.Write(LogLevel.Fatal, exp, value);
        public static void Fatal(this ILogProvider log, string value) => log.Write(LogLevel.Fatal, value);
        public static void Fatal(this ILogProvider log, string format, params object?[] args) => log.Write(LogLevel.Fatal, format, args);
        public static void Fatal(this ILogProvider log, string format, params ValueType[] args) => log.Write(LogLevel.Fatal, format, args);
        public static void Information(this ILogProvider log, Exception exp, string value = "") => log.Write(LogLevel.Information, exp, value);
        public static void Information(this ILogProvider log, string value) => log.Write(LogLevel.Information, value);
        public static void Information(this ILogProvider log, string format, params object?[] args) => log.Write(LogLevel.Information, format, args);
        public static void Information(this ILogProvider log, string format, params ValueType[] args) => log.Write(LogLevel.Information, format, args);
        public static void Verbose(this ILogProvider log, Exception exp, string value = "") => log.Write(LogLevel.Verbose, exp, value);
        public static void Verbose(this ILogProvider log, string value) => log.Write(LogLevel.Verbose, value);
        public static void Verbose(this ILogProvider log, string format, params object?[] args) => log.Write(LogLevel.Verbose, format, args);
        public static void Verbose(this ILogProvider log, string format, params ValueType[] args) => log.Write(LogLevel.Verbose, format, args);
        public static void Warn(this ILogProvider log, Exception exp, string value = "") => log.Write(LogLevel.Warning, exp, value);
        public static void Warn(this ILogProvider log, string value) => log.Write(LogLevel.Warning, value);
        public static void Warn(this ILogProvider log, string format, params object?[] args) => log.Write(LogLevel.Warning, format, args);
        public static void Warn(this ILogProvider log, string format, params ValueType[] args) => log.Write(LogLevel.Warning, format, args);
    }
}
