/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IAsyncQueue.cs 
*
* IAsyncQueue.cs is part of VNLib.Utils which is part of the larger 
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
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Provides a generic asynchronous queue
    /// </summary>
    /// <typeparam name="T">The item message type</typeparam>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public interface IAsyncQueue<T>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        /// <summary>
        /// Attemts to enqueue an item if the queue has the capacity
        /// </summary>
        /// <param name="item">The item to eqneue</param>
        /// <returns>True if the queue can accept another item, false otherwise</returns>
        bool TryEnqueue(T item);

        /// <summary>
        /// Enqueues an item to the end of the queue and notifies a waiter that an item was enqueued
        /// </summary>
        /// <param name="item">The item to enqueue</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Asynchronously waits for an item to be Enqueued to the end of the queue.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The item at the begining of the queue</returns>
        ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default);
       
        /// <summary>
        /// Removes the object at the beginning of the queue and stores it to the result parameter. Without waiting for a change 
        /// event. 
        /// </summary>
        /// <param name="result">The item that was at the begining of the queue</param>
        /// <returns>True if the queue could be read synchronously, false if the lock could not be entered, or the queue contains no items</returns>
        bool TryDequeue([MaybeNullWhen(false)] out T result);
       
        /// <summary>
        /// Peeks the object at the beginning of the queue and stores it to the result parameter. Without waiting for a change 
        /// event. 
        /// </summary>
        /// <param name="result">The item that was at the begining of the queue</param>
        /// <returns>True if the queue could be read synchronously, false if the lock could not be entered, or the queue contains no items</returns>
        bool TryPeek([MaybeNullWhen(false)] out T result);
    }
}
