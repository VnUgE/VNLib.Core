/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMListenerSessionParams.cs 
*
* FBMListenerSessionParams.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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

using System.Text;

namespace VNLib.Net.Messaging.FBM.Server
{
    /// <summary>
    /// Represents a configuration structure for an <see cref="FBMListener"/>
    /// listening session
    /// </summary>
    public readonly struct FBMListenerSessionParams
    {
        /// <summary>
        /// The size of the buffer to use while reading data from the websocket
        /// in the listener loop
        /// </summary>
        public readonly int RecvBufferSize { get; init; }
        /// <summary>
        /// The size of the buffer to store <see cref="FBMMessageHeader"/> values in 
        /// the <see cref="FBMRequestMessage"/>
        /// </summary>
        public readonly int MaxHeaderBufferSize { get; init; }
        /// <summary>
        /// The size of the internal message response buffer when
        /// not streaming
        /// </summary>
        public readonly int ResponseBufferSize { get; init; }        
        /// <summary>
        /// The FMB message header character encoding
        /// </summary>
        public readonly Encoding HeaderEncoding { get; init; }

        /// <summary>
        /// The absolute maxium size (in bytes) message to process before
        /// closing the websocket connection. This value should be negotiaed 
        /// by clients or hard-coded to avoid connection issues
        /// </summary>
        public readonly int MaxMessageSize { get; init; }
    }
}
