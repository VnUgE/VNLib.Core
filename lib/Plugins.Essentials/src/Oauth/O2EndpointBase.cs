/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: O2EndpointBase.cs 
*
* O2EndpointBase.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Extensions;

namespace VNLib.Plugins.Essentials.Oauth
{
    /// <summary>
    /// An base class for HttpEntity processors (endpoints) for processing
    /// Oauth2 client requests. Similar to <seealso cref="ProtectedWebEndpoint"/>
    /// but for Oauth2 sessions
    /// </summary>
    public abstract class O2EndpointBase : ResourceEndpointBase
    {
        ///<inheritdoc/>
        public override async ValueTask<VfReturnType> Process(HttpEntity entity)
        {
            try
            {
                VfReturnType rt;
                ERRNO preProc = PreProccess(entity);

                //Entity was responded to by the pre-processor
                if (preProc < 0)
                {
                    return VfReturnType.VirtualSkip;
                }

                if (preProc == ERRNO.E_FAIL)
                {
                    rt = VfReturnType.Forbidden;
                    goto Exit;
                }

                //If websockets are quested allow them to be processed in a logged-in/secure context
                if (entity.Server.IsWebSocketRequest)
                {
                    return await WebsocketRequestedAsync(entity);
                }

                //Capture return type
                rt = entity.Server.Method switch
                {
                    //Get request to get account
                    HttpMethod.GET => await GetAsync(entity),
                    HttpMethod.POST => await PostAsync(entity),
                    HttpMethod.DELETE => await DeleteAsync(entity),
                    HttpMethod.PUT => await PutAsync(entity),
                    HttpMethod.PATCH => await PatchAsync(entity),
                    HttpMethod.OPTIONS => await OptionsAsync(entity),
                    _ => await AlternateMethodAsync(entity, entity.Server.Method),
                };

            Exit:
                //Write a standard Ouath2 error messag
                return rt switch
                {
                    VfReturnType.VirtualSkip => VfReturnType.VirtualSkip,
                    VfReturnType.ProcessAsFile => VfReturnType.ProcessAsFile,
                    VfReturnType.NotFound => O2VirtualClose(entity, HttpStatusCode.NotFound, ErrorType.InvalidRequest, "The requested resource could not be found"),
                    VfReturnType.BadRequest => O2VirtualClose(entity, HttpStatusCode.BadRequest, ErrorType.InvalidRequest, "Your request was not properlty formatted and could not be proccessed"),
                    VfReturnType.Error => O2VirtualClose(entity, HttpStatusCode.InternalServerError, ErrorType.ServerError, "There was a server error processing your request"),
                    _ => O2VirtualClose(entity, HttpStatusCode.Forbidden, ErrorType.InvalidClient, "You do not have access to this resource"),
                };
            }
            catch (TerminateConnectionException)
            {
                //A TC exception is intentional and should always propagate to the runtime
                throw;
            }
            //Re-throw exceptions that are cause by reading the transport layer
            catch (IOException ioe) when (ioe.InnerException is SocketException)
            {
                throw;
            }
            catch (ContentTypeUnacceptableException)
            {
                //Respond with an 406 error message
                return O2VirtualClose(entity, HttpStatusCode.NotAcceptable, ErrorType.InvalidRequest, "The response type is not acceptable for this endpoint");
            }
            catch (InvalidJsonRequestException)
            {
                //Respond with an error message
                return O2VirtualClose(entity, HttpStatusCode.BadRequest, ErrorType.InvalidRequest, "The request body was not a proper JSON schema");
            }
            catch (Exception ex)
            {
                //Log an uncaught excetpion and return an error code (log may not be initialized)
                Log?.Error(ex);
                //Respond with an error message
                return O2VirtualClose(entity, HttpStatusCode.InternalServerError, ErrorType.ServerError, "There was a server error processing your request");
            }
        }

        /// <summary>
        /// Runs base pre-processing and ensures "sessions" OAuth2 token
        /// session is loaded
        /// </summary>
        /// <param name="entity">The request entity to process</param>
        /// <inheritdoc/>
        protected override ERRNO PreProccess(HttpEntity entity)
        {
            //Make sure session is loaded (token is valid)
            if (!entity.Session.IsSet)
            {
                entity.CloseResponseError(HttpStatusCode.Forbidden, ErrorType.InvalidToken, "Your token is not valid");
                return -1;
            }
            //Must be an oauth session
            if (entity.Session.SessionType != Sessions.SessionType.OAuth2)
            {
                return false;
            }
            return base.PreProccess(entity);
        }

        public static VfReturnType O2VirtualClose(HttpEntity entity, HttpStatusCode statusCode, ErrorType type, string message)
        {
            entity.CloseResponseError(statusCode, type, message);
            return VfReturnType.VirtualSkip;
        }
    }
}
