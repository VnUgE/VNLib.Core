/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Collections;
using System.Collections.Generic;

namespace VNLib.Utils
{
    /// <summary>
    /// Provides methods for parsing an argument list
    /// </summary>
    public class ArgumentList : IIndexable<int, string>, IEnumerable<string>
    {
        private readonly List<string> _args;

        /// <summary>
        /// Initalzies a the argument parser by copying the given argument array
        /// </summary>
        /// <param name="args">The array of arguments to clone</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ArgumentList(string[] args) : this(args as IEnumerable<string>)
        { }

        /// <summary>
        /// Initalizes the argument parser by copying the given argument list
        /// </summary>
        /// <param name="args">The argument list to clone</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ArgumentList(IEnumerable<string> args)
        {
            ArgumentNullException.ThrowIfNull(args);
            _args = [.. args];
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
        /// <param name="arg">The name of the argument to check existence of</param>
        /// <returns>A value that indicates if the argument is present in the list</returns>
        public bool HasArgument(string arg) => HasArgument(_args, arg);

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
        public string? GetArgument(string arg) => GetArgument(_args, arg);

        ///<inheritdoc/>
        public IEnumerator<string> GetEnumerator() => _args.GetEnumerator();

        ///<inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Captures the command line arguments from the currently executing process
        /// and returns them as an ArgumentList
        /// </summary>
        /// <returns>The <see cref="ArgumentList"/> containing the current process's argument list</returns>
        public static ArgumentList CaptureCurrentArgs()
        {
            /*
             *  Capture the current command line arguments and 
             *  pop the first argument which is always the program 
             *  name
             */
            string[] strings = Environment.GetCommandLineArgs();
            return new ArgumentList(strings.Skip(1));
        }

        /// <summary>
        /// Determines of the given argument is present in the argument list
        /// </summary>
        /// <param name="argsList">The collection to search for the arugment within</param>
        /// <param name="argName">The name of the argument to check existence of</param>
        /// <returns>A value that indicates if the argument is present in the list</returns>
        public static bool HasArgument<T>(T argsList, string argName) where T : IEnumerable<string>
            => argsList.Contains(argName, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Determines if the argument is present in the argument list and 
        /// has a non-null value following it.
        /// </summary>
        ///  <param name="argsList">The collection to search for the arugment within</param>
        /// <param name="argName">The name of the argument to check existence of</param>
        /// <returns>A value that indicates if a non-null argument is present in the list</returns>
        public static bool HasArgumentValue<T>(T argsList, string argName) where T : IEnumerable<string>
            => GetArgument(argsList, argName) != null;

        /// <summary>
        /// Gets the value following the specified argument, or 
        /// null no value follows the specified argument
        /// </summary>
        /// <param name="argsList">The collection to search for the arugment within</param>
        /// <param name="argName">The name of the argument to check existence of</param>
        /// <returns>The argument value if found</returns>
        public static string? GetArgument<T>(T argsList, string argName) where T : IEnumerable<string>
        {
            ArgumentNullException.ThrowIfNull(argsList);

            /*
             * Try to optimize some fetching for types that have
             * better performance for searching/indexing
             */
            if (argsList is IList<string> argList)
            {
                int index = argList.IndexOf(argName);
                return index == -1 || index + 1 >= argList.Count
                    ? null
                    : argList[index + 1];
            }
            else if (argsList is string[] argsArr)
            {
                return findInArray(argsArr, argName);
            }
            else
            {
                //TODO use linq instead of converting to array on every call
                return findInArray(
                    argsList.ToArray(),
                    argName
                );
            }

            static string? findInArray(string[] argsArr, string argName)
            {
                int index = Array.IndexOf(argsArr, argName);
                return index == -1 || index + 1 >= argsArr.Length
                    ? null
                    : argsArr[index + 1];
            }
        }
    }
}
