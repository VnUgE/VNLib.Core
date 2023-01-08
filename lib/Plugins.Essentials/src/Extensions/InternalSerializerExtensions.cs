/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.IO;
using System.Text.Json;

#nullable enable

namespace VNLib.Plugins.Essentials.Extensions
{
    
    internal static class InternalSerializerExtensions
    {
        
        internal static void Serialize<T>(this Utf8JsonWriter writer, IJsonSerializerBuffer buffer, T value, JsonSerializerOptions? options)
        {
            //Get stream
            Stream output = buffer.GetSerialzingStream();           
            try
            {
                //Reset writer
                writer.Reset(output);

                //Serialize
                JsonSerializer.Serialize(writer, value, options);

                //flush output
                writer.Flush();
            }
            finally
            {
                buffer.SerializationComplete();
            }
        }

        internal static void Serialize(this Utf8JsonWriter writer, IJsonSerializerBuffer buffer, object value, Type type, JsonSerializerOptions? options)
        {
            //Get stream
            Stream output = buffer.GetSerialzingStream();
            try
            {
                //Reset writer
                writer.Reset(output);

                //Serialize
                JsonSerializer.Serialize(writer, value, type, options);

                //flush output
                writer.Flush();
            }
            finally
            {
                buffer.SerializationComplete();
            }
        }

        internal static void Serialize(this Utf8JsonWriter writer, IJsonSerializerBuffer buffer, JsonDocument document)
        {
            //Get stream
            Stream output = buffer.GetSerialzingStream();
            try
            {
                //Reset writer
                writer.Reset(output);

                //Serialize
                document.WriteTo(writer);

                //flush output
                writer.Flush();
            }
            finally
            {
                buffer.SerializationComplete();
            }
        }
    }
}