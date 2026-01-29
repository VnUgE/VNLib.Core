/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: MutexReleaser.cs 
*
* MutexReleaser.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.CompilerServices;
using System.Threading;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Represents a releaser handle for a <see cref="Mutex"/>
    /// that has been entered and will be released. Best if used
    /// within a using() statment
    /// </summary>
    /// <param name="Handle">The Mutex reference</param>
    public readonly record struct MutexReleaser(Mutex Handle) : IDisposable
    {
        /// <summary>
        ///  Releases the held System.Threading.Mutex once.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose() => Handle.ReleaseMutex();

        /// <summary>
        /// Releases the held System.Threading.Mutex once.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void ReleaseMutex() => Handle.ReleaseMutex();

        /// <summary>
        /// Releases the held System.Threading.Mutex once.
        /// </summary>
        [Obsolete]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void ReleaseMutext() => ReleaseMutex();       
    }
}