/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: NoOpTimerWrapper.cs 
*
* NoOpTimerWrapper.cs is part of VNLib.Net.Transport.Tcp which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.Tcp is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.Tcp is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

namespace VNLib.Net.Transport.Tcp.Internal
{
    /// <summary>
    /// A no-operation implementation of <see cref="INetTimer"/> used when no timeout is required.
    /// All methods are empty and have no effect.
    /// </summary>
    internal readonly struct NoOpTimerWrapper : INetTimer
    {
        /// <inheritdoc/>
        public readonly void Start() { }

        /// <inheritdoc/>
        public readonly void Stop() { }
    }
}
