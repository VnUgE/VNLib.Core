/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpEncodedSegment.cs 
*
* HttpEncodedSegment.cs is part of VNLib.Net.Http which is part of 
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

using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// Holds a pre-encoded segment of data
    /// </summary>
    /// <param name="Buffer">The buffer containing the segment data</param>
    /// <param name="Offset">The offset in the buffer to begin the segment at</param>
    /// <param name="Length">The length of the segment</param>
    internal readonly record struct HttpEncodedSegment(byte[] Buffer, uint Offset, ushort Length)
    {
        /// <summary>
        /// Validates the bounds of the array so calls to <see cref="DangerousCopyTo(Span{byte}, int)"/>
        /// won't cause a buffer over/under run
        /// </summary>
        public readonly void Validate() => MemoryUtil.CheckBounds(Buffer, Offset, Length);

        /// <summary>
        /// Performs a dangerous reference based copy-to (aka memmove)
        /// </summary>
        /// <param name="output">The output buffer to write the encoded segment to</param>
        /// <param name="offset">Points to the first byte in the buffer to write to</param>
        internal readonly int DangerousCopyTo(Span<byte> output, int offset)
        {
            Debug.Assert(output.Length >= Length, "Output span was empty and could not be written to");
            Debug.Assert(offset >= 0, "Buffer underrun detected");
            Debug.Assert(offset + Length <= output.Length, "Output span was too small to hold the encoded segment");

            //Get reference of output buffer span
            return DangerousCopyTo(ref MemoryMarshal.GetReference(output), (nuint)offset);
        }

        /// <summary>
        /// Performs a dangerous reference based copy-to (aka memmove)
        /// to the supplied <see cref="IHttpBuffer"/> at the supplied offset.
        /// This operation performs bounds checks
        /// </summary>
        /// <param name="buffer">The <see cref="IHttpBuffer"/> to copy data to</param>
        /// <param name="offset">The byte offset to the first byte of the desired segment</param>
        /// <returns>The number of bytes written to the segment</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal readonly int DangerousCopyTo(IHttpBuffer buffer, int offset)
        {
            //Ensure enough space is available
            if(offset + Length <= buffer.Size)
            {
                //More efficient to get the offset ref from the buffer directly
                ref byte dst = ref buffer.DangerousGetBinRef(offset);
                return DangerousCopyTo(ref dst, destOffset: 0);
            }

            throw new ArgumentOutOfRangeException(nameof(offset), "Buffer is too small to hold the encoded segment");
        }

        private readonly int DangerousCopyTo(ref byte output, nuint destOffset)
        {
            Debug.Assert(!Unsafe.IsNullRef(ref output), "Output span was empty and could not be written to");

            //Get references of output buffer and array buffer
            ref byte src = ref MemoryMarshal.GetArrayDataReference(Buffer);

            //Call memmove with the buffer offset and desired length
            MemoryUtil.SmallMemmove(ref src, Offset, ref output, destOffset, Length);
            return Length;
        }

        /// <summary>
        /// Allocates a new <see cref="HttpEncodedSegment"/> buffer from the supplied string
        /// using the supplied encoding
        /// </summary>
        /// <param name="data">The string data to encode</param>
        /// <param name="enc">The encoder used to convert the character data to bytes</param>
        /// <returns>The initalized <see cref="HttpEncodedSegment"/> structure</returns>
        /// <exception cref="OverflowException"></exception>
        public static HttpEncodedSegment FromString(string data, Encoding enc)
        {
            byte[] encoded = enc.GetBytes(data);
            return new HttpEncodedSegment(encoded, 0, checked((ushort)encoded.Length));
        }
    }
}