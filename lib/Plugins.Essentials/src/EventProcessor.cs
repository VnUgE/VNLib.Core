/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Plugins.Essentials.Accounts;
using VNLib.Plugins.Essentials.Content;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Essentials
{

    /// <summary>
    /// Provides an abstract base implementation of <see cref="IWebRoot"/>
    /// that breaks down simple processing procedures, routing, and session 
    /// loading.
    /// </summary>
    public abstract class EventProcessor(EventProcessorConfig config) : IWebRoot, IWebProcessor
    {
        private static readonly AsyncLocal<EventProcessor?> _currentProcessor = new();

        /// <summary>
        /// Gets the current (ambient) async local event processor
        /// </summary>
        public static EventProcessor? Current => _currentProcessor.Value;

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
        /// For pre-processing a request entity before all processing happens, but after 
        /// a session is attached to the entity.
        /// </summary>
        /// <param name="entity">The http entity to process</param>
        /// <param name="result">The results to return to the file processor, or <see cref="FileProcessArgs.Continue"/> to continue further processing</param>
        public abstract void PreProcessEntity(HttpEntity entity, out FileProcessArgs result);

        /// <summary>
        /// Allows for post processing of a selected <see cref="FileProcessArgs"/> for the given entity
        /// <para>
        /// Post processing may mutate the <paramref name="chosenRoutine"/> to change the 
        /// result of the operation. Consider events with the <see cref="FileProcessArgs.VirtualSkip"/>
        /// have already been responded to.
        /// </para>
        /// </summary>
        /// <param name="entity">The http entity to process</param>
        /// <param name="chosenRoutine">The selected file processing routine for the given request</param>
        public abstract void PostProcessEntity(HttpEntity entity, ref FileProcessArgs chosenRoutine);

        ///<inheritdoc/>
        public virtual EventProcessorConfig Options => config;

        ///<inheritdoc/>
        public string Hostname => config.Hostname;


        /*
         * Okay. So this is suposed to be a stupid fast lookup table for lock-free 
         * service pool exchanges. The goal is for future runtime service expansion.
         * 
         * The reason lookups must be unnoticabyly fast is because the should be
         * VERY rarley changed and will be read on every request.
         * 
         * The goal of this table specifially is to make sure requesting a desired 
         * service is extremely fast and does not require any locks or synchronization.
         */
        const int SESS_INDEX = 0;
        const int ROUTER_INDEX = 1;
        const int SEC_INDEX = 2;
        
        /// <summary>
        /// The internal service pool for the processor
        /// </summary>
        protected readonly HttpProcessorServicePool ServicePool = new([
            //Order must match the indexes above
            typeof(ISessionProvider),           
            typeof(IPageRouter), 
            typeof(IAccountSecurityProvider)
        ]);
      

        /*
         * Fields are not marked as volatile because they should not 
         * really be updated at all in production uses, and if hot-reload
         * is used, I don't consider a dirty read to be a large enough 
         * problem here.
         */

        private IAccountSecurityProvider? _accountSec;
        private ISessionProvider? _sessions;
        private IPageRouter? _router;
      
        ///<inheritdoc/>
        public IAccountSecurityProvider? AccountSecurity
        {
            //Exchange the version of the account security provider
            get => ServicePool.ExchangeVersion(ref _accountSec, SEC_INDEX);
        }

        private readonly MiddlewareController _middleware = new(config);

        private readonly FilePathCache _pathCache = FilePathCache.GetCacheStore(config.FilePathCacheMaxAge);

        ///<inheritdoc/>
        public virtual async ValueTask ClientConnectedAsync(IHttpEvent httpEvent)
        {
            /*
             * read any "volatile" properties into local copies for the duration
             * of the request processing. This is to ensure that the properties
             * are not changed during the processing of the request.
             */

            ISessionProvider? sessions = ServicePool.ExchangeVersion(ref _sessions, SESS_INDEX);
            IPageRouter? router = ServicePool.ExchangeVersion(ref _router, ROUTER_INDEX);

            //event cancellation token
            HttpEntity entity = new(httpEvent, this);

            //Set ambient processor context
            _currentProcessor.Value = this;           

            try
            {
                //If sessions are set, get a session for the current connection
                if (sessions != null)
                {
                    //Get the session
                    entity.EventSessionHandle = await sessions.GetSessionAsync(httpEvent, entity.EventCancellation);

                    //If the processor had an error recovering the session, return the result to the processor
                    if (entity.EventSessionHandle.EntityStatus != FileProcessArgs.Continue)
                    {
                        goto ProcessRoutine;
                    }

                    //Attach the new session to the entity
                    entity.AttachSession();
                }

                try
                {
                    PreProcessEntity(entity, out entity.EventArgs);

                    //If preprocess returned a value, exit
                    if (entity.EventArgs != FileProcessArgs.Continue)
                    {
                        goto RespondAndExit;
                    }

                    //Exec middleware
                    if(!await _middleware.ProcessAsync(entity))
                    {
                        goto RespondAndExit;
                    }

                    if (!config.EndpointTable.IsEmpty)
                    {
                        //See if the virtual file is servicable
                        if (config.EndpointTable.TryGetEndpoint(entity.Server.Path, out IVirtualEndpoint<HttpEntity>? vf))
                        {
                            //Invoke the page handler process method
                            VfReturnType rt = await vf.Process(entity);

                            //Process a virtual file
                            GetArgsFromVirtualReturn(entity, rt, out entity.EventArgs);

                            //If the entity was processed by the handler, exit
                            if (entity.EventArgs != FileProcessArgs.Continue)
                            {
                                goto RespondAndExit;
                            }
                        }
                    }

                    //If no virtual processor handled the ws request, deny it
                    if (entity.Server.IsWebSocketRequest)
                    {
                        entity.EventArgs = FileProcessArgs.Deny;
                    }
                    else
                    {
                        //Finally route the connection as a file
                        entity.EventArgs = await RouteFileAsync(router, entity);
                    }

                RespondAndExit:

                    //Normal post-process
                    _middleware.PostProcess(entity);

                    //Call post processor method
                    PostProcessEntity(entity, ref entity.EventArgs);
                }
                finally 
                {
                    //Capture all session release exceptions 
                    try
                    {
                        //Release the session
                        await entity.EventSessionHandle.ReleaseAsync(httpEvent);
                    }
                    catch (Exception ex)
                    {
                        config.Log.Error(ex, "Exception raised while releasing the assocated session");
                    }
                }

            ProcessRoutine:

                //Finally process the file
                ProcessRoutine(httpEvent, in entity.EventArgs);
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
                config.Log.Warn(ruf);
                CloseWithError(HttpStatusCode.ServiceUnavailable, httpEvent);
            }
            catch (SessionException se)
            {
                config.Log.Warn(se, "An exception was raised while attempting to get or save a session");
                CloseWithError(HttpStatusCode.ServiceUnavailable, httpEvent);
            }
            catch (OperationCanceledException oce)
            {
                config.Log.Warn(oce, "Request execution time exceeded, connection terminated");
                CloseWithError(HttpStatusCode.ServiceUnavailable, httpEvent);
            }
            catch (IOException ioe) when (ioe.InnerException is SocketException)
            {
                throw;
            }
            catch (Exception ex)
            {
                config.Log.Warn(ex, "Unhandled exception during application code execution.");
                //Invoke the root error handler
                CloseWithError(HttpStatusCode.InternalServerError, httpEvent);
            }
            finally
            {
                entity.Dispose();
                _currentProcessor.Value = null;
            }
        }

        /// <summary>
        /// Accepts the entity to process a file for an the selected <see cref="FileProcessArgs"/> 
        /// by user code and determines what file-system file to open and respond to the connection with.
        /// </summary>
        /// <param name="entity">The entity to process the file for</param>
        /// <param name="args">The selected <see cref="FileProcessArgs"/> to determine what file to process</param>
        protected virtual void ProcessRoutine(IHttpEvent entity, ref readonly FileProcessArgs args)
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
                
                if (!entity.Server.Accepts(fileType))
                {
                    //Unacceptable
                    CloseWithError(HttpStatusCode.NotAcceptable, entity);
                    return;
                }

                //set last modified time as the files last write time
                entity.Server.LastModified(fileLastModified);


                //Open the file handle directly, reading will always be sequentially read and async

#pragma warning disable CA2000 // Dispose objects before losing scope
                DirectFileStream dfs = DirectFileStream.Open(filename);
#pragma warning restore CA2000 // Dispose objects before losing scope

                long endOffset = checked((long)entity.Server.Range.End);
                long startOffset = checked((long)entity.Server.Range.Start);

                //Follows rfc7233 -> https://www.rfc-editor.org/rfc/rfc7233#section-1.2
                switch (entity.Server.Range.RangeType)
                {
                    case HttpRangeType.FullRange:
                        if (endOffset > dfs.Length || endOffset - startOffset < 0)
                        {
                            //The start offset is greater than the file length, return range not satisfiable
                            entity.Server.Headers[HttpResponseHeader.ContentRange] = $"bytes */{dfs.Length}";
                            entity.CloseResponse(HttpStatusCode.RequestedRangeNotSatisfiable);
                        }
                        else
                        {
                            //Seek the stream to the specified start position
                            dfs.Seek(startOffset, SeekOrigin.Begin);

                            //Set range header, by passing the actual full content size
                            entity.SetContentRangeHeader(entity.Server.Range, dfs.Length);

                            //Send the response, with actual response length (diff between stream length and position)
                            entity.CloseResponse(HttpStatusCode.PartialContent, fileType, dfs, endOffset - startOffset + 1);
                        }
                        break;
                    case HttpRangeType.FromStart:
                        if (startOffset > dfs.Length)
                        {
                            //The start offset is greater than the file length, return range not satisfiable
                            entity.Server.Headers[HttpResponseHeader.ContentRange] = $"bytes */{dfs.Length}";
                            entity.CloseResponse(HttpStatusCode.RequestedRangeNotSatisfiable);
                        }
                        else
                        {
                            //Seek the stream to the specified start position
                            dfs.Seek(startOffset, SeekOrigin.Begin);
                           
                            entity.SetContentRangeHeader(entity.Server.Range, dfs.Length);
                            
                            entity.CloseResponse(HttpStatusCode.PartialContent, fileType, dfs, dfs.Length - dfs.Position);
                        }
                        break;

                    case HttpRangeType.FromEnd:
                        if (endOffset > dfs.Length)
                        {
                            //The end offset is greater than the file length, return range not satisfiable
                            entity.Server.Headers[HttpResponseHeader.ContentRange] = $"bytes */{dfs.Length}";
                            entity.CloseResponse(HttpStatusCode.RequestedRangeNotSatisfiable);
                        }
                        else
                        {
                            //Seek the stream to the specified end position, server auto range will handle the rest
                            dfs.Seek(-endOffset, SeekOrigin.End);
                            
                            entity.SetContentRangeHeader(entity.Server.Range, dfs.Length);
                            
                            entity.CloseResponse(HttpStatusCode.PartialContent, fileType, dfs, dfs.Length - dfs.Position);
                        }
                        break;
                    //No range or invalid range (the server is supposed to ignore invalid ranges)
                    default:
                        //send the whole file
                        entity.CloseResponse(HttpStatusCode.OK, fileType, dfs, dfs.Length);
                        break;
                }
            }
            catch (IOException ioe)
            {
                config.Log.Information(ioe, "Unhandled exception during file opening.");
                CloseWithError(HttpStatusCode.Locked, entity);
                return;
            }
            catch (Exception ex)
            {
                config.Log.Error(ex, "Unhandled exception during file opening.");
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
        /// <param name="args">The process args to end processing for the virtual endpoint</param>
        protected virtual void GetArgsFromVirtualReturn(HttpEntity entity, VfReturnType returnType, out FileProcessArgs args)
        {
            if (returnType == VfReturnType.VirtualSkip)
            {
                //Virtual file was handled by the handler
                args = FileProcessArgs.VirtualSkip;
                return;
            }
            else if (returnType == VfReturnType.ProcessAsFile)
            {
                args = FileProcessArgs.Continue;
                return;
            }

            //If not a get request, process it directly
            if (entity.Server.Method == HttpMethod.GET)
            {
                switch (returnType)
                {
                    case VfReturnType.Forbidden:
                        args = FileProcessArgs.Deny;
                        return;
                    case VfReturnType.NotFound:
                        args = FileProcessArgs.NotFound;
                        return;
                    case VfReturnType.Error:
                        args = FileProcessArgs.Error;
                        return;
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

            args = FileProcessArgs.VirtualSkip;
        }
      
        /// <summary>
        /// Determines the best <see cref="FileProcessArgs"/> processing response for the given connection.
        /// Alternativley may respond to the entity directly.
        /// </summary>
        /// <param name="router">A reference to the current <see cref="IPageRouter"/> instance if cofigured</param>
        /// <param name="entity">The http entity to process</param>
        /// <returns>The results to return to the file processor, this method must return an argument</returns>
        protected virtual ValueTask<FileProcessArgs> RouteFileAsync(IPageRouter? router, HttpEntity entity)
        {
            if(router != null)
            {
                //Route file async from the router reference
                return router.RouteAsync(entity);
            }
            else
            {
                return ValueTask.FromResult(FileProcessArgs.Continue);
            }
        }      

        /// <summary>
        /// Finds the file specified by the request and the server root the user has requested.
        /// Determines if it exists, has permissions to access it, and allowed file attributes.
        /// Also finds default files and files without extensions
        /// </summary>
        public bool FindResourceInRoot(string resourcePath, bool fullyQualified, out string path)
        {
            //Special case where user's can specify a fullly qualified path (meant to reach a remote file, eg UNC/network share or other disk)
            if (fullyQualified 
                && Path.IsPathRooted(resourcePath) 
                && Path.IsPathFullyQualified(resourcePath) 
                && FileOperations.FileExists(resourcePath)
            )
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
            //Try to get the translated file path from cache
            if (_pathCache.TryGetMappedPath(resourcePath, out path))
            {
                return true;
            }

            //Cache miss, force a lookup
            if (FindFileResourceInternal(resourcePath, out path))
            {
                //Store the path in the cache for next lookup
                _pathCache.StorePathMapping(resourcePath, path);
                return true;
            }

            return false;
        }

        private bool FindFileResourceInternal(string resourcePath, out string path)
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

        /// <summary>
        /// A pool of services that an <see cref="EventProcessor"/> will use can be exchanged at runtime
        /// </summary>
        /// <param name="expectedTypes">An ordered array of desired types</param>
        protected sealed class HttpProcessorServicePool(Type[] expectedTypes)
        {
            private readonly uint[] _serviceTable = new uint[expectedTypes.Length];
            private readonly WeakReference<object?>[] _objects = CreateServiceArray(expectedTypes.Length);
            private readonly ImmutableArray<Type> _types = [.. expectedTypes];

            /// <summary>
            /// Gets all of the desired types for the servicec pool
            /// </summary>
            public ImmutableArray<Type> Types => _types;

            /// <summary>
            /// Sets a desired service instance in the pool, or clears it
            /// from the pool.
            /// </summary>
            /// <param name="service">The service type to publish</param>
            /// <param name="instance">The service instance to store</param>
            public void SetService(Type service, object? instance)
            {
                ArgumentNullException.ThrowIfNull(service);

                //Make sure the instance is of the correct type
                if(instance is not null && !service.IsInstanceOfType(instance))
                {
                    throw new ArgumentException("The instance does not match the service type");
                }

                //If the service type is not desired, return
                int index = Array.IndexOf(expectedTypes, service);
                if (index != -1)
                {
                    //Set the service as a new weak reference atomically
                    Volatile.Write(ref _objects[index], new(instance));

                    //Notify that the service has been updated
                    Interlocked.Exchange(ref _serviceTable[index], 1);
                }
            }

            /// <summary>
            /// Determines if a desired services has been modified within
            /// the pool, if it has, the service will be exchanged for the
            /// new service.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="instance">A reference to the internal instance to exhange</param>
            /// <param name="tableIndex">The constant index for the service type</param>
            /// <returns>The exchanged service instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            internal T? ExchangeVersion<T>(ref T? instance, int tableIndex) where T : class?
            {
                //Clear modified flag
                if (Interlocked.Exchange(ref _serviceTable[tableIndex], 0) == 1)
                {
                    //Atomic read on the reference instance
                    WeakReference<object?> wr = Volatile.Read(ref _objects[tableIndex]);

                    //Try to get the object instance
                    _ = wr.TryGetTarget(out object? value);

                    instance = (T?)value;
                }

                return instance;
            }

            private static WeakReference<object?>[] CreateServiceArray(int size)
            {
                WeakReference<object?>[] arr = new WeakReference<object?>[size];
                Array.Fill(arr, new (null));
                return arr;
            }
        }
    }    
}
