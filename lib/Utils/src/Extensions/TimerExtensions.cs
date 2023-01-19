/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: TimerExtensions.cs 
*
* TimerExtensions.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Resources;

namespace VNLib.Utils.Extensions
{

    /// <summary>
    /// Contains extension methods for <see cref="Timer"/>
    /// </summary>
    public static class TimerExtensions
    {
        /// <summary>
        /// Attempts to stop the timer
        /// </summary>
        /// <returns>True if the timer was successfully modified, false otherwise</returns>
        public static bool Stop(this Timer timer) => timer.Change(Timeout.Infinite, Timeout.Infinite);

        /// <summary>
        /// Attempts to stop an active timer and prepare a <see cref="OpenHandle"/> configured to restore the state of the timer to the specified timespan
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="resumeTime"><see cref="TimeSpan"/> representing the amount of time the timer should wait before invoking the callback function</param>
        /// <returns>A new <see cref="OpenHandle"/> if the timer was stopped successfully that will resume the timer when closed, null otherwise</returns>
        public static TimerResumeHandle? Stop(this Timer timer, TimeSpan resumeTime)
        {
            return timer.Change(Timeout.Infinite, Timeout.Infinite) ? new TimerResumeHandle(timer, resumeTime) : null;
        }

        /// <summary>
        /// Attempts to reset and start a timer
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="wait"><see cref="TimeSpan"/> to wait before the timer event is fired</param>
        /// <returns>True if the timer was successfully modified</returns>
        public static bool Restart(this Timer timer, TimeSpan wait) => timer.Change(wait, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Attempts to reset and start a timer
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="waitMilliseconds">Time in milliseconds to wait before the timer event is fired</param>
        /// <returns>True if the timer was successfully modified</returns>
        public static bool Restart(this Timer timer, int waitMilliseconds) => timer.Change(waitMilliseconds, Timeout.Infinite);
    }
}