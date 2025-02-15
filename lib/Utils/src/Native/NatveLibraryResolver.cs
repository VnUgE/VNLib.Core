/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: NatveLibraryResolver.cs 
*
* NatveLibraryResolver.cs is part of VNLib.Utils which is part of 
* the larger VNLib collection of libraries and utilities.
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace VNLib.Utils.Native
{
    /// <summary>
    /// Uses a supplied library state to resolve and load a platform native library
    /// into the process memory space.
    /// </summary>
    /// <param name="suppliedLibraryPath">The raw caller-supplied library path to resolve</param>
    /// <param name="assembly">A assembly loading the desired library</param>
    /// <param name="searchPath">The dll loader search path requirements</param>
    internal readonly struct NatveLibraryResolver(string suppliedLibraryPath, Assembly assembly, DllImportSearchPath searchPath)
    {
        private readonly string _libFileName = Path.GetFileName(suppliedLibraryPath);
        private readonly string? _relativeDir = Path.GetDirectoryName(suppliedLibraryPath);

        internal readonly bool HasRelativeDir => _relativeDir != null;

        internal readonly bool IsFullPath => Path.IsPathRooted(suppliedLibraryPath);

        /// <summary>
        /// Resolves and attempts to load the current library into the current process. 
        /// </summary>
        /// <param name="library">The <see cref="SafeLibraryHandle"/> if the library was successfully resolved</param>
        /// <returns>True if the library was resolved and loaded into the process, false otherwise</returns>
        internal readonly bool ResolveAndLoadLibrary([NotNullWhen(true)] out SafeLibraryHandle? library)
        {
            //Try naive load if the path is an absolute path
            if (IsFullPath && _tryLoad(suppliedLibraryPath, out library))
            {
                return true;
            }

            //Try to load the library from the relative directory
            if (HasRelativeDir && TryLoadRelativeToWorkingDir(out library))
            {
                return true;
            }

            //Path is just a file name, so search for prefixes and extensions loading directly
            if (TryNaiveLoadLibrary(out library))
            {
                return true;
            }

            //Try searching for the file in directories
            return TryLoadFromSafeDirs(out library);
        }

        /*
         * Naive load builds file names that are platform dependent 
         * and probes for the library using the NativeLibrary.TryLoad rules
         * to load the library.
         * 
         * If the file has an extension, it will attempt to load the library
         * directly using the file name. Otherwise, it will continue to probe
         */
        private readonly bool TryNaiveLoadLibrary([NotNullWhen(true)] out SafeLibraryHandle? library)
        {
            foreach (string probingFileName in GetProbingFileNames(_libFileName))
            {
                if (_tryLoad(probingFileName, out library))
                {
                    return true;
                }
            }

            library = null;
            return false;
        }

        /*
         * Attempts to probe library files names that are located inside safe directories
         * specified by the caller using the DllImportSearchPath enum. 
         */
        private readonly bool TryLoadFromSafeDirs([NotNullWhen(true)] out SafeLibraryHandle? library)
        {
            //Try enumerating safe directories
            if (searchPath.HasFlag(DllImportSearchPath.SafeDirectories))
            {
                foreach (string dir in GetSpecialDirPaths())
                {
                    if (TryLoadInDirectory(dir, out library))
                    {
                        return true;
                    }
                }
            }

            //Check application directory first (including subdirectories)
            if (searchPath.HasFlag(DllImportSearchPath.ApplicationDirectory))
            {
                //get the current directory
                if (TryLoadInDirectory(Directory.GetCurrentDirectory(), out library))
                {
                    return true;
                }
            }

            //See if search in the calling assembly directory
            if (searchPath.HasFlag(DllImportSearchPath.AssemblyDirectory))
            {
                //Get the calling assmblies directory
                string libDir = assembly.Location;
                Debug.WriteLine("Native library searching for calling assembly location:{0} ", libDir);
                if (TryLoadInDirectory(libDir, out library))
                {
                    return true;
                }
            }

            //Search system32 dir
            if (searchPath.HasFlag(DllImportSearchPath.System32))
            {
                string sys32Dir = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

                //Get the system directory
                if (TryLoadInDirectory(sys32Dir, out library))
                {
                    return true;
                }
            }

            library = null;
            return false;
        }

        /*
         * Users may specify realtive directories to search for the library
         * in the current working directory, so this function attempts to load
         * the library from the relative directory if the user has specified one
         */
        private readonly bool TryLoadRelativeToWorkingDir([NotNullWhen(true)] out SafeLibraryHandle? library)
        {
            string libDir = Directory.GetCurrentDirectory();
            return TryLoadInDirectory(Path.Combine(libDir, _relativeDir!), out library);
        }

        /*
         * Attempts to load libraries that are located in the specified directory
         * by probing for the library file name in the directory path using 
         * prefixes and extensions
         */
        private readonly bool TryLoadInDirectory(string baseDir, [NotNullWhen(true)] out SafeLibraryHandle? library)
        {
            IEnumerable<string> fullProbingPaths = GetProbingFileNames(_libFileName).Select(p => Path.Combine(baseDir, p));

            foreach (string probingFilePath in fullProbingPaths)
            {
                if (_tryLoad(probingFilePath, out library))
                {
                    return true;
                }
            }

            library = null;
            return false;
        }

        /*
         * core load function
         */
        internal readonly bool _tryLoad(string filePath, [NotNullWhen(true)] out SafeLibraryHandle? library)
        {
            //Attempt a naive load
            if (NativeLibrary.TryLoad(filePath, assembly, searchPath, out IntPtr libHandle))
            {
                library = SafeLibraryHandle.FromExisting(libHandle, true);
                return true;
            }

            library = null;
            return false;
        }

        private static readonly Environment.SpecialFolder[] SafeDirs =
        [
            Environment.SpecialFolder.SystemX86,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.ProgramFiles
        ];

        private static IEnumerable<string> GetSpecialDirPaths() => SafeDirs.Select(Environment.GetFolderPath);

        private static IEnumerable<string> GetProbingFileNames(string libraryFileName)
        {
            //Inlcude the library file name if it has an extension
            if (Path.HasExtension(libraryFileName))
            {
                yield return libraryFileName;
            }

            foreach (string prefix in GetNativeLibPrefixs())
            {
                foreach (string libExtension in GetNativeLibExtension())
                {
                    yield return GetLibraryFileName(prefix, libraryFileName, libExtension);
                }
            }

            static string GetLibraryFileName(string prefix, string libPath, string extension)
            {
                //Get dir name from the lib path if it has one


                libPath = Path.Combine(prefix, libPath);

                //If the library path already has an extension, just search for the file
                if (!Path.HasExtension(libPath))
                {
                    //slice the lib to its file name
                    libPath = Path.GetFileName(libPath);

                    if (extension.Length > 0)
                    {
                        libPath = Path.ChangeExtension(libPath, extension);
                    }
                }

                return libPath;
            }

            static string[] GetNativeLibPrefixs()
            {
                if (OperatingSystem.IsWindows())
                {
                    return [""];
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return ["", "lib"];
                }
                else if (OperatingSystem.IsLinux())
                {
                    return ["", "lib_", "lib"];
                }
                else
                {
                    Debug.Fail("Unknown OS type");
                    return [];
                }
            }

            static string[] GetNativeLibExtension()
            {
                if (OperatingSystem.IsWindows())
                {
                    return ["", ".dll"];
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return ["", ".dylib"];
                }
                else if (OperatingSystem.IsLinux())
                {
                    return ["", ".so", ".so.1"];
                }
                else
                {
                    Debug.Fail("Unknown OS type");
                    return [];
                }
            }
        }
    }
}
