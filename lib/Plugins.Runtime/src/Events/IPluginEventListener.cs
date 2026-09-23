/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginEventListener.cs 
*
* IPluginEventListener.cs is part of VNLib.Plugins.Runtime which
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime. If not, see http://www.gnu.org/licenses/.
*/

namespace VNLib.Plugins.Runtime.Events
{
    /// <summary>
    /// Represents a plugin event consumer.
    /// <para>
    /// Listeners are dispatched in registration order. If your listener depends on another listener having 
    /// already run (e.g., config being applied before logging), ensure the dependent listener is registered 
    /// first.
    /// </para>
    /// </summary>
    public interface IPluginEventListener
    {
        /// <summary>
        /// Called by the registered <see cref="PluginController"/> to notify this listener that the plugins 
        /// within the collection have been initialized and are about to be loaded.
        /// <para>
        /// This event is called before the plugins are loaded, so any initialization that needs to be 
        /// done before the plugins are loaded can be done here.
        /// </para>
        /// <para>
        /// Plugins may be in an undefined state. Instances are just created, this hook provides any pre-loading 
        /// initialization that may be necessary. For example assigning information required for loading
        /// plugins like configuration data.
        /// </para>
        /// <para>
        /// <strong>Exception contract:</strong> Not all hooks are guaranteed to be called, in the event an exception
        /// is raised during load event handling. If a listener raises an exception, it will abort the load process
        /// immediately. 
        /// </para>
        /// </summary>
        /// <param name="controller">The collection on which the load event occurred</param>
        /// <param name="state">The registration state parameter</param>
        virtual void OnBeforeLoading(PluginController controller, object? state) { }

        /// <summary>
        /// Called by the registered <see cref="PluginController"/>
        /// to notify this listener that the plugins within the collection
        /// have been initialized and loaded
        /// <para>
        /// <strong>Exception contract:</strong> Not all hooks are guaranteed to be called, in the event an exception
        /// is raised during load event handling. If a listener raises an exception, it will abort the notification dispatch
        /// immediately. 
        /// </para>
        /// </summary>
        /// <param name="controller">The collection on which the load event occurred</param>
        /// <param name="state">The registration state parameter</param>
        void OnPluginLoaded(PluginController controller, object? state);

        /// <summary>
        /// Called by the registered <see cref="PluginController"/> to notify this listener 
        /// that the plugins within the collection are about to be unloaded. 
        /// <para>
        /// This is called before the plugins are unloaded, so any cleanup that needs to be done before the 
        /// plugins are unloaded can be done here.
        /// </para>
        /// <para>
        /// <strong>Exception contract:</strong> Not all hooks are guaranteed to be called, in the event an exception
        /// is raised during load event handling. If a listener raises an exception, it will abort the unload process
        /// immediately. Plugins will remain "loaded" if any handler raises an exception. 
        /// </para>
        /// </summary>
        /// <param name="controller">The controller that is reloading</param>
        /// <param name="state">The registration state parameter</param>
        void OnPluginUnloaded(PluginController controller, object? state);

        /// <summary>
        /// An optional hook that notifies handlers that the controller has successfully unloaded plugins.
        /// <para>
        /// This handler is guaranteed to be called if the plugin successfully unloads and all pre-unload
        /// hooks complete successfully.
        /// </para>
        /// <para>
        /// <strong>Exception contract:</strong> exceptions thrown from this hook are suppressed on a best-effort 
        /// basis. All registered listeners will be invoked regardless of individual failures, since this hook is 
        /// intended for cleanup and partial failure will not prevent other listeners from cleaning up.
        /// </para>
        /// </summary>
        virtual void OnAfterUnloaded(PluginController controller, object? state) { }
    }
}
