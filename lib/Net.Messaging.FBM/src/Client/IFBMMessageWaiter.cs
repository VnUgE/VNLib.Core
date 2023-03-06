/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMRequest.cs 
*
* FBMRequest.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.IO;


namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// A data structure that exposes controls for the request/response
    /// async control flow.
    /// </summary>
    internal interface IFBMMessageWaiter
    {
        /// <summary>
        /// Called by the client to prepare the waiter before 
        /// sending the request to the server
        /// </summary>
        void OnBeginRequest();

        /// <summary>
        /// Asynchronously waits for the server to respond while observing a cancellation token
        /// or a timeout
        /// </summary>
        /// <param name="timeout">The maxium time to wait for the server to respond (may be default/0)</param>
        /// <param name="cancellation">The cancellation token to observe</param>
        /// <returns>A task that completes when the server responds</returns>
        Task WaitAsync(TimeSpan timeout, CancellationToken cancellation);

        /// <summary>
        /// Called by the client to cleanup the waiter when the request is completed
        /// or errored. This method is exposed incase an error happens before the wait is 
        /// entered.
        /// </summary>
        void OnEndRequest();

        /// <summary>
        /// Set by the client when the response has been successfully received by the client
        /// </summary>
        /// <param name="ms">The response data to pass to the response</param>
        void Complete(VnMemoryStream ms);

        /// <summary>
        /// Called to invoke a manual cancellation of a request waiter. This method should
        /// be called from a different thread, it may yield to complete the cancellation 
        /// operation.
        /// </summary>
        void ManualCancellation();
    }
}
