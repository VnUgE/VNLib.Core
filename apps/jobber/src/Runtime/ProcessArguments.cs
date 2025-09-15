/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: ProcessArguments.cs
*
* ProcessArguments.cs is part of jobber which is part of the larger 
* VNLib collection of libraries and utilities.
*
* jobber is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* jobber is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with jobber. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;

using VNLib.Utils;

namespace Jobber.Runtime
{

    /// <summary>
    /// Command-line argument helper for jobber
    /// </summary>
    internal sealed class ProcessArguments(IEnumerable<string> args) : ArgumentList(args)
    {
        public bool Quiet => HasArgument("--quiet") || HasArgument("-q");
        public bool Verbose => HasArgument("-v") || HasArgument("--verbose");
        public bool Debug => HasArgument("-d") || HasArgument("--debug");
        public bool DryRun => HasArgument("--dry-run");
        public bool List => HasArgument("--list");

        public string? ConfigPath => GetArgument("--config");
        public string? WaitForService => GetArgument("--wait");

        public int? StopTimeoutOverride
        {
            get
            {
                string? val = GetArgument("--stop-timeout");
                if (val != null && int.TryParse(val, out int parsed))
                {
                    return parsed;
                }
                return null;
            }
        }
    }
}