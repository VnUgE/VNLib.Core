/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: SplitHttpBufferElement.cs 
*
* SplitHttpBufferElement.cs is part of VNLib.Net.Http which is 
* part of the larger VNLib collection of libraries and utilities.
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core.Buffering
{
    internal abstract class SplitHttpBufferElement : HttpBufferElement, ISplitHttpBuffer
    {
        ///<inheritdoc/>
        public int BinSize { get; }

        internal SplitHttpBufferElement(int binSize)
        {
            BinSize = binSize;
        }

        ///<inheritdoc/>
        public Span<char> GetCharSpan()
        {
            //Get full buffer span
            Span<byte> _base = base.GetBinSpan();

            //Upshift to end of bin buffer
            _base = _base[BinSize..];

            //Return char span
            return MemoryMarshal.Cast<byte, char>(_base);
        }

        /*
         * Override to trim the bin buffer to the actual size of the 
         * binary segment of the buffer
         */
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Span<byte> GetBinSpan() => base.GetBinSpan(BinSize);


        /// <summary>
        /// Gets the size total of the buffer required for binary data and char data
        /// </summary>
        /// <param name="binSize">The desired size of the binary buffer</param>
        /// <returns>The total size of the binary buffer required to store the binary and character buffer</returns>
        public static int GetfullSize(int binSize) => binSize + MemoryUtil.ByteCount<char>(binSize);
    }
}
