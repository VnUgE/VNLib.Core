/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: TestLoadConfig.cs
*
* TestLoadConfig.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
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

namespace VNLib.Plugins.Runtime.Tests.Helpers
{
    /// <summary>
    /// A test implementation of <see cref="IPluginAssemblyLoadConfig"/> that provides configurable 
    /// assembly load configuration for unit tests.
    /// </summary>
    public sealed class TestLoadConfig : IPluginAssemblyLoadConfig
    {
        /// <summary>
        /// Gets the <see cref="TestLocalAssemblyLoader"/> that will be returned from <see cref="GetLoader"/>.
        /// </summary>
        public TestLocalAssemblyLoader Loader { get; } = new ();

        /// <inheritdoc/>
        public bool Unloadable { get; set; } = true;

        /// <inheritdoc/>
        public string AssemblyFile { get; set; } = "test.dll";

        /// <inheritdoc/>
        public bool WatchForReload { get; set; } = false;

        /// <inheritdoc/>
        public TimeSpan ReloadDelay { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestLoadConfig"/> class with default values
        /// and a new <see cref="TestLocalAssemblyLoader"/>.
        /// </summary>
        public TestLoadConfig()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestLoadConfig"/> class with a custom loader.
        /// </summary>
        /// <param name="loader">The assembly loader to return from <see cref="GetLoader"/>.</param>
        public TestLoadConfig(TestLocalAssemblyLoader loader) 
            => Loader = loader ?? throw new ArgumentNullException(nameof(loader));

        /// <inheritdoc/>
        public IAssemblyLoader GetLoader() => Loader;
    }
}
