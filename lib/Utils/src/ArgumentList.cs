/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ArgumentList.cs 
*
* ArgumentList.cs is part of VNLib.Utils which is part of the larger 
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
using System.Linq;
using System.Collections.Generic;

namespace VNLib.Utils
{
    /// <summary>
    /// Provides methods for parsing an argument list
    /// </summary>
    public class ArgumentList : IIndexable<int, string>
    {
        private readonly List<string> _args;

        /// <summary>
        /// Initalzies a the argument parser by copying the given argument array
        /// </summary>
        /// <param name="args">The array of arguments to clone</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ArgumentList(string[] args)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            _args = args.ToList();
        }

        /// <summary>
        /// Initalizes the argument parser by copying the given argument list
        /// </summary>
        /// <param name="args">The argument list to clone</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ArgumentList(IReadOnlyList<string> args)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            _args = args.ToList();
        }

        /// <summary>
        /// Gets the number of arguments in the list
        /// </summary>
        public int Count => _args.Count;

        ///<inheritdoc/>
        public string this[int key]
        {
            get => _args[key]; 
            set => _args[key] = value; 
        }

        /// <summary>
        /// Determines of the given argument is present in the argument list
        /// </summary>
        /// <param name="arg"></param>
        /// <returns>A value that indicates if the argument is present in the list</returns>
        public bool HasArgument(string arg) => _args.Contains(arg);

        /// <summary>
        /// Determines if the argument is present in the argument list and 
        /// has a non-null value following it.
        /// </summary>
        /// <param name="arg">The argument name to test</param>
        /// <returns>A value that indicates if a non-null argument is present in the list</returns>
        public bool HasArgumentValue(string arg) => GetArgument(arg) != null;

        /// <summary>
        /// Gets the value following the specified argument, or 
        /// null no value follows the specified argument
        /// </summary>
        /// <param name="arg">The argument to get following value of</param>
        /// <returns>The argument value if found</returns>
        public string? GetArgument(string arg)
        {
            int index = _args.IndexOf(arg);
            return index == -1 || index + 1 >= _args.Count ? null : this[index + 1];
        }

     
    }
}