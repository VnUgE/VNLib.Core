/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ExclusiveResourceHandle.cs 
*
* ExclusiveResourceHandle.cs is part of VNLib.Utils which is part of the larger 
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
    /// While in scope, holds an exclusive lock on the specified object that implements the <see cref="IExclusiveResource"/> interface
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ExclusiveResourceHandle<T> : OpenResourceHandle<T> where T : IExclusiveResource
    {
        private readonly Action Release;
        private readonly Lazy<T> LazyVal;

        /// <summary>
        /// <inheritdoc/>
        /// <br></br>
        /// <br></br>
        /// This value is lazy inialized and will invoke the factory function on first access.
        /// Accessing this variable is thread safe while the handle is in scope
        /// <br></br>
        /// <br></br>
        /// Exceptions will be propagated during initialziation
        /// </summary>
        public override T Resource => LazyVal.Value;

        /// <summary>
        /// Creates a new <see cref="ExclusiveResourceHandle{TResource}"/> wrapping the 
        /// <see cref="IExclusiveResource"/> object to manage its lifecycle and reuse
        /// </summary>
        /// <param name="factory">Factory function that will generate the value when used</param>
        /// <param name="release">Callback function that will be invoked after object gets disposed</param>
        internal ExclusiveResourceHandle(Func<T> factory, Action release)
        {
            //Store the release action
            Release = release;
            //Store the new lazy val from the factory function (enabled thread safey)
            LazyVal = new(factory, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        protected override void Free()
        {
            try
            {
                //Dispose the value if it was created, otherwise do not create it
                if (LazyVal.IsValueCreated)
                {
                    Resource?.Release();
                }
            }
            finally
            {
                //Always invoke the release callback 
                Release();
            }
        }
    }
}