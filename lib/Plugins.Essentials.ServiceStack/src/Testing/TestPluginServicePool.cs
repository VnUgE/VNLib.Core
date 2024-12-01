/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: TestPluginServicePool.cs 
*
* TestPluginServicePool.cs is part of VNLib.Plugins which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins. If not, see http://www.gnu.org/licenses/.
*/

//Only export on DEBUG builds for testing purposes
#if DEBUG

using System;
using System.Linq;
using System.Collections.Generic;

using VNLib.Plugins.Essentials.Runtime;

namespace VNLib.Plugins.Essentials.ServiceStack.Testing
{

    /// <summary>
    /// A service container for plugin testing 
    /// </summary>
    public sealed class TestPluginServicePool : IPluginServicePool
    {
        private readonly List<TestService> _services = [];

        ///<inheritdoc/>
        public void ExportService(Type serviceType, object service, ExportFlags flags = ExportFlags.None)
            => _services.Add(new(serviceType, service, flags));

        /// <summary>
        /// Retrieves the types of all loaded services
        /// </summary>
        /// <returns>An enumeration of service types</returns>
        public IEnumerable<Type> EnumerateTypes()
            => _services.Select(static s => s.ServiceType);

        /// <summary>
        /// Retrieves all loaded service instances
        /// </summary>
        /// <returns>A enumeration of all service instances</returns>
        public IEnumerable<object> EnumerateObjects()
            => _services.Select(static s => s.Service);

        /// <summary>
        /// Enumerates all services in the service pool with their types
        /// </summary>
        /// <returns>An enumeration of <see cref="KeyValuePair{ Type, Object }"/> mapping of service 
        /// types to their instances as they were exported to the container.
        /// </returns>
        public IEnumerable<KeyValuePair<Type, object>> EnumerateServices()
            => _services.Select(static s => new KeyValuePair<Type, object>(s.ServiceType, s.Service));

        /// <summary>
        /// Gets all <see cref="IEndpoint"/> exposed by a <see cref="IVirtualEndpointDefinition"/> if 
        /// the plugin exports the service. Otherwise an empty array is returned
        /// </summary>
        /// <returns>The array of endpoints of any exist, or and empty array if the plugin does not export endpoints</returns>
        public IEndpoint[] GetEndpoints()
        {
            //Try to get the endpoint defintion
            if (GetService(typeof(IVirtualEndpointDefinition)) is IVirtualEndpointDefinition defintion)
            {
                //Return the endpoints from the definition
                return defintion
                    .GetEndpoints()
                    .ToArray();
            }

            return [];
        }

        /// <summary>
        /// Retrieves a service of the desired type from the service pool
        /// </summary>
        /// <param name="serviceType">The type of the service to fetch</param>
        /// <returns>The service instance</returns>
        public object? GetService(Type serviceType)
        {
            return _services
                .Where(s => s.ServiceType.Equals(serviceType))
                .Select(static s => s.Service)
                .FirstOrDefault();
        }

        /// <summary>
        /// Checks if a service of the desired type is available in the service pool
        /// </summary>
        /// <param name="serviceType">The type of the service to fetch</param>
        /// <returns>True if the service has been added to the pool</returns>
        public bool HasService(Type serviceType)
            => _services.Any(s => s.ServiceType.Equals(serviceType));

        /// <summary>
        /// Retrieves a service of the desired type from the service pool
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>The service instance</returns>
        public T GetService<T>() => (T)GetService(typeof(T))!;

        /// <summary>
        /// Checks if a service of the desired type is available in the service pool
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the service has been added to the pool</returns>
        public bool HasService<T>() => HasService(typeof(T));

        /// <summary>
        /// Removes all services from the service pool
        /// </summary>
        public void Clear() => _services.Clear();

        private record TestService(Type ServiceType, object Service, ExportFlags Flags);

    }
}

#endif