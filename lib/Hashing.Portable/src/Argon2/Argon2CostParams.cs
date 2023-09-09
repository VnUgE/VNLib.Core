/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: Argon2CostParams.cs 
*
* Argon2CostParams.cs is part of VNLib.Hashing.Portable which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.Portable is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.Portable is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.Portable. If not, see http://www.gnu.org/licenses/.
*/

namespace VNLib.Hashing
{
    /// <summary>
    /// Stores Argon2 hashing cost parameters
    /// </summary>
    public readonly ref struct Argon2CostParams
    {
        /// <summary>
        /// Initializes a new structure of <see cref="Argon2CostParams"/> with the specified parameters
        /// </summary>
        public Argon2CostParams()
        { }

        /// <summary>
        /// Argon2 hash time cost parameter
        /// </summary>
        public readonly uint TimeCost { get; init; } = 2;

        /// <summary>
        /// Argon2 hash memory cost parameter
        /// </summary>
        public readonly uint MemoryCost { get; init; } = 65535;

        /// <summary>
        /// Argon2 hash parallelism parameter
        /// </summary>
        public readonly uint Parallelism { get; init; } = 4;
    }
}