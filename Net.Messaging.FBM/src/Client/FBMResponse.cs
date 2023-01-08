/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMResponse.cs 
*
* FBMResponse.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;

using VNLib.Utils.IO;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// A Fixed Buffer Message client response linked to the request that generated it.
    /// Once the request is disposed or returned this message state is invalid
    /// </summary>
    public readonly struct FBMResponse : IDisposable, IEquatable<FBMResponse>
    {
        private readonly Action? _onDispose;

        /// <summary>
        /// True when a response body was recieved and properly parsed
        /// </summary>
        public readonly bool IsSet { get; }
        /// <summary>
        /// The raw response message packet
        /// </summary>
        public readonly VnMemoryStream? MessagePacket { get; }
        /// <summary>
        /// A collection of response message headers
        /// </summary>
        public readonly IReadOnlyList<KeyValuePair<HeaderCommand, ReadOnlyMemory<char>>> Headers { get; }
        /// <summary>
        /// Status flags of the message parse operation
        /// </summary>
        public readonly HeaderParseError StatusFlags { get; }
        /// <summary>
        /// The body segment of the response message
        /// </summary>
        public readonly ReadOnlySpan<byte> ResponseBody => IsSet ? Helpers.GetRemainingData(MessagePacket!) : ReadOnlySpan<byte>.Empty;

        /// <summary>
        /// Initailzies a response message structure and parses response
        /// packet structure
        /// </summary>
        /// <param name="vms">The message buffer (message packet)</param>
        /// <param name="status">The size of the buffer to alloc for header value storage</param>
        /// <param name="headerList">The collection of headerse</param>
        /// <param name="onDispose">A method that will be invoked when the message response body is disposed</param>
        public FBMResponse(VnMemoryStream? vms, HeaderParseError status, IReadOnlyList<KeyValuePair<HeaderCommand, ReadOnlyMemory<char>>> headerList, Action onDispose)
        {
            MessagePacket = vms;
            StatusFlags = status;
            Headers = headerList;
            IsSet = true;
            _onDispose = onDispose;
        }

        /// <summary>
        /// Creates an unset response structure
        /// </summary>
        public FBMResponse()
        {
            MessagePacket = null;
            StatusFlags = HeaderParseError.InvalidHeaderRead;
            Headers = Array.Empty<KeyValuePair<HeaderCommand, ReadOnlyMemory<char>>>();
            IsSet = false;
            _onDispose = null;
        }

        /// <summary>
        /// Releases any resources associated with the response message
        /// </summary>
        public void Dispose() => _onDispose?.Invoke();
        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is FBMResponse response && Equals(response);
        ///<inheritdoc/>
        public override int GetHashCode() => IsSet ? MessagePacket!.GetHashCode() : 0;
        ///<inheritdoc/>
        public static bool operator ==(FBMResponse left, FBMResponse right) => left.Equals(right);
        ///<inheritdoc/>
        public static bool operator !=(FBMResponse left, FBMResponse right) => !(left == right);
        ///<inheritdoc/>
        public bool Equals(FBMResponse other) => IsSet && other.IsSet && MessagePacket == other.MessagePacket;

    }
}
