/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: SessionHandle.cs 
*
* SessionHandle.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Threading.Tasks;

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// The callback method signature used to release attached sessions
    /// </summary>
    /// <param name="session">The session being released</param>
    /// <param name="event">The connection the session is attached to</param>
    /// <returns>A value task that resolves when the session has been released from the connection</returns>
    public delegate ValueTask SessionReleaseCallback(ISession session, IHttpEvent @event);
    
    /// <summary>
    /// A handle that holds exclusive access to a <see cref="ISession"/>
    /// session object
    /// </summary>
    public readonly record struct SessionHandle
    {
        /// <summary>
        /// An empty <see cref="SessionHandle"/> instance. (A handle without a session object)
        /// </summary>
        public static readonly SessionHandle Empty = new(null, FileProcessArgs.Continue, null);

        private readonly SessionReleaseCallback? ReleaseCb;

        /// <summary>
        /// True when a valid session is held by the current handle
        /// </summary>
        internal readonly bool IsSet => SessionData != null;

        /// <summary>
        /// The session data object associated with the current session
        /// </summary>
        public readonly ISession? SessionData { get; }

        /// <summary>
        /// A value indicating if the connection is valid and should continue to be processed
        /// </summary>
        public readonly FileProcessArgs EntityStatus { get; }

        /// <summary>
        /// Initializes a new <see cref="SessionHandle"/>
        /// </summary>
        /// <param name="sessionData">The session data instance</param>
        /// <param name="callback">A callback that is invoked when the handle is released</param>
        /// <param name="entityStatus"></param>
        public SessionHandle(ISession? sessionData, FileProcessArgs entityStatus, SessionReleaseCallback? callback)
        {
            SessionData = sessionData;
            ReleaseCb = callback;
            EntityStatus = entityStatus;
        }
        /// <summary>
        /// Initializes a new <see cref="SessionHandle"/>
        /// </summary>
        /// <param name="sessionData">The session data instance</param>
        /// <param name="callback">A callback that is invoked when the handle is released</param>
        public SessionHandle(ISession sessionData, SessionReleaseCallback callback):this(sessionData, FileProcessArgs.Continue, callback)
        {}

        /// <summary>
        /// Releases the session from use
        /// </summary>
        /// <param name="event">The current connection event object</param>
        public readonly ValueTask ReleaseAsync(IHttpEvent @event) => ReleaseCb?.Invoke(SessionData!, @event) ?? ValueTask.CompletedTask;
    }
}
