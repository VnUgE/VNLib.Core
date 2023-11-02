/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IFBMServerErrorHandler.cs 
*
* IFBMServerErrorHandler.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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

using System;

namespace VNLib.Net.Messaging.FBM.Server
{
    /// <summary>
    /// An server side FBM protocol error handler abstraction
    /// </summary>
    public interface IFBMServerErrorHandler
    {
        /// <summary>
        /// An exception handler for unhandled events that occur during a listening session
        /// </summary>
        /// <param name="ex">The exception that caused this handler to be invoked</param>
        void OnProcessError(Exception ex);

        /// <summary>
        /// An exception handler for invalid messages that occur during a listening session.
        /// NOTE: The context parameter is likely in an invlaid state and should be read carefully
        /// </summary>
        /// <param name="context">The context that the error occured while parsing on</param>
        /// <param name="ex">The exception explaining the reason this handler was invoked</param>
        /// <returns>A value that indicates if the server should continue processing</returns>
        bool OnInvalidMessage(FBMContext context, Exception ex);
    }
}
