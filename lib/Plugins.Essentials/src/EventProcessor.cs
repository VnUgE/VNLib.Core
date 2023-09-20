/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: EventProcessor.cs 
*
* EventProcessor.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Plugins.Essentials.Accounts;
using VNLib.Plugins.Essentials.Content;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Essentials.Middleware;
using VNLib.Plugins.Essentials.Endpoints;

#nullable enable

namespace VNLib.Plugins.Essentials
{

    /// <summary>
    /// Provides an abstract base implementation of <see cref="IWebRoot"/>
    /// that breaks down simple processing procedures, routing, and session 
    /// loading.
    /// </summary>
    public abstract class EventProcessor : IWebRoot, IWebProcessor
    {
        private static readonly AsyncLocal<EventProcessor?> _currentProcessor = new();

        /// <summary>
        /// Gets the current (ambient) async local event processor
        /// </summary>
        public static EventProcessor? Current => _currentProcessor.Value;

        /// <summary>
        /// The filesystem entrypoint path for the site
        /// </summary>
        public abstract string Directory { get; }

        ///<inheritdoc/>
        public abstract string Hostname { get; }

        /// <summary>
        /// Gets the EP processing options
        /// </summary>
        public abstract IEpProcessingOptions Options { get; }

        ///<inheritdoc/>
        public abstract IReadOnlyDictionary<string, Redirect> Redirects { get; }

        /// <summary>
        /// Event log provider
        /// </summary>
        protected abstract ILogProvider Log { get; }

        /// <summary>
        /// <para>
        /// Called when the server intends to process a file and requires translation from a 
        /// uri path to a usable filesystem path 
        /// </para>
        /// <para>
        /// NOTE: This function must be thread-safe!
        /// </para>
        /// </summary>
        /// <param name="requestPath">The path requested by the request </param>
        /// <returns>The translated and filtered filesystem path used to identify the file resource</returns>
        public abstract string TranslateResourcePath(string requestPath);

        /// <summary>
        /// <para>
        /// When an error occurs and is handled by the library, this event is invoked 
        /// </para>
        /// <para>
        /// NOTE: This function must be thread-safe!
        /// </para>
        /// </summary>
        /// <param name="errorCode">The error code that was created during processing</param>
        /// <param name="entity">The active IHttpEvent representing the faulted request</param>
        /// <returns>A value indicating if the entity was proccsed by this call</returns>
        public abstract bool ErrorHandler(HttpStatusCode errorCode, IHttpEvent entity);

        /// <summary>
        /// For pre-processing a request entity before all endpoint lookups are performed
        /// </summary>
        /// <param name="entity">The http entity to process</param>
        /// <returns>The results to return to the file processor, or null of the entity requires further processing</returns>
        public abstract ValueTask<FileProcessArgs> PreProcessEntityAsync(HttpEntity entity);

        /// <summary>
        /// Allows for post processing of a selected <see cref="FileProcessArgs"/> for the given entity
        /// </summary>
        /// <param name="entity">The http entity to process</param>
        /// <param name="chosenRoutine">The selected file processing routine for the given request</param>
        public abstract void PostProcessFile(HttpEntity entity, in FileProcessArgs chosenRoutine);

        ///<inheritdoc/>
        public abstract IAccountSecurityProvider AccountSecurity { get; }

        /// <summary>
        /// The table of virtual endpoints that will be used to process requests
        /// </summary>
        /// <remarks>
        /// May be overriden to provide a custom endpoint table
        /// </remarks>
        public virtual IVirtualEndpointTable EndpointTable { get; } = new SemiConsistentVeTable();

        /// <summary>
        /// The middleware chain that will be used to process requests
        /// </summary>
        /// <remarks>
        /// If derrieved, may be overriden to provide a custom middleware chain
        /// </remarks>
        public virtual IHttpMiddlewareChain MiddlewareChain { get; } = new SemiConistentMiddlewareChain();


        /// <summary>
        /// An <see cref="ISessionProvider"/> that connects stateful sessions to 
        /// HTTP connections
        /// </summary>
        private ISessionProvider? Sessions;

        /// <summary>
        /// Sets or resets the current <see cref="ISessionProvider"/>
        /// for all connections
        /// </summary>
        /// <param name="sp">The new <see cref="ISessionProvider"/></param>
        public void SetSessionProvider(ISessionProvider? sp) => _ = Interlocked.Exchange(ref Sessions, sp);


        /// <summary>
        /// An <see cref="IPageRouter"/> to route files to be processed
        /// </summary>
        private IPageRouter? Router;

        /// <summary>
        /// Sets or resets the current <see cref="IPageRouter"/>
        /// for all connections
        /// </summary>
        /// <param name="router"><see cref="IPageRouter"/> to route incomming connections</param>
        public void SetPageRouter(IPageRouter? router) => _ = Interlocked.Exchange(ref Router, router);
    
   

        ///<inheritdoc/>
        public virtual async ValueTask ClientConnectedAsync(IHttpEvent httpEvent)
        {
            //load ref to session provider
            ISessionProvider? _sessions = Sessions;
            
            //Set ambient processor context
            _currentProcessor.Value = this;

            //Start cancellation token
            CancellationTokenSource timeout = new(Options.ExecutionTimeout);

            FileProcessArgs args;

            try
            {
                //Session handle, default to the shared empty session
                SessionHandle sessionHandle = SessionHandle.Empty;
                
                //If sessions are set, get a session for the current connection
                if (_sessions != null)
                {
                    //Get the session
                    sessionHandle = await _sessions.GetSessionAsync(httpEvent, timeout.Token);
                    //If the processor had an error recovering the session, return the result to the processor
                    if (sessionHandle.EntityStatus != FileProcessArgs.Continue)
                    {
                        ProcessFile(httpEvent, sessionHandle.EntityStatus);
                        return;
                    }
                }
                try
                {
                    //Setup entity
                    HttpEntity entity = new(httpEvent, this, in sessionHandle, timeout.Token);

                    //Pre-process entity
                    args = await PreProcessEntityAsync(entity);

                    //If preprocess returned a value, exit
                    if (args != FileProcessArgs.Continue)
                    {
                        ProcessFile(httpEvent, in args);
                        return;
                    }

                    //Handle middleware before file processing
                    LinkedListNode<IHttpMiddleware>? mwNode = MiddlewareChain.GetCurrentHead();

                    //Loop though nodes
                    while(mwNode != null)
                    {
                        //Process
                        HttpMiddlewareResult result = await mwNode.ValueRef.ProcessAsync(entity);
                        
                        switch (result)
                        {
                            //move next
                            case HttpMiddlewareResult.Continue:
                                mwNode = mwNode.Next;
                                break;

                            //Middleware completed the connection, time to exit
                            case HttpMiddlewareResult.Complete:
                                return;
                        }
                    }                

                    if (!EndpointTable.IsEmpty)
                    {
                        //See if the virtual file is servicable
                        if (!EndpointTable.TryGetEndpoint(entity.Server.Path, out IVirtualEndpoint<HttpEntity>? vf))
                        {
                            args = FileProcessArgs.Continue;
                        }
                        else
                        {
                            //Invoke the page handler process method
                            VfReturnType rt = await vf.Process(entity);

                            //Process a virtual file
                            args = GetArgsFromReturn(entity, rt);
                        }                      

                        //If the entity was processed, exit
                        if (args != FileProcessArgs.Continue)
                        {
                            ProcessFile(httpEvent, in args);
                            return;
                        }
                    }

                    //If no virtual processor handled the ws request, deny it
                    if (entity.Server.IsWebSocketRequest)
                    {
                        ProcessFile(httpEvent, in FileProcessArgs.Deny);
                        return;
                    }

                    //Finally process as file
                    args = await RouteFileAsync(entity);

                    //Finally process the file
                    ProcessFile(httpEvent, in args);
                }
                finally
                {
                    //Capture all session release exceptions 
                    try
                    {
                        //Release the session
                        await sessionHandle.ReleaseAsync(httpEvent);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception raised while releasing the assocated session");
                    }
                }
            }
            catch (ContentTypeUnacceptableException)
            {
                /*
                 *  The user application attempted to set a content that the client does not accept
                 *  Assuming this exception was uncaught by application code, there should not be 
                 *  any response body, either way we should respond with the unacceptable status code
                 */
                CloseWithError(HttpStatusCode.NotAcceptable, httpEvent);
            }
            catch (TerminateConnectionException)
            {
                throw;
            }
            catch (ResourceUpdateFailedException ruf)
            {
                Log.Warn(ruf);
                CloseWithError(HttpStatusCode.ServiceUnavailable, httpEvent);
            }
            catch (SessionException se)
            {
                Log.Warn(se, "An exception was raised while attempting to get or save a session");
                CloseWithError(HttpStatusCode.ServiceUnavailable, httpEvent);
                return;
            }
            catch (OperationCanceledException oce)
            {
                Log.Warn(oce, "Request execution time exceeded, connection terminated");
                CloseWithError(HttpStatusCode.ServiceUnavailable, httpEvent);
            }
            catch (IOException ioe) when (ioe.InnerException is SocketException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Unhandled exception during application code execution.");
                //Invoke the root error handler
                CloseWithError(HttpStatusCode.InternalServerError, httpEvent);
            }
            finally
            {
                timeout.Dispose();
                _currentProcessor.Value = null;
            }
        }

        /// <summary>
        /// Accepts the entity to process a file for an the selected <see cref="FileProcessArgs"/> 
        /// by user code and determines what file-system file to open and respond to the connection with.
        /// </summary>
        /// <param name="entity">The entity to process the file for</param>
        /// <param name="args">The selected <see cref="FileProcessArgs"/> to determine what file to process</param>
        protected virtual void ProcessFile(IHttpEvent entity, in FileProcessArgs args)
        {
            try
            {
                string? filename = null;
                //Switch on routine
                switch (args.Routine)
                {
                    //Close the connection with an error state
                    case FpRoutine.Error:
                        CloseWithError(HttpStatusCode.InternalServerError, entity);
                        return;
                    //Redirect the user
                    case FpRoutine.Redirect:
                        //Set status code
                        entity.Redirect(RedirectType.Found, args.Alternate);
                        return;
                    //Deny
                    case FpRoutine.Deny:
                        CloseWithError(HttpStatusCode.Forbidden, entity);
                        return;
                    //Not return not found
                    case FpRoutine.NotFound:
                        CloseWithError(HttpStatusCode.NotFound, entity);
                        return;
                    //Serve other file
                    case FpRoutine.ServeOther:
                        {
                            //Use the specified relative alternate path the user specified
                            if (FindResourceInRoot(args.Alternate, out string otherPath))
                            {
                                filename = otherPath;
                            }
                        }
                        break;
                    //Normal file lookup
                    case FpRoutine.Continue:
                        {
                            //Lookup the file based on the client requested local path
                            if (FindResourceInRoot(entity.Server.Path, out string path))
                            {
                                filename = path;
                            }
                        }
                        break;
                    //The user indicated that the file is a fully qualified path, and should be treated directly
                    case FpRoutine.ServeOtherFQ:
                        {
                            //Get the absolute path of the file rooted in the current server root and determine if it exists
                            if (FindResourceInRoot(args.Alternate, true, out string fqPath))
                            {
                                filename = fqPath;
                            }
                        }
                        break;
                    //The user has indicated they handled all necessary action, and we will exit
                    case FpRoutine.VirtualSkip:
                        return;
                    default:
                        break;
                }

                //If the file was not set or the request method is not a GET (or HEAD), return not-found
                if (filename == null || (entity.Server.Method & (HttpMethod.GET | HttpMethod.HEAD)) == 0)
                {
                    CloseWithError(HttpStatusCode.NotFound, entity);
                    return;
                }

                DateTime fileLastModified = File.GetLastWriteTimeUtc(filename);

                //See if the last modifed header was set
                DateTimeOffset? ifModifedSince = entity.Server.LastModified();
                
                //If the header was set, check the date, if the file has been modified since, continue sending the file
                if (ifModifedSince.HasValue && ifModifedSince.Value > fileLastModified)
                {
                    //File has not been modified 
                    entity.CloseResponse(HttpStatusCode.NotModified);
                    return;
                }

                //Get the content type of he file
                ContentType fileType = HttpHelpers.GetContentTypeFromFile(filename);
                
                //Make sure the client accepts the content type
                if (entity.Server.Accepts(fileType))
                {
                    //set last modified time as the files last write time
                    entity.Server.LastModified(fileLastModified);

                    //try to open the selected file for reading and allow sharing
                    FileStream fs = new (filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    
                    //Check for range 
                    if (entity.Server.Range != null && entity.Server.Range.Item1 > 0)
                    {
                        //Seek the stream to the specified position
                        fs.Seek(entity.Server.Range.Item1, SeekOrigin.Begin);
                        entity.CloseResponse(HttpStatusCode.PartialContent, fileType, fs);
                    }
                    else
                    {
                        //send the whole file
                        entity.CloseResponse(HttpStatusCode.OK, fileType, fs);
                    }
                }
                else
                {
                    //Unacceptable
                    CloseWithError(HttpStatusCode.NotAcceptable, entity);
                }
            }
            catch (IOException ioe)
            {
                Log.Information(ioe, "Unhandled exception during file opening.");
                CloseWithError(HttpStatusCode.Locked, entity);
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception during file opening.");
                //Invoke the root error handler
                CloseWithError(HttpStatusCode.InternalServerError, entity);
                return;
            }
        }

        private void CloseWithError(HttpStatusCode code, IHttpEvent entity)
        {
            //Invoke the inherited error handler
            if (!ErrorHandler(code, entity))
            {
                //Disable cache
                entity.Server.SetNoCache();
                //Error handler does not have a response for the error code, so return a generic error code
                entity.CloseResponse(code);
            }
        }
      
        /// <summary>
        /// Gets the <see cref="FileProcessArgs"/> that will finalize the response from the 
        /// given <see cref="VfReturnType"/>
        /// </summary>
        /// <param name="entity">The entity to be processed</param>
        /// <param name="returnType">The virtual file processor return type</param>
        /// <returns>The process args to end processing for the virtual endpoint</returns>
        protected virtual FileProcessArgs GetArgsFromReturn(HttpEntity entity, VfReturnType returnType)
        {
            if (returnType == VfReturnType.VirtualSkip)
            {
                //Virtual file was handled by the handler
                return FileProcessArgs.VirtualSkip;
            }
            else if (returnType == VfReturnType.ProcessAsFile)
            {
                return FileProcessArgs.Continue;
            }

            //If not a get request, process it directly
            if (entity.Server.Method == HttpMethod.GET)
            {
                switch (returnType)
                {
                    case VfReturnType.Forbidden:
                        return FileProcessArgs.Deny;
                    case VfReturnType.NotFound:
                        return FileProcessArgs.NotFound;
                    case VfReturnType.Error:
                        return FileProcessArgs.Error;
                    default:
                        break;
                }
            }

            switch (returnType)
            {
                case VfReturnType.Forbidden:
                    entity.CloseResponse(HttpStatusCode.Forbidden);
                    break;
                case VfReturnType.BadRequest:
                    entity.CloseResponse(HttpStatusCode.BadRequest);
                    break;
                case VfReturnType.Error:
                    entity.CloseResponse(HttpStatusCode.InternalServerError);
                    break;
                case VfReturnType.NotFound:
                default:
                    entity.CloseResponse(HttpStatusCode.NotFound);
                    break;
            }

            return FileProcessArgs.VirtualSkip;
        }
      
        /// <summary>
        /// Determines the best <see cref="FileProcessArgs"/> processing response for the given connection.
        /// Alternativley may respond to the entity directly.
        /// </summary>
        /// <param name="entity">The http entity to process</param>
        /// <returns>The results to return to the file processor, this method must return an argument</returns>
        protected virtual async ValueTask<FileProcessArgs> RouteFileAsync(HttpEntity entity)
        {
            //Read local copy of the router            
            IPageRouter? router = Router;

            //Make sure router is set
            if (router == null)
            {
                return FileProcessArgs.Continue;
            }
            //Get a file routine 
            FileProcessArgs routine = await router.RouteAsync(entity);
            //Call post processor method
            PostProcessFile(entity, in routine);
            //Return the routine
            return routine;
        }      

        /// <summary>
        /// Finds the file specified by the request and the server root the user has requested.
        /// Determines if it exists, has permissions to access it, and allowed file attributes.
        /// Also finds default files and files without extensions
        /// </summary>
        public bool FindResourceInRoot(string resourcePath, bool fullyQualified, out string path)
        {
            //Special case where user's can specify a fullly qualified path (meant to reach a remote file, eg UNC/network share or other disk)
            if (fullyQualified && Path.IsPathRooted(resourcePath) && Path.IsPathFullyQualified(resourcePath) && FileOperations.FileExists(resourcePath))
            {
                path = resourcePath;
                return true;
            }
            //Otherwise invoke non fully qualified path
            return FindResourceInRoot(resourcePath, out path);
        }

        /// <summary>
        /// Determines if a requested resource exists within the <see cref="EventProcessor"/> and is allowed to be accessed.
        /// </summary>
        /// <param name="resourcePath">The path to the resource</param>
        /// <param name="path">An out parameter that is set to the absolute path to the existing and accessable resource</param>
        /// <returns>True if the resource exists and is allowed to be accessed</returns>
        public bool FindResourceInRoot(string resourcePath, out string path)
        {
            //Check after fully qualified path name because above is a special case
            path = TranslateResourcePath(resourcePath);
            string extension = Path.GetExtension(path);
            //Make sure extension isnt blocked
            if (Options.ExcludedExtensions.Contains(extension))
            {
                return false;
            }
            //Trailing / means dir, so look for a default file (index.html etc) (most likely so check first?)
            if (Path.EndsInDirectorySeparator(path))
            {
                string comp = path;
                //Find default file if blank
                foreach (string d in Options.DefaultFiles)
                {
                    path = Path.Combine(comp, d);
                    if (FileOperations.FileExists(path))
                    {
                        //Get attributes
                        FileAttributes att = FileOperations.GetAttributes(path);
                        //Make sure the file is accessable and isnt an unsafe file
                        return ((att & Options.AllowedAttributes) > 0) && ((att & Options.DissallowedAttributes) == 0);
                    }
                }
            }
            //try the file as is
            else if (FileOperations.FileExists(path))
            {
                //Get attributes
                FileAttributes att = FileOperations.GetAttributes(path);
                //Make sure the file is accessable and isnt an unsafe file
                return ((att & Options.AllowedAttributes) > 0) && ((att & Options.DissallowedAttributes) == 0);
            }
            return false;
        }
    }
}