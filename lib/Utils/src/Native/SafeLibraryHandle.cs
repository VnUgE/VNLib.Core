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
using System.Reflection;
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

        private SafeLibraryHandle(IntPtr libHandle, bool ownsHandle) : base(IntPtr.Zero, ownsHandle) => SetHandle(libHandle);

        /// <summary>
        /// Loads a native function pointer from the library of the specified name and 
        /// creates a new managed delegate
        /// </summary>
        /// <typeparam name="T">The native method delegate type</typeparam>
        /// <param name="functionName">The name of the native function</param>
        /// <returns>A wapper handle around the native method delegate</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException">If the handle is closed or invalid</exception>
        /// <exception cref="EntryPointNotFoundException">When the specified entrypoint could not be found</exception>
        public SafeMethodHandle<T> GetFunction<T>(string functionName) where T : Delegate
        {
            //Increment handle count before obtaining a method
            bool success = false;
            DangerousAddRef(ref success);

            ObjectDisposedException.ThrowIf(success == false, this);

            try
            {
                //Get the method pointer
                IntPtr nativeMethod = NativeLibrary.GetExport(handle, functionName);
                AdvancedTrace.WriteLine($"Loaded function '{functionName}' with address: 0x'{nativeMethod:x}'");
                return new(this, Marshal.GetDelegateForFunctionPointer<T>(nativeMethod));
            }
            catch
            {
                DangerousRelease();
                throw;
            }
        }

        /// <summary>
        /// Gets an delegate wrapper for the specified native function without tracking its referrence.
        /// The caller must manage the <see cref="SafeLibraryHandle"/> referrence count in order
        /// to not leak resources or cause process corruption
        /// </summary>
        /// <typeparam name="T">The native method delegate type</typeparam>
        /// <param name="functionName">The name of the native library function</param>
        /// <returns>A the delegate wrapper on the native method</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException">If the handle is closed or invalid</exception>
        /// <exception cref="EntryPointNotFoundException">When the specified entrypoint could not be found</exception>
        public T DangerousGetFunction<T>(string functionName) where T : Delegate
        {
            this.ThrowIfClosed();
            //Get the method pointer
            IntPtr nativeMethod = NativeLibrary.GetExport(handle, functionName);
            AdvancedTrace.WriteLine($"Loaded function '{functionName}' with address: 0x'{nativeMethod:x}'");
            //Get the delegate for the function pointer
            return Marshal.GetDelegateForFunctionPointer<T>(nativeMethod);
        }

        ///<inheritdoc/>
        protected override bool ReleaseHandle()
        {
            AdvancedTrace.WriteLine($"Releasing library handle: 0x'{handle:x}'");
            //Free the library and set the handle as invalid
            NativeLibrary.Free(handle);
            SetHandleAsInvalid();
            return true;
        }

        /// <summary>
        /// Finds and loads the specified native libary into the current process by its name at runtime.
        /// This function defaults to the executing assembly
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
            //See if the path includes a file extension
            return TryLoadLibrary(libPath, searchPath, out SafeLibraryHandle? lib)
                ? lib
                : throw new DllNotFoundException($"The library '{libPath}' or one of its dependencies could not be found");
        }

        /// <summary>
        /// Finds and loads the specified native libary into the current process by its name at runtime 
        /// </summary>
        /// <param name="libPath">The path (or name of libary) to search for</param>
        /// <param name="searchPath">
        /// The <see cref="DllImportSearchPath"/> used to search for libaries 
        /// within the current filesystem
        /// </param>
        /// <param name="assembly">The assembly loading the native library</param>
        /// <returns>The loaded <see cref="SafeLibraryHandle"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public static SafeLibraryHandle LoadLibrary(
            string libPath, 
            Assembly assembly, 
            DllImportSearchPath searchPath = DllImportSearchPath.ApplicationDirectory
        )
        {
            //See if the path includes a file extension
            return TryLoadLibrary(libPath, assembly, searchPath, out SafeLibraryHandle? lib)
                ? lib
                : throw new DllNotFoundException($"The library '{libPath}' or one of its dependencies could not be found");
        }

        /// <summary>
        /// Creates a new <see cref="SafeLibraryHandle"/> from an existing library pointer. 
        /// </summary>
        /// <param name="libHandle">A pointer to the existing (and loaded) library</param>
        /// <param name="ownsHandle">A value that specifies whether the wrapper owns the library handle now</param>
        /// <returns>A safe library wrapper around the existing library pointer</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public unsafe static SafeLibraryHandle FromExisting(nint libHandle, bool ownsHandle)
        {
            ArgumentNullException.ThrowIfNull(libHandle.ToPointer(), nameof(libHandle));
            return new(libHandle, ownsHandle);
        }

        /// <summary>
        /// Attempts to load the specified native libary into the current process by its name at runtime.
        /// This function defaults to the executing assembly
        /// </summary>
        ///<param name="libPath">The path (or name of libary) to search for</param>
        /// <param name="searchPath">
        /// The <see cref="DllImportSearchPath"/> used to search for libaries 
        /// within the current filesystem
        /// </param>
        /// <param name="library">The handle to the libary if successfully loaded</param>
        /// <returns>True if the libary was found and loaded into the current process</returns>
        public static bool TryLoadLibrary(
            string libPath,
            DllImportSearchPath searchPath,
            [NotNullWhen(true)] out SafeLibraryHandle? library
        )
        {
            return TryLoadLibrary(
                libPath,
                Assembly.GetExecutingAssembly(),        //Use the executing assembly as the default loading assembly
                searchPath,
                out library
            );
        }

      

        /// <summary>
        /// Attempts to load the specified native libary into the current process by its name at runtime 
        /// </summary>
        ///<param name="libPath">The path (or name of libary) to search for</param>
        /// <param name="searchPath">
        /// The <see cref="DllImportSearchPath"/> used to search for libaries 
        /// within the current filesystem
        /// </param>
        /// <param name="library">The handle to the libary if successfully loaded</param>
        /// <param name="assembly">The assembly loading the native library</param>
        /// <returns>True if the libary was found and loaded into the current process</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryLoadLibrary(
            string libPath, 
            Assembly assembly, 
            DllImportSearchPath searchPath, 
            [NotNullWhen(true)] out SafeLibraryHandle? library
        )
        {
            ArgumentNullException.ThrowIfNull(libPath);
            ArgumentNullException.ThrowIfNull(assembly);

            NatveLibraryResolver resolver = new(libPath, assembly, searchPath);

            bool success = resolver.ResolveAndLoadLibrary(out library);
            AdvancedTrace.WriteLineIf(success, $"Loaded library '{libPath}' with address: 0x'{library?.DangerousGetHandle():x}'");
            return success;
        }
    }
}
