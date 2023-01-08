/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpRequestBody.cs 
*
* HttpRequestBody.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Collections.Generic;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// Represents a higher-level request entity body (query arguments, request body etc)
    /// that has been parsed and captured
    /// </summary>
    internal class HttpRequestBody
    {
        public readonly List<FileUpload> Uploads;
        public readonly Dictionary<string, string> RequestArgs;
        public readonly Dictionary<string, string> QueryArgs;

        public HttpRequestBody()
        {
            Uploads = new(1);

            //Request/query args should not be request sensitive
            RequestArgs = new(StringComparer.OrdinalIgnoreCase);
            QueryArgs = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Releases all resources used by the current instance
        /// </summary>
        public void OnComplete()
        {
            //Only enumerate/clear if file uplaods are present
            if (Uploads.Count > 0)
            {
                //Dispose all initialized files 
                for (int i = 0; i < Uploads.Count; i++)
                {
                    Uploads[i].Free();
                }
                //Emtpy list
                Uploads.Clear();
            }
            //Clear request args and file uplaods
            RequestArgs.Clear();
            QueryArgs.Clear();
        }
    }
}