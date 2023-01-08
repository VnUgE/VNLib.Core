/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: OauthSessionCacheExhaustedException.cs 
*
* OauthSessionCacheExhaustedException.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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


namespace VNLib.Plugins.Essentials.Oauth
{
    /// <summary>
    /// Raised when the session cache space has been exhausted and cannot 
    /// load the new session into cache.
    /// </summary>
    public class OauthSessionCacheExhaustedException : Exception
    {
        public OauthSessionCacheExhaustedException(string message) : base(message)
        {}
        public OauthSessionCacheExhaustedException(string message, Exception innerException) : base(message, innerException)
        {}
        public OauthSessionCacheExhaustedException()
        {}
    }
}