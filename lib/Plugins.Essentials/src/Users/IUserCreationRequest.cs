/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IUserCreationRequest.cs 
*
* IUserCreationRequest.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Users
{
    /// <summary>
    /// A request to create a new user
    /// </summary>
    public interface IUserCreationRequest
    {
        /// <summary>
        /// The value to store in the users password field. By default this 
        /// value will be hashed before being stored in the database, unless 
        /// <see cref="UseRawPassword"/> is set to true.
        /// </summary>
        PrivateString? Password { get; }

        /// <summary>
        /// The user's initial privilege level
        /// </summary>
        ulong Privileges { get; }

        /// <summary>
        /// The user's unique username (may also be an email address
        /// </summary>
        string Username { get; }

        /// <summary>
        /// Should the password be stored as-is in the database?
        /// </summary>
        bool UseRawPassword { get; }

        /// <summary>
        /// The user's initial status
        /// </summary>
        UserStatus InitialStatus { get; }
    }
}