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
using VNLib.Plugins.Essentials.Accounts;

namespace VNLib.Plugins.Essentials.Users
{

    /// <summary>
    /// A backing store that provides user accounts
    /// </summary>
    public interface IUserManager
    {
        /// <summary>
        /// Gets the internal password hash provider if one is available
        /// </summary>
        /// <returns>The internal hash provider if available, null otherwise</returns>
        IPasswordHashingProvider? GetHashProvider();

        /// <summary>
        /// Computes uinuqe user-id that is safe for use in the database. 
        /// </summary>
        /// <param name="input">The value to convert to a safe user-id</param>
        /// <returns>The safe-user id</returns>
        string ComputeSafeUserId(string input);

        /// <summary>
        /// Gets the number of entries in the current user table
        /// </summary>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The number of users in the table, or -1 if the operation failed</returns>
        Task<long> GetUserCountAsync(CancellationToken cancellation = default);

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
        /// Creates a new user account in the store as per the request. The user-id field is optional, 
        /// and if set to null or empty, will be generated automatically by the store.
        /// </summary>
        /// <param name="userId">An optional user id to force</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <param name="creation">The account email address</param>
        /// <returns>An object representing a user's account if successful, null otherwise</returns>
        /// <exception cref="UserExistsException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="UserCreationFailedException"></exception>
        Task<IUser> CreateUserAsync(IUserCreationRequest creation, string? userId, CancellationToken cancellation = default);

        /// <summary>
        /// Validates a password associated with the specified user
        /// </summary>
        /// <param name="user">The user to validate the password against</param>
        /// <param name="password">The password to test against the user</param>
        /// <param name="flags">Validation flags</param>
        /// <param name="cancellation">A token to cancel the validation</param>
        /// <returns>A value greater than 0 if successful, 0 or negative values if a failure occured</returns>
        Task<ERRNO> ValidatePasswordAsync(IUser user, PrivateString password, PassValidateFlags flags, CancellationToken cancellation = default);

        /// <summary>
        /// An operation that will attempt to recover a user's password if possible. Not all user
        /// managment systems allow recovering passwords for users. This method should return
        /// null if the operation is not supported.
        /// <para>
        /// The returned value will likely not be the user's raw password but instead a hashed
        /// or encrypted version of the password. 
        /// </para>
        /// </summary>
        /// <param name="user">The user to recover the password for</param>
        /// <param name="cancellation">A token to cancel the opertion</param>
        /// <returns>The password if found</returns>
        /// <exception cref="NotSupportedException"></exception>
        Task<PrivateString?> RecoverPasswordAsync(IUser user, CancellationToken cancellation = default);

        /// <summary>
        /// Updates a password associated with the specified user. If the update fails, the transaction
        /// is rolled back.
        /// </summary>
        /// <param name="user">The user account to update the password of</param>
        /// <param name="newPass">The new password to set</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The result of the operation, the result should be 1 (aka true)</returns>
        Task<ERRNO> UpdatePasswordAsync(IUser user, PrivateString newPass, CancellationToken cancellation = default);
    }
}