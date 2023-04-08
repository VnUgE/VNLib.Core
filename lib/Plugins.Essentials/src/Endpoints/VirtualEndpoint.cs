/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: VirtualEndpoint.cs 
*
* VirtualEndpoint.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Threading.Tasks;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.Endpoints
{
    /// <summary>
    /// Provides a base class for <see cref="IVirtualEndpoint{T}"/> entity processors
    /// with checks and a log provider
    /// </summary>
    /// <typeparam name="T">The entity type to process</typeparam>
    public abstract class VirtualEndpoint<T> : MarshalByRefObject, IVirtualEndpoint<T>
    {
        ///<inheritdoc/>
        public virtual string Path { get; protected set; }

        /// <summary>
        /// An <see cref="ILogProvider"/> to write logs to
        /// </summary>
        protected ILogProvider Log { get; set; }

        /// <summary>
        /// Sets the log and path and checks the values
        /// </summary>
        /// <param name="Path">The path this instance represents</param>
        /// <param name="log">The log provider that will be used</param>
        /// <exception cref="ArgumentException"></exception>
        protected void InitPathAndLog(string Path, ILogProvider log)
        {
            if (string.IsNullOrWhiteSpace(Path) || Path[0] != '/')
            {
                throw new ArgumentException("Path must begin with a '/' character", nameof(Path));
            }
            //Store path
            this.Path = Path;
            //Store log
            Log = log ?? throw new ArgumentNullException(nameof(log));
        }

        ///<inheritdoc/>
        public abstract ValueTask<VfReturnType> Process(T entity);
    }
}