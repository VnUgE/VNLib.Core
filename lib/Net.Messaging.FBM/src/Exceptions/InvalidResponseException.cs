/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: InvalidResponseException.cs 
*
* InvalidResponseException.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Runtime.Serialization;

namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// Raised when a response to an FBM request is not in a valid state
    /// </summary>
    public class InvalidResponseException : FBMException
    {
        ///<inheritdoc/>
        public InvalidResponseException()
        {
        }
        ///<inheritdoc/>
        public InvalidResponseException(string message) : base(message)
        {
        }
        ///<inheritdoc/>
        public InvalidResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
        ///<inheritdoc/>
        protected InvalidResponseException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
