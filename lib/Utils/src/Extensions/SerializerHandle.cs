/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SerializerHandle.cs 
*
* SerializerHandle.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Async;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Holds the exlcusive lock to the the <see cref="IAsyncAccessSerializer{TMoniker}"/>
    /// and releases the lock when the handle is disposed
    /// </summary>
    /// <typeparam name="TMoniker">The moniker type</typeparam>
    /// <param name="Moniker">The monike that referrences the entered lock</param>
    /// <param name="Serializer">The serialzer this handle will release the lock on when disposed</param>
    public readonly record struct SerializerHandle<TMoniker>(TMoniker Moniker, IAsyncAccessSerializer<TMoniker> Serializer) : IDisposable
    {
        /// <summary>
        /// Releases the exclusive lock on the moinker back to the serializer;
        /// </summary>
        public readonly void Dispose() => Serializer.Release(Moniker);
    }
}
