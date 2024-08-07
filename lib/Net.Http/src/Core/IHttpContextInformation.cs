﻿/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpContextInformation.cs 
*
* IHttpContextInformation.cs is part of VNLib.Net.Http which is part of the larger 
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

using System.Text;

namespace VNLib.Net.Http.Core
{
    internal interface IHttpContextInformation
    {
        /// <summary>
        /// Gets a reference to the <see cref="HttpEncodedSegment"/> containing the CRLF sequence
        /// </summary>
        ref readonly HttpEncodedSegment CrlfSegment { get; }

        /// <summary>
        /// Gets a reference to the <see cref="HttpEncodedSegment"/> containing the final chunk sequence
        /// </summary>
        ref readonly HttpEncodedSegment FinalChunkSegment { get; }

        /// <summary>
        /// The current connection's encoding
        /// </summary>
        Encoding Encoding { get; }

        /// <summary>
        /// The current connection's http version
        /// </summary>
        HttpVersion CurrentVersion { get; }
    }
}