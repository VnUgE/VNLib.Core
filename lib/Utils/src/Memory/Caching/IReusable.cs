/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IReusable.cs 
*
* IReusable.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory.Caching
{
    /// <summary>
    /// An interface that exposes event hooks for use within an <see cref="ObjectRental{T}"/>, managment.
    /// </summary>
    public interface IReusable
    {
        /// <summary>
        /// The instance should prepare itself for use (or re-use)
        /// <para>
        /// This method is guarunteed to be called directly after a constructor 
        /// when a new instance is allocated and before it is ever returned to a
        /// caller.
        /// </para>
        /// </summary>
        void Prepare();

        /// <summary>
        /// The intance is being returned and should determine if it's state is reusabled
        /// </summary>
        /// <returns>true if the instance can/should be reused, false if it should not be reused</returns>
        bool Release();
    }
}