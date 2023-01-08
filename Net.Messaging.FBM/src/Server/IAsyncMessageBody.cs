/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IAsyncMessageBody.cs 
*
* IAsyncMessageBody.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http;

namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// A disposable message body container for asynchronously reading a variable length message body
    /// </summary>
    public interface IAsyncMessageBody : IAsyncDisposable
    {
        /// <summary>
        /// The message body content type
        /// </summary>
        ContentType ContentType { get; }

        /// <summary>
        /// The number of bytes remaining to be read from the message body
        /// </summary>
        int RemainingSize { get; }

        /// <summary>
        /// Reads the next chunk of data from the message body
        /// </summary>
        /// <param name="buffer">The buffer to copy output data to</param>
        /// <param name="token">A token to cancel the operation</param>
        /// <returns></returns>
        ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default);
    }
    
}
