/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: VnWebHeaderCollection.cs 
*
* VnWebHeaderCollection.cs is part of VNLib.Net.Http which is part of the larger 
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

using System;
using System.Net;
using System.Collections.Generic;


namespace VNLib.Net.Http
{
    ///<inheritdoc/>
    public sealed class VnWebHeaderCollection : WebHeaderCollection, IEnumerable<KeyValuePair<string?, string?>>
    {
        IEnumerator<KeyValuePair<string?, string?>> IEnumerable<KeyValuePair<string?, string?>>.GetEnumerator()
        {
            for (int i = 0; i < Keys.Count; i++)
            {
                yield return new(Keys[i], Get(i));
            }
        }
    }
}
