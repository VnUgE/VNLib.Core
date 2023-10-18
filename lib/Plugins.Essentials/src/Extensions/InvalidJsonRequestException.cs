/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: InvalidJsonRequestException.cs 
*
* InvalidJsonRequestException.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using System.Text.Json;

namespace VNLib.Plugins.Essentials.Extensions
{
    /// <summary>
    /// Wraps a <see cref="JsonException"/> that is thrown when a JSON request message
    /// was unsuccessfully parsed.
    /// </summary>
    public class InvalidJsonRequestException : JsonException
    {
        /// <summary>
        /// Creates a new <see cref="InvalidJsonRequestException"/> wrapper from a base <see cref="JsonException"/>
        /// </summary>
        /// <param name="baseExp"></param>
        public InvalidJsonRequestException(JsonException baseExp) 
            : base(baseExp.Message, baseExp.Path, baseExp.LineNumber, baseExp.BytePositionInLine, baseExp.InnerException)
        {
            base.HelpLink = baseExp.HelpLink;
            base.Source = baseExp.Source;
        }

        public InvalidJsonRequestException()
        {}

        public InvalidJsonRequestException(string message) : base(message)
        {}

        public InvalidJsonRequestException(string message, System.Exception innerException) : base(message, innerException)
        {}
    }
}