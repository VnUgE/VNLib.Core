/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: CompressionOperation.cs 
*
* CompressorManager.cs is part of VNLib.Net.Compression which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Compression is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Compression is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Compression. If not, see http://www.gnu.org/licenses/.
*/

using System.Runtime.InteropServices;

namespace VNLib.Net.Compression
{
    /// <summary>
    /// Matches the native compression operation struct
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe ref struct CompressionOperation
    {
        #region readonly

        /// <summary>
        /// A pointer to the input buffer
        /// </summary>
        public void* inputBuffer;

        /// <summary>
        /// A pointer to the output buffer
        /// </summary>
        public void* outputBuffer;

        /// <summary>
        /// A value that indicates a flush operation, 0 for no flush, above 0 for flush
        /// </summary>
        public int flush;
       
        /// <summary>
        /// The size of the input buffer
        /// </summary>
        public uint inputSize;
        
        /// <summary>
        /// The size of the output buffer
        /// </summary>
        public uint outputSize;
        
        #endregion

        /// <summary>
        /// An output variable, the number of bytes read from the input buffer
        /// </summary>
        public uint bytesRead;

        /// <summary>
        /// An output variable, the number of bytes written to the output buffer
        /// </summary>
        public uint bytesWritten;
    }
}