﻿/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ReusableResponseStream.cs 
*
* ReusableResponseStream.cs is part of VNLib.Net.Http which is part 
* of the larger VNLib collection of libraries and utilities.
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

using System.IO;

namespace VNLib.Net.Http.Core.Response
{

    internal abstract class ReusableResponseStream
    {
        protected Stream? transport;

        /// <summary>
        /// Called when a new connection is established
        /// </summary>
        /// <param name="transport"></param>
        public virtual void OnNewConnection(Stream transport) => this.transport = transport;

        /// <summary>
        /// Called when the connection is released
        /// </summary>
        public virtual void OnRelease() => transport = null;

    }
}