/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: ConfigurationInitalizerAttribute.cs 
*
* ConfigurationInitalizerAttribute.cs is part of VNLib.Plugins which is part of the larger 
* VNLib collection of libraries and utilities.
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

using System;
using System.Text.Json;

namespace VNLib.Plugins.Attributes
{
    /// <summary>
    /// Set this attribute on an <see cref="IPlugin"/> instance method to define the configuration initializer.
    /// This attribute can only be defined on a single instance method and cannot be overloaded.
    /// <br></br>
    /// A plugin host should invoke this method before <see cref="IPlugin.Load"/>
    /// <br></br>
    /// Method signature <code>public void [methodname] (<see cref="JsonDocument"/> config)</code> 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ConfigurationInitalizerAttribute : Attribute
    { }

    /// <summary>
    /// Represents a safe configuration initializer delegate method
    /// </summary>
    /// <param name="config">The configuration object that plugin will use</param>
    public delegate void ConfigInitializer(JsonDocument config);
}
