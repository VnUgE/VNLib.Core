/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IAsyncEventSource.cs 
*
* IAsyncEventSource.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Async
{
    /// <summary>
    /// A type that publishes events to asynchronous event queues
    /// </summary>
    /// <typeparam name="T">The event item type to publish</typeparam>
    public interface IAsyncEventSource<T>
    {
        /// <summary>
        /// Subscribes a new queue to publish events to
        /// </summary>
        /// <param name="queue">The queue instance to publish new events to</param>
        void Subscribe(IAsyncQueue<T> queue);

        /// <summary>
        /// Unsubscribes a previously subscribed queue from receiving events
        /// </summary>
        /// <param name="queue">The queue instance to unregister from events</param>
        void Unsubscribe(IAsyncQueue<T> queue);
    }
}
