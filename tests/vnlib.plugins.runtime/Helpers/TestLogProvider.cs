/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: TestLogProvider.cs
*
* TestLogProvider.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
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
    /// A test implementation of <see cref="ILogProvider"/> that provides configurable logging behavior 
    /// and call tracking for unit tests.
    /// </summary>
    public sealed class TestLogProvider : ILogProvider
    {
        /// <summary>
        /// Gets or sets a value that indicates whether log writes should be tracked.
        /// When <see langword="true"/>, captures write calls and their arguments.
        /// The default is <see langword="false"/>.
        /// </summary>
        public bool TrackWrites { get; set; }

        /// <summary>
        /// Gets the number of times any <c>Write</c> method has been called.
        /// Only increments when <see cref="TrackWrites"/> is <see langword="true"/>.
        /// </summary>
        public int WriteCallCount { get; private set; }

        /// <summary>
        /// Gets the most recently captured log event, or <see langword="null"/> if no event has been recorded.
        /// </summary>
        public LastLogEvent? LastEvent { get; private set; }

        /// <inheritdoc/>
        public object GetLogProvider() => this;

        /// <inheritdoc/>
        public void Flush()
        {
            // No-op
        }

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel level) => TrackWrites;
      
        /// <inheritdoc/>
        public void Write(LogLevel level, Exception exception, string value = "")
        {
            if (!TrackWrites)
            {
                return;
            }

            WriteCallCount++;
            LastEvent = new (level, value, exception);
        }

        /// <inheritdoc/>
        public void Write(LogLevel level, string value, params object?[]? args)
        {
            if (!TrackWrites)
            {
                return;
            }

            WriteCallCount++;
            LastEvent = new (level, value, ObjectArgs: args);
        }

        /// <inheritdoc/>
        public void Write(LogLevel level, string value)
            => Write(level, value, args: (object[]?)null);


        /// <inheritdoc/>
        public void Write(LogLevel level, string value, params ValueType[] args)
        {
            Write(
                level, 
                value, 
                args: Array.ConvertAll(args, static valueType => (object?)valueType)
            );
        }
    }
}
