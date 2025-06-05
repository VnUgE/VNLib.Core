/*
* Copyright (c) 2025 Vaughn Nugent
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
        public readonly IReadOnlyList<FBMMessageHeader> Headers { get; }

        /// <summary>
        /// Status flags of the message parse operation
        /// </summary>
        public readonly HeaderParseError StatusFlags { get; }

        /// <summary>
        /// The body segment of the response message
        /// </summary>
        public readonly ReadOnlySpan<byte> ResponseBody => IsSet ? Helpers.GetRemainingData(MessagePacket!) : [];

        /// <summary>
        /// Initailzies a response message structure and parses response
        /// packet structure
        /// </summary>
        /// <param name="vms">The message buffer (message packet)</param>
        /// <param name="status">The size of the buffer to alloc for header value storage</param>
        /// <param name="headerList">The collection of headerse</param>
        public FBMResponse(VnMemoryStream? vms, HeaderParseError status, IReadOnlyList<FBMMessageHeader> headerList)
        {
            MessagePacket = vms;
            StatusFlags = status;
            Headers = headerList;
            IsSet = true;
        }

        /// <summary>
        /// Creates an unset response structure
        /// </summary>
        public FBMResponse()
        {
            MessagePacket = null;
            StatusFlags = HeaderParseError.InvalidHeaderRead;
            Headers = [];
            IsSet = false;
        }

        /// <summary>
        /// Releases any resources associated with the response message
        /// </summary>
        public readonly void Dispose()
        {
            /*
             * Originally this struct held a delegate to a function inside the 
             * request that created it. Which did the following actions. Since 
             * the request is disposable or reusable, any data in use that 
             * are held by the request should be disposed or cleaned up with 
             * request hooks. So we can just clear the headers and dispose
             * the response. It doesn't really matter if this function is called
             * either again the request will clean up after itself. 
             */

            //Clear the header list if possible
            if (Headers is ICollection<FBMMessageHeader> hc)
            {
                hc.Clear();
            }

            //Dispose the message packet if it exists
            MessagePacket?.Dispose();
        }

        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is FBMResponse response && Equals(response);

        ///<inheritdoc/>
        public bool Equals(FBMResponse other) => IsSet && other.IsSet && ReferenceEquals(MessagePacket, other.MessagePacket);

        ///<inheritdoc/>
        public override int GetHashCode() => IsSet ? MessagePacket!.GetHashCode() : 0;

        ///<inheritdoc/>
        public static bool operator ==(FBMResponse left, FBMResponse right) => left.Equals(right);

        ///<inheritdoc/>
        public static bool operator !=(FBMResponse left, FBMResponse right) => !(left == right);
    }
}
