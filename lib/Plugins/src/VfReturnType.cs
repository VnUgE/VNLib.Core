/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: Interfaces.cs 
*
* Interfaces.cs is part of VNLib.Plugins which is part of the larger 
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

namespace VNLib.Plugins
{

    /// <summary>
    /// Represents the result of a virutal endpoint processing operation
    /// </summary>
    public enum VfReturnType
    {
        /// <summary>
        /// Signals that the virtual endpoint 
        /// </summary>
        ProcessAsFile, 
        /// <summary>
        /// Signals that the virtual endpoint generated a response, and 
        /// the connection should be completed
        /// </summary>
        VirtualSkip, 
        /// <summary>
        /// Signals that the virtual endpoint determined that the connection 
        /// should be denied.
        /// </summary>
        Forbidden, 
        /// <summary>
        /// Signals that the resource the virtual endpoint was processing 
        /// does not exist.
        /// </summary>
        NotFound, 
        /// <summary>
        /// Signals that the virutal endpoint determined the request was invalid
        /// </summary>
        BadRequest, 
        /// <summary>
        /// Signals that the virtual endpoint had an error
        /// </summary>
        Error
    }
}
