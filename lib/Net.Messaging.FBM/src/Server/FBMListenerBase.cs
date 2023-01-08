/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMListenerBase.cs 
*
* FBMListenerBase.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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

using VNLib.Utils.Logging;
using VNLib.Utils.Memory;
using VNLib.Plugins.Essentials;

namespace VNLib.Net.Messaging.FBM.Server
{
    /// <summary>
    /// Provides a simple base class for an <see cref="FBMListener"/>
    /// processor
    /// </summary>
    public abstract class FBMListenerBase
    {

        /// <summary>
        /// The initialzied listener
        /// </summary>
        protected FBMListener? Listener { get; private set; }
        /// <summary>
        /// A provider to write log information to
        /// </summary>
        protected abstract ILogProvider Log { get; }

        /// <summary>
        /// Initializes the <see cref="FBMListener"/>
        /// </summary>
        /// <param name="heap">The heap to alloc buffers from</param>
        protected void InitListener(IUnmangedHeap heap)
        {
            Listener = new(heap);
            //Attach service handler
            Listener.OnProcessError += Listener_OnProcessError;
        }

        /// <summary>
        /// A single event service routine for servicing errors that occur within
        /// the listener loop
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">The exception that was raised</param>
        protected virtual void Listener_OnProcessError(object? sender, Exception e)
        {
            //Write the error to the log
            Log.Error(e);
        }

        private async Task OnReceivedAsync(FBMContext context, object? userState, CancellationToken token)
        {
            try
            {
                await ProcessAsync(context, userState, token);
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Async operation cancelled");
            }
            catch(Exception ex)
            {
                Log.Error(ex);
            }
        }

        /// <summary>
        /// Begins listening for requests on the current websocket until 
        /// a close message is received or an error occurs
        /// </summary>
        /// <param name="wss">The <see cref="WebSocketSession"/> to receive messages on</param>
        /// <param name="args">The arguments used to configured this listening session</param>
        /// <param name="userState">A state token to use for processing events for this connection</param>
        /// <returns>A <see cref="Task"/> that completes when the connection closes</returns>
        public virtual async Task ListenAsync(WebSocketSession wss, FBMListenerSessionParams args, object? userState)
        {
            _ = Listener ?? throw new InvalidOperationException("The listener has not been intialized");
            await Listener.ListenAsync(wss, OnReceivedAsync, args, userState);
        }

        /// <summary>
        /// A method to service an incoming message
        /// </summary>
        /// <param name="context">The context containing the message to be serviced</param>
        /// <param name="userState">A state token passed on client connected</param>
        /// <param name="exitToken">A token that reflects the state of the listener</param>
        /// <returns>A task that completes when the message has been serviced</returns>
        protected abstract Task ProcessAsync(FBMContext context, object? userState, CancellationToken exitToken);
    }
}
