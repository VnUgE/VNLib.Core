/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: TestServicePool.cs
*
* TestServicePool.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
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
    /// A test implementation of <see cref="IPluginServicePool"/> that provides a no-op service 
    /// export mechanism for unit tests.
    /// </summary>
    public sealed class TestServicePool : IPluginServicePool
    {
        /// <inheritdoc/>
        public void ExportService(Type serviceType, object service, ExportFlags flags = ExportFlags.None)
        {
            // No-op implementation for testing
        }
    }
}
