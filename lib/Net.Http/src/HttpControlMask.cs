/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpControlMask.cs 
*
* HttpControlMask.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// Contains HttpServer related function masks for altering http server
    /// behavior
    /// </summary>
    public static class HttpControlMask
    {
        /// <summary>
        /// Tells the http server that dynamic response compression should be disabled
        /// </summary>
        public const ulong CompressionDisabled = 0x01UL;

        /// <summary>
        /// Tells the server not to set a 0 content length header when sending a response that does 
        /// not have an entity body to send. 
        /// </summary>
        public const ulong ImplictContentLengthDisabled = 0x02UL;

        /// <summary>
        /// Tells the server to disable keep alive for the connection after the response is sent.
        /// </summary>
        public const ulong KeepAliveDisabled = 0x04UL;
    }
}