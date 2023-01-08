/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: TerminateConnectionException.cs 
*
* TerminateConnectionException.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Net;

namespace VNLib.Net.Http
{
    /// <summary>
    /// User code may throw this exception to signal the <see cref="HttpServer"/> to drop
    /// the transport connection and return an optional status code 
    /// </summary>
    public class TerminateConnectionException : Exception
    {
        internal HttpStatusCode Code { get; }

        /// <summary>
        /// Creates a new instance that terminates the connection without sending a response to the connection
        /// </summary>
        public TerminateConnectionException() : base(){}
        /// <summary>
        /// Creates a new instance of the connection exception with an error code to respond to the connection with
        /// </summary>
        /// <param name="responseCode">The status code to send to the user</param>
        public TerminateConnectionException(HttpStatusCode responseCode)
        {
            this.Code = responseCode;
        }

        public TerminateConnectionException(string message) : base(message)
        {}

        public TerminateConnectionException(string message, Exception innerException) : base(message, innerException)
        {}
    }
}