/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: AccountManager.cs 
*
* AccountManager.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Users;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;

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
        public const int MAX_EMAIL_CHARS = 50;
        public const int ID_FIELD_CHARS = 65;
        public const int STREET_ADDR_CHARS = 150;
        public const int MAX_LOGIN_COUNT = 10;
        public const int MAX_FAILED_RESET_ATTEMPS = 5;

        /// <summary>
        /// The maximum time in seconds for a login message to be considered valid
        /// </summary>
        public const double MAX_TIME_DIFF_SECS = 10.00;
        /// <summary>
        /// The size in bytes of the random passwords generated when invoking the <see cref="SetRandomPasswordAsync(PasswordHashing, IUserManager, IUser, int)"/>
        /// </summary>
        public const int RANDOM_PASS_SIZE = 128;
        /// <summary>
        /// The name of the header that will identify a client's identiy
        /// </summary>
        public const string LOGIN_TOKEN_HEADER = "X-Web-Token";
        /// <summary>
        /// The origin string of a local user account. This value will be set if an
        /// account is created through the VNLib.Plugins.Essentials.Accounts library
        /// </summary>
        public const string LOCAL_ACCOUNT_ORIGIN = "local";
        /// <summary>
        /// The size (in bytes) of the challenge secret
        /// </summary>
        public const int CHALLENGE_SIZE = 64;
        /// <summary>
        /// The size (in bytes) of the sesssion long user-password challenge
        /// </summary>
        public const int SESSION_CHALLENGE_SIZE = 128;

        //The buffer size to use when decoding the base64 public key from the user
        private const int PUBLIC_KEY_BUFFER_SIZE = 1024;
        /// <summary>
        /// The name of the login cookie set when a user logs in
        /// </summary>
        public const string LOGIN_COOKIE_NAME = "VNLogin";
        /// <summary>
        /// The name of the login client identifier cookie (cookie that is set fir client to use to determine if the user is logged in)
        /// </summary>
        public const string LOGIN_COOKIE_IDENTIFIER = "li";
        
        private const int LOGIN_COOKIE_SIZE = 64;
        
        //Session entry keys
        private const string BROWSER_ID_ENTRY = "acnt.bid";
        private const string CLIENT_PUB_KEY_ENTRY = "acnt.pbk";
        private const string CHALLENGE_HMAC_ENTRY = "acnt.cdig";
        private const string FAILED_LOGIN_ENTRY = "acnt.flc";
        private const string LOCAL_ACCOUNT_ENTRY = "acnt.ila";
        private const string ACC_ORIGIN_ENTRY = "__.org";
        private const string TOKEN_UPDATE_TIME_ENTRY = "acnt.tut";
        //private const string CHALLENGE_HASH_ENTRY = "acnt.chl";

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

        //Timeouts     
        public static readonly TimeSpan LoginCookieLifespan = TimeSpan.FromHours(1);
        public static readonly TimeSpan RegenIdPeriod = TimeSpan.FromMinutes(25);

        /// <summary>
        /// The client data encryption padding.
        /// </summary>
        public static readonly RSAEncryptionPadding ClientEncryptonPadding = RSAEncryptionPadding.OaepSHA256;

        /// <summary>
        /// The size (in bytes) of the web-token hash size
        /// </summary>
        private static readonly int TokenHashSize = (SHA384.Create().HashSize / 8);
        
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
        public static async Task<ERRNO> SetRandomPasswordAsync(this PasswordHashing passHashing, IUserManager manager, IUser user, int size = RANDOM_PASS_SIZE)
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

        #endregion

        #region Client Auth Extensions

        /// <summary>
        /// Runs necessary operations to grant authorization to the specified user of a given session and user with provided variables
        /// </summary>
        /// <param name="ev">The connection and session to log-in</param>
        /// <param name="loginMessage">The message of the client to set the log-in status of</param>
        /// <param name="user">The user to log-in</param>
        /// <returns>The encrypted base64 token secret data to send to the client</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="CryptographicException"></exception>
        public static string GenerateAuthorization(this HttpEntity ev, LoginMessage loginMessage, IUser user)
        {
            return GenerateAuthorization(ev, loginMessage.ClientPublicKey, loginMessage.ClientID, user);
        }

        /// <summary>
        /// Runs necessary operations to grant authorization to the specified user of a given session and user with provided variables
        /// </summary>
        /// <param name="ev">The connection and session to log-in</param>
        /// <param name="base64PubKey">The clients base64 public key</param>
        /// <param name="clientId">The browser/client id</param>
        /// <param name="user">The user to log-in</param>
        /// <returns>The encrypted base64 token secret data to send to the client</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="CryptographicException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static string GenerateAuthorization(this HttpEntity ev, string base64PubKey, string clientId, IUser user)
        {
            if (!ev.Session.IsSet || ev.Session.SessionType != SessionType.Web)
            {
                throw new InvalidOperationException("The session is not set or the session is not a web-based session type");
            }
            //Update session-id for "upgrade"
            ev.Session.RegenID();
            //derrive token from login data
            TryGenerateToken(base64PubKey, out string base64ServerToken, out string base64ClientData);
            //Clear flags
            user.FailedLoginCount(0);
            //Get the "local" account flag from the user object
            bool localAccount = user.IsLocalAccount();
            //Set login cookie and session login hash
            ev.SetLogin(localAccount);
            //Store variables
            ev.Session.UserID = user.UserID;
            ev.Session.Privilages = user.Privilages;
            //Store browserid/client id if specified
            SetBrowserID(in ev.Session, clientId);
            //Store the clients public key
            SetBrowserPubKey(in ev.Session, base64PubKey);
            //Set local account flag
            ev.Session.HasLocalAccount(localAccount);
            //Store the base64 server key to compute the hmac later
            ev.Session.Token = base64ServerToken;
            //Update the last token upgrade time
            ev.Session.LastTokenUpgrade(ev.RequestedTimeUtc);
            //Return the client encrypted data
            return base64ClientData;
        }

        /*
         * Notes for RSA client token generator code below
         * 
         * To log-in a client with the following API the calling code
         * must have already determined that the client should be 
         * logged in (verified passwords or auth tokens).
         * 
         * The client will send a LoginMessage object that will
         * contain the following Information.
         * 
         * - The clients RSA public key in base64 subject-key info format
         * - The client browser's id hex string
         * - The clients local-time
         * 
         * The TryGenerateToken method, will generate a random-byte token,
         * encrypt it using the clients RSA public key, return the encrypted
         * token data to the client, and only the client will be able to 
         * decrypt the token data. 
         * 
         * The token data is also hashed with SHA-256 (for future use) and
         * stored in the client's session store. The client must decrypt
         * the token data, hash it, and return it as a header for verification.
         * 
         * Ideally the client should sign the data and send the signature or 
         * hash back, but it wont prevent MITM, and for now I think it just
         * adds extra overhead for every connection during the HttpEvent.TokenMatches()
         * check extension method
         */

        private ref struct TokenGenBuffers
        {
            public readonly Span<byte> Buffer { private get; init; }
            public readonly Span<byte> SignatureBuffer => Buffer[..64];



            public int ClientPbkWritten;
            public readonly Span<byte> ClientPublicKeyBuffer => Buffer.Slice(64, 1024);
            public readonly ReadOnlySpan<byte> ClientPbkOutput => ClientPublicKeyBuffer[..ClientPbkWritten];



            public int ClientEncBytesWritten;
            public readonly Span<byte> ClientEncOutputBuffer => Buffer[(64 + 1024)..];
            public readonly ReadOnlySpan<byte> EncryptedOutput => ClientEncOutputBuffer[..ClientEncBytesWritten];
        }
        
        /// <summary>
        /// Computes a random buffer, encrypts it with the client's public key,
        /// computes the digest of that key and returns the base64 encoded strings 
        /// of those components
        /// </summary>
        /// <param name="base64clientPublicKey">The user's public key credential</param>
        /// <param name="base64Digest">The base64 encoded digest of the secret that was encrypted</param>
        /// <param name="base64ClientData">The client's user-agent header value</param>
        /// <returns>A string representing a unique signed token for a given login context</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="CryptographicException"></exception>
        private static void TryGenerateToken(string base64clientPublicKey, out string base64Digest, out string base64ClientData)
        {
            //Temporary work buffer
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(4096, true);
            /*
             * Create a new token buffer for bin buffers.
             * This buffer struct is used to break up 
             * a single block of memory into individual
             * non-overlapping (important!) buffer windows
             * for named purposes
             */
            TokenGenBuffers tokenBuf = new()
            {
                Buffer = buffer.Span
            };
            //Recover the clients public key from its base64 encoding
            if (!Convert.TryFromBase64String(base64clientPublicKey, tokenBuf.ClientPublicKeyBuffer, out tokenBuf.ClientPbkWritten))
            {
                throw new InternalBufferOverflowException("Failed to recover the clients RSA public key");
            }
            /*
             * Fill signature buffer with random data
             * this signature will be stored and used to verify 
             * signed client messages. It will also be encryped 
             * using the clients RSA keys
             */
            RandomHash.GetRandomBytes(tokenBuf.SignatureBuffer);
            /*
             * Setup a new RSA Crypto provider that is initialized with the clients
             * supplied public key. RSA will be used to encrypt the server secret
             * that only the client will be able to decrypt for the current connection
             */
            using RSA rsa = RSA.Create();
            //Setup rsa from the users public key
            rsa.ImportSubjectPublicKeyInfo(tokenBuf.ClientPbkOutput, out _);
            //try to encypte output data
            if (!rsa.TryEncrypt(tokenBuf.SignatureBuffer, tokenBuf.ClientEncOutputBuffer, RSAEncryptionPadding.OaepSHA256, out tokenBuf.ClientEncBytesWritten))
            {
                throw new InternalBufferOverflowException("Failed to encrypt the server secret");
            }
            //Compute the digest of the raw server key
            base64Digest = ManagedHash.ComputeBase64Hash(tokenBuf.SignatureBuffer, HashAlg.SHA384);
            /*
             * The client will send a hash of the decrypted key and will be used
             * as a comparison to the hash string above ^
             */
            base64ClientData = Convert.ToBase64String(tokenBuf.EncryptedOutput, Base64FormattingOptions.None);
        }

        /// <summary>
        /// Determines if the client sent a token header, and it maches against the current session
        /// </summary>
        /// <returns>true if the client set the token header, the session is loaded, and the token matches the session, false otherwise</returns>
        public static bool TokenMatches(this HttpEntity ev)
        {
            //Get the token from the client header, the client should always sent this
            string? clientDigest = ev.Server.Headers[LOGIN_TOKEN_HEADER];
            //Make sure a session is loaded
            if (!ev.Session.IsSet || ev.Session.IsNew || string.IsNullOrWhiteSpace(clientDigest))
            {
                return false;
            }
            /*
            * Alloc buffer to do conversion and zero initial contents incase the 
            * payload size has been changed.
            * 
            * The buffer just needs to be large enoguh for the size of the hashes 
            * that are stored in base64 format.
            * 
            * The values in the buffers will be the raw hash of the client's key
            * and the stored key sent during initial authorziation. If the hashes
            * are equal it should mean that the client must have the private
            * key that generated the public key that was sent
            */
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(TokenHashSize * 2, true);
            //Slice up buffers 
            Span<byte> headerBuffer = buffer.Span[..TokenHashSize];
            Span<byte> sessionBuffer = buffer.Span[TokenHashSize..];
            //Convert the header token and the session token
            if (Convert.TryFromBase64String(clientDigest, headerBuffer, out int headerTokenLen)
                && Convert.TryFromBase64String(ev.Session.Token, sessionBuffer, out int sessionTokenLen))
            {
                //Do a fixed time equal (probably overkill, but should not matter too much)
                if(CryptographicOperations.FixedTimeEquals(headerBuffer[..headerTokenLen], sessionBuffer[..sessionTokenLen]))
                {
                    return true;
                }
            }

            /*
             * If the token does not match, or cannot be found, check if the client
             * has login cookies set, if not remove them.
             * 
             * This does not affect the session, but allows for a web client to update
             * its login state if its no-longer logged in
             */

            //Expire login cookie if set
            if (ev.Server.RequestCookies.ContainsKey(LOGIN_COOKIE_NAME))
            {
                ev.Server.ExpireCookie(LOGIN_COOKIE_NAME, sameSite: CookieSameSite.SameSite);
            }
            //Expire the LI cookie if set
            if (ev.Server.RequestCookies.ContainsKey(LOGIN_COOKIE_IDENTIFIER))
            {
                ev.Server.ExpireCookie(LOGIN_COOKIE_IDENTIFIER, sameSite: CookieSameSite.SameSite);
            }

            return false;
        }

        /// <summary>
        /// Regenerates the user's login token with the public key stored
        /// during initial logon
        /// </summary>
        /// <returns>The base64 of the newly encrypted secret</returns>
        public static string? RegenerateClientToken(this HttpEntity ev)
        {
            if(!ev.Session.IsSet || ev.Session.SessionType != SessionType.Web)
            {
                return null;
            }
            //Get the client's stored public key
            string clientPublicKey = ev.Session.GetBrowserPubKey();
            //Make sure its set
            if (string.IsNullOrWhiteSpace(clientPublicKey))
            {
                return null;
            }
            //Generate a new token using the stored public key
            TryGenerateToken(clientPublicKey, out string base64Digest, out string base64ClientData);
            //store the token to the user's session
            ev.Session.Token = base64Digest;
            //Update the last token upgrade time
            ev.Session.LastTokenUpgrade(ev.RequestedTimeUtc);
            //return the clients encrypted secret
            return base64ClientData;
        }

        /// <summary>
        /// Tries to encrypt the specified data using the stored public key and store the encrypted data into 
        /// the output buffer.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="data">Data to encrypt</param>
        /// <param name="outputBuffer">The buffer to store encrypted data in</param>
        /// <returns>
        /// The number of encrypted bytes written to the output buffer,
        /// or false (0) if the operation failed, or if no credential is 
        /// stored.
        /// </returns>
        /// <exception cref="CryptographicException"></exception>
        public static ERRNO TryEncryptClientData(this in SessionInfo session, ReadOnlySpan<byte> data, in Span<byte> outputBuffer)
        {
            if (!session.IsSet)
            {
                return false;
            }
            //try to get the public key from the client
            string base64PubKey = session.GetBrowserPubKey();
            return TryEncryptClientData(base64PubKey, data, in outputBuffer);
        }
        /// <summary>
        /// Tries to encrypt the specified data using the specified public key
        /// </summary>
        /// <param name="base64PubKey">A base64 encoded public key used to encrypt client data</param>
        /// <param name="data">Data to encrypt</param>
        /// <param name="outputBuffer">The buffer to store encrypted data in</param>
        /// <returns>
        /// The number of encrypted bytes written to the output buffer,
        /// or false (0) if the operation failed, or if no credential is 
        /// specified.
        /// </returns>
        /// <exception cref="CryptographicException"></exception>
        public static ERRNO TryEncryptClientData(ReadOnlySpan<char> base64PubKey, ReadOnlySpan<byte> data, in Span<byte> outputBuffer)
        {
            if (base64PubKey.IsEmpty)
            {
                return false;
            }
            //Alloc a buffer for decoding the public key
            using UnsafeMemoryHandle<byte> pubKeyBuffer = MemoryUtil.UnsafeAlloc<byte>(PUBLIC_KEY_BUFFER_SIZE, true);
            //Decode the public key
            ERRNO pbkBytesWritten = VnEncoding.TryFromBase64Chars(base64PubKey, pubKeyBuffer);
            //Try to encrypt the data
            return pbkBytesWritten ? TryEncryptClientData(pubKeyBuffer.Span[..(int)pbkBytesWritten], data, in outputBuffer) : false;
        }
        /// <summary>
        /// Tries to encrypt the specified data using the specified public key
        /// </summary>
        /// <param name="rawPubKey">The raw SKI public key</param>
        /// <param name="data">Data to encrypt</param>
        /// <param name="outputBuffer">The buffer to store encrypted data in</param>
        /// <returns>
        /// The number of encrypted bytes written to the output buffer,
        /// or false (0) if the operation failed, or if no credential is 
        /// specified.
        /// </returns>
        /// <exception cref="CryptographicException"></exception>
        public static ERRNO TryEncryptClientData(ReadOnlySpan<byte> rawPubKey, ReadOnlySpan<byte> data, in Span<byte> outputBuffer)
        {
            if (rawPubKey.IsEmpty)
            {
                return false;
            }
            //Setup new empty rsa
            using RSA rsa = RSA.Create();
            //Import the public key
            rsa.ImportSubjectPublicKeyInfo(rawPubKey, out _);
            //Encrypt data with OaepSha256 as configured in the browser
            return rsa.TryEncrypt(data, outputBuffer, ClientEncryptonPadding, out int bytesWritten) ? bytesWritten : false;
        }

        /// <summary>
        /// Stores the clients public key specified during login
        /// </summary>
        /// <param name="session"></param>
        /// <param name="base64PubKey"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetBrowserPubKey(in SessionInfo session, string base64PubKey) => session[CLIENT_PUB_KEY_ENTRY] = base64PubKey;

        /// <summary>
        /// Gets the clients stored public key that was specified during login
        /// </summary>
        /// <returns>The base64 encoded public key string specified at login</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetBrowserPubKey(this in SessionInfo session) => session[CLIENT_PUB_KEY_ENTRY];

        /// <summary>
        /// Stores the login key as a cookie in the current session as long as the session exists
        /// </summary>/
        /// <param name="ev">The event to log-in</param>
        /// <param name="localAccount">Does the session belong to a local user account</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetLogin(this HttpEntity ev, bool? localAccount = null)
        {
            //Make sure the session is loaded
            if (!ev.Session.IsSet)
            {
                return;
            }
            string loginString = RandomHash.GetRandomBase64(LOGIN_COOKIE_SIZE);
            //Set login cookie and session login hash
            ev.Server.SetCookie(LOGIN_COOKIE_NAME, loginString, "", "/", LoginCookieLifespan, CookieSameSite.SameSite, true, true);
            ev.Session.LoginHash = loginString;
            //If not set get from session storage
            localAccount ??= ev.Session.HasLocalAccount();
            //Set the client identifier cookie to a value indicating a local account
            ev.Server.SetCookie(LOGIN_COOKIE_IDENTIFIER, localAccount.Value ? "1" : "2", "", "/", LoginCookieLifespan, CookieSameSite.SameSite, false, true);
        }

        /// <summary>
        /// Invalidates the login status of the current connection and session (if session is loaded)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvalidateLogin(this HttpEntity ev)
        {
            //Expire the login cookie
            ev.Server.ExpireCookie(LOGIN_COOKIE_NAME, sameSite: CookieSameSite.SameSite, secure: true);
            //Expire the identifier cookie
            ev.Server.ExpireCookie(LOGIN_COOKIE_IDENTIFIER, sameSite: CookieSameSite.SameSite, secure: true);
            if (ev.Session.IsSet)
            {
                //Invalidate the session
                ev.Session.Invalidate();
            }
        }

        /// <summary>
        /// Determines if the current session login cookie matches the value stored in the current session (if the session is loaded)
        /// </summary>
        /// <returns>True if the session is active, the cookie was properly received, and the cookie value matches the session. False otherwise</returns>
        public static bool LoginCookieMatches(this HttpEntity ev)
        {
            //Sessions must be loaded
            if (!ev.Session.IsSet)
            {
                return false;
            }
            //Try to get the login string from the request cookies
            if (!ev.Server.RequestCookies.TryGetNonEmptyValue(LOGIN_COOKIE_NAME, out string? liCookie))
            {
                return false;
            }
            /*
             * Alloc buffer to do conversion and zero initial contents incase the 
             * payload size has been changed.
             * 
             * Since the cookie size and the local copy should be the same size
             * and equal to the LOGIN_COOKIE_SIZE constant, the buffer size should
             * be 2 * LOGIN_COOKIE_SIZE, and it can be split in half and shared
             * for both conversions
             */
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(2 * LOGIN_COOKIE_SIZE, true);
            //Slice up buffers 
            Span<byte> cookieBuffer = buffer.Span[..LOGIN_COOKIE_SIZE];
            Span<byte> sessionBuffer = buffer.Span.Slice(LOGIN_COOKIE_SIZE, LOGIN_COOKIE_SIZE);
            //Convert cookie and session hash value
            if (Convert.TryFromBase64String(liCookie, cookieBuffer, out _)
                && Convert.TryFromBase64String(ev.Session.LoginHash, sessionBuffer, out _))
            {
                //Do a fixed time equal (probably overkill, but should not matter too much)
                if(CryptographicOperations.FixedTimeEquals(cookieBuffer, sessionBuffer))
                {
                    //If the user is "logged in" and the request is using the POST method, then we can update the cookie
                    if(ev.Server.Method == HttpMethod.POST && ev.Session.Created.Add(RegenIdPeriod) < ev.RequestedTimeUtc)
                    {
                        //Regen login token
                        ev.SetLogin();
                        ev.Session.RegenID();
                    }

                    return true;
                }
            }
            return false;
        }
      
        /// <summary>
        /// Determines if the client's login cookies need to be updated
        /// to reflect its state with the current session's state
        /// for the client
        /// </summary>
        /// <param name="ev"></param>
        public static void ReconcileCookies(this HttpEntity ev)
        {
            //Only handle cookies if session is loaded and is a web based session
            if (!ev.Session.IsSet || ev.Session.SessionType != SessionType.Web)
            {
                return;
            }
            if (ev.Session.IsNew)
            {
                //If either login cookies are set on a new session, clear them
                if (ev.Server.RequestCookies.ContainsKey(LOGIN_COOKIE_NAME) || ev.Server.RequestCookies.ContainsKey(LOGIN_COOKIE_IDENTIFIER))
                {
                    //Expire the login cookie
                    ev.Server.ExpireCookie(LOGIN_COOKIE_NAME, sameSite:CookieSameSite.SameSite, secure:true);
                    //Expire the identifier cookie
                    ev.Server.ExpireCookie(LOGIN_COOKIE_IDENTIFIER, sameSite: CookieSameSite.SameSite, secure: true);
                }
            }
            //If the session is not supposed to be logged in, clear the login cookies if they were set
            else if (string.IsNullOrEmpty(ev.Session.LoginHash))
            {
                //If one of either cookie is not set 
                if (ev.Server.RequestCookies.ContainsKey(LOGIN_COOKIE_NAME))
                {
                    //Expire the login cookie
                    ev.Server.ExpireCookie(LOGIN_COOKIE_NAME, sameSite: CookieSameSite.SameSite, secure: true);
                }
                if (ev.Server.RequestCookies.ContainsKey(LOGIN_COOKIE_IDENTIFIER))
                {
                    //Expire the identifier cookie
                    ev.Server.ExpireCookie(LOGIN_COOKIE_IDENTIFIER, sameSite: CookieSameSite.SameSite, secure: true);
                }
            }
        }

        /// <summary>
        /// Gets the last time the session token was set
        /// </summary>
        /// <param name="session"></param>
        /// <returns>The last time the token was updated/generated, or <see cref="DateTimeOffset.MinValue"/> if not set</returns>
        public static DateTimeOffset LastTokenUpgrade(this in SessionInfo session)
        {
            //Get the serialized time value
            string timeString = session[TOKEN_UPDATE_TIME_ENTRY];
            return long.TryParse(timeString, out long time) ? DateTimeOffset.FromUnixTimeSeconds(time) : DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Updates the last time the session token was set
        /// </summary>
        /// <param name="session"></param>
        /// <param name="updated">The UTC time the last token was set</param>
        private static void LastTokenUpgrade(this in SessionInfo session, DateTimeOffset updated) 
            => session[TOKEN_UPDATE_TIME_ENTRY] = updated.ToUnixTimeSeconds().ToString();

        /// <summary>
        /// Stores the browser's id during a login process
        /// </summary>
        /// <param name="session"></param>
        /// <param name="browserId">Browser id value to store</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetBrowserID(in SessionInfo session, string browserId) => session[BROWSER_ID_ENTRY] = browserId;

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
        private static void HasLocalAccount(this in SessionInfo session, bool value) => session[LOCAL_ACCOUNT_ENTRY] = value ? "1" : null;
        /// <summary>
        /// Gets a value indicating if the session belongs to a local user account
        /// </summary>
        /// <param name="session"></param>
        /// <returns>True if the current user's account is a local account</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasLocalAccount(this in SessionInfo session) => int.TryParse(session[LOCAL_ACCOUNT_ENTRY], out int value) && value > 0;

        #endregion

        #region Client Challenge

        /*
         * Generates a secret that is used to compute the unique hmac digest of the 
         * current user's password. The digest is stored in the current session
         * and used to compare future requests that require password re-authentication.
         * The client will compute the digest of the user's password and send the digest
         * instead of the user's password
         */

        /// <summary>
        /// Generates a new password challenge for the current session and specified password
        /// </summary>
        /// <param name="session"></param>
        /// <param name="password">The user's password to compute the hash of</param>
        /// <returns>The raw derrivation key to send to the client</returns>
        public static byte[] GenPasswordChallenge(this in SessionInfo session, PrivateString password) 
        {
            ReadOnlySpan<char> rawPass = password;
            //Calculate the password buffer size required
            int passByteCount = Encoding.UTF8.GetByteCount(rawPass);
            //Allocate the buffer
            using UnsafeMemoryHandle<byte> bufferHandle = MemoryUtil.UnsafeAlloc<byte>(passByteCount + 64, true);
            //Slice buffers
            Span<byte> utf8PassBytes = bufferHandle.Span[..passByteCount];
            Span<byte> hashBuffer = bufferHandle.Span[passByteCount..];
            //Encode the password into the buffer
            _ = Encoding.UTF8.GetBytes(rawPass, utf8PassBytes);
            try
            {
                //Get random secret buffer
                byte[] secretKey = RandomHash.GetRandomBytes(SESSION_CHALLENGE_SIZE);
                //Compute the digest
                int count = HMACSHA512.HashData(secretKey, utf8PassBytes, hashBuffer);
                //Store the user's password digest
                session[CHALLENGE_HMAC_ENTRY] = VnEncoding.ToBase32String(hashBuffer[..count], false);
                return secretKey;
            }
            finally
            {
                //Wipe buffer
                RandomHash.GetRandomBytes(utf8PassBytes);
            }
        }
        /// <summary>
        /// Verifies the stored unique digest of the user's password against 
        /// the client derrived password
        /// </summary>
        /// <param name="session"></param>
        /// <param name="base64PasswordDigest">The base64 client derrived digest of the user's password to verify</param>
        /// <returns>True if formatting was correct and the derrived passwords match, false otherwise</returns>
        /// <exception cref="FormatException"></exception>
        public static bool VerifyChallenge(this in SessionInfo session, ReadOnlySpan<char> base64PasswordDigest)
        {
            string base32Digest = session[CHALLENGE_HMAC_ENTRY];
            if (string.IsNullOrWhiteSpace(base32Digest))
            {
                return false;
            }
            int bufSize = base32Digest.Length + base64PasswordDigest.Length;
            //Alloc buffer
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(bufSize);
            //Split buffers
            Span<byte> localBuf = buffer.Span[..base32Digest.Length];
            Span<byte> passBuf = buffer.Span[base32Digest.Length..];
            //Recover the stored base32 digest
            ERRNO count = VnEncoding.TryFromBase32Chars(base32Digest, localBuf);
            if (!count)
            {
                return false;
            }
            //Recover base64 bytes
            if(!Convert.TryFromBase64Chars(base64PasswordDigest, passBuf, out int passBytesWritten))
            {
                return false;
            }
            //Trim buffers
            localBuf = localBuf[..(int)count];
            passBuf = passBuf[..passBytesWritten];
            //Compare and return
            return CryptographicOperations.FixedTimeEquals(passBuf, localBuf);
        }

        #endregion

        #region Privilage Extensions
        /// <summary>
        /// Compares the users privilage level against the specified level
        /// </summary>
        /// <param name="session"></param>
        /// <param name="level">64bit privilage level to compare</param>
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
            return (TimestampedCounter)value;
        }
        /// <summary>
        /// Sets the number of failed login attempts for the current session
        /// </summary>
        /// <param name="user"></param>
        /// <param name="value">The value to set the failed login attempt count</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FailedLoginCount(this IUser user, uint value)
        {
            TimestampedCounter counter = new(value);
            //Cast the counter to a ulong and store as a ulong
            user.SetValueType(FAILED_LOGIN_ENTRY, (ulong)counter);
        }
        /// <summary>
        /// Sets the number of failed login attempts for the current session
        /// </summary>
        /// <param name="user"></param>
        /// <param name="value">The value to set the failed login attempt count</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FailedLoginCount(this IUser user, TimestampedCounter value)
        {
            //Cast the counter to a ulong and store as a ulong
            user.SetValueType(FAILED_LOGIN_ENTRY, (ulong)value);
        }
        /// <summary>
        /// Increments the failed login attempt count
        /// </summary>
        /// <param name="user"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FailedLoginIncrement(this IUser user)
        {
            TimestampedCounter current = user.FailedLoginCount();
            user.FailedLoginCount(current.Count + 1);
        }

        #endregion
    }
}