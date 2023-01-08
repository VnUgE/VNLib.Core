/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IWaitHandle.cs 
*
* IWaitHandle.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Provides basic thread synchronization functions similar to <see cref="WaitHandle"/>
    /// </summary>
    public interface IWaitHandle
    {
        /// <summary>
        /// Waits for exclusive access to the resource indefinitly. If the signal is never received this method never returns
        /// </summary>
        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns>true if the current thread received the signal</returns>
        public virtual bool WaitOne() => WaitOne(Timeout.Infinite);
        /// <summary>
        /// Waits for exclusive access to the resource until the specified number of milliseconds
        /// </summary>
        /// <param name="millisecondsTimeout">Time in milliseconds to wait for exclusive access to the resource</param>
        /// <returns>true if the current thread received the signal, false if the timout expired, and access was not granted</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        bool WaitOne(int millisecondsTimeout);
        /// <summary>
        /// Waits for exclusive access to the resource until the specified <see cref="TimeSpan"/>
        /// </summary>
        /// <returns>true if the current thread received the signal, false if the timout expired, and access was not granted</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public virtual bool WaitOne(TimeSpan timeout) => WaitOne(Convert.ToInt32(timeout.TotalMilliseconds));
    }
}