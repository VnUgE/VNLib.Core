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
            Console.WriteLine("Loading configuration file from {0}", nameOnly);

            if (filename.EndsWith(".json"))
            {
                return FromJson(filename);
            }
            else if (filename.EndsWith(".yaml") || filename.EndsWith(".yml"))
            {
                return FromYaml(filename);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Reads a server configuration from the specified JSON document
        /// </summary>
        /// <param name="configPath">The file path of the json cofiguration file</param>
        /// <returns>A new <see cref="JsonServerConfig"/> wrapping the server config</returns>
        public static JsonServerConfig? FromJson(string fileName)
        {
            if (!FileOperations.FileExists(fileName))
            {
                return null;
            }

            //Open the config file
            using FileStream fs = File.OpenRead(fileName);

            //Allow comments
            JsonDocumentOptions jdo = new()
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            return new JsonServerConfig(JsonDocument.Parse(fs, jdo));
        }

        public static JsonServerConfig? FromYaml(string fileName)
        {
            if (!FileOperations.FileExists(fileName))
            {
                return null;
            }

            /*
             * The following code reads the configuration as a yaml
             * object and then serializes it over to json. 
             */

            using StreamReader reader = OpenFileRead(fileName);

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

        private static StreamReader OpenFileRead(string fileName)
        {
            return new StreamReader(
                stream: File.OpenRead(fileName),
                encoding: System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: false
            );
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
