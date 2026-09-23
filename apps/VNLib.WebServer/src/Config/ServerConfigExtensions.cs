/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ServerConfigExtensions.cs 
*
* ServerConfigExtensions.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Reflection;

namespace VNLib.WebServer.Config
{
    internal static class ServerConfigExtensions
    {
        /// <summary>
        /// Attempts to deserialize the desired configuration type from the configuration document that must 
        /// be attributed with <see cref="ConfigurationKeyAttribute"/> otherwise an exception will be raised.
        /// </summary>
        /// <typeparam name="T">The configuration type to deserialize</typeparam>
        /// <param name="config">The server configuration to read the property from</param>
        /// <returns>The deserialized config value or null if not present in the document</returns>
        /// <exception cref="ArgumentException">Thrown when the type T is not attributed with <see cref="ConfigurationKeyAttribute"/>.</exception>
        public static T? GetConfigProperty<T>(this IServerConfig config)
        {
            Type t = typeof(T);
            ConfigurationKeyAttribute? configKey = t.GetCustomAttribute<ConfigurationKeyAttribute>(inherit: false)
                ?? throw new ArgumentException($"Missing {nameof(ConfigurationKeyAttribute)} attribute on type {t.Name}");

            return config.GetConfigProperty<T>(configKey.ConfigKey);
        }
    }
}
