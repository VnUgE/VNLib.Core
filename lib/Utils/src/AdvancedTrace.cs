/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AdvancedTrace.cs 
*
* AdvancedTrace.cs is part of VNLib.Utils which is part of the larger 
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

using System.Diagnostics;

namespace VNLib.Utils
{
    /// <summary>
    /// Provides methods for advanced tracing that are only optionally compiled 
    /// with the VNLIB_ADVANCED_TRACING symbol defined
    /// </summary>
    internal static class AdvancedTrace
    {
        const string AdvancedTraceSymbol = "VNLIB_ADVANCED_TRACING";

        [Conditional(AdvancedTraceSymbol)]
        public static void WriteLine(string? message) => Trace.WriteLine(message);

        [Conditional(AdvancedTraceSymbol)]
        public static void WriteLine(string? message, string? category) => Trace.WriteLine(message, category);

        [Conditional(AdvancedTraceSymbol)]
        public static void WriteLineIf(bool condition, string? message) => Trace.WriteLineIf(condition, message);

        [Conditional(AdvancedTraceSymbol)]
        public static void WriteLineIf(bool condition, string? message, string? category) => Trace.WriteLineIf(condition, message, category);

        [Conditional(AdvancedTraceSymbol)]
        public static void WriteLine(object? value) => Trace.WriteLine(value);
    }
}