/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: HeaderDataAccumulator.cs 
*
* HeaderDataAccumulator.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

using VNLib.Utils.IO;


namespace VNLib.Net.Messaging.FBM.Server
{

    /// <summary>
    /// Reusable sliding window impl
    /// </summary>
    internal sealed class HeaderDataAccumulator(int bufferSize, IFBMMemoryManager memManager) 
        : ISlindingWindowBuffer<byte>
    {

        //Initlaizes a new handle but does not allocate usable memory yet
        private readonly IFBMMemoryHandle _handle = memManager.InitHandle();

        ///<inheritdoc/>
        public int WindowStartPos { get; private set; }

        ///<inheritdoc/>
        public int WindowEndPos { get; private set; }

        ///<inheritdoc/>
        public Memory<byte> Buffer => _handle.GetMemory();

        ///<inheritdoc/>
        public void Advance(int count) 
            => WindowEndPos += count;
        
        ///<inheritdoc/>
        public void AdvanceStart(int count) 
            => WindowEndPos += count;
        
        ///<inheritdoc/>
        public void Reset()
        {
            WindowStartPos = 0;
            WindowEndPos = 0;
        }

        /// <summary>
        /// Allocates the internal message buffer
        /// </summary>
        public void Prepare() 
            => memManager.AllocBuffer(_handle, bufferSize);

        ///<inheritdoc/>
        public void Close()
        {
            Reset();
            memManager.FreeBuffer(_handle);
        }
    }
    
}
