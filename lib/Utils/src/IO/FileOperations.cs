/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: FileOperations.cs 
*
* FileOperations.cs is part of VNLib.Utils which is part of the larger 
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
using System.IO;
using System.Runtime.InteropServices;

namespace VNLib.Utils.IO
{  

    /// <summary>
    /// Contains cross-platform optimized filesystem operations.
    /// </summary>
    public static partial class FileOperations
    {
        /// <summary>
        /// Represents an invalid file attributes value returned when file operations fail
        /// </summary>
        public const int INVALID_FILE_ATTRIBUTES = -1;
        
        [LibraryImport("Shlwapi", EntryPoint = "PathFileExistsW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool PathFileExists([MarshalAs(UnmanagedType.LPWStr)] string path);

        [LibraryImport("kernel32", EntryPoint = "GetFileAttributesW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return:MarshalAs(UnmanagedType.I4)]
        private static unsafe partial int GetFileAttributes([MarshalAs(UnmanagedType.LPWStr)] string path);

        static readonly bool IsWindows = OperatingSystem.IsWindows();

        /// <summary>
        /// Determines if a file exists. If application is current running in the Windows operating system, Shlwapi.PathFileExists is invoked,
        /// otherwise <see cref="File.Exists(string?)"/> is invoked
        /// </summary>
        /// <param name="filePath">the path to the file</param>
        /// <returns>True if the file can be opened, false otherwise</returns>
        public static bool FileExists(string filePath)
        {
            //If windows is detected, use the unmanged function
            if (!IsWindows)
            {
                return File.Exists(filePath);
            }

            //Invoke the winapi file function
            return PathFileExists(filePath);
        }

        /// <summary>
        /// If Windows is detected at load time, gets the attributes for the specified file.
        /// </summary>
        /// <param name="filePath">The path to the existing file</param>
        /// <returns>The attributes of the file </returns>
        /// <exception cref="PathTooLongException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public static FileAttributes GetAttributes(string filePath)
        {
            //If windows is detected, use the unmanged function
            if (!IsWindows)
            {
                return File.GetAttributes(filePath);
            }

            //Invoke the winapi file function and cast the returned int value to file attributes
            int attr = GetFileAttributes(filePath);

            //Check for error
            if (attr == INVALID_FILE_ATTRIBUTES)
            {
                throw new FileNotFoundException("The requested file was not found", filePath);
            }

            //Cast to file attributes and return
            return (FileAttributes)attr;
        }
    }
}