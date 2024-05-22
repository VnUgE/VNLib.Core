/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpPerfCounter.cs 
*
* HttpPerfCounter.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Diagnostics;

using VNLib.Utils.Logging;


namespace VNLib.Net.Http.Core.PerfCounter
{
    internal static class HttpPerfCounter
    {

        [Conditional("DEBUG")]
        internal static void StartCounter(ref HttpPerfCounterState state)
        {
            state.StopValue = state.StartValue = TimeProvider.System.GetTimestamp();
        }

        [Conditional("DEBUG")]
        internal static void StopCounter(ref HttpPerfCounterState state)
        {
            state.StopValue = TimeProvider.System.GetTimestamp();
        }

        /// <summary>
        /// Gets the total time elapsed in microseconds
        /// </summary>
        /// <returns>The time in microseconds that has elapsed since the timer was started and stopped</returns>
        internal static TimeSpan GetElapsedTime(ref readonly HttpPerfCounterState state) 
            => TimeProvider.System.GetElapsedTime(state.StartValue, state.StopValue);

        /*
        * Enable http performance counters for tracing. 
        * Only available in debug builds until it can be 
        * configured for zero-cost
        */

        [Conditional("DEBUG")]
        internal static void StopAndLog(ref HttpPerfCounterState state, ref readonly HttpConfig config, string counter) 
        {
            if (!config.DebugPerformanceCounters)
            {
                return;
            }

            StopCounter(ref state);
            
            TimeSpan duration = GetElapsedTime(in state);

            config.ServerLog.Debug("[PERF]: ({state}) - {us}us elapsed", counter, duration.TotalMicroseconds);
        }
    }
}