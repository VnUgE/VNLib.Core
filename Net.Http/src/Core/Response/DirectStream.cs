/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: DirectStream.cs 
*
* DirectStream.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Net.Http.Core
{
    internal partial class HttpResponse
    {
        private class DirectStream : Stream
        {
            private Stream? BaseStream;

            public void Prepare(Stream transport)
            {
                BaseStream = transport;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                BaseStream!.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                BaseStream!.Write(buffer);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return BaseStream!.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return BaseStream!.WriteAsync(buffer, cancellationToken);
            }
         

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new InvalidOperationException("Stream does not have a length property");
            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Stream does not support reading");
            public override long Seek(long offset, SeekOrigin origin) => throw new InvalidOperationException("Stream does not support seeking");
            public override void SetLength(long value) => throw new InvalidOperationException("Stream does not support seeking");

            public override void Flush() => BaseStream!.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => BaseStream!.FlushAsync(cancellationToken);


            public override void Close()
            {
                BaseStream = null;
            }


            protected override void Dispose(bool disposing)
            {
                //Do not call base dispose
                Close();
            }

            public override ValueTask DisposeAsync()
            {
                Close();
                return ValueTask.CompletedTask;
            }
        }
    }
}