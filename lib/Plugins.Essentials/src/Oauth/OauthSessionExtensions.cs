/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: OauthSessionExtensions.cs 
*
* OauthSessionExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Essentials.Oauth
{
    /// <summary>
    /// Represents an active oauth session
    /// </summary>
    public static class OauthSessionExtensions 
    {
        public const string APP_ID_ENTRY = "oau.apid";
        public const string REFRESH_TOKEN_ENTRY = "oau.rt";
        public const string SCOPES_ENTRY = "oau.scp";
        public const string TOKEN_TYPE_ENTRY = "oau.typ";


        /// <summary>
        /// The ID of the application that granted the this token access
        /// </summary>
        public static string AppID(this in SessionInfo session) => session[APP_ID_ENTRY];

        /// <summary>
        /// The refresh token for this current token
        /// </summary>
        public static string RefreshToken(this in SessionInfo session) => session[REFRESH_TOKEN_ENTRY];

        /// <summary>
        /// The token's privilage scope
        /// </summary>
        public static string Scopes(this in SessionInfo session) => session[SCOPES_ENTRY];
        /// <summary>
        /// The Oauth2 token type
        /// </summary>,
        public static string Type(this in SessionInfo session) => session[TOKEN_TYPE_ENTRY];

        /// <summary>
        /// Determines if the current session has the required scope type and the 
        /// specified permission
        /// </summary>
        /// <param name="session"></param>
        /// <param name="type">The scope type</param>
        /// <param name="permission">The scope permission</param>
        /// <returns>True if the current session has the required scope, false otherwise</returns>
        public static bool HasScope(this in SessionInfo session, string type, string permission)
        {
            //Join the permissions components
            string perms = string.Concat(type, ":", permission);
            return session.HasScope(perms);
        }
        /// <summary>
        /// Determines if the current session has the required scope type and the 
        /// specified permission
        /// </summary>
        /// <param name="session"></param>
        /// <param name="scope">The scope to compare</param>
        /// <returns>True if the current session has the required scope, false otherwise</returns>
        public static bool HasScope(this in SessionInfo session, ReadOnlySpan<char> scope)
        {
            //Split the scope components and check them against the joined permission
            return session.Scopes().AsSpan().Contains(scope, StringComparison.OrdinalIgnoreCase);
        }
    }
}