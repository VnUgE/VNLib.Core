/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ObjectRentalBase.cs 
*
* ObjectRentalBase.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory.Caching
{
    /// <summary>
    /// Provides concurrent storage for reusable objects to be rented and returned. This class
    /// and its members is thread-safe
    /// </summary>
    public abstract class ObjectRental : VnDisposeable
    {
        /// <summary>
        /// Creates a new <see cref="ObjectRental{T}"/> store
        /// </summary>
        /// <param name="quota">The maximum number of elements that will be cached</param>
        public static ObjectRental<TNew> Create<TNew>(int quota = 0) where TNew : class, new()
        {
            static TNew constructor() => new();
            return new ObjectRental<TNew>(constructor, null, null, quota);
        }
        /// <summary>
        /// Creates a new <see cref="ObjectRental{T}"/> store with generic rental and return callback handlers
        /// </summary>
        /// <param name="rentCb">Function responsible for preparing an instance to be rented</param>
        /// <param name="returnCb">Function responsible for cleaning up an instance before reuse</param>
        /// <param name="quota">The maximum number of elements that will be cached</param>
        public static ObjectRental<TNew> Create<TNew>(Action<TNew>? rentCb, Action<TNew>? returnCb, int quota = 0) where TNew : class, new()
        {
            static TNew constructor() => new();
            return new ObjectRental<TNew>(constructor, rentCb, returnCb, quota);
        }
        /// <summary>
        /// Creates a new <see cref="ObjectRental{T}"/> store with a generic constructor function
        /// </summary>
        /// <param name="constructor">The function invoked to create a new instance when required</param>
        /// <param name="quota">The maximum number of elements that will be cached</param>
        /// <returns></returns>
        public static ObjectRental<TNew> Create<TNew>(Func<TNew> constructor, int quota = 0) where TNew: class
        {
            return new ObjectRental<TNew>(constructor, null, null, quota);
        }
        /// <summary>
        /// Creates a new <see cref="ObjectRental{T}"/> store with generic rental and return callback handlers
        /// </summary>
        /// <param name="constructor">The function invoked to create a new instance when required</param>
        /// <param name="rentCb">Function responsible for preparing an instance to be rented</param>
        /// <param name="returnCb">Function responsible for cleaning up an instance before reuse</param>
        /// <param name="quota">The maximum number of elements that will be cached</param>
        public static ObjectRental<TNew> Create<TNew>(Func<TNew> constructor, Action<TNew>? rentCb, Action<TNew>? returnCb, int quota = 0) where TNew : class
        {
            return new ObjectRental<TNew>(constructor, rentCb, returnCb, quota);
        }

        /// <summary>
        /// Creates a new <see cref="ThreadLocalObjectStorage{TNew}"/> store with generic rental and return callback handlers
        /// </summary>
        /// <typeparam name="TNew"></typeparam>
        /// <param name="constructor">The function invoked to create a new instance when required</param>
        /// <param name="rentCb">Function responsible for preparing an instance to be rented</param>
        /// <param name="returnCb">Function responsible for cleaning up an instance before reuse</param>
        /// <returns>The initialized store</returns>
        public static ThreadLocalObjectStorage<TNew> CreateThreadLocal<TNew>(Func<TNew> constructor, Action<TNew>? rentCb, Action<TNew>? returnCb) where TNew : class
        {
            return new ThreadLocalObjectStorage<TNew>(constructor, rentCb, returnCb);
        }
        /// <summary>
        /// Creates a new <see cref="ThreadLocalObjectStorage{T}"/> store with generic rental and return callback handlers
        /// </summary>
        /// <param name="rentCb">Function responsible for preparing an instance to be rented</param>
        /// <param name="returnCb">Function responsible for cleaning up an instance before reuse</param>
        public static ThreadLocalObjectStorage<TNew> CreateThreadLocal<TNew>(Action<TNew>? rentCb, Action<TNew>? returnCb) where TNew : class, new()
        {
            static TNew constructor() => new();
            return new ThreadLocalObjectStorage<TNew>(constructor, rentCb, returnCb);
        }
        /// <summary>
        /// Creates a new <see cref="ThreadLocalObjectStorage{T}"/> store
        /// </summary>
        public static ThreadLocalObjectStorage<TNew> CreateThreadLocal<TNew>() where TNew : class, new()
        {
            static TNew constructor() => new();
            return new ThreadLocalObjectStorage<TNew>(constructor, null, null);
        }
        /// <summary>
        /// Creates a new <see cref="ThreadLocalObjectStorage{T}"/> store with a generic constructor function
        /// </summary>
        /// <param name="constructor">The function invoked to create a new instance when required</param>
        /// <returns></returns>
        public static ThreadLocalObjectStorage<TNew> CreateThreadLocal<TNew>(Func<TNew> constructor) where TNew : class
        {
            return new ThreadLocalObjectStorage<TNew>(constructor, null, null);
        }

        /// <summary>
        /// Creates a new <see cref="ReusableStore{T}"/> instance with a parameterless constructor
        /// </summary>
        /// <typeparam name="T">The <see cref="IReusable"/> type</typeparam>
        /// <param name="quota">The maximum number of elements that will be cached</param>
        /// <returns></returns>
        public static ReusableStore<T> CreateReusable<T>(int quota = 0) where T : class, IReusable, new()
        {
            static T constructor() => new();
            return new(constructor, quota);
        }
        /// <summary>
        /// Creates a new <see cref="ReusableStore{T}"/> instance with the specified constructor
        /// </summary>
        /// <typeparam name="T">The <see cref="IReusable"/> type</typeparam>
        /// <param name="constructor">The constructor function invoked to create new instances of the <see cref="IReusable"/> type</param>
        /// <param name="quota">The maximum number of elements that will be cached</param>
        /// <returns></returns>
        public static ReusableStore<T> CreateReusable<T>(Func<T> constructor, int quota = 0) where T : class, IReusable => new(constructor, quota);

        /// <summary>
        /// Creates a new <see cref="ThreadLocalReusableStore{T}"/> instance with a parameterless constructor
        /// </summary>
        /// <typeparam name="T">The <see cref="IReusable"/> type</typeparam>
        /// <returns></returns>
        public static ThreadLocalReusableStore<T> CreateThreadLocalReusable<T>() where T : class, IReusable, new()
        {
            static T constructor() => new();
            return new(constructor);
        }
        /// <summary>
        /// Creates a new <see cref="ThreadLocalReusableStore{T}"/> instance with the specified constructor
        /// </summary>
        /// <typeparam name="T">The <see cref="IReusable"/> type</typeparam>
        /// <param name="constructor">The constructor function invoked to create new instances of the <see cref="IReusable"/> type</param>
        /// <returns></returns>
        public static ThreadLocalReusableStore<T> CreateThreadLocalReusable<T>(Func<T> constructor) where T : class, IReusable => new(constructor);
    }
}
