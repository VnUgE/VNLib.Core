/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IFBMServerMessageHandler.cs 
*
* IFBMServerMessageHandler.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Net.Messaging.FBM.Server
{
    /// <summary>
    /// A server side FBM protocol handler 
    /// </summary>
    public interface IFBMServerMessageHandler : IFBMServerErrorHandler
    {
        /// <summary>
        /// Handles processing of a normal incoming message
        /// </summary>
        /// <param name="context">The context to process for this new message</param>
        /// <param name="cancellationToken">A token that signals the session has been cancelled</param>
        /// <returns>A task representing the asynchronous work</returns>
        Task HandleMessage(FBMContext context, CancellationToken cancellationToken);
    }
}
