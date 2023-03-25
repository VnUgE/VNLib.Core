/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ProtectionSettings.cs 
*
* ProtectionSettings.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

namespace VNLib.Plugins.Essentials.Endpoints
{
    /// <summary>
    /// A data structure containing a basic security protocol
    /// for connection pre-checks. Settings are the most 
    /// strict by default
    /// </summary>
    public readonly record struct ProtectionSettings
    {
        /// <summary>
        /// Requires TLS be enabled for all incomming requets (or loopback adapter)
        /// </summary>
        public readonly bool DisabledTlsRequired { get; init; }

        /// <summary>
        /// Checks that sessions are enabled for incomming requests 
        /// and that they are not new sessions.
        /// </summary>
        public readonly bool DisableSessionsRequired { get; init; }

        /// <summary>
        /// Enables referr match protection. Requires that if a referer header is
        /// set that it matches the current origin
        /// </summary>
        public readonly bool DisableRefererMatch { get; init; }

        /// <summary>
        /// Requires all connections to have pass an IsBrowser() check
        /// (requires a valid user-agent header that contains Mozilla in
        /// the string)
        /// </summary>
        public readonly bool DisableBrowsersOnly { get; init; } 

        /// <summary>
        /// Disables response caching, by setting the cache control headers appropriatly.
        /// Default is disabled
        /// </summary>
        public readonly bool EnableCaching { get; init; }      
    }
}
