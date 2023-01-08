/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ISession.cs 
*
* ISession.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Net;

using VNLib.Utils;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// Flags to specify <see cref="ISession"/> session types
    /// </summary>
    public enum SessionType
    {
        /// <summary>
        /// The session is a "basic" or web based session
        /// </summary>
        Web,
        /// <summary>
        /// The session is an OAuth2 session type
        /// </summary>
        OAuth2
    }

    /// <summary>
    /// Represents a connection oriented session data
    /// </summary>
    public interface ISession : IIndexable<string, string>
    {
        /// <summary>
        /// A value specifying the type of the loaded session
        /// </summary>
        SessionType SessionType { get; }
        /// <summary>
        /// UTC time in when the session was created
        /// </summary>
        DateTimeOffset Created { get; }
        /// <summary>
        /// Privilages associated with user specified during login
        /// </summary>
        ulong Privilages { get; set; }
        /// <summary>
        /// Key that identifies the current session. (Identical to cookie::sessionid)
        /// </summary>
        string SessionID { get; }
        /// <summary>
        /// User ID associated with session
        /// </summary>
        string UserID { get; set; }
        /// <summary>
        /// Marks the session as invalid
        /// </summary>
        void Invalidate(bool all = false);
        /// <summary>
        /// Gets or sets the session's authorization token
        /// </summary>
        string Token { get; set; }
        /// <summary>
        /// The IP address belonging to the client
        /// </summary>
        IPAddress UserIP { get; }
        /// <summary>
        /// Sets the session ID to be regenerated if applicable
        /// </summary>
        void RegenID();

        /// <summary>
        /// A value that indicates this session was newly created
        /// </summary>
        bool IsNew { get; }
    }
}