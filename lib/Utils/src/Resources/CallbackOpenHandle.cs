/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: CallbackOpenHandle.cs 
*
* CallbackOpenHandle.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Utils.Resources
{
    /// <summary>
    /// A concrete <see cref="OpenHandle"/> for a defered operation or a resource that should be released or unwound
    /// when the instance lifetime has ended.
    /// </summary>
    public sealed class CallbackOpenHandle : OpenHandle
    {
        private readonly Action ReleaseFunc;
        /// <summary>
        /// Creates a new generic <see cref="OpenHandle"/> with the specified release callback method
        /// </summary>
        /// <param name="release">The callback function to invoke when the <see cref="OpenHandle"/> is disposed</param>
        public CallbackOpenHandle(Action release) => ReleaseFunc = release;
        ///<inheritdoc/>
        protected override void Free() => ReleaseFunc();
    }
}