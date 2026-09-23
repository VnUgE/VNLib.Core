/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IAsyncLazy.cs 
*
* IAsyncLazy.cs is part of VNLib.Utils which is part of the larger 
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
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Represents an asynchronous lazy operation with non-blocking access to the target value.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the asynchronous operation.</typeparam>
    public interface IAsyncLazy<T>
    {
        /// <summary>
        /// Gets a value that indicates whether the asynchronous operation has completed.
        /// </summary>
        bool Completed { get; }

        /// <summary>
        /// Gets an awaiter used to await the asynchronous operation.
        /// </summary>
        /// <returns>A <see cref="TaskAwaiter{T}"/> for the asynchronous operation.</returns>
        TaskAwaiter<T> GetAwaiter();

        /// <summary>
        /// Gets the target value of the asynchronous operation without blocking.
        /// </summary>
        /// <remarks>
        /// If the operation failed, throws the exception that caused the failure.
        /// If the operation has not completed, throws an <see cref="InvalidOperationException"/>.
        /// </remarks>
        T Value { get; }

        /// <summary>
        /// Gets or allocates a task that represents the asynchronous result.
        /// </summary>
        /// <returns>A task that represents the asynchronous lazy result that completes with the resulting value.</returns>
        Task<T> AsTask();
    }
}
