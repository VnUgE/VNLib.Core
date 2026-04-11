/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: SessionInfo.cs 
*
* SessionInfo.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Text.Json;
using System.Runtime.CompilerServices;

using VNLib.Utils;

using static VNLib.Plugins.Essentials.Statics;


/*
 * SessionInfo is a structure since it is only meant used in 
 * an HttpEntity context, so it may be allocated as part of 
 * the HttpEntity object, so have a single larger object
 * passed by ref, and created once per request. It may even
 * be cached and reused in the future. But for now user-apis
 * should not be cached until a safe use policy is created.
 */

namespace VNLib.Plugins.Essentials.Sessions
{   
    /// <summary>
    /// When attached to a connection, provides persistent session storage and information based
    /// on a connection.
    /// </summary>
    /// <remarks>
    /// This structure should not be stored and should not be accessed when the parent http entity 
    /// has been closed.
    /// </remarks>
    public readonly struct SessionInfo : IObjectStorage, IEquatable<SessionInfo>
    { 

        private readonly ISession UserSession;

        /// <summary>
        /// A value indicating if the current instance has been initialized 
        /// with a session. Otherwise properties are undefined
        /// </summary>
        public readonly bool IsSet;

        internal SessionInfo(ISession session)
        {
            UserSession = session;
            IsSet = true;
        }     

        /// <summary>
        /// Key that identifies the current session. (Identical to cookie::sessionid)
        /// </summary>
        public readonly string SessionID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UserSession.SessionID;
        }       

        /// <summary>
        /// Was this session just created on this connection?
        /// </summary>
        public readonly bool IsNew
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UserSession.IsNew;
        }

        /// <summary>
        /// The time the session was created
        /// </summary>
        public readonly DateTimeOffset Created
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UserSession.Created;
        }       

        /// <summary>
        /// <para>
        /// Gets or sets the user-id for the current session.
        /// </para>
        /// <para>
        /// Login routines usually set this value and it should be read-only
        /// </para>
        /// </summary>
        public readonly string UserID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UserSession.UserID;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => UserSession.UserID = value;
        }

        /// <summary>
        /// Privileges associated with user specified during login
        /// </summary>
        public readonly ulong Privileges
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UserSession.Privileges;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => UserSession.Privileges = value;
        }  

        /// <summary>
        /// A value specifying the type of the backing session
        /// </summary>
        public readonly string SessionType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UserSession.SessionType;
        }

        /// <summary>
        /// Flags the session as invalid. IMPORTANT: the user's session data is no longer valid, no data 
        /// will be saved to the session store when the session closes
        /// </summary>
        public readonly void Invalidate(bool all = false)
            => UserSession.Invalidate(all);

        /// <summary>
        /// Marks the session ID to be regenerated during closing event
        /// </summary>
        public readonly void RegenID()
            => UserSession.RegenID();

        /// <summary>
        /// Marks the session to be detached from the current connection.
        /// </summary>
        public readonly void Detach()
            => UserSession.Detach();

#nullable disable

        ///<inheritdoc/>
        public T GetObject<T>(string key) 
            => JsonSerializer.Deserialize<T>(this[key], SR_OPTIONS);
        
        ///<inheritdoc/>
        public void SetObject<T>(string key, T obj) 
            => this[key] = obj == null ? null: JsonSerializer.Serialize(obj, SR_OPTIONS);

#nullable enable

        /// <summary>
        /// Accesses the session's general storage
        /// </summary>
        /// <param name="index">Key for specified data</param>
        /// <returns>Value associated with the key from the session's general storage</returns>
        public readonly string this[string index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UserSession[index];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => UserSession[index] = value;
        }       

        ///<inheritdoc/>
        public readonly bool Equals(SessionInfo other) 
            => SessionID.Equals(other.SessionID, StringComparison.Ordinal);

        ///<inheritdoc/>
        public readonly override bool Equals(object? obj) 
            => obj is SessionInfo si && Equals(si);

        ///<inheritdoc/>
        public readonly override int GetHashCode() 
            => SessionID.GetHashCode(StringComparison.Ordinal);

        ///<inheritdoc/>
        public static bool operator ==(SessionInfo left, SessionInfo right) 
            => left.Equals(right);

        ///<inheritdoc/>
        public static bool operator !=(SessionInfo left, SessionInfo right) 
            => !(left == right);
    }
}
