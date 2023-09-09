/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IAsyncExclusiveResource.cs 
*
* IAsyncExclusiveResource.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Resources;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public interface IAsyncExclusiveResource : IResource
    {
        /// <summary>
        /// Releases the resource from use. Called when a <see cref="ExclusiveResourceHandle{T}"/> is disposed
        /// </summary>
        ValueTask ReleaseAsync(CancellationToken cancellation = default);
    }
}