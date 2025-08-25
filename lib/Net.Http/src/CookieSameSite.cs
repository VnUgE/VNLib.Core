/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: CookieSameSite.cs 
*
* CookieSameSite.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

namespace VNLib.Net.Http
{
    /// <summary>
    /// Specifies an HTTP cookie SameSite type
    /// </summary>
    public enum CookieSameSite 
    {
        /// <summary>
        /// Cookie samesite property lax mode
        /// </summary>
        Lax, 
        /// <summary>
        /// Cookie samesite property, None mode
        /// </summary>
        None, 
        /// <summary>
        /// Cookie samesite property, strict mode
        /// </summary>
        Strict
    }
}