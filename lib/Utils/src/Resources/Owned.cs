/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: Owned.cs 
*
* Owned.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Resources
{
    /// <summary>
    /// A structure that wraps a disposable value and indicates whether this instance is 
    /// responsible for disposing the value.
    /// </summary>
    /// <typeparam name="T">The disposable type to wrap</typeparam>
    /// <param name="value">The disposable value</param>
    /// <param name="ownsValue">
    /// The value that indicates if ownership should be passed on to this wrapper. If true, the value will be disposed 
    /// when this instance is disposed. False indicates the instance is not responsible for disposing the value.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>"
    public readonly struct Owned<T>(T value, bool ownsValue) : IDisposable where T : IDisposable
    {

        /// <summary>
        /// The owned value.
        /// </summary>
        public T Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Indicates whether the <see cref="Value"/> should be disposed when this instance is disposed.
        /// </summary>
        public bool OwnsValue { get; } = ownsValue;

        /// <summary>
        /// Disposes the <see cref="Value"/> if this instance owns it (when <see cref="OwnsValue"/> is true).
        /// </summary>
        public void Dispose()
        {
            if (OwnsValue)
            {
                Value.Dispose();
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Owned{T}"/> with the same value, 
        /// but allows for changing the ownership flag.
        /// </summary>
        /// <param name="ownsValue">The new ownership value</param>
        /// <returns>The new <see cref="Owned{T}"/> wrapper with new ownership</returns>
        public Owned<T> WithOwnership(bool ownsValue) => new(Value, ownsValue);

        /// <summary>
        /// Creates a new instance of <see cref="Owned{T}"/> that does not own the value.
        /// </summary>
        /// <param name="value">The value to wrap</param>
        /// <returns>The new <see cref="Owned{T}"/> wrapper that does not own the value</returns>
        public static Owned<T> Shared(T value) => new(value, ownsValue: false);

        /// <summary>
        /// Creates a new instance of <see cref="Owned{T}"/> that owns the value.
        /// </summary>
        /// <param name="value">The value to wrap</param>
        /// <returns>The new <see cref="Owned{T}"/> wrapper that owns the value</returns>
        public static Owned<T> OwnedValue(T value) => new(value, ownsValue: true);

        /// <summary>
        /// Implicit conversion to the owned value.
        /// </summary>
        /// <param name="owned">The owned wrapper</param>
        /// <returns>The underlying <see cref="Value"/> from the wrapper</returns>
        public static implicit operator T(in Owned<T> owned) => owned.Value;
    }
}
