/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SemSlimReleaser.cs 
*
* SemSlimReleaser.cs is part of VNLib.Utils which is part of the larger 
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
using System.Threading;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Represents a releaser handle for a <see cref="SemaphoreSlim"/>
    /// that has been entered and will be released. Best if used
    /// within a using() statment
    /// </summary>
    public readonly struct SemSlimReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        internal SemSlimReleaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
        /// <summary>
        /// Releases the System.Threading.SemaphoreSlim object once.
        /// </summary>
        public readonly void Dispose() => _semaphore.Release();
        /// <summary>
        /// Releases the System.Threading.SemaphoreSlim object once.
        /// </summary>
        /// <returns>The previous count of the <see cref="SemaphoreSlim"/></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="SemaphoreFullException"></exception>
        public readonly int Release() => _semaphore.Release();
    }
}