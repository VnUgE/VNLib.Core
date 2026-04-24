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
    /// <summary>
    /// Provides the abstract base for a unidirectional socket pipeline strategy, owning a
    /// <see cref="System.IO.Pipelines.Pipe"/>, an operation timer, and lifecycle state.
    /// Subclasses implement the producer or consumer direction of the pipeline.
    /// </summary>
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
        /// Gets or sets a value indicating whether the strategy's pipeline has been started at least once.
        /// Used to determine whether <see cref="System.IO.Pipelines.Pipe.Reset"/> is required on release.
        /// </summary>
        protected bool IsStarted;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketStrategyBase"/> class with the supplied pipe options.
        /// </summary>
        /// <param name="pipeOptions">The options used to configure the internal <see cref="System.IO.Pipelines.Pipe"/>.</param>
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
        /// Called when the operation timer expires. Implementations should cancel the relevant
        /// pending pipe operation so that the blocked caller returns with a canceled result.
        /// </summary>
        /// <param name="state">The timer callback state object; typically unused.</param>
        protected abstract void OnTimeoutExpired(object? state);

        /// <summary>
        /// Completes the send pipeline, which will cause the worker task to complete once all data is flushed to
        /// the socket. This should be called when the pipeline is requested to be closed, to ensure that all data is
        /// flushed and resources are cleaned up properly.
        /// </summary>
        public abstract void CompletePipeline();
    }
}
