/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: LazyInitializer.cs 
*
* LazyInitializer.cs is part of VNLib.Utils which is part of the larger 
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
using System.Threading;
using System.Diagnostics;

namespace VNLib.Utils.Resources
{
    /// <summary>
    /// A lazy initializer that creates a single instance of a type
    /// and shares it across all threads. This class simply guarantees
    /// that the instance is only created once and shared across all
    /// threads as efficiently as possible for long running processes.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="initalizer">The callback function that initializes the instance</param>
    public sealed class LazyInitializer<T>(Func<T> initalizer)
    {
        private readonly object _lock = new();
        private readonly Func<T> initalizer = initalizer ?? throw new ArgumentNullException(nameof(initalizer));

        private T? _instance;
        private bool _isLoaded;      

        /// <summary>
        /// A value indicating if the instance has ben loaded
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// Gets or creates the instance only once and returns 
        /// the shared instance
        /// </summary>
        /// <remarks>
        /// NOTE: 
        /// Accessing this property may block the calling thread
        /// if the instance has not yet been loaded. Only one thread
        /// will create the instance, all other threads will wait
        /// for the instance to be created.
        /// </remarks>
        public T Instance
        {
            get
            {
                //See if instance is already loaded (this read is atomic in .NET)
                if (_isLoaded)
                {
                    return _instance!;
                }

                /*
                 * Instance has not yet been loaded. Only one thread
                 * must load the object, all other threads must wait
                 * for the object to be loaded.
                 */

                if (Monitor.TryEnter(_lock, 0))
                {
                    try
                    {
                        /*
                         * Lock was entered without waiting (lock was available), this will now be
                         * the thread that invokes the load function                      
                         */

                        _instance = initalizer();

                        //Finally set the load state
                        _isLoaded = true;
                    }
                    finally
                    {
                        Monitor.Exit(_lock);
                    }
                }
                else
                {
                    //wait for lock to be released, when it is, the object should be loaded
                    Monitor.Enter(_lock);
                    Monitor.Exit(_lock);

                    //object instance should now be available to non-creating threads
                    Debug.Assert(_isLoaded);
                }

                return _instance!;
            }
        }
    }
}