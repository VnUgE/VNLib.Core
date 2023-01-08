/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnDisposeable.cs 
*
* VnDisposeable.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.CompilerServices;

namespace VNLib.Utils
{
    /// <summary>
    /// Provides a base class with abstract methods for for disposable objects, with disposed check method
    /// </summary>
    public abstract class VnDisposeable : IDisposable
    {
        ///<inheritdoc/>
        protected bool Disposed { get; private set; }

        /// <summary>
        /// When overriden in a child class, is responsible for freeing resources
        /// </summary>
        protected abstract void Free();

        /// <summary>
        /// Checks if the current object has been disposed. Method will be inlined where possible
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void Check()
        {
            if (Disposed)
            {
                throw new ObjectDisposedException("Object has been disposed");
            }
        }

        /// <summary>
        /// Sets the internal state to diposed without calling <see cref="Free"/> operation.
        /// Usefull if another code-path performs the free operation independant of a dispose opreation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetDisposedState() => Disposed = true;
        ///<inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    //Call free method
                    Free();
                }
                Disposed = true;
            }
        }  
        //Finalizer is not needed here

        ///<inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}