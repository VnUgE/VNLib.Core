/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IAccountSecurityProvider.cs 
*
* IAccountSecurityProvider.cs is part of VNLib.Plugins.Essentials which 
* is part of the larger VNLib collection of libraries and utilities.
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

using VNLib.Utils;
using VNLib.Plugins.Essentials.Users;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Provides account security to client connections. Providing authoirzation,
    /// verification, and client data encryption.
    /// </summary>
    public interface IAccountSecurityProvider
    {
        /// <summary>
        /// Generates a new authorization for the connection with its client security information
        /// </summary>
        /// <param name="entity">The connection to authorize</param>
        /// <param name="clientInfo">The client security information required for authorization</param>
        /// <param name="user">The user object to authorize the connection for</param>
        /// <returns>The new authorization information for the connection</returns>
        IClientAuthorization AuthorizeClient(HttpEntity entity, IClientSecInfo clientInfo, IUser user);

        /// <summary>
        /// Regenerates the client's authorization status for a currently logged-in user
        /// </summary>
        /// <param name="entity">The connection to re-authorize</param>
        /// <returns>The new <see cref="IClientAuthorization"/> containing the new authorization information</returns>
        IClientAuthorization ReAuthorizeClient(HttpEntity entity);

        /// <summary>
        /// Determines if the connection is considered authorized for the desired 
        /// security level
        /// </summary>
        /// <param name="entity">The connection to determine the status of</param>
        /// <param name="level">The authorziation level to check for</param>
        /// <returns>True if the given connection meets the desired authorzation status</returns>
        bool IsClientAuthorized(HttpEntity entity, AuthorzationCheckLevel level);

        /// <summary>
        /// Encryptes data using the stored client's authorization information. 
        /// </summary>
        /// <param name="entity">The connection to encrypt data for</param>
        /// <param name="data">The data to encrypt</param>
        /// <param name="outputBuffer">The buffer to write the encrypted data to</param>
        /// <returns>The number of bytes written to the output buffer, or o/false if the data could not be encrypted</returns>
        ERRNO TryEncryptClientData(HttpEntity entity, ReadOnlySpan<byte> data, Span<byte> outputBuffer);

        /// <summary>
        /// Attempts a one-time encryption of client data for a non-authorized user 
        /// based on the client's <see cref="IClientSecInfo"/> data.
        /// </summary>
        /// <param name="clientSecInfo">The client's <see cref="IClientSecInfo"/> credentials used to encrypt the message</param>
        /// <param name="data">The data to encrypt</param>
        /// <param name="outputBuffer">The output buffer to write encrypted data to</param>
        /// <returns>The number of bytes written to the output buffer, 0/false if the operation failed</returns>
        ERRNO TryEncryptClientData(IClientSecInfo clientSecInfo, ReadOnlySpan<byte> data, Span<byte> outputBuffer);

        /// <summary>
        /// Invalidates a logged in connection
        /// </summary>
        /// <param name="entity">The connection to invalidate the login status of</param>
        void InvalidateLogin(HttpEntity entity);
    }
}