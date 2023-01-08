/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: ClientContract.cs 
*
* ClientContract.cs is part of VNLib.Net.Rest.Client which is part of the larger 
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

using RestSharp;
using VNLib.Utils.Memory.Caching;
using VNLib.Utils.Resources;

namespace VNLib.Net.Rest.Client
{
    /// <summary>
    /// Represents a RestClient least contract. When disposed,
    /// releases it for use by other waiters
    /// </summary>
    public class ClientContract : OpenResourceHandle<RestClient>
    {
        private readonly RestClient _client;
        private readonly ObjectRental<RestClient> _pool;

        internal ClientContract(RestClient client, ObjectRental<RestClient> pool)
        {
            _client = client;
            _pool = pool;
        }
        ///<inheritdoc/>
        public override RestClient Resource
        {
            get
            {
                Check();
                return _client;
            }
        }
        ///<inheritdoc/>
        protected override void Free()
        {
            _pool.Return(_client);
        }
    }
}
