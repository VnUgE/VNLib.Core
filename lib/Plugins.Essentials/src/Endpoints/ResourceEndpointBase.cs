/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ResourceEndpointBase.cs 
*
* ResourceEndpointBase.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Extensions;

namespace VNLib.Plugins.Essentials.Endpoints
{

    /// <summary>
    /// Provides a base class for implementing un-authenticated resource endpoints
    /// with basic (configurable) security checks
    /// </summary>
    public abstract class ResourceEndpointBase : VirtualEndpoint<HttpEntity>
    {
        /// <summary>
        /// Default protection settings. Protection settings are the most 
        /// secure by default, should be loosened an necessary
        /// </summary>
        protected virtual ProtectionSettings EndpointProtectionSettings { get; }

        ///<inheritdoc/>
        public override async ValueTask<VfReturnType> Process(HttpEntity entity)
        {
            try
            {
                ERRNO preProc = PreProccess(entity);

                if (preProc == ERRNO.E_FAIL)
                {
                    return VfReturnType.Forbidden;
                }

                //Entity was responded to by the pre-processor
                if (preProc < 0)
                {
                    return VfReturnType.VirtualSkip;
                }

                //If websockets are quested allow them to be processed in a logged-in/secure context
                if (entity.Server.IsWebSocketRequest)
                {
                    return await WebsocketRequestedAsync(entity);
                }
               
                //Call process method
                return await OnProcessAsync(entity);
            }
            catch (InvalidJsonRequestException ije)
            {
                //Write the je to debug log
                Log.Debug(ije, "Failed to de-serialize a request entity body");
                //If the method is not POST/PUT/PATCH return a json message
                if ((entity.Server.Method & (HttpMethod.HEAD | HttpMethod.OPTIONS | HttpMethod.TRACE | HttpMethod.DELETE)) > 0)
                {
                    return VfReturnType.BadRequest;
                }
                //Only allow if json is an accepted response type
                if (!entity.Server.Accepts(ContentType.Json))
                {
                    return VfReturnType.BadRequest;
                }
                //Build web-message
                WebMessage webm = new()
                {
                    Result = "Request body is not valid json"
                };
                //Set the response webm
                entity.CloseResponseJson(HttpStatusCode.BadRequest, webm);
                //return virtual
                return VfReturnType.VirtualSkip;
            }
            catch (TerminateConnectionException)
            {
                //A TC exception is intentional and should always propagate to the runtime
                throw;
            }
            catch (ContentTypeUnacceptableException)
            {
                /*
                 * The runtime will handle a 406 unaccetptable response 
                 * and invoke the proper error app handler
                 */
                throw;
            }
            //Re-throw exceptions that are cause by reading the transport layer
            catch (IOException ioe) when (ioe.InnerException is SocketException)
            {
                throw;
            }
            catch (Exception ex)
            {
                //Log an uncaught excetpion and return an error code (log may not be initialized)
                Log?.Error(ex);
                return VfReturnType.Error;
            }
        }

        /// <summary>
        /// Allows for synchronous Pre-Processing of an entity. The result 
        /// will determine if the method processing methods will be invoked, or 
        /// a <see cref="VfReturnType.Forbidden"/> error code will be returned
        /// </summary>
        /// <param name="entity">The incomming request to process</param>
        /// <returns>
        /// True if processing should continue, false if the response should be 
        /// <see cref="VfReturnType.Forbidden"/>, less than 0 if entity was 
        /// responded to.
        /// </returns>
        protected virtual ERRNO PreProccess(HttpEntity entity)
        {
            //Disable cache if requested
            if (!EndpointProtectionSettings.EnableCaching)
            {
                entity.Server.SetNoCache();
            }

            //Enforce TLS
            if (!EndpointProtectionSettings.DisabledTlsRequired && !entity.IsSecure && !entity.IsLocalConnection)
            {
                return false;
            }

            //Enforce browser check
            if (!EndpointProtectionSettings.DisableBrowsersOnly && !entity.Server.IsBrowser())
            {
                return false;
            }

            //Enforce refer check
            if (!EndpointProtectionSettings.DisableRefererMatch && entity.Server.Referer != null && !entity.Server.RefererMatch())
            {
                return false;
            }

            //enforce session basic
            if (!EndpointProtectionSettings.DisableSessionsRequired && (!entity.Session.IsSet || entity.Session.IsNew))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Called by the process method to process a connection after it has been pre-processed.
        /// By default, this method simply selects the request method type and invokes the desired 
        /// handler
        /// </summary>
        /// <param name="entity">The entity to process</param>
        /// <returns>A value task that completes when the processing has completed</returns>
        protected virtual ValueTask<VfReturnType> OnProcessAsync(HttpEntity entity)
        {
            ValueTask<VfReturnType> op = entity.Server.Method switch
            {
                //Get request to get account
                HttpMethod.GET => GetAsync(entity),
                HttpMethod.POST => PostAsync(entity),
                HttpMethod.DELETE => DeleteAsync(entity),
                HttpMethod.PUT => PutAsync(entity),
                HttpMethod.PATCH => PatchAsync(entity),
                HttpMethod.OPTIONS => OptionsAsync(entity),
                _ => AlternateMethodAsync(entity, entity.Server.Method),
            };
            return op;
        }

        /// <summary>
        /// This method gets invoked when an incoming POST request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual ValueTask<VfReturnType> PostAsync(HttpEntity entity)
        {
            return ValueTask.FromResult(Post(entity));
        }
        /// <summary>
        /// This method gets invoked when an incoming GET request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual ValueTask<VfReturnType> GetAsync(HttpEntity entity)
        {
            return ValueTask.FromResult(Get(entity));
        }
        /// <summary>
        /// This method gets invoked when an incoming DELETE request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual ValueTask<VfReturnType> DeleteAsync(HttpEntity entity)
        {
            return ValueTask.FromResult(Delete(entity));
        }
        /// <summary>
        /// This method gets invoked when an incoming PUT request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual ValueTask<VfReturnType> PutAsync(HttpEntity entity)
        {
            return ValueTask.FromResult(Put(entity));
        }
        /// <summary>
        /// This method gets invoked when an incoming PATCH request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual ValueTask<VfReturnType> PatchAsync(HttpEntity entity)
        {
            return ValueTask.FromResult(Patch(entity));
        }

        protected virtual ValueTask<VfReturnType> OptionsAsync(HttpEntity entity)
        {
            return ValueTask.FromResult(Options(entity));
        }

        /// <summary>
        /// Invoked when a request is received for a method other than GET, POST, DELETE, or PUT;
        /// </summary>
        /// <param name="entity">The entity that </param>
        /// <param name="method">The request method</param>
        /// <returns>The results of the processing</returns>
        protected virtual ValueTask<VfReturnType> AlternateMethodAsync(HttpEntity entity, HttpMethod method)
        {
            return ValueTask.FromResult(AlternateMethod(entity, method));
        }

        /// <summary>
        /// Invoked when the current endpoint received a websocket request
        /// </summary>
        /// <param name="entity">The entity that requested the websocket</param>
        /// <returns>The results of the operation</returns>
        protected virtual ValueTask<VfReturnType> WebsocketRequestedAsync(HttpEntity entity)
        {
            return ValueTask.FromResult(WebsocketRequested(entity));
        }

        /// <summary>
        /// This method gets invoked when an incoming POST request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual VfReturnType Post(HttpEntity entity)
        {
            //Return method not allowed
            entity.CloseResponse(HttpStatusCode.MethodNotAllowed);
            return VfReturnType.VirtualSkip;
        }
        /// <summary>
        /// This method gets invoked when an incoming GET request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual VfReturnType Get(HttpEntity entity)
        {
            return VfReturnType.ProcessAsFile;
        }
        /// <summary>
        /// This method gets invoked when an incoming DELETE request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual VfReturnType Delete(HttpEntity entity)
        {
            entity.CloseResponse(HttpStatusCode.MethodNotAllowed);
            return VfReturnType.VirtualSkip;
        }
        /// <summary>
        /// This method gets invoked when an incoming PUT request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual VfReturnType Put(HttpEntity entity)
        {
            entity.CloseResponse(HttpStatusCode.MethodNotAllowed);
            return VfReturnType.VirtualSkip;
        }
        /// <summary>
        /// This method gets invoked when an incoming PATCH request to the endpoint has been requested.
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <returns>The result of the operation to return to the file processor</returns>
        protected virtual VfReturnType Patch(HttpEntity entity)
        {
            entity.CloseResponse(HttpStatusCode.MethodNotAllowed);
            return VfReturnType.VirtualSkip;
        }
        /// <summary>
        /// Invoked when a request is received for a method other than GET, POST, DELETE, or PUT;
        /// </summary>
        /// <param name="entity">The entity that </param>
        /// <param name="method">The request method</param>
        /// <returns>The results of the processing</returns>
        protected virtual VfReturnType AlternateMethod(HttpEntity entity, HttpMethod method)
        {
            //Return method not allowed
            entity.CloseResponse(HttpStatusCode.MethodNotAllowed);
            return VfReturnType.VirtualSkip;
        }

        protected virtual VfReturnType Options(HttpEntity entity)
        {
            return VfReturnType.Forbidden;
        }

        /// <summary>
        /// Invoked when the current endpoint received a websocket request
        /// </summary>
        /// <param name="entity">The entity that requested the websocket</param>
        /// <returns>The results of the operation</returns>
        protected virtual VfReturnType WebsocketRequested(HttpEntity entity)
        {
            entity.CloseResponse(HttpStatusCode.Forbidden);
            return VfReturnType.VirtualSkip;
        }
    }
}