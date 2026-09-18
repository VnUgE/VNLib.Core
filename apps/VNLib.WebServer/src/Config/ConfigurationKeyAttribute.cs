/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ConfigurationKeyAttribute.cs 
*
* ConfigurationKeyAttribute.cs is part of VNLib.WebServer which is part of the larger 
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

namespace VNLib.WebServer.Config
{
    /// <summary>
    /// Apply to a class to declare the configuration key desired when deserializing the type
    /// </summary>
    /// <param name="configKey">The configuration key used to recover this element</param>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ConfigurationKeyAttribute(string configKey) : Attribute
    {
        /// <summary>
        /// The configuration key to use when deserializing the element from config
        /// </summary>
        internal readonly string ConfigKey = string.IsNullOrWhiteSpace(configKey)
            ? throw new ArgumentException("Configuration key cannot be null, empty, or whitespace.", nameof(configKey))
            : configKey;
    }
}
