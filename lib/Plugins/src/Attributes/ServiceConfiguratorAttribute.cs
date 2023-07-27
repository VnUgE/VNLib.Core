/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: ServiceConfiguratorAttribute.cs 
*
* ServiceConfiguratorAttribute.cs is part of VNLib.Plugins which is part of the larger 
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
using System.ComponentModel.Design;

namespace VNLib.Plugins.Attributes
{
    /// <summary>
    /// <para>
    /// Declare this attribute on an <see cref="IPlugin"/> instance method to define the service configuration
    /// method. When declared, allows the plugin to expose shared types to the host
    /// </para>
    /// <para>
    /// This method may be runtime dependant, it may not be called on all platforms, and it 
    /// may not be required.
    /// </para>
    /// <para>
    /// Lifecycle: This method may be called by the runtime anytime after the <see cref="IPlugin.Load"/>
    /// method, its exposed services are considered invalid after the <see cref="IPlugin.Unload"/>
    /// method is called.
    /// </para>
    /// <para>
    /// Method signature: <code>void OnServiceConfiguration(<see cref="IServiceContainer"/> container)</code>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ServiceConfiguratorAttribute : Attribute
    { }

    /// <summary>
    /// A safe delegate that matches the signature of the <see cref="ServiceConfiguratorAttribute"/>
    /// method exposed
    /// </summary>
    /// <param name="sender"></param>
    public delegate void ServiceConfigurator(IServiceContainer sender);
}
