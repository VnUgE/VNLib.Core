/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.WebServer.IntegrationTests.TestPlugin
* File: TestPluginEntry.cs
*
* TestPluginEntry.cs is part of VNLib.WebServer.IntegrationTests.TestPlugin which is part of
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

using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Plugins;
using VNLib.Plugins.Essentials.Endpoints;

namespace VNLib.WebServer.IntegrationTests.TestPlugin
{
    /// <summary>
    /// A minimal test plugin that exports fixed JSON endpoints for
    /// integration testing the web server over HTTP.
    /// </summary>
    /// <remarks>
    /// Endpoints are exported as an <see cref="IVirtualEndpointDefinition"/>
    /// through the plugin service collection so the test package only
    /// depends on core libraries.
    /// </remarks>
    public sealed class TestPluginEntry : PluginBase, IVirtualEndpointDefinition
    {
        private readonly List<IEndpoint> _endpoints = [];

        ///<inheritdoc/>
        public override string PluginName => "WebServerIntegrationTestPlugin";

        ///<inheritdoc/>
        protected override void OnLoad()
        {
            _endpoints.Add(new HelloEndpoint(this));
            _endpoints.Add(new EchoEndpoint(this));

            Services.Add(new ServiceExport(typeof(IVirtualEndpointDefinition), this, ExportFlags.None));

            Log.Information("Test plugin loaded with {count} endpoints", _endpoints.Count);
        }

        ///<inheritdoc/>
        protected override void OnUnLoad()
        {
            _endpoints.Clear();

            Log.Information("Test plugin unloaded");
        }

        ///<inheritdoc/>
        public IEnumerable<IEndpoint> GetEndpoints() => _endpoints;
    }
}
