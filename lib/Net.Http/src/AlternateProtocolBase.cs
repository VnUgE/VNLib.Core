/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: AlternateProtocolBase.cs 
*
* AlternateProtocolBase.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Net.Http
{
    /// <summary>
    /// A base class for all non-http protocol handlers
    /// </summary>
    public abstract class AlternateProtocolBase : MarshalByRefObject, IAlternateProtocol
    {
        /// <summary>
        /// A cancelation source that allows for canceling running tasks, that is linked 
        /// to the server that called <see cref="RunAsync(Stream)"/>.
        /// </summary>
        /// <remarks>
        /// This property is only available while the <see cref="RunAsync(Stream)"/> 
        /// method is executing
        /// </remarks>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected CancellationTokenSource CancelSource { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        
        ///<inheritdoc/>
        async Task IAlternateProtocol.RunAsync(Stream transport, CancellationToken handlerToken)
        {
            //Create new cancel source
            CancelSource ??= new();
            //Register the token to cancel the source and save the registration for unregister on dispose
            CancellationTokenRegistration Registration = handlerToken.Register(CancelSource.Cancel);
            try
            {
                //Call child initialize method
                await RunAsync(transport).ConfigureAwait(false);
                
                await CancelSource.CancelAsync();
            }
            finally
            {
                //dispose the cancelation registration
                await Registration.DisposeAsync();
                //Dispose cancel source
                CancelSource.Dispose();
            }
        }

        /// <summary>
        /// Is the current socket connected using transport security
        /// </summary>
        public required virtual bool IsSecure { get; init; }

        /// <summary>
        /// Determines if the instance is pending cancelation 
        /// </summary>
        public bool IsCancellationRequested => CancelSource.IsCancellationRequested;

        /// <summary>
        /// Cancels all pending operations. This session will be unusable after this function is called
        /// </summary>
        public virtual void CancelAll() => CancelSource?.Cancel();

        /// <summary>
        /// Called when the protocol swtich handshake has completed and the transport is 
        /// available for the new protocol
        /// </summary>
        /// <param name="transport">The transport stream</param>
        /// <returns>A task that represents the active use of the transport, and when complete all operations are unwound</returns>
        protected abstract Task RunAsync(Stream transport);    
    }
}