/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: AlternateProtocolTransportStreamWrapper.cs 
*
* AlternateProtocolTransportStreamWrapper.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.IO;
using System.Threading.Tasks;

using VNLib.Utils.IO;

#pragma warning disable CA2215 // Dispose methods should call base class dispose

namespace VNLib.Net.Http.Core
{
    internal sealed class AlternateProtocolTransportStreamWrapper : BackingStream<Stream>
    {
        public AlternateProtocolTransportStreamWrapper(Stream transport)
        {
            this.BaseStream = transport;
        }

        //Do not allow the caller to dispose the transport stream

        protected override void Dispose(bool disposing)
        { }
        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
        public override void Close()
        {}
    }
}
