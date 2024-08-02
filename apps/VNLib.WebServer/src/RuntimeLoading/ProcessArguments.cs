/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ProcessArguments.cs 
*
* ProcessArguments.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System.Collections.Generic;

using VNLib.Utils;

namespace VNLib.WebServer.RuntimeLoading
{
    internal sealed class ProcessArguments(IEnumerable<string> args) : ArgumentList(args)
    {
        public bool Verbose => HasArgument("-v") || HasArgument("--verbose");
        public bool Debug => HasArgument("-d") || HasArgument("--debug");
        public bool Silent => HasArgument("-s") || HasArgument("--silent");
        public bool DoubleVerbose => Verbose && HasArgument("-vv");
        public bool LogHttp => HasArgument("--log-http");
        public bool ZeroAllocations => HasArgument("--zero-alloc");
    }
}
