/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: SocketStrategyBase.cs 
*
* SocketStrategyBase.cs is part of VNLib.Net.Transport.Tcp which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.Tcp is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.Tcp is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Threading;
using System.IO.Pipelines;

using VNLib.Utils;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp.Internal
{
    internal abstract class SocketStrategyBase : VnDisposeable, IReusable
    {
        /// <summary>
        /// A timer to be used for operations. Configured to cancel a pending flush on the pipe when 
        /// it elapses, which will cause the flush to complete with a canceled state.
        /// </summary>
        protected readonly Timer OpTimer;

        /// <summary>
        /// The underlying pipe used for pipelining data
        /// </summary>
        protected readonly Pipe Pipe;

        /// <summary>
        /// A value used to track the state of the strategy, to determine if it has been 
        /// started and if the pipe needs to be reset on release
        /// </summary>
        protected bool IsStarted;

        public SocketStrategyBase(PipeOptions pipeOptions)
        {
            Pipe = new(pipeOptions);
            OpTimer = new(OnTimeoutExpired, state: this, Timeout.Infinite, Timeout.Infinite);
        }

        /// <inheritdoc/>
        public virtual void Prepare() { }

        /// <inheritdoc/>
        public virtual bool Release()
        {
            if (IsStarted)
            {
                Pipe.Reset();
            }

            IsStarted = false;
            return true;
        }

        /// <inheritdoc/>
        protected override void Free() => OpTimer.Dispose();

        /// <summary>
        /// Configured to fire when the <see cref="OpTimer"/> elapses, typically 
        /// used for notifying consumers that the operation has been cancelled
        /// </summary>
        /// <param name="state"></param>
        protected abstract void OnTimeoutExpired(object? state);

        /// <summary>
        /// Completes the send pipeline, which will cause the worker task to complete once all data is flushed to
        /// the socket. This should be called when the pipeline is requested to be closed, to ensure that all data is
        /// flushed and resources are cleaned up properly.
        /// </summary>
        public abstract void CompletePipeline();
    }
}
