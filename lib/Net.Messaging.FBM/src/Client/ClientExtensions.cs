/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: ClientExtensions.cs 
*
* ClientExtensions.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Runtime.CompilerServices;

using VNLib.Utils;

namespace VNLib.Net.Messaging.FBM.Client
{

    public static class ClientExtensions
    {
        /// <summary>
        /// Writes the location header of the requested resource
        /// </summary>
        /// <param name="request"></param>
        /// <param name="location">The location address</param>
        /// <exception cref="OutOfMemoryException"></exception>
        public static void WriteLocation(this FBMRequest request, ReadOnlySpan<char> location)
        {
            request.WriteHeader(HeaderCommand.Location, location);
        }

        /// <summary>
        /// Writes the location header of the requested resource
        /// </summary>
        /// <param name="request"></param>
        /// <param name="location">The location address</param>
        /// <exception cref="OutOfMemoryException"></exception>
        public static void WriteLocation(this FBMRequest request, Uri location)
        {
            request.WriteHeader(HeaderCommand.Location, location.ToString());
        }

        /// <summary>
        /// If the <see cref="FBMResponse.IsSet"/> property is false, raises an <see cref="InvalidResponseException"/>
        /// </summary>
        /// <param name="response"></param>
        /// <exception cref="InvalidResponseException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNotSet(this in FBMResponse response)
        {
            if (!response.IsSet)
            {
                throw new InvalidResponseException("The response state is undefined (no response received)");
            }

            //Also throw if buffer header buffer size was too small
            if(response.StatusFlags == HeaderParseError.HeaderOutOfMem)
            {
                throw new InternalBufferTooSmallException("The internal header buffer was too small to store response headers");
            }
        }
    }
}
