// Copyright (c) Nate McMaster.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

/*
 * Modifications Copyright (c) 2024 Vaughn Nugent
 * 
 * Changes:
 *      - Use the new .NET 8.0 collection syntax
 *      - Use the new OperatingSystem class for platform detection
 *      - Remove static constructor for new best practice guidlines
 */

using System;
using System.Diagnostics;

namespace McMaster.NETCore.Plugins
{
    internal class PlatformInformation
    {
        public static readonly string[] NativeLibraryExtensions = GetNativeLibExtension();
        public static readonly string[] NativeLibraryPrefixes = GetNativeLibPrefixs();

        public static readonly string[] ManagedAssemblyExtensions =
        [
                ".dll",
                ".ni.dll",
                ".exe",
                ".ni.exe"
        ];

        private static string[] GetNativeLibPrefixs()
        {
            if (OperatingSystem.IsWindows())
            {
                return [""];
            }
            else if (OperatingSystem.IsMacOS())
            {
                return ["", "lib",];
            }
            else if (OperatingSystem.IsLinux())
            {
                return ["", "lib"];
            }
            else
            {
                Debug.Fail("Unknown OS type");
                return [];
            }
        }

        private static string[] GetNativeLibExtension()
        {
            if (OperatingSystem.IsWindows())
            {
                return [".dll"];
            }
            else if (OperatingSystem.IsMacOS())
            {
                return [".dylib"];
            }
            else if (OperatingSystem.IsLinux())
            {
                return [".so", ".so.1"];
            }
            else
            {
                Debug.Fail("Unknown OS type");
                return [];
            }
        }
    }
}
