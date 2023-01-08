/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMInvalidRequestException.cs 
*
* FBMInvalidRequestException.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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


namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// Raised when a request message is not in a valid state and cannot be sent
    /// </summary>
    public class FBMInvalidRequestException : FBMException
    {
        public FBMInvalidRequestException()
        {
        }

        public FBMInvalidRequestException(string message) : base(message)
        {
        }

        public FBMInvalidRequestException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
