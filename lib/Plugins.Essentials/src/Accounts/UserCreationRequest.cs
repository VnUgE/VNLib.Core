/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: UserCreationRequest.cs 
*
* UserCreationRequest.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Utils.Memory;
using VNLib.Plugins.Essentials.Users;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// A concrete implementation of <see cref="IUserCreationRequest"/> 
    /// that can be used to create a new user.
    /// </summary>
    public class UserCreationRequest : IUserCreationRequest
    {
        ///<inheritdoc/>
        public PrivateString? Password { get; init; }

        ///<inheritdoc/>
        public ulong Privileges { get; init; } = AccountUtil.MINIMUM_LEVEL;

        ///<inheritdoc/>
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// Obsolete: Use the Username property instead
        /// </summary>
        [Obsolete("Use the Username property instead")]
        public string EmailAddress
        {
            get => Username;
            init => Username = value;
        }

        ///<inheritdoc/>
        public bool UseRawPassword { get; init; }

        ///<inheritdoc/>
        public UserStatus InitialStatus { get; init; } = UserStatus.Unverified;
    }
}