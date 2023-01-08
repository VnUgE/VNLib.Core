/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ProtectedWebEndpoint.cs 
*
* ProtectedWebEndpoint.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Net;

using VNLib.Utils;
using VNLib.Plugins.Essentials.Accounts;

namespace VNLib.Plugins.Essentials.Endpoints
{
    /// <summary>
    /// Implements <see cref="UnprotectedWebEndpoint"/> to provide 
    /// authoriation checks before processing
    /// </summary>
    public abstract class ProtectedWebEndpoint : UnprotectedWebEndpoint
    {
        ///<inheritdoc/>
        protected override ERRNO PreProccess(HttpEntity entity)
        {
            if (!base.PreProccess(entity))
            {
                return false;
            }
            //The loggged in flag must be set, and the token must also match
            if (!entity.LoginCookieMatches() || !entity.TokenMatches())
            {
                //Return unauthorized status
                entity.CloseResponse(HttpStatusCode.Unauthorized);
                //A return value less than 0 signals a virtual skip event
                return -1;
            }
            //Continue
            return true;
        }
    }
}