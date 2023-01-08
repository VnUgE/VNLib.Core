/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IPageRouter.cs 
*
* IPageRouter.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using System.Threading.Tasks;

//Import account system for privilage masks

namespace VNLib.Plugins.Essentials.Content
{
    /// <summary>
    /// Determines file routines (routing) for incomming connections
    /// </summary>
    public interface IPageRouter
    {
        /// <summary>
        /// Determines what file path to return to a user for the given incoming connection 
        /// </summary>
        /// <param name="entity">The connection to proccess</param>
        /// <returns>A <see cref="ValueTask"/> that returns the <see cref="FileProcessArgs"/> to pass to the file processor</returns>
        ValueTask<FileProcessArgs> RouteAsync(HttpEntity entity);
    }
}