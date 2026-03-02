/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: ServiceHostBinder.cs
*
* ServiceHostBinder.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// An <see cref="IServiceBinder"/> backed by a fixed collection of 
    /// <see cref="IServiceHost"/> instances. Routes <see cref="Bind"/> and 
    /// <see cref="Unbind"/> to every host in the collection.
    /// <para>
    /// Both operations are best-effort: all hosts are visited regardless of 
    /// per-host failures. Exceptions are collected and re-thrown as 
    /// <see cref="System.AggregateException"/> after the full sweep completes.
    /// </para>
    /// </summary>
    public sealed class ServiceHostBinder(IServiceHost[] hosts) : IServiceBinder
    {
        private readonly IServiceHost[] _hosts = hosts;

        /// <inheritdoc/>
        public void Bind(IServiceBinding binding)
            => _hosts.TryForeach(h => h.OnServiceAttach(binding));

        /// <inheritdoc/>
        public void Unbind(IServiceBinding binding)
            => _hosts.TryForeach(h => h.OnServiceDetach(binding));
    }
}
