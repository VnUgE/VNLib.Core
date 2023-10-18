/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: UserExtensions.cs 
*
* UserExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Text.Json;

using VNLib.Plugins.Essentials.Users;
using VNLib.Plugins.Essentials.Accounts;

namespace VNLib.Plugins.Essentials.Extensions
{
    /// <summary>
    /// Provides extension methods to the Users namespace
    /// </summary>
    public static class UserExtensions
    {
        
        private const string PROFILE_ENTRY = "__.prof";

        /// <summary>
        /// Stores the user's profile to their entry. 
        /// <br/>
        /// NOTE: You must validate/filter data before storing
        /// </summary>
        /// <param name="ud"></param>
        /// <param name="profile">The profile object to store on account</param>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static void SetProfile(this IUser ud, AccountData? profile)
        {
            //Clear entry if its null
            if (profile == null)
            {
                ud[PROFILE_ENTRY] = null!;
                return;
            }
            //Dont store duplicate values
            profile.Created = null;
            profile.EmailAddress = null;
            ud.SetObject(PROFILE_ENTRY, profile);
        }

        /// <summary>
        /// Stores the serialized string user's profile to their entry.
        /// <br/>
        /// NOTE: No data validation checks are performed
        /// </summary>
        /// <param name="ud"></param>
        /// <param name="jsonProfile">The JSON serialized "raw" profile data</param>
        public static void SetProfile(this IUser ud, string jsonProfile) => ud[PROFILE_ENTRY] = jsonProfile;

        /// <summary>
        /// Recovers the user's stored profile
        /// </summary> 
        /// <returns>The user's profile stored in the entry or null if no entry is found</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static AccountData? GetProfile(this IUser ud)
        {
            //Recover profile data, or create new empty profile data
            AccountData? ad = ud.GetObject<AccountData>(PROFILE_ENTRY);
            if (ad == null)
            {
                return null;
            }
            //Set email the same as the account
            ad.EmailAddress = ud.EmailAddress;
            //Store the rfc time
            ad.Created = ud.Created.ToString("R");
            return ad;
        }
    }
}