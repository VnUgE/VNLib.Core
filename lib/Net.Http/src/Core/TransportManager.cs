/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: TransportManager.cs 
*
* TransportManager.cs is part of VNLib.Net.Http which is part of 
* the larger VNLib collection of libraries and utilities.
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
using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VNLib.Net.Http.Core
{
    internal sealed class TransportManager
    {
        public bool IsBufferWriter;

#nullable disable

#if DEBUG

        private Stream _stream;
        private IBufferWriter<byte> _asWriter;

        public Stream Stream
        {
            get
            {
                Debug.Assert(_stream != null, "Transport stream was accessed but was set to null");
                return _stream;
            }
            private set => _stream = value;
        }

        public IBufferWriter<byte> Writer
        {
            get
            {
                Debug.Assert(_asWriter != null, "Transport buffer writer accessed but the writer is null");
                return _asWriter;
            }
            private set => _asWriter = value;
        }

#else
        public Stream Stream;
        public IBufferWriter<byte> Writer;
#endif

#nullable restore

        public Task FlushAsync() => Stream.FlushAsync();

        /// <summary>
        /// Assigns a new transport stream to the wrapper
        /// as a new connection is assigned to write responses to
        /// </summary>
        /// <param name="transportStream">The transport stream to wrap</param>
        public void OnNewConnection(Stream transportStream)
        {
            Stream = transportStream;

            //Capture a buffer writer if the incoming stream supports direct writing
            if (transportStream is IBufferWriter<byte> bw)
            {
                Writer = bw;
                IsBufferWriter = true;
            }
        }

        /// <summary>
        /// Closes the current connection and resets the transport stream
        /// </summary>
        public void OnRelease()
        {
            Stream = null;
            Writer = null;
            IsBufferWriter = false;
        }
    }
}