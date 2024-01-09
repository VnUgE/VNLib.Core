/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SafeLibraryHandle.cs 
*
* SafeLibraryHandle.cs is part of VNLib.Utils which is part of the larger 
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
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Native
{
    /// <summary>
    /// Represents a safe handle to a native library loaded to the current process
    /// </summary>
    public sealed class SafeLibraryHandle : SafeHandle
    {
        ///<inheritdoc/>
        public override bool IsInvalid => handle == IntPtr.Zero;

        private SafeLibraryHandle(IntPtr libHandle) : base(IntPtr.Zero, true)
        {
            //Init handle
            SetHandle(libHandle);
        }

        /// <summary>
        /// Finds and loads the specified native libary into the current process by its name at runtime 
        /// </summary>
        /// <param name="libPath">The path (or name of libary) to search for</param>
        /// <param name="searchPath">
        /// The <see cref="DllImportSearchPath"/> used to search for libaries 
        /// within the current filesystem
        /// </param>
        /// <returns>The loaded <see cref="SafeLibraryHandle"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public static SafeLibraryHandle LoadLibrary(string libPath, DllImportSearchPath searchPath = DllImportSearchPath.ApplicationDirectory)
        {
            _ = libPath ?? throw new ArgumentNullException(nameof(libPath));
            //See if the path includes a file extension
            return TryLoadLibrary(libPath, searchPath, out SafeLibraryHandle? lib)
                ? lib
                : throw new DllNotFoundException($"The library {libPath} or one of its dependencies could not be found");
        }

        /// <summary>
        /// Attempts to load the specified native libary into the current process by its name at runtime 
        /// </summary>
        ///<param name="libPath">The path (or name of libary) to search for</param>
        /// <param name="searchPath">
        /// The <see cref="DllImportSearchPath"/> used to search for libaries 
        /// within the current filesystem
        /// </param>
        /// <param name="lib">The handle to the libary if successfully loaded</param>
        /// <returns>True if the libary was found and loaded into the current process</returns>
        public static bool TryLoadLibrary(string libPath, DllImportSearchPath searchPath, [NotNullWhen(true)] out SafeLibraryHandle? lib)
        {
            lib = null;
            //Allow full rooted paths
            if (Path.IsPathRooted(libPath))
            {
                //Attempt a native load
                if (NativeLibrary.TryLoad(libPath, out IntPtr libHandle))
                {
                    lib = new(libHandle);
                    return true;
                }
                return false;
            }
            //Check application directory first (including subdirectories)
            if ((searchPath & DllImportSearchPath.ApplicationDirectory) > 0)
            {
                //get the current directory
                string libDir = Directory.GetCurrentDirectory();
                if (TryLoadLibraryInternal(libDir, libPath, SearchOption.TopDirectoryOnly, out lib))
                {
                    return true;
                }
            }
            //See if search in the calling assembly directory
            if ((searchPath & DllImportSearchPath.AssemblyDirectory) > 0)
            {
                //Get the calling assmblies directory
                string libDir = Assembly.GetCallingAssembly().Location;
                Debug.WriteLine("Native library searching for calling assembly location:{0} ", libDir);
                if (TryLoadLibraryInternal(libDir, libPath, SearchOption.TopDirectoryOnly, out lib))
                {
                    return true;
                }
            }
            //Search system32 dir
            if ((searchPath & DllImportSearchPath.System32) > 0)
            {
                //Get the system directory
                string libDir = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                if (TryLoadLibraryInternal(libDir, libPath, SearchOption.TopDirectoryOnly, out lib))
                {
                    return true;
                }
            }
            //Attempt a native load
            {
                if (NativeLibrary.TryLoad(libPath, out IntPtr libHandle))
                {
                    lib = new(libHandle);
                    return true;
                }
                return false;
            }
        }

        private static bool TryLoadLibraryInternal(string libDir, string libPath, SearchOption dirSearchOptions, [NotNullWhen(true)] out SafeLibraryHandle? libary)
        {
            //Try to find the libary file
            string? libFile = GetLibraryFile(libDir, libPath, dirSearchOptions);
            //Load libary
            if (libFile != null && NativeLibrary.TryLoad(libFile, out IntPtr libHandle))
            {
                libary = new SafeLibraryHandle(libHandle);
                return true;
            }
            libary = null;
            return false;
        }
        private static string? GetLibraryFile(string dirPath, string libPath, SearchOption search)
        {
            //slice the lib to its file name
            libPath = Path.GetFileName(libPath);
            libPath = Path.ChangeExtension(libPath, OperatingSystem.IsWindows() ? ".dll" : ".so");
            //Select the first file that matches the name
            return Directory.EnumerateFiles(dirPath, libPath, search).FirstOrDefault();
        }

        /// <summary>
        /// Loads a native method from the library of the specified name and managed delegate
        /// </summary>
        /// <typeparam name="T">The native method delegate type</typeparam>
        /// <param name="methodName">The name of the native method</param>
        /// <returns>A wapper handle around the native method delegate</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException">If the handle is closed or invalid</exception>
        /// <exception cref="EntryPointNotFoundException">When the specified entrypoint could not be found</exception>
        public SafeMethodHandle<T> GetMethod<T>(string methodName) where T : Delegate
        {
            //Increment handle count before obtaining a method
            bool success = false;
            DangerousAddRef(ref success);            

            ObjectDisposedException.ThrowIf(success == false, "The libary has been released!");

            try
            {
                //Get the method pointer
                IntPtr nativeMethod = NativeLibrary.GetExport(handle, methodName);
                //Get the delegate for the function pointer
                T method = Marshal.GetDelegateForFunctionPointer<T>(nativeMethod);
                return new(this, method);
            }
            catch
            {
                DangerousRelease();
                throw;
            }
        }
        /// <summary>
        /// Gets an delegate wrapper for the specified method without tracking its referrence.
        /// The caller must manage the <see cref="SafeLibraryHandle"/> referrence count in order
        /// to not leak resources or cause process corruption
        /// </summary>
        /// <typeparam name="T">The native method delegate type</typeparam>
        /// <param name="methodName">The name of the native method</param>
        /// <returns>A the delegate wrapper on the native method</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException">If the handle is closed or invalid</exception>
        /// <exception cref="EntryPointNotFoundException">When the specified entrypoint could not be found</exception>
        public T DangerousGetMethod<T>(string methodName) where T : Delegate
        {
            this.ThrowIfClosed();
            //Get the method pointer
            IntPtr nativeMethod = NativeLibrary.GetExport(handle, methodName);
            //Get the delegate for the function pointer
            return Marshal.GetDelegateForFunctionPointer<T>(nativeMethod);
        }

        ///<inheritdoc/>
        protected override bool ReleaseHandle()
        {
            //Free the library and set the handle as invalid
            NativeLibrary.Free(handle);
            SetHandleAsInvalid();
            return true;
        }
    }
}
