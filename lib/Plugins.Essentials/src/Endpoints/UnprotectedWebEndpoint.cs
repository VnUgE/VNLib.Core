/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: UnprotectedWebEndpoint.cs 
*
* UnprotectedWebEndpoint.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Utils;

namespace VNLib.Plugins.Essentials.Endpoints
{
    /// <summary>
    /// A base class for un-authenticated web (browser) based resource endpoints
    /// to implement. Adds additional security checks
    /// </summary>
    public abstract class UnprotectedWebEndpoint : ResourceEndpointBase
    {
        ///<inheritdoc/>
        protected override ERRNO PreProccess(HttpEntity entity)
        {
            return base.PreProccess(entity) && entity.Session.IsSet && entity.Session.SessionType == Sessions.SessionType.Web;
        }
    }
}