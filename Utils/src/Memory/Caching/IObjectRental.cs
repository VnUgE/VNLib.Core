/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IObjectRental.cs 
*
* IObjectRental.cs is part of VNLib.Utils which is part of the larger 
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
    /// A thread safe store for reusing CLR managed objects
    /// </summary>
    /// <typeparam name="T">The reusable object class</typeparam>
    public interface IObjectRental<T> where T: class
    {
        /// <summary>
        /// Gets an object from the store, or creates a new one if none are available
        /// </summary>
        /// <returns>An instance of <typeparamref name="T"/> from the store if available or a new instance if none were available</returns>
        T Rent();

        /// <summary>
        /// Returns a rented object back to the rental store for reuse
        /// </summary>
        /// <param name="item">The previously rented item</param>
        void Return(T item);
    }
   
}