/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: TestLocalAssemblyLoader.cs
*
* TestLocalAssemblyLoader.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime.Tests. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Reflection;

namespace VNLib.Plugins.Runtime.Tests.Helpers
{
    /// <summary>
    /// A test implementation of <see cref="IAssemblyLoader"/> that returns a configurable
    /// assembly without performing real assembly loading. Used to test plugin discovery
    /// and lifecycle without requiring external assemblies.
    /// </summary>
    /// <remarks>
    /// By default the loader returns the test assembly itself, allowing plugin discovery
    /// to find test plugin types defined in the same assembly as the tests. The returned
    /// assembly can be overridden via <see cref="AssemblyToReturn"/>.
    /// </remarks>
    public sealed class TestLocalAssemblyLoader(Assembly assembly) : IAssemblyLoader
    {
        /// <summary>
        /// Gets the number of times <see cref="Load"/> has been called.
        /// </summary>
        public int LoadCallCount { get; private set; }

        /// <summary>
        /// Gets the number of times <see cref="Unload"/> has been called.
        /// </summary>
        public int UnloadCallCount { get; private set; }

        /// <summary>
        /// Gets the number of times <see cref="Dispose"/> has been called.
        /// Used to verify the runtime releases loader resources during teardown.
        /// </summary>
        public int DisposeCallCount { get; private set; }

        /// <summary>
        /// Gets or sets the assembly instance to return from <see cref="GetAssembly"/>.
        /// Defaults to the assembly supplied at construction time.
        /// </summary>
        public Assembly AssemblyToReturn { get; set; } = assembly;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestLocalAssemblyLoader"/> class
        /// using the currently executing assembly as the assembly to return.
        /// </summary>
        public TestLocalAssemblyLoader()
            :this(Assembly.GetExecutingAssembly())
        { }
      
        /// <inheritdoc/>
        public void Load() => LoadCallCount++;


        /// <inheritdoc/>
        public Assembly GetAssembly() => AssemblyToReturn;

        /// <inheritdoc/>
        public void Unload() => UnloadCallCount++;

        /// <inheritdoc/>
        public void Dispose() => DisposeCallCount++;
    }
}
