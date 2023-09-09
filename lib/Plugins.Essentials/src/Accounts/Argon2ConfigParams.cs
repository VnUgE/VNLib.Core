/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: Argon2ConfigParams.cs 
*
* Argon2ConfigParams.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// The configuration parameters for the Argon2 hashing algorithm
    /// </summary>
    public readonly record struct Argon2ConfigParams
    {

        /// <summary>
        /// Initializes a new <see cref="Argon2ConfigParams"/> instance with the default values
        /// </summary>
        public Argon2ConfigParams()
        { }

        /// <summary>
        /// The length of the random salt to use in bytes (defaults to 32)
        /// </summary>
        public int SaltLen { get; init; } = 32;

        /// <summary>
        /// The Argon2 time cost parameter (defaults to 4)
        /// </summary>
        public uint TimeCost { get; init; } = 4;

        /// <summary>
        /// The Argon2 memory cost parameter (defaults to UInt16.MaxValue)
        /// </summary>
        public uint MemoryCost { get; init; } = UInt16.MaxValue;

        /// <summary>
        /// The Argon2 default hash length parameter (defaults to 128)
        /// </summary>
        public uint HashLen { get; init; } = 128;

        /// <summary>
        /// The Argon2 parallelism parameter (defaults to the number of logical processors)
        /// </summary>
        public uint Parallelism { get; init; } = (uint)Environment.ProcessorCount;
    }
}