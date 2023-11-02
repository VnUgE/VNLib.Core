/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IAsyncEventSink.cs 
*
* IAsyncEventSink.cs is part of VNLib.Utils which is part of the larger 
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
    /// A type that receives events from asynchronous event sources and publishes 
    /// them to subscribers.
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    public interface IAsyncEventSink<T>
    {
        /// <summary>
        /// Publishes a single event to all subscribers
        /// </summary>
        /// <param name="evnt">The event to publish</param>
        /// <returns>A value that indicates if the event was successfully published to subscribers</returns>
        bool PublishEvent(T evnt);

        /// <summary>
        /// Publishes an array of events to all subscribers
        /// </summary>
        /// <param name="events">The array of events to publish</param>
        /// <returns>A value that indicates if the events were successfully published to subscribers</returns>
        bool PublishEvents(T[] events);
    }
}
