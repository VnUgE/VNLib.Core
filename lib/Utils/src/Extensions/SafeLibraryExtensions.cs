/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SafeLibraryExtensions.cs 
*
* SafeLibraryExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.Reflection;

using VNLib.Utils.Native;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// When applied to a delegate, specifies the name of the native method to load
    /// </summary>
    [AttributeUsage(AttributeTargets.Delegate)]
    public sealed class SafeMethodNameAttribute : Attribute
    {
        /// <summary>
        /// Creates a new <see cref="SafeMethodNameAttribute"/>
        /// </summary>
        /// <param name="MethodName">The name of the native method</param>
        public SafeMethodNameAttribute(string MethodName) => this.MethodName = MethodName;
        /// <summary>
        /// Creates a new <see cref="SafeMethodNameAttribute"/>, that uses the 
        /// delegate name as the native method name
        /// </summary>
        public SafeMethodNameAttribute() => MethodName = null;
        /// <summary>
        /// The name of the native method
        /// </summary>
        public string? MethodName { get; }
    }
    

    /// <summary>
    /// Contains native library extension methods
    /// </summary>
    public static class SafeLibraryExtensions
    {
        const string _missMemberExceptionMessage = $"The delegate type is missing the required {nameof(SafeMethodNameAttribute)} to designate the native method to load";

        /// <summary>
        /// Loads a native method from the current <see cref="SafeLibraryHandle"/>
        /// that has a <see cref="SafeMethodNameAttribute"/> 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="library"></param>
        /// <returns></returns>
        /// <exception cref="MissingMemberException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        [Obsolete("Use GetFunction<T>() extension instead")]
        public static SafeMethodHandle<T> GetMethod<T>(this SafeLibraryHandle library) where T : Delegate
         => GetFunction<T>(library);

        /// <summary>
        /// Loads a native function from the current <see cref="SafeLibraryHandle"/>
        /// that has a <see cref="SafeMethodNameAttribute"/> 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="library"></param>
        /// <returns></returns>
        /// <exception cref="MissingMemberException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        public static SafeMethodHandle<T> GetFunction<T>(this SafeLibraryHandle library) where T : Delegate
        {
            ArgumentNullException.ThrowIfNull(library);

            Type t = typeof(T);
            //Get the method name attribute
            SafeMethodNameAttribute? attr = t.GetCustomAttribute<SafeMethodNameAttribute>();
            _ = attr ?? throw new MissingMemberException(_missMemberExceptionMessage);
            return library.GetFunction<T>(attr.MethodName ?? t.Name);
        }

        /// <summary>
        /// Loads a native method from the current <see cref="SafeLibraryHandle"/>
        /// that has a <see cref="SafeMethodNameAttribute"/> 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="library"></param>
        /// <returns></returns>
        /// <exception cref="MissingMemberException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <remarks>
        /// The libraries handle count is left unmodified
        /// </remarks>
        [Obsolete("Use DangerousGetFunction<T>() extension instead")]
        public static T DangerousGetMethod<T>(this SafeLibraryHandle library) where T: Delegate
            => DangerousGetFunction<T>(library);

        /// <summary>
        /// Loads a native method from the current <see cref="SafeLibraryHandle"/>
        /// that has a <see cref="SafeMethodNameAttribute"/> 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="library"></param>
        /// <returns></returns>
        /// <exception cref="MissingMemberException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <remarks>
        /// The libraries handle count is left unmodified
        /// </remarks>
        public static T DangerousGetFunction<T>(this SafeLibraryHandle library) where T : Delegate
        {
            ArgumentNullException.ThrowIfNull(library);

            Type t = typeof(T);
            //Get the method name attribute
            SafeMethodNameAttribute? attr = t.GetCustomAttribute<SafeMethodNameAttribute>();
            return string.IsNullOrWhiteSpace(attr?.MethodName)
                ? throw new MissingMemberException(_missMemberExceptionMessage)
                : library.DangerousGetFunction<T>(attr.MethodName);
        }
    }
}
