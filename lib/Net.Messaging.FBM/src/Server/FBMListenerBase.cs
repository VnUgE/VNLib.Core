/*
* Copyright (c) 2023 Vaughn Nugent
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
using VNLib.Plugins.Essentials;

namespace VNLib.Net.Messaging.FBM.Server
{

    /// <summary>
    /// Provides a simple base class for an <see cref="FBMListener"/>
    /// processor
    /// </summary>
    public abstract class FBMListenerBase<T> : IFBMServerErrorHandler
    {

        /// <summary>
        /// The initialzied listener
        /// </summary>
        protected abstract FBMListener Listener { get; }

        /// <summary>
        /// A provider to write log information to
        /// </summary>
        protected abstract ILogProvider Log { get; }

        /// <summary>
        /// Begins listening for requests on the current websocket until 
        /// a close message is received or an error occurs
        /// </summary>
        /// <param name="wss">The <see cref="WebSocketSession"/> to receive messages on</param>
        /// <param name="args">The arguments used to configured this listening session</param>
        /// <param name="userState">A state token to use for processing events for this connection</param>
        /// <returns>A <see cref="Task"/> that completes when the connection closes</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public virtual Task ListenAsync(WebSocketSession wss, T userState, FBMListenerSessionParams args)
        {
            _ = Listener ?? throw new InvalidOperationException("The listener has not been intialized");
            //Initn new event handler
            FBMEventHandler handler = new(userState, this);
            return Listener.ListenAsync(wss, handler, args);
        }

        /// <summary>
        /// A method to service an incoming message
        /// </summary>
        /// <param name="context">The context containing the message to be serviced</param>
        /// <param name="userState">A state token passed on client connected</param>
        /// <param name="exitToken">A token that reflects the state of the listener</param>
        /// <returns>A task that completes when the message has been serviced</returns>
        protected abstract Task ProcessAsync(FBMContext context, T? userState, CancellationToken exitToken);

        ///<inheritdoc/>
        public virtual bool OnInvalidMessage(FBMContext context, Exception ex)
        {
            Log.Error("Invalid message received for session {ses}\n{ex}", context.Request.ConnectionId, ex);
            //Invalid id should be captured already, so if oom, do not allow, but if a single header is invalid, it will be ignored by default
            return !context.Request.ParseStatus.HasFlag(HeaderParseError.HeaderOutOfMem);
        }

        ///<inheritdoc/>
        public virtual void OnProcessError(Exception ex) => Log.Error(ex);


        private sealed record class FBMEventHandler(T State, FBMListenerBase<T> Lb) : IFBMServerMessageHandler
        {
            ///<inheritdoc/>
            public Task HandleMessage(FBMContext context, CancellationToken cancellationToken) => Lb.ProcessAsync(context, State, cancellationToken);

            ///<inheritdoc/>
            public bool OnInvalidMessage(FBMContext context, Exception ex) => Lb.OnInvalidMessage(context, ex);

            ///<inheritdoc/>
            public void OnProcessError(Exception ex) => Lb.OnProcessError(ex);
        }
    }
}
