/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: SemiConsistentVeTable.cs 
*
* SemiConsistentVeTable.cs is part of VNLib.Plugins.Essentials which 
* is part of the larger VNLib collection of libraries and utilities.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Endpoints;

namespace VNLib.Plugins.Essentials
{
    internal class SemiConsistentVeTable : IVirtualEndpointTable
    {

        /*
        * The VE table is read-only for the processor and my only 
        * be updated by the application via the methods below
        * 
        * Since it would be very inefficient to track endpoint users
        * using locks, we can assume any endpoint that is currently 
        * processing requests cannot be stopped, so we just focus on
        * swapping the table when updates need to be made.
        * 
        * This means calls to modify the table will read the table 
        * (clone it), modify the local copy, then exhange it for 
        * the active table so new requests will be processed on the 
        * new table.
        * 
        * To make the calls to modify the table thread safe, a lock is 
        * held while modification operations run, then the updated
        * copy is published. Any threads reading the old table
        * will continue to use a stale endpoint. 
       */

        /// <summary>
        /// A "lookup table" that represents virtual endpoints to be processed when an
        /// incomming connection matches its path parameter
        /// </summary>
        private IReadOnlyDictionary<string, IVirtualEndpoint<HttpEntity>> VirtualEndpoints = new Dictionary<string, IVirtualEndpoint<HttpEntity>>(StringComparer.OrdinalIgnoreCase);


        /*
         * A lock that is held by callers that intend to 
         * modify the vep table at the same time
         */
        private readonly object VeUpdateLock = new();

        ///<inheritdoc/>
        public bool IsEmpty => VirtualEndpoints.Count == 0;


        ///<inheritdoc/>
        public void AddEndpoint(params IEndpoint[] endpoints)
        {
            //Check
            _ = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
            //Make sure all endpoints specify a path
            if (endpoints.Any(static e => string.IsNullOrWhiteSpace(e?.Path)))
            {
                throw new ArgumentException("Endpoints array contains one or more empty endpoints");
            }

            if (endpoints.Length == 0)
            {
                return;
            }

            //Get virtual endpoints
            IEnumerable<IVirtualEndpoint<HttpEntity>> eps = endpoints
                                        .Where(static e => e is IVirtualEndpoint<HttpEntity>)
                                        .Select(static e => (IVirtualEndpoint<HttpEntity>)e);

            //Get http event endpoints and create wrapper classes for conversion
            IEnumerable<IVirtualEndpoint<HttpEntity>> evs = endpoints
                                        .Where(static e => e is IVirtualEndpoint<IHttpEvent>)
                                        .Select(static e => new EvEndpointWrapper((e as IVirtualEndpoint<IHttpEvent>)!));

            //Uinion endpoints by their paths to combine them
            IEnumerable<IVirtualEndpoint<HttpEntity>> allEndpoints = eps.UnionBy(evs, static s => s.Path);

            lock (VeUpdateLock)
            {
                //Clone the current dictonary
                Dictionary<string, IVirtualEndpoint<HttpEntity>> newTable = new(VirtualEndpoints, StringComparer.OrdinalIgnoreCase);
                //Insert the new eps, and/or overwrite old eps
                foreach (IVirtualEndpoint<HttpEntity> ep in allEndpoints)
                {
                    newTable.Add(ep.Path, ep);
                }

                //Store the new table
                _ = Interlocked.Exchange(ref VirtualEndpoints, newTable);
            }
        }

        ///<inheritdoc/>
        public void RemoveEndpoint(params IEndpoint[] eps)
        {
            _ = eps ?? throw new ArgumentNullException(nameof(eps));
            //Call remove on path
            RemoveEndpoint(eps.Select(static s => s.Path).ToArray());
        }

        ///<inheritdoc/>
        public void RemoveEndpoint(params string[] paths)
        {
            _ = paths ?? throw new ArgumentNullException(nameof(paths));

            //Make sure all endpoints specify a path
            if (paths.Any(static e => string.IsNullOrWhiteSpace(e)))
            {
                throw new ArgumentException("Paths array contains one or more empty strings");
            }

            if (paths.Length == 0)
            {
                return;
            }

            //take update lock
            lock (VeUpdateLock)
            {
                //Clone the current dictonary
                Dictionary<string, IVirtualEndpoint<HttpEntity>> newTable = new(VirtualEndpoints, StringComparer.OrdinalIgnoreCase);

                foreach (string eps in paths)
                {
                    _ = newTable.Remove(eps);
                }

                //Store the new table
                _ = Interlocked.Exchange(ref VirtualEndpoints, newTable);
            }
        }

        ///<inheritdoc/>
        public bool TryGetEndpoint(string path, out IVirtualEndpoint<HttpEntity>? endpoint) => VirtualEndpoints.TryGetValue(path, out endpoint);


        /* 
         * Wrapper class for converting IHttpEvent endpoints to 
         * httpEntityEndpoints
         */
        private sealed record class EvEndpointWrapper(IVirtualEndpoint<IHttpEvent> Wrapped) : IVirtualEndpoint<HttpEntity>
        {
            string IEndpoint.Path => Wrapped.Path;
            ValueTask<VfReturnType> IVirtualEndpoint<HttpEntity>.Process(HttpEntity entity) => Wrapped.Process(entity);
        }
    }
}