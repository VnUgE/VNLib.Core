/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: LastLogEvent.cs
*
* LastLogEvent.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime.Tests. If not, see http://www.gnu.org/licenses/.
*/

using System;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Runtime.Tests.Helpers
{
    /// <summary>
    /// Simple log event data storage record that captures log event data
    /// in a single immutable object rather than separate properties.
    /// </summary>
    /// <param name="Level">The log level of the event</param>
    /// <param name="Message">The log message</param>
    /// <param name="Exception">An optional exception associated with the log event</param>
    /// <param name="ObjectArgs">Optional object arguments used when formatting the log message</param>
    public sealed record class LastLogEvent(
        LogLevel Level,
        string Message,
        Exception? Exception = null,
        object?[]? ObjectArgs = null
    );
}
