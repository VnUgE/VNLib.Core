/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IClientAuthorization.cs 
*
* IClientAuthorization.cs is part of VNLib.Plugins.Essentials which is 
* part of the larger VNLib collection of libraries and utilities.
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


namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Contains the client's minimum authorization variables
    /// </summary>
    public interface IClientAuthorization
    {
        /// <summary>
        /// Gets the client specific authorization data as a string, may be serialized
        /// and will be sent to the client.
        /// </summary>
        /// <returns>A string representation of the client's authorization data</returns>
        string GetClientAuthDataString();

        /// <summary>
        /// Gets the client specific authorization data raw object and may be serialized
        /// as needed.
        /// </summary>
        /// <returns>The authorization object to send to the client</returns>
        object GetClientAuthData();
    }
}