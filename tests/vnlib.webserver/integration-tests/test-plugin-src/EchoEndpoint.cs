/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.WebServer.IntegrationTests.TestPlugin
* File: EchoEndpoint.cs
*
* EchoEndpoint.cs is part of VNLib.WebServer.IntegrationTests.TestPlugin which is part of
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
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Plugins;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Extensions;

namespace VNLib.WebServer.IntegrationTests.TestPlugin
{
    /// <summary>
    /// A content-reading echo endpoint. Parses the posted JSON entity body
    /// and reflects the <c>message</c> property along with the
    /// <c>x-echo-check</c> request header back to the caller. A missing or
    /// invalid entity body is reported as a bad request.
    /// </summary>
    internal sealed class EchoEndpoint : ResourceEndpointBase
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
        /// Initializes a new instance of the <see cref="EchoEndpoint"/> class.
        /// </summary>
        /// <param name="plugin">The owning plugin, used for path and log initialization.</param>
        public EchoEndpoint(PluginBase plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            InitEndpoint("/test/echo", plugin.Log);
        }

        ///<inheritdoc/>
        protected override async ValueTask<VfReturnType> PostAsync(HttpEntity entity)
        {
            //Entity bodies arrive as file uploads, a missing body is a bad request
            if (entity.Files.Count == 0)
            {
                return BadRequest(entity, "Missing request entity body");
            }

            //Recover the posted json document from the upload
            using JsonDocument? request = await entity.GetJsonFromFileAsync()
                .ConfigureAwait(false);

            if (request is null)
            {
                return BadRequest(entity, "Invalid request data");
            }

            //The message property is required for the echo response
            if (!request.RootElement.TryGetProperty("message", out JsonElement message)
                || message.ValueKind != JsonValueKind.String)
            {
                return BadRequest(entity, "Missing message property");
            }

            //Reflect the check header and observed content type for header coverage
            string? checkHeader = entity.Server.Headers["x-echo-check"];

            object response = new
            {
                success = true,
                message = message.GetString(),
                echoedHeader = checkHeader,
                jsonContent = entity.Server.ContentType == ContentType.Json,
            };

            return VirtualOkJson(entity, response);
        }

        private static VfReturnType BadRequest(HttpEntity entity, string result)
        {
            WebMessage response = new()
            {
                Result = result,
                Success = false,
            };

            return VirtualCloseJson(entity, response, HttpStatusCode.BadRequest);
        }
    }
}
