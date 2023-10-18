/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IUser.cs 
*
* IUser.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Utils.Async;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Users
{
    /// <summary>
    /// Represents an abstract user account
    /// </summary>
    public interface IUser : IAsyncExclusiveResource, IDisposable, IObjectStorage, IEnumerable<KeyValuePair<string, string>>, IIndexable<string, string>
    {
        /// <summary>
        /// The user's privilege level 
        /// </summary>
        ulong Privileges { get; set; }

        /// <summary>
        /// The user's ID
        /// </summary>
        string UserID { get; }

        /// <summary>
        /// Date the user's account was created
        /// </summary>
        DateTimeOffset Created { get; }

        /// <summary>
        /// The user's password hash if retreived from the backing store, otherwise null
        /// </summary>
        PrivateString? PassHash { get; }

        /// <summary>
        /// Status of account
        /// </summary>
        UserStatus Status { get; set; }

        /// <summary>
        /// Is the account only usable from local network?
        /// </summary>
        bool LocalOnly { get; set; }

        /// <summary>
        /// The user's email address
        /// </summary>
        string EmailAddress { get; set; }

        /// <summary>
        /// Marks the user for deletion on release
        /// </summary>
        void Delete();
    }
}