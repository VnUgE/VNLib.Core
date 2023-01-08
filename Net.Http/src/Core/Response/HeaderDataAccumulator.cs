/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HeaderDataAccumulator.cs 
*
* HeaderDataAccumulator.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Text;
using System.Runtime.InteropServices;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using static VNLib.Net.Http.Core.CoreBufferHelpers;

namespace VNLib.Net.Http.Core
{
    internal partial class HttpResponse
    {

        /// <summary>
        /// Specialized data accumulator for compiling response headers
        /// </summary>
        private class HeaderDataAccumulator : IDataAccumulator<char>, IStringSerializeable, IHttpLifeCycle
        {
            private readonly int BufferSize;

            public HeaderDataAccumulator(int bufferSize)
            {
                //Calc correct char buffer size from bin buffer
                this.BufferSize = bufferSize * sizeof(char);
            }

            /*
             * May be an issue but wanted to avoid alloc 
             * if possible since this is a field in a ref
             * type
             */

            private UnsafeMemoryHandle<byte>? _handle;

            public void Advance(int count)
            {
                //Advance writer
                AccumulatedSize += count;
            }

            public void WriteLine() => this.Append(HttpHelpers.CRLF);

            public void WriteLine(ReadOnlySpan<char> data)
            {
                this.Append(data);
                WriteLine();
            }

            /*Use bin buffers and cast to char buffer*/
            private Span<char> Buffer => MemoryMarshal.Cast<byte, char>(_handle!.Value.Span);

            public int RemainingSize => Buffer.Length - AccumulatedSize;
            public Span<char> Remaining => Buffer[AccumulatedSize..];
            public Span<char> Accumulated => Buffer[..AccumulatedSize];
            public int AccumulatedSize { get; set; }

            /// <summary>
            /// Encodes the buffered data and writes it to the stream,
            /// attemts to avoid further allocation where possible
            /// </summary>
            /// <param name="enc"></param>
            /// <param name="baseStream"></param>
            public void Flush(Encoding enc, Stream baseStream)
            {
                ReadOnlySpan<char> span = Accumulated;
                //Calc the size of the binary buffer
                int byteSize = enc.GetByteCount(span);
                //See if there is enough room in the current char buffer
                if (RemainingSize < (byteSize / sizeof(char)))
                {
                    //We need to alloc a binary buffer to write data to
                    using UnsafeMemoryHandle<byte> bin = GetBinBuffer(byteSize, false);
                    //encode data
                    int encoded = enc.GetBytes(span, bin.Span);
                    //Write to stream
                    baseStream.Write(bin.Span[..encoded]);
                }
                else
                {
                    //Get bin buffer by casting remaining accumulator buffer
                    Span<byte> bin = MemoryMarshal.Cast<char, byte>(Remaining);
                    //encode data
                    int encoded = enc.GetBytes(span, bin);
                    //Write to stream
                    baseStream.Write(bin[..encoded]);
                }
                Reset();
            }

            public void Reset() => AccumulatedSize = 0;

           

            public void OnPrepare()
            {
                //Alloc buffer
                _handle = GetBinBuffer(BufferSize, false);
            }

            public void OnRelease()
            {
                _handle!.Value.Dispose();
                _handle = null;
            }

            public void OnNewRequest()
            {}

            public void OnComplete()
            {
                Reset();
            }


            ///<inheritdoc/>
            public string Compile() => Accumulated.ToString();
            ///<inheritdoc/>
            public void Compile(ref ForwardOnlyWriter<char> writer) => writer.Append(Accumulated);
            ///<inheritdoc/>
            public ERRNO Compile(in Span<char> buffer)
            {
                ForwardOnlyWriter<char> writer = new(buffer);
                Compile(ref writer);
                return writer.Written;
            }
            ///<inheritdoc/>
            public override string ToString() => Compile();
        }
    }
}