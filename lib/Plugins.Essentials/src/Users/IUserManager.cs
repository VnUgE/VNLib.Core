/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IUserManager.cs 
*
* IUserManager.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Users
{
    /// <summary>
    /// A backing store that provides user accounts
    /// </summary>
    public interface IUserManager
    {
        /// <summary>
        /// Attempts to get a user object without their password from the database asynchronously
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The user's <see cref="IUser"/> object, null if the user was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        Task<IUser?> GetUserFromIDAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to get a user object without their password from the database asynchronously
        /// </summary>
        /// <param name="emailAddress">The user's email address</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The user's <see cref="IUser"/> object, null if the user was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        Task<IUser?> GetUserFromEmailAsync(string emailAddress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to get a user object with their password from the database on the current thread
        /// </summary>
        /// <param name="userid">The id of the user</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The user's <see cref="IUser"/> object, null if the user was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        Task<IUser?> GetUserAndPassFromIDAsync(string userid, CancellationToken cancellation = default);

        /// <summary>
        /// Attempts to get a user object with their password from the database asynchronously
        /// </summary>
        /// <param name="emailAddress">The user's email address</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>The user's <see cref="IUser"/> object, null if the user was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        Task<IUser?> GetUserAndPassFromEmailAsync(string emailAddress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new user in the current user's table and if successful returns the new user object (without password)
        /// </summary>
        /// <param name="userid">The user id</param>
        /// <param name="privileges">A number representing the privilage level of the account</param>
        /// <param name="passHash">Value to store in the password field</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <param name="emailAddress">The account email address</param>
        /// <returns>An object representing a user's account if successful, null otherwise</returns>
        /// <exception cref="UserExistsException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="UserCreationFailedException"></exception>
        Task<IUser> CreateUserAsync(string userid, string emailAddress, ulong privileges, PrivateString passHash, CancellationToken cancellation = default);

        /// <summary>
        /// Updates a password associated with the specified user. If the update fails, the transaction
        /// is rolled back.
        /// </summary>
        /// <param name="user">The user account to update the password of</param>
        /// <param name="newPass">The new password to set</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The result of the operation, the result should be 1 (aka true)</returns>
        Task<ERRNO> UpdatePassAsync(IUser user, PrivateString newPass, CancellationToken cancellation = default);

        /// <summary>
        /// Gets the number of entries in the current user table
        /// </summary>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The number of users in the table, or -1 if the operation failed</returns>
        Task<long> GetUserCountAsync(CancellationToken cancellation = default);
    }
}