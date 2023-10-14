/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: AccountUtil.cs 
*
* AccountUtil.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

using VNLib.Hashing;
using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Users;
using VNLib.Plugins.Essentials.Sessions;

#nullable enable

namespace VNLib.Plugins.Essentials.Accounts
{

    /// <summary>
    /// Provides essential constants, static methods, and session/user extensions 
    /// to facilitate unified user-controls, athentication, and security
    /// application-wide
    /// </summary>
    public static partial class AccountUtil
    {

        /// <summary>
        /// The size in bytes of the random passwords generated when invoking the <see cref="SetRandomPasswordAsync(PasswordHashing, IUserManager, IUser, int)"/>
        /// </summary>
        public const int RANDOM_PASS_SIZE = 240;
     
        /// <summary>
        /// The origin string of a local user account. This value will be set if an
        /// account is created through the VNLib.Plugins.Essentials.Accounts library
        /// </summary>
        public const string LOCAL_ACCOUNT_ORIGIN = "local";
       
       
        //Session entry keys
        private const string BROWSER_ID_ENTRY = "acnt.bid";
        private const string FAILED_LOGIN_ENTRY = "acnt.flc";
        private const string LOCAL_ACCOUNT_ENTRY = "acnt.ila";
        private const string ACC_ORIGIN_ENTRY = "__.org";

        //Privlage masks 
        public const ulong READ_MSK =       0x0000000000000001L;
        public const ulong DOWNLOAD_MSK =   0x0000000000000002L;
        public const ulong WRITE_MSK =      0x0000000000000004L;
        public const ulong DELETE_MSK =     0x0000000000000008L;
        public const ulong ALLFILE_MSK =    0x000000000000000FL;
        public const ulong OPTIONS_MSK =    0x000000000000FF00L;
        public const ulong GROUP_MSK =      0x00000000FFFF0000L;
        public const ulong LEVEL_MSK =      0x000000FF00000000L;

        public const byte OPTIONS_MSK_OFFSET = 0x08;
        public const byte GROUP_MSK_OFFSET = 0x10;
        public const byte LEVEL_MSK_OFFSET = 0x18;

        public const ulong MINIMUM_LEVEL =  0x0000000100000001L;

        
        /// <summary>
        /// Speical character regual expresion for basic checks
        /// </summary>
        public static readonly Regex SpecialCharacters = new(@"[\r\n\t\a\b\e\f#?!@$%^&*\+\-\~`|<>\{}]", RegexOptions.Compiled);

        #region Password/User helper extensions

        /// <summary>
        /// Generates and sets a random password for the specified user account
        /// </summary>
        /// <param name="manager">The configured <see cref="IUserManager"/> to process the password update on</param>
        /// <param name="user">The user instance to update the password on</param>
        /// <param name="passHashing">The <see cref="PasswordHashing"/> instance to hash the random password with</param>
        /// <param name="size">Size (in bytes) of the generated random password</param>
        /// <returns>A value indicating the results of the event (number of rows affected, should evaluate to true)</returns>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<ERRNO> SetRandomPasswordAsync(this IPasswordHashingProvider passHashing, IUserManager manager, IUser user, int size = RANDOM_PASS_SIZE)
        {
            _ = manager ?? throw new ArgumentNullException(nameof(manager));
            _ = user ?? throw new ArgumentNullException(nameof(user));
            _ = passHashing ?? throw new ArgumentNullException(nameof(passHashing));
            if (user.IsReleased)
            {
                throw new ObjectDisposedException("The specifed user object has been released");
            }
            //Alloc a buffer
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(size);
            //Use the CGN to get a random set
            RandomHash.GetRandomBytes(buffer.Span);
            //Hash the new random password
            using PrivateString passHash = passHashing.Hash(buffer.Span);
            //Write the password to the user account
            return await manager.UpdatePassAsync(user, passHash);
        }
       
        /// <summary>
        /// Creates a new user with a random user id and the specified email address and password.
        /// If privileges are left null, the minimum privileges will be set.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="emailAddress">The user's email address or secondary id</param>
        /// <param name="password">The user's password</param>
        /// <param name="privileges">Optional user privilage level</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that resolves the new user</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="UserExistsException"></exception>
        /// <exception cref="UserCreationFailedException"></exception>
        public static Task<IUser> CreateUserAsync(this IUserManager manager, string emailAddress, PrivateString password, ulong? privileges, CancellationToken cancellation = default)
        {
            _ = manager ?? throw new ArgumentNullException(nameof(manager));
            //Create a random user id
            string randomId = GetRandomUserId();
            //Set the default/minimum privileges
            privileges ??= MINIMUM_LEVEL;
            return manager.CreateUserAsync(randomId, emailAddress, privileges.Value, password, cancellation);
        }

        /// <summary>
        /// Checks to see if the current user account was created
        /// using a local account.
        /// </summary>
        /// <param name="user"></param>
        /// <returns>True if the account is a local account, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLocalAccount(this IUser user) => LOCAL_ACCOUNT_ORIGIN.Equals(user.GetAccountOrigin(), StringComparison.Ordinal);

        /// <summary>
        /// If this account was created by any means other than a local account creation. 
        /// Implementors can use this method to determine the origin of the account.
        /// This field is not required
        /// </summary>
        /// <returns>The origin of the account</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetAccountOrigin(this IUser ud) => ud[ACC_ORIGIN_ENTRY];
        /// <summary>
        /// If this account was created by any means other than a local account creation. 
        /// Implementors can use this method to specify the origin of the account. This field is not required
        /// </summary>
        /// <param name="ud"></param> 
        /// <param name="origin">Value of the account origin</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAccountOrigin(this IUser ud, string origin) => ud[ACC_ORIGIN_ENTRY] = origin;

        /// <summary>
        /// Gets a random user-id generated from crypograhic random number
        /// then hashed (SHA1) and returns a hexadecimal string
        /// </summary>
        /// <returns>The random string user-id</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetRandomUserId() => RandomHash.GetRandomHash(HashAlg.SHA1, 64, HashEncodingMode.Hexadecimal);

        /// <summary>
        /// Generates a cryptographically secure random password, then hashes it 
        /// and returns the hash of the new password
        /// </summary>
        /// <param name="hashing"></param>
        /// <param name="size">The size (in bytes) of the new random password</param>
        /// <returns>A <see cref="PrivateString"/> that contains the new password hash</returns>
        public static PrivateString GetRandomPassword(this IPasswordHashingProvider hashing, int size = RANDOM_PASS_SIZE)
        {
            //Get random bytes
            byte[] randBuffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                Span<byte> span = randBuffer.AsSpan(0, size);

                //Generate random password
                RandomHash.GetRandomBytes(span);

                //hash the password
                return hashing.Hash(span);
            }
            finally
            {
                //Zero the block and return to pool
                MemoryUtil.InitializeBlock(randBuffer.AsSpan());
                ArrayPool<byte>.Shared.Return(randBuffer);
            }
        }

        /// <summary>
        /// Asynchronously verifies the desired user's password. If the user is not found or the password is not found
        /// returns false. Returns true if the user exist's has a valid password hash and matches the supplied password value.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="userId">The id of the user to check the password against</param>
        /// <param name="rawPassword">The raw password of the user to compare hashes against</param>
        /// <param name="hashing">The password hashing tools</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that completes with the value of the password hashing match.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<bool> VerifyPasswordAsync(this IUserManager manager, string userId, PrivateString rawPassword, IPasswordHashingProvider hashing, CancellationToken cancellation)
        {
            _ = userId ?? throw new ArgumentNullException(nameof(userId));
            _ = rawPassword ?? throw new ArgumentNullException(nameof(rawPassword));
            _ = hashing ?? throw new ArgumentNullException(nameof(hashing));

            //Get the user, may be null if the user does not exist
            using IUser? user = await manager.GetUserAndPassFromIDAsync(userId, cancellation);

            return user != null && hashing.Verify(user.PassHash.ToReadOnlySpan(), rawPassword.ToReadOnlySpan());
        }

        /// <summary>
        /// Verifies the user's raw password against the hashed password using the specified 
        /// <see cref="PasswordHashing"/> instance
        /// </summary>
        /// <param name="user"></param>
        /// <param name="rawPassword"></param>
        /// <param name="hashing">The <see cref="IPasswordHashingProvider"/> provider instance</param>
        /// <returns>True if the password </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VerifyPassword(this IUser user, PrivateString rawPassword, IPasswordHashingProvider hashing)
        {
            return user.PassHash != null && hashing.Verify(user.PassHash, rawPassword);
        }

        /// <summary>
        /// Verifies a password against its previously encoded hash.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="passHash">Previously hashed password</param>
        /// <param name="password">Raw password to compare against</param>
        /// <returns>True if bytes derrived from password match the hash, false otherwise</returns>
        /// <exception cref="NotSupportedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Verify(this IPasswordHashingProvider provider, PrivateString passHash, PrivateString password)
        {
            //Casting PrivateStrings to spans will reference the base string directly
            return provider.Verify((ReadOnlySpan<char>)passHash, (ReadOnlySpan<char>)password);
        }

        /// <summary>
        /// Hashes a specified password, with the initialized pepper, and salted with CNG random bytes.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="password">Password to be hashed</param>
        /// <exception cref="NotSupportedException"></exception>
        /// <returns>A <see cref="PrivateString"/> of the hashed and encoded password</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrivateString Hash(this IPasswordHashingProvider provider, PrivateString password)
        {
            return provider.Hash((ReadOnlySpan<char>)password);
        }

        #endregion



        #region Client Auth Extensions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IAccountSecurityProvider GetSecProviderOrThrow(this HttpEntity entity)
        {
            return entity.RequestedRoot.AccountSecurity 
                ?? throw new NotSupportedException("The processor this connection originated from does not have an account security provider loaded");
        }

        /// <summary>
        /// Determines if the current client has the authroziation level to access a given resource
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="mode">The authoziation level</param>
        /// <returns>True if the connection has the desired authorization status</returns>
        /// <exception cref="NotSupportedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsClientAuthorized(this HttpEntity entity, AuthorzationCheckLevel mode = AuthorzationCheckLevel.Critical)
        {
            IAccountSecurityProvider prov = entity.GetSecProviderOrThrow();

            return prov.IsClientAuthorized(entity, mode);
        }

        /// <summary>
        /// Runs necessary operations to grant authorization to the specified user of a given session and user with provided variables
        /// </summary>
        /// <param name="entity">The connection and session to log-in</param>
        /// <param name="secInfo">The clients login security information</param>
        /// <param name="user">The user to log-in</param>
        /// <returns>The encrypted base64 token secret data to send to the client</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="CryptographicException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static IClientAuthorization GenerateAuthorization(this HttpEntity entity, IClientSecInfo secInfo, IUser user)
        {
            _ = secInfo ?? throw new ArgumentNullException(nameof(secInfo));

            if (!entity.Session.IsSet || entity.Session.SessionType != SessionType.Web)
            {
                throw new InvalidOperationException("The session is not set or the session is not a web-based session type");
            }

            IAccountSecurityProvider provider = entity.GetSecProviderOrThrow();

            //Regen the session id
            entity.Session.RegenID();

            //Authorize client
            IClientAuthorization auth = provider.AuthorizeClient(entity, secInfo, user);

            //Clear flags
            user.ClearFailedLoginCount();

            //Store variables
            entity.Session.UserID = user.UserID;
            entity.Session.Privilages = user.Privileges;

            //Store client id for later use
            entity.Session[BROWSER_ID_ENTRY] = secInfo.ClientId;

            //Get the "local" account flag from the user object
            bool localAccount = user.IsLocalAccount();

            //Set local account flag on the session
            entity.Session.HasLocalAccount(localAccount);

            //Return the client encrypted data
            return auth;
        }

        /// <summary>
        /// Generates a client authorization from the supplied security info 
        /// using the default <see cref="IAccountSecurityProvider"/> and 
        /// stored the required variables in the <paramref name="response"/>
        /// response <see cref="WebMessage"/>
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="secInfo">The client's <see cref="IClientSecInfo"/> used to authorize the client</param>
        /// <param name="user">The user requesting the authenticated use</param>
        /// <param name="response">The response to store variables in</param>
        /// <exception cref="NotSupportedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GenerateAuthorization(this HttpEntity entity, IClientSecInfo secInfo, IUser user, WebMessage response)
        {
            //Authorize the client
            IClientAuthorization auth = GenerateAuthorization(entity, secInfo, user);

            //Set client token
            response.Token = auth.SecurityToken.ClientToken;
        }

        /// <summary>
        /// Regenerates the client authorization if the client has a currently valid authorization
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>The new <see cref="IClientAuthorization"/> for the regenerated credentials</returns>
        /// <exception cref="NotSupportedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IClientAuthorization ReAuthorizeClient(this HttpEntity entity)
        {
            //Get default provider
            IAccountSecurityProvider prov = entity.GetSecProviderOrThrow();

            //Re-authorize the client
            return prov.ReAuthorizeClient(entity);
        }

        /// <summary>
        /// Regenerates the client authorization if the client has a currently valid authorization
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="response">The response message to return to the client</param>
        /// <exception cref="NotSupportedException"></exception>
        /// <returns>The new <see cref="IClientAuthorization"/> for the regenerated credentials</returns>
        public static void ReAuthorizeClient(this HttpEntity entity, WebMessage response)
        {           
            IAccountSecurityProvider prov = entity.GetSecProviderOrThrow();

            //Re-authorize the client
            IClientAuthorization auth = prov.ReAuthorizeClient(entity);

            //Store the client token in response message
            response.Token = auth.SecurityToken.ClientToken;

            //Regen session id also
            entity.Session.RegenID();
        }

        /// <summary>
        /// Attempts to encrypt the supplied data with session stored client information. The user must 
        /// be authorized
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="data">The data to encrypt for the current client</param>
        /// <param name="output">The buffer to write encypted data to</param>
        /// <exception cref="NotSupportedException"></exception>
        /// <returns>The number of bytes encrypted and written to the output buffer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ERRNO TryEncryptClientData(this HttpEntity entity, ReadOnlySpan<byte> data, Span<byte> output)
        {
            //Confirm session is loaded
            if(!entity.Session.IsSet || entity.Session.IsNew)
            {
                return false;
            }

            //Use the default sec provider
            IAccountSecurityProvider prov = entity.GetSecProviderOrThrow();
            return prov.TryEncryptClientData(entity, data, output);
        }

        /// <summary>
        /// Attempts to encrypt client data against the supplied security information instance, without a secured session.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="secInfo">Used for unauthorized connections to encrypt client data based on client security info</param>
        /// <param name="data">The data to encrypt for the current client</param>
        /// <param name="output">The buffer to write encypted data to</param>
        /// <returns>The number of bytes encrypted and written to the output buffer</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static ERRNO TryEncryptClientData(this HttpEntity entity, IClientSecInfo secInfo, ReadOnlySpan<byte> data, Span<byte> output)
        {
            _ = secInfo ?? throw new ArgumentNullException(nameof(secInfo));

            //Use the default sec provider
            IAccountSecurityProvider prov = entity.GetSecProviderOrThrow();
            return prov.TryEncryptClientData(secInfo, data, output);
        }

        /// <summary>
        /// Invalidates the login status of the current connection and session (if session is loaded)
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvalidateLogin(this HttpEntity entity)
        {
            //Invalidate against the sec provider
            IAccountSecurityProvider prov = entity.GetSecProviderOrThrow();
            
            prov.InvalidateLogin(entity);

            //Invalidate the session also
            entity.Session.Invalidate();
        }

        /// <summary>
        /// Gets the current browser's id if it was specified during login process
        /// </summary>
        /// <returns>The browser's id if set, <see cref="string.Empty"/> otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetBrowserID(this in SessionInfo session) => session[BROWSER_ID_ENTRY];

        /// <summary>
        /// Specifies that the current session belongs to a local user-account
        /// </summary>
        /// <param name="session"></param>
        /// <param name="value">True for a local account, false otherwise</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HasLocalAccount(this in SessionInfo session, bool value) => session[LOCAL_ACCOUNT_ENTRY] = value ? "1" : null!;
        
        /// <summary>
        /// Gets a value indicating if the session belongs to a local user account
        /// </summary>
        /// <param name="session"></param>
        /// <returns>True if the current user's account is a local account</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasLocalAccount(this in SessionInfo session) => int.TryParse(session[LOCAL_ACCOUNT_ENTRY], out int value) && value > 0;

        #endregion

        #region Privilege Extensions
        /// <summary>
        /// Compares the users privilege level against the specified level
        /// </summary>
        /// <param name="session"></param>
        /// <param name="level">64bit privilege level to compare</param>
        /// <returns>true if the current user has at least the specified level or higher</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasLevel(this in SessionInfo session, byte level) => (session.Privilages & LEVEL_MSK) >= (((ulong)level << LEVEL_MSK_OFFSET) & LEVEL_MSK);
        /// <summary>
        /// Determines if the group ID of the current user matches the specified group
        /// </summary>
        /// <param name="session"></param>
        /// <param name="groupId">Group ID to compare</param>
        /// <returns>true if the user belongs to the group, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasGroup(this in SessionInfo session, ushort groupId) => (session.Privilages & GROUP_MSK) == (((ulong)groupId << GROUP_MSK_OFFSET) & GROUP_MSK);
        /// <summary>
        /// Determines if the current user has an equivalent option code
        /// </summary>
        /// <param name="session"></param>
        /// <param name="option">Option code check</param>
        /// <returns>true if the user options field equals the option</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasOption(this in SessionInfo session, byte option) => (session.Privilages & OPTIONS_MSK) == (((ulong)option << OPTIONS_MSK_OFFSET) & OPTIONS_MSK);

        /// <summary>
        /// Returns the status of the user's privlage read bit
        /// </summary>
        /// <returns>true if the current user has the read permission, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanRead(this in SessionInfo session) => (session.Privilages & READ_MSK) == READ_MSK;
        /// <summary>
        /// Returns the status of the user's privlage write bit
        /// </summary>
        /// <returns>true if the current user has the write permission, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanWrite(this in SessionInfo session) => (session.Privilages & WRITE_MSK) == WRITE_MSK;
        /// <summary>
        /// Returns the status of the user's privlage delete bit
        /// </summary>
        /// <returns>true if the current user has the delete permission, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanDelete(this in SessionInfo session) => (session.Privilages & DELETE_MSK) == DELETE_MSK;
        #endregion

        #region flc       

        /// <summary>
        /// Gets the current number of failed login attempts
        /// </summary>
        /// <param name="user"></param>
        /// <returns>The current number of failed login attempts</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimestampedCounter FailedLoginCount(this IUser user)
        {
            ulong value = user.GetValueType<string, ulong>(FAILED_LOGIN_ENTRY);
            return TimestampedCounter.FromUInt64(value);
        }

        /// <summary>
        /// Clears any pending flc count.
        /// </summary>
        /// <param name="user"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearFailedLoginCount(this IUser user)
        {
            //Cast the counter to a ulong and store as a ulong
            user.SetValueType(FAILED_LOGIN_ENTRY, (ulong)0);
        }

        /// <summary>
        /// Sets the number of failed login attempts for the current session
        /// </summary>
        /// <param name="user"></param>
        /// <param name="time">Explicitly sets the time of the internal counter</param>
        /// <param name="value">The value to set the failed login attempt count</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FailedLoginCount(this IUser user, uint value, DateTimeOffset time)
        {
            TimestampedCounter counter = TimestampedCounter.FromValues(value, time);
            //Cast the counter to a ulong and store as a ulong
            user.SetValueType(FAILED_LOGIN_ENTRY, counter.ToUInt64());
        }

        /// <summary>
        /// Sets the number of failed login attempts for the current session
        /// </summary>
        /// <param name="user"></param>
        /// <param name="value">The value to set the failed login attempt count</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FailedLoginCount(this IUser user, uint value) => FailedLoginCount(user, value, DateTimeOffset.UtcNow);

        /// <summary>
        /// Sets the number of failed login attempts for the current session
        /// </summary>
        /// <param name="user"></param>
        /// <param name="value">The value to set the failed login attempt count</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FailedLoginCount(this IUser user, TimestampedCounter value)
        {
            //Cast the counter to a ulong and store as a ulong
            user.SetValueType(FAILED_LOGIN_ENTRY, value.ToUInt64());
        }

        #endregion
    }
}