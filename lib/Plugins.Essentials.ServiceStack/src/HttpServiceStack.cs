/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: HttpServiceStack.cs 
*
* HttpServiceStack.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// An HTTP servicing stack that manages a collection of HTTP servers
    /// and their service domain. This stack manages the HTTP servers 
    /// listening for requests, and the domain that responds to those 
    /// requests.
    /// </summary>
    public sealed class HttpServiceStack : VnDisposeable
    {       
        private readonly IReadOnlyCollection<IHttpServer> _servers;
        private readonly ServiceDomain _serviceDomain;

        private CancellationTokenSource? _cts;
        private Task WaitForAllTask;

        /// <summary>
        /// A collection of all loaded servers
        /// </summary>
        public IEnumerable<IHttpServer> Servers => _servers;

        /// <summary>
        /// The service domain containing all virtual hosts and their attached services
        /// </summary>
        public ServiceDomain ServiceDomain => _serviceDomain;

        /// <summary>
        /// Initializes a new <see cref="HttpServiceStack"/> that will 
        /// manage HTTP servers for the specified service domain
        /// </summary>
        /// <param name="servers">The collection of HTTP servers to manage</param>
        /// <param name="serviceDomain">The service domain containing virtual hosts</param>
        internal HttpServiceStack(IReadOnlyCollection<IHttpServer> servers, ServiceDomain serviceDomain)
        {
            _servers = servers;
            _serviceDomain = serviceDomain;
            WaitForAllTask = Task.CompletedTask;
        }

        /// <summary>
        /// Starts all configured servers that observe a cancellation
        /// token to cancel
        /// </summary>
        /// <param name="parentToken">The token to observe which may stop servers and cleanup the provider</param>
        public void StartServers(CancellationToken parentToken = default)
        {
            Check();

            //Init new linked cts to stop all servers if cancelled
            _cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);

            //Start all servers
            Task[] runners = _servers.Select(s => s.Start(_cts.Token)).ToArray();

            //Check for failed startups
            Task? firstFault = runners.Where(static t => t.IsFaulted).FirstOrDefault();
           
            //Raise first exception
            firstFault?.GetAwaiter().GetResult();

            //Task that waits for all to exit then cleans up
            WaitForAllTask = Task.WhenAll(runners);              
        }

        /// <summary>
        /// Stops listening on all configured servers and returns a task that completes 
        /// when the service host has stopped all servers and unloaded resources
        /// </summary>
        /// <returns>The task that completes when all servers have exited</returns>
        public Task StopAndWaitAsync()
        {
            Check();

            _cts?.Cancel();
            return WaitForAllTask;
        }       

        ///<inheritdoc/>
        protected override void Free()
        {
            _cts?.Dispose();
        }
    }
}
