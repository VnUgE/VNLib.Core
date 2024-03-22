/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: MonoCypherLibrary.cs 
*
* MonoCypherLibrary.cs is part of VNLib.Hashing.Portable which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.Portable is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.Portable is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.Portable. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using VNLib.Utils;
using VNLib.Utils.Native;
using VNLib.Utils.Resources;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing.Native.MonoCypher
{

    /// <summary>
    /// Wraps a safe library handle to the MonoCypher library and 
    /// provides access to the MonoCypher functions
    /// </summary>
    public unsafe class MonoCypherLibrary : VnDisposeable
    {
        public const string MONOCYPHER_LIB_ENVIRONMENT_VAR_NAME = "VNLIB_MONOCYPHER_DLL_PATH";
        public const string MONOCYPHER_LIB_DEFAULT_NAME = "vnlib_monocypher";

        /// <summary>
        /// Determines if the default MonoCypher library can be loaded into 
        /// the current process.
        /// </summary>
        /// <returns>true if the user enabled the default library, false otherwise</returns>
        public static bool CanLoadDefaultLibrary() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(MONOCYPHER_LIB_ENVIRONMENT_VAR_NAME)) == false;

        private static readonly LazyInitializer<MonoCypherLibrary> _defaultLib = new (LoadDefaultLibraryInternal);

        /// <summary>
        /// Gets the default MonoCypher library for the current process
        /// <para>
        /// You should call <see cref="CanLoadDefaultLibrary"/> before accessing 
        /// this property to ensure that the default library can be loaded
        /// </para>
        /// </summary>
        public static MonoCypherLibrary Shared => _defaultLib.Instance;

        /// <summary>
        /// Loads a new instance of the MonoCypher library with environment defaults
        /// <para>
        /// You should call <see cref="CanLoadDefaultLibrary"/> before calling this 
        /// function
        /// </para>
        /// </summary>
        /// <returns>The new library instance</returns>
        /// <exception cref="DllNotFoundException"></exception>
        /// <exception cref="MissingMemberException"></exception>
        public static MonoCypherLibrary LoadNewInstance() => LoadDefaultLibraryInternal();

        /// <summary>
        /// Loads the MonoCypher library from the specified shared library path
        /// </summary>
        /// <param name="path">The file path or library name to search for</param>
        /// <param name="searchPath">The directory search flags</param>
        /// <returns>The new <see cref="MonoCypherLibrary"/> instance</returns>
        public static MonoCypherLibrary LoadLibrary(string path, DllImportSearchPath searchPath)
        {
            SafeLibraryHandle lib = SafeLibraryHandle.LoadLibrary(path, searchPath);
            return new(lib, true);
        }

        private static MonoCypherLibrary LoadDefaultLibraryInternal()
        {
            //Get the path to the library or default
            string? monoCypherLibPath = Environment.GetEnvironmentVariable(MONOCYPHER_LIB_ENVIRONMENT_VAR_NAME) ?? MONOCYPHER_LIB_DEFAULT_NAME;

            Trace.WriteLine("Attempting to load global native MonoCypher library from: " + monoCypherLibPath, "MonoCypher");

            SafeLibraryHandle lib = SafeLibraryHandle.LoadLibrary(monoCypherLibPath, DllImportSearchPath.SafeDirectories);
            return new(lib, true);
        }


        private readonly SafeLibraryHandle _library;
        private readonly FunctionTable _functions;
        private readonly bool _ownsHandle;

        internal ref readonly FunctionTable Functions
        {
            get
            {
                Check();
                return ref _functions;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="MonoCypherLibrary"/> consuming the 
        /// specified library handle
        /// </summary>
        /// <param name="library">The safe MonoCypher library handle</param>
        /// <param name="ownsHandle">A value that indicates if the current instance owns the library handle</param>
        /// <exception cref="ArgumentNullException"></exception>
        public MonoCypherLibrary(SafeLibraryHandle library, bool ownsHandle)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _ownsHandle = ownsHandle;

            //Init the function table
            InitFunctionTable(library, out _functions);
        }
        
        private static void InitFunctionTable(SafeLibraryHandle library, out FunctionTable functions)
        {
            functions = new FunctionTable
            {
                //Argon2
                Argon2Hash = library.DangerousGetFunction<MCPasswordModule.Argon2Hash>(),
                Argon2CalcWorkArea = library.DangerousGetFunction<MCPasswordModule.Argon2CalcWorkArea>(),

                //Blake2b
                Blake2GetContextSize = library.DangerousGetFunction<MCBlake2Module.Blake2GetContextSize>(),
                Blake2Init = library.DangerousGetFunction<MCBlake2Module.Blake2Init>(),
                Blake2Update = library.DangerousGetFunction<MCBlake2Module.Blake2Update>(),
                Blake2Final = library.DangerousGetFunction<MCBlake2Module.Blake2Final>(),
                Blake2GethashSize = library.DangerousGetFunction<MCBlake2Module.Blake2GetHashSize>(),
            };
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            if(_ownsHandle)
            {
                _library.Dispose();
            }
        }

        internal readonly struct FunctionTable
        {
            //Argon2 module
            public readonly MCPasswordModule.Argon2Hash Argon2Hash { get; init; }
            public readonly MCPasswordModule.Argon2CalcWorkArea Argon2CalcWorkArea { get; init; }

            //Blake2 module
            public readonly MCBlake2Module.Blake2GetContextSize Blake2GetContextSize { get; init; }
            public readonly MCBlake2Module.Blake2Init Blake2Init { get; init; }
            public readonly MCBlake2Module.Blake2Update Blake2Update { get; init; }           
            public readonly MCBlake2Module.Blake2Final Blake2Final { get; init; }
            public readonly MCBlake2Module.Blake2GetHashSize Blake2GethashSize { get; init; }
        }
    }
}