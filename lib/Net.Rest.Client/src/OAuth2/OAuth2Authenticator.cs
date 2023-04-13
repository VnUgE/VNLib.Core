/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: OAuth2Authenticator.cs 
*
* OAuth2Authenticator.cs is part of VNLib.Net.Rest.Client which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Rest.Client is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Rest.Client is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Rest.Client. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using RestSharp;
using RestSharp.Authenticators;

using VNLib.Utils;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Rest.Client.OAuth2
{
    /// <summary>
    /// A self-contained OAuth2 RestSharp authenticator. Contains resources
    /// that should be disposed, when no longer in use. Represents a single 
    /// session on the remote server
    /// </summary>
    public class OAuth2Authenticator : VnDisposeable, IAuthenticator
    {
        private string? AccessToken;
        private DateTime Expires;

        private uint _counter;

        private readonly RestClient _client;
        private readonly Func<Credential> CredFunc;
        private readonly string TokenAuthPath;
        private readonly SemaphoreSlim _tokenLock;

        /// <summary>
        /// Initializes a new <see cref="OAuth2Authenticator"/> to be used for 
        /// authorizing requests to a remote server
        /// </summary>
        /// <param name="clientOps">The RestClient options for the client used to authenticate with the OAuth2 server</param>
        /// <param name="credFunc">The credential factory function</param>
        /// <param name="tokenPath">The path within the remote OAuth2 server to authenticate against</param>
        public OAuth2Authenticator(RestClientOptions clientOps, Func<Credential> credFunc, string tokenPath)
        {
            //Default to expired
            Expires = DateTime.MinValue;
            _client = new(clientOps);
            CredFunc = credFunc;
            TokenAuthPath = tokenPath;
            _tokenLock = new(1, 1);
        }

        async ValueTask IAuthenticator.Authenticate(IRestClient client, RestRequest request)
        {
            //Wait for access to the token incase another thread is updating it
            using SemSlimReleaser releaser = await _tokenLock.GetReleaserAsync(CancellationToken.None);
            //Check expiration
            if (Expires < DateTime.UtcNow)
            {
                //We need to refresh the token
                await RefreshTokenAsync();
            }
            //Add bearer token to the request
            request.AddOrUpdateHeader("Authorization", AccessToken!);
            //Inc counter
            _counter++;
        }

        private async Task RefreshTokenAsync()
        {
            using Credential creds = CredFunc();
            //Build request to the authentication endpoint
            RestRequest tokenRequest = new(TokenAuthPath, Method.Post);
            //Setup grant-type paramter
            tokenRequest.AddParameter("grant_type", "client_credentials", ParameterType.GetOrPost);
            //Add client id
            tokenRequest.AddParameter("client_id", creds.UserName, ParameterType.GetOrPost);
            //Add secret
            tokenRequest.AddParameter("client_secret", creds.Password, ParameterType.GetOrPost);
            //exec get token
            RestResponse tokenResponse = await _client.ExecuteAsync(tokenRequest);
            if (!tokenResponse.IsSuccessful)
            {
                throw new OAuth2AuthenticationException(tokenResponse, tokenResponse.ErrorException);
            }
            try
            {
                //Get a json doc for the response data
                using JsonDocument response = JsonDocument.Parse(tokenResponse.RawBytes);
                //Get expiration
                int expiresSec = response.RootElement.GetProperty("expires_in").GetInt32();
                //get access token
                string? accessToken = response.RootElement.GetProperty("access_token").GetString();
                string? tokenType = response.RootElement.GetProperty("token_type").GetString();

                //Store token variables, expire a few minutes before the server to allow for time disparity
                Expires = DateTime.UtcNow.AddSeconds(expiresSec).Subtract(TimeSpan.FromMinutes(2));
                //compile auth header value
                AccessToken = $"{tokenType} {accessToken}";
                //Reset counter
                _counter = 0;
            }
            catch (Exception ex)
            {
                throw new OAuth2AuthenticationException(tokenResponse, ex);
            }
        }

        protected override void Free()
        {
            _tokenLock.Dispose();
            _client.Dispose();
        }
    }
}
