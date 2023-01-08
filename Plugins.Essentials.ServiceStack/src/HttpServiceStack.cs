/*
* Copyright (c) 2022 Vaughn Nugent
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
* published by the Free Software Foundation, either version 2 of the
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

using VNLib.Utils;
using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// The service domain controller that manages all 
    /// servers for an application based on a 
    /// <see cref="ServiceDomain"/>
    /// </summary>
    public sealed class HttpServiceStack : VnDisposeable
    {
        private readonly LinkedList<HttpServer> _servers;
        private readonly ServiceDomain _serviceDomain;

        private CancellationTokenSource? _cts;
        private Task WaitForAllTask;

        /// <summary>
        /// A collection of all loaded servers
        /// </summary>
        public IReadOnlyCollection<HttpServer> Servers => _servers;

        /// <summary>
        /// The service domain's plugin controller
        /// </summary>
        public IPluginController PluginController => _serviceDomain;

        /// <summary>
        /// Initializes a new <see cref="HttpServiceStack"/> that will 
        /// generate servers to listen for services exposed by the 
        /// specified host context
        /// </summary>
        internal HttpServiceStack(LinkedList<HttpServer> servers, ServiceDomain serviceDomain)
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

            LinkedList<Task> runners = new();

            foreach(HttpServer server in _servers)
            {
                //Start servers and add run task to list
                Task run = server.Start(_cts.Token);
                runners.AddLast(run);
            }

            //Task that waits for all to exit then cleans up
            WaitForAllTask = Task.WhenAll(runners)
                .ContinueWith(OnAllServerExit, CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
        }

        /// <summary>
        /// Stops listening on all configured servers
        /// and returns a task that completes when the service 
        /// host has stopped all servers and unloaded resources
        /// </summary>
        /// <returns>The task that completes when</returns>
        public Task StopAndWaitAsync()
        {
            _cts?.Cancel();
            return WaitForAllTask;
        }

        private void OnAllServerExit(Task allExit)
        {
            //Unload the hosts
            _serviceDomain.UnloadAll();
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            _cts?.Dispose();

            _serviceDomain.Dispose();
            
            //remove all lists
            _servers.Clear();
        }
    }
}
