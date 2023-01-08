/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IOAuth2Provider.cs 
*
* IOAuth2Provider.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Essentials.Oauth
{
    /// <summary>
    /// An interface that Oauth2 serice providers must implement 
    /// to provide sessions to an <see cref="EventProcessor"/>
    /// processor endpoint processor
    /// </summary>
    public interface IOAuth2Provider : ISessionProvider
    {
        /// <summary>
        /// Gets a value indicating how long a session may be valid for
        /// </summary>
        public TimeSpan MaxTokenLifetime { get; }
    }
}