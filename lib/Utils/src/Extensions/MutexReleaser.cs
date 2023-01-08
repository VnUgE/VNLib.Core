/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Threading;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Represents a releaser handle for a <see cref="Mutex"/>
    /// that has been entered and will be released. Best if used
    /// within a using() statment
    /// </summary>
    public readonly struct MutexReleaser : IDisposable, IEquatable<MutexReleaser>
    {
        private readonly Mutex _mutext;
        internal MutexReleaser(Mutex mutex) => _mutext = mutex;
        /// <summary>
        ///  Releases the held System.Threading.Mutex once.
        /// </summary>
        public readonly void Dispose() => _mutext.ReleaseMutex();
        /// <summary>
        /// Releases the held System.Threading.Mutex once.
        /// </summary>
        public readonly void ReleaseMutext() => _mutext.ReleaseMutex();

        ///<inheritdoc/>
        public bool Equals(MutexReleaser other) => _mutext.Equals(other._mutext);
        
        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is MutexReleaser releaser && Equals(releaser);

        ///<inheritdoc/>
        public override int GetHashCode() => _mutext.GetHashCode();

        ///<inheritdoc/>
        public static bool operator ==(MutexReleaser left, MutexReleaser right) => left.Equals(right);
        ///<inheritdoc/>
        public static bool operator !=(MutexReleaser left, MutexReleaser right) => !(left == right);
    }
}