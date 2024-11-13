/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: InternalSerializerExtensions.cs 
*
* InternalSerializerExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using System;
using System.Text.Json;

using VNLib.Utils.IO;

namespace VNLib.Plugins.Essentials.Extensions
{
    
    internal static class InternalSerializerExtensions
    {
        
        internal static void Serialize<T>(this Utf8JsonWriter writer, VnMemoryStream buffer, T value, JsonSerializerOptions? options)
        {
            try
            {
                //Reset and init the output stream
                writer.Reset(buffer);
               
                JsonSerializer.Serialize(writer, value, options);
           
                writer.Flush();

                buffer.Seek(0, System.IO.SeekOrigin.Begin);
            }
            finally
            {
                writer.Reset();
            }
        }

        internal static void Serialize(this Utf8JsonWriter writer, VnMemoryStream buffer, object value, Type type, JsonSerializerOptions? options)
        {
            try
            {
                //Reset and init the output stream
                writer.Reset(buffer);
            
                JsonSerializer.Serialize(writer, value, type, options);
           
                writer.Flush();

                buffer.Seek(0, System.IO.SeekOrigin.Begin);
            }
            finally
            {
                writer.Reset();
            }
        }

        internal static void Serialize(this Utf8JsonWriter writer, VnMemoryStream buffer, JsonDocument document)
        {           
            try
            {
                //Reset and init the output stream
                writer.Reset(buffer);
                
                document.WriteTo(writer);

                writer.Flush();

                buffer.Seek(0, System.IO.SeekOrigin.Begin);
            }
            finally
            {
                writer.Reset();
            }
        }
    }
}