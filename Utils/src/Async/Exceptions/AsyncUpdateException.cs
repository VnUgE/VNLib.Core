/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AsyncUpdateException.cs 
*
* AsyncUpdateException.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Resources;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Represents an exception that was raised during an asyncronous update of a resource. The <see cref="Exception.InnerException"/> stores the 
    /// details of the actual exception raised
    /// </summary>
    public sealed class AsyncUpdateException : ResourceUpdateFailedException
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="inner"></param>
        public AsyncUpdateException(Exception inner) : base("", inner) { }

        public AsyncUpdateException()
        {}

        public AsyncUpdateException(string message) : base(message)
        {}

        public AsyncUpdateException(string message, Exception innerException) : base(message, innerException)
        {}
    }
}