/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: AuthorzationCheckLevel.cs 
*
* AuthorzationCheckLevel.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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


#nullable enable

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Specifies how critical the security check is for a user to access 
    /// a given resource
    /// </summary>
    public enum AuthorzationCheckLevel
    {
        /// <summary>
        /// No authorization check is required.
        /// </summary>
        None,
        /// <summary>
        /// Is there any information that the client may have authorization. NOTE: Not a security check!
        /// </summary>
        Any,
        /// <summary>
        /// The authorization check is not considered criticial, just a basic confirmation
        /// that the user should be logged it, but does not need to access secure
        /// resources.
        /// </summary>
        Medium,
        /// <summary>
        /// The a full authorization check is required as the user may access 
        /// secure resouces.
        /// </summary>
        Critical
    }
}