/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ClientSecurityToken.cs 
*
* ClientSecurityToken.cs is part of VNLib.Plugins.Essentials which is part 
* of the larger VNLib collection of libraries and utilities.
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
    /// A structure that contains the client/server information
    /// for client/server authorization
    /// </summary>
    /// <param name="ClientToken">
    /// The public portion of the token to send to the client
    /// </param>
    /// <param name="ServerToken">
    /// The secret portion of the token that is to be 
    /// stored on the server (usually in the client's session)
    /// </param>
    public readonly record struct ClientSecurityToken(string ClientToken, string ServerToken)
    { }
}