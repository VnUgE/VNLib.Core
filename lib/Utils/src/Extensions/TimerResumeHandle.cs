/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: TimerResetHandle.cs 
*
* TimerResetHandle.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// A handle that represents a paused timer that may be resumed when the handle is disposed
    /// or the Resume() method is called
    /// </summary>
    public readonly struct TimerResumeHandle: IEquatable<TimerResumeHandle>, IDisposable
    {
        private readonly Timer _timer;
        private readonly TimeSpan _resumeTime;

        internal TimerResumeHandle(Timer timer, TimeSpan resumeTime)
        {
            _timer = timer;
            _resumeTime = resumeTime;
        }

        /// <summary>
        /// Resumes the timer to the configured time from the call to Timer.Stop()
        /// </summary>
        public void Resume() => _timer.Change(_resumeTime, Timeout.InfiniteTimeSpan);
        /// <summary>
        /// Releases any resources held by the Handle, and resumes the timer to 
        /// the configured time from the call to Timer.Stop()
        /// </summary>
        public void Dispose() => Resume();

        ///<inheritdoc/>
        public bool Equals(TimerResumeHandle other) => _timer.Equals(other) && _resumeTime == other._resumeTime;
        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is TimerResumeHandle trh && Equals(trh);
        ///<inheritdoc/>
        public override int GetHashCode() => _timer.GetHashCode() + _resumeTime.GetHashCode();
        ///<inheritdoc/>
        public static bool operator ==(TimerResumeHandle left, TimerResumeHandle right) => left.Equals(right);
        ///<inheritdoc/>
        public static bool operator !=(TimerResumeHandle left, TimerResumeHandle right) => !(left == right);
    }
}