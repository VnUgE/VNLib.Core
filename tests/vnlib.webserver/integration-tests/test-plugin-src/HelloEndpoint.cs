/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.WebServer.IntegrationTests.TestPlugin
* File: HelloEndpoint.cs
*
* HelloEndpoint.cs is part of VNLib.WebServer.IntegrationTests.TestPlugin which is part of
* the larger VNLib collection of libraries and utilities.
*
* VNLib.WebServer.IntegrationTests.TestPlugin is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer.IntegrationTests.TestPlugin is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with VNLib.WebServer.IntegrationTests.TestPlugin. If not, see http://www.gnu.org/licenses/.
*/

using System;

using VNLib.Plugins;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Extensions;

namespace VNLib.WebServer.IntegrationTests.TestPlugin
{
    /// <summary>
    /// A minimal unauthenticated endpoint used to verify the server is
    /// routing plugin endpoints. Responds to GET requests with a fixed
    /// JSON message.
    /// </summary>
    internal sealed class HelloEndpoint : ResourceEndpointBase
    {
        /*
         * CI traffic is plain HTTP without sessions, so the default
         * protection settings must be loosened or every request
         * is rejected before reaching the handler.
         */
        protected override ProtectionSettings EndpointProtectionSettings { get; } = new()
        {
            DisabledTlsRequired = true,
            DisableSessionsRequired = true,
            DisableRefererMatch = true,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="HelloEndpoint"/> class.
        /// </summary>
        /// <param name="plugin">The owning plugin, used for path and log initialization.</param>
        public HelloEndpoint(PluginBase plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            InitEndpoint("/test/hello", plugin.Log);
        }

        ///<inheritdoc/>
        protected override VfReturnType Get(HttpEntity entity)
        {
            WebMessage response = new()
            {
                Result = "hello",
                Success = true,
            };

            return VirtualOk(entity, response);
        }
    }
}
