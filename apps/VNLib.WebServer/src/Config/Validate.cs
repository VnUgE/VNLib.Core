/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: Validate.cs 
*
* Validate.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;

namespace VNLib.WebServer.Config
{
    internal static class Validate
    {
        [DoesNotReturn]
        public static void EnsureNotNull<T>(T? obj, string message) where T : class
        {
            if (obj is null)
            {
                throw new ServerConfigurationException(message);
            }

            if (obj is string s && string.IsNullOrWhiteSpace(s))
            {
                throw new ServerConfigurationException(message);
            }
        }

        public static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
            {
                throw new ServerConfigurationException(message);
            }
        }

        public static void EnsureValidIp(string? address, string message)
        {
            if (!IPAddress.TryParse(address, out _))
            {
                throw new ServerConfigurationException(message);
            }
        }

        public static void EnsureNotEqual<T>(T a, T b, string message)
        {
            if (a is null || b is null)
            {
                throw new ServerConfigurationException(message);
            }

            if (a.Equals(b))
            {
                throw new ServerConfigurationException(message);
            }
        }

        public static void EnsureRangeEx(ulong value, ulong min, ulong max, string message)
        {
            if (value < min || value > max)
            {
                throw new ServerConfigurationException(message);
            }
        }

        public static void EnsureRangeEx(long value, long min, long max, string message)
        {
            if (value < min || value > max)
            {
                throw new ServerConfigurationException(message);
            }
        }

        public static void EnsureRange(ulong value, ulong min, ulong max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            EnsureRangeEx(value, min, max, $"Value for {paramName} must be between {min} and {max}. Value: {value}");
        }

        public static void EnsureRange(long value, long min, long max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            EnsureRangeEx(value, min, max, $"Value for {paramName} must be between {min} and {max}. Value: {value}");
        }

        public static void FileExists(string path)
        {
            if (!FileOperations.FileExists(path))
            {
                throw new ServerConfigurationException($"Required file: {path} not found");
            }
        }
    }
}
