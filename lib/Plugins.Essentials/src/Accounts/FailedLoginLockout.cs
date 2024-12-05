/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: FailedLoginLockout.cs 
*
* FailedLoginLockout.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

using VNLib.Plugins.Essentials.Users;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Allows tracking of failed login attempts and lockout of accounts
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FailedLoginLockout"/> class.
    /// </remarks>
    /// <param name="maxCounts">The max number of failed login attempts before a lockout occurs</param>
    /// <param name="maxTimeout">The max duration for a lockout to last</param>
    public class FailedLoginLockout(uint maxCounts, TimeSpan maxTimeout)
    {
        private readonly uint _maxCounts = maxCounts;
        private readonly TimeSpan _duration = maxTimeout;

        /// <summary>
        /// Increments the lockout counter for the supplied user. If the lockout count
        /// has been exceeded, it is not incremented. If the lockout count has expired,
        /// it is reset and 0 is returned.
        /// </summary>
        /// <param name="user">The user to increment the failed login count</param>
        /// <param name="now">The current time</param>
        /// <returns>The new lockout count after incrementing or 0 if the count was cleared</returns>
        public bool IncrementOrClear(IUser user, DateTimeOffset now)
        {
            //Recover last counter value
            TimestampedCounter current = user.FailedLoginCount();

            //See if the flc timeout period has expired
            if (current.LastModified.Add(_duration) < now)
            {
                //clear flc flag
                user.ClearFailedLoginCount();
                return false;
            }

            if (current.Count <= _maxCounts)
            {
                //Increment counter
                user.FailedLoginCount(current.Count + 1, now);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the current user's lockout count has been exceeded, or 
        /// clears a previous lockout if the timeout period has expired.
        /// </summary>
        /// <param name="user">The user to increment the failed login count</param>
        /// <param name="now">The current time</param>
        /// <returns>A value that indicates if the count has been exceeded</returns>
        public bool CheckOrClear(IUser user, DateTimeOffset now)
        {
            //Recover last counter value
            TimestampedCounter flc = user.FailedLoginCount();

            //See if the flc timeout period has expired
            if (flc.LastModified.Add(_duration) < now)
            {
                //clear flc flag
                user.ClearFailedLoginCount();
                return false;
            }

            return flc.Count >= _maxCounts;
        }

        /// <summary>
        /// Checks if the current user's lockout count has been exceeded
        /// </summary>
        /// <param name="user">The user to check the counter for</param>
        /// <returns>True if the lockout has been exceeded</returns>
        public bool IsCountExceeded(IUser user)
        {
            //Recover last counter value
            TimestampedCounter flc = user.FailedLoginCount();
            //Count has been exceeded, and has not timed out yet
            return flc.Count >= _maxCounts;
        }

        /// <summary>
        /// Increments the lockout counter for the supplied user. 
        /// </summary>
        /// <param name="user">The user to increment the count on</param>
        /// <param name="now"></param>
        public void Increment(IUser user, DateTimeOffset now)
        {
            //Recover last counter value
            TimestampedCounter current = user.FailedLoginCount();

            //Only increment if the count is less than max counts
            if (current.Count <= _maxCounts)
            {
                //Increment counter
                user.FailedLoginCount(current.Count + 1, now);
            }
        }
    }
}