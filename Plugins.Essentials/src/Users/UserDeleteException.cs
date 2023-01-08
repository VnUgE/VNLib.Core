/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: UserDeleteException.cs 
*
* UserDeleteException.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using VNLib.Utils.Resources;

namespace VNLib.Plugins.Essentials.Users
{
    /// <summary>
    /// Raised when a user flagged for deletion could not be deleted. See the <see cref="Exception.InnerException"/> 
    /// for the Exception that cause the opertion to fail
    /// </summary>
    public class UserDeleteException : ResourceDeleteFailedException
    {
        public UserDeleteException(string message, Exception cause) : base(message, cause) { }

        public UserDeleteException()
        {}

        public UserDeleteException(string message) : base(message)
        {}
    }
}