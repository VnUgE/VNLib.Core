/*
* Copyright (c) 2022 Vaughn Nugent
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

using System;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

using VNLib.Net.Http;

#nullable enable

namespace VNLib.Plugins.Essentials.Sessions
{
    public delegate ValueTask SessionReleaseCallback(ISession session, IHttpEvent @event);
    
    /// <summary>
    /// A handle that holds exclusive access to a <see cref="ISession"/>
    /// session object
    /// </summary>
    public readonly struct SessionHandle : IEquatable<SessionHandle>
    {
        /// <summary>
        /// An empty <see cref="SessionHandle"/> instance. (A handle without a session object)
        /// </summary>
        public static readonly SessionHandle Empty = new(null, FileProcessArgs.Continue, null);

        private readonly SessionReleaseCallback? ReleaseCb;

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
        public ValueTask ReleaseAsync(IHttpEvent @event) => ReleaseCb?.Invoke(SessionData!, @event) ?? ValueTask.CompletedTask;

        /// <summary>
        /// Determines if another <see cref="SessionHandle"/> is equal to the current handle.
        /// Handles are equal if neither handle is set or if their SessionData object is equal.
        /// </summary>
        /// <param name="other">The other handle to</param>
        /// <returns>true if neither handle is set or if their SessionData object is equal, false otherwise</returns>
        public bool Equals(SessionHandle other)
        {
            //If neither handle is set, then they are equal, otherwise they are equal if the session objects themselves are equal
            return (!IsSet && !other.IsSet) || (SessionData?.Equals(other.SessionData) ?? false);
        }
        ///<inheritdoc/>
        public override bool Equals([NotNullWhen(true)] object? obj) => (obj is SessionHandle other) && Equals(other);
        ///<inheritdoc/>
        public override int GetHashCode()
        {
            return IsSet ? SessionData!.GetHashCode() : base.GetHashCode();
        }

        /// <summary>
        /// Checks if two <see cref="SessionHandle"/> instances are equal
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(SessionHandle left, SessionHandle right) => left.Equals(right);

        /// <summary>
        /// Checks if two <see cref="SessionHandle"/> instances are not equal
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(SessionHandle left, SessionHandle right) => !(left == right);
    }
}
