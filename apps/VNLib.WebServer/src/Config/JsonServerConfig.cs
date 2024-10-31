/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: JsonServerConfig.cs 
*
* JsonServerConfig.cs is part of VNLib.WebServer which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text.Json;

using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

using VNLib.Utils.IO;

namespace VNLib.WebServer.Config
{
    internal sealed class JsonServerConfig(JsonDocument doc) : IServerConfig
    {
        public JsonElement GetDocumentRoot() => doc.RootElement;

        public static JsonServerConfig? FromFile(string filename)
        {
            string nameOnly = Path.GetFileName(filename);
          
            if (!FileOperations.FileExists(filename))
            {
                Console.WriteLine("Configuration file {0} does not exist", filename);
                return null;
            }

            using Stream fileStream = File.OpenRead(filename);

            if (filename.EndsWith(".json"))
            {
                Console.WriteLine("Loading json configuration file from {0}", nameOnly);
                return FromStream(fileStream, yaml: false);
            }
            else if (filename.EndsWith(".yaml") || filename.EndsWith(".yml"))
            {
                Console.WriteLine("Loading yaml configuration file from {0}", nameOnly);
                return FromStream(fileStream, yaml: true);
            }
            else
            {
                Console.WriteLine("Unknown file type for configuration file {0}", nameOnly);
                return null;
            }
        }

        public static JsonServerConfig? FromStdin()
        {
            Console.WriteLine("Reading JSON configuration from stdin");

            using Stream stdIn = Console.OpenStandardInput();

            return FromStream(stdIn, false);
        }

        private static JsonServerConfig? FromStream(Stream stream, bool yaml)
        {
            if (yaml)
            {
                using StreamReader reader = new (
                    stream,
                    encoding: System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true
                );

                object? yamlObject = new DeserializerBuilder()
                    .WithNodeTypeResolver(new NumberTypeResolver())
                    .Build()
                    .Deserialize(reader);

                ISerializer serializer = new SerializerBuilder()
                    .JsonCompatible()
                    .Build();

                using VnMemoryStream ms = new();
                using (StreamWriter sw = new(ms, leaveOpen: true))
                {
                    serializer.Serialize(sw, yamlObject);
                }

                ms.Seek(0, SeekOrigin.Begin);

                return new JsonServerConfig(JsonDocument.Parse(ms));
            }
            else
            {
                try
                {
                    //Allow comments
                    JsonDocumentOptions jdo = new()
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    };

                    //Default to json
                    return new JsonServerConfig(JsonDocument.Parse(stream, jdo));
                }
                catch(JsonException je)
                {
                    Console.WriteLine(
                        "ERROR: Failed to parse json configuration. Error occured at line {0}, byte position {1}", 
                        je.LineNumber, 
                        je.BytePositionInLine
                    );
                }
            }

            return null;
        }

        public class NumberTypeResolver : INodeTypeResolver
        {
            public bool Resolve(NodeEvent? nodeEvent, ref Type currentType)
            {
                if (nodeEvent is Scalar scalar)
                {
                    if(long.TryParse(scalar.Value, out _))
                    {
                        currentType = typeof(int);
                        return true;
                    }

                    if (double.TryParse(scalar.Value, out _))
                    {
                        currentType = typeof(double);
                        return true;
                    }

                    if (bool.TryParse(scalar.Value, out _))
                    {
                        currentType = typeof(bool);
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
