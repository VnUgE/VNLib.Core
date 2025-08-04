/*
* Copyright (c) 2025 Vaughn Nugent
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
        private static readonly JsonSerializerOptions _ops = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        //Allow comments
        private static readonly JsonDocumentOptions _jdo = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        ///<inheritdoc/>
        public JsonElement GetDocumentRoot() => doc.RootElement;

        ///<inheritdoc/>
        public T? GetConfigProperty<T>(string key)
        {
            JsonElement current = doc.RootElement;

            //Loop through the namespace properties to find nested json objects
            foreach (string name in key.Split("::"))
            {
                switch (current.ValueKind)
                {
                    //If the current element is an object, try to get a nested property
                    case JsonValueKind.Object:
                        if (current.TryGetProperty(name, out JsonElement value))
                        {
                            current = value;
                        }
                        else
                        {
                            return default;
                        }
                        break;
                    default:
                        return current.Deserialize<T>(_ops);
                }
            }

            return current.Deserialize<T>(_ops);
        }

        public static JsonServerConfig? FromFile(string filename)
        {
            if (!FileOperations.FileExists(filename))
            {
                Console.WriteLine("Configuration file {0} does not exist", filename);
                return null;
            }

            using FileStream fileStream = File.OpenRead(filename);

            // Use the file name to get the config type
            ConfigFileType type = GetConfigFileType(filename);

            Console.WriteLine($"Loading {type} configuration file from {filename}");

            return FromStream(fileStream, type);            
        }

       
        public static JsonServerConfig? FromStdin()
        {
            Console.WriteLine("Reading JSON configuration from stdin");

            using Stream stdIn = Console.OpenStandardInput();

            // For now, we have to default to JSON from stdin
            return FromStream(stdIn, ConfigFileType.Json);
        }      

        private static JsonServerConfig? FromStream(Stream stream, ConfigFileType type)
        {
            return ReadConfigToJson(stream, type) is JsonDocument doc
                ? new JsonServerConfig(doc)
                : null;
        }

        /// <summary>
        /// A helper function that will attempt to parse a json or yaml file stream
        /// into a <see cref="JsonDocument"/>.
        /// </summary>
        /// <param name="stream">The open file to read syncronously</param>
        /// <returns>The parsed document if read successfully</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static JsonDocument? ReadConfigFileToJson(FileStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            // Get the config type from the file extension
            ConfigFileType type = GetConfigFileType(stream.Name);

            return ReadConfigToJson(stream, type);
        }       

        /// <summary>
        /// A helper function that will attempt to parse a json or yaml data stream
        /// into a <see cref="JsonDocument"/>.
        /// </summary>
        /// <param name="stream">The open file to read syncronously</param>
        /// <returns>The parsed document if read successfully</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static JsonDocument? ReadConfigToJson(Stream stream, ConfigFileType configType)
        {
            if (!stream.CanRead)
            {
                throw new ArgumentException("Stream must be readable", nameof(stream));
            }

            switch (configType)
            {
                case ConfigFileType.Json:
                    {
                        try
                        {
                            //Default to json
                            return JsonDocument.Parse(stream, _jdo);
                        }
                        catch (JsonException je)
                        {
                            Console.WriteLine(
                                "ERROR: Failed to parse json configuration. Error occured at line {0}, byte position {1}",
                                je.LineNumber,
                                je.BytePositionInLine
                            );
                        }
                    }
                    break;

                case ConfigFileType.Yaml:
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

                        return JsonDocument.Parse(ms, _jdo);
                    }
            }

            return null;
        }

        /// <summary>
        /// A list of supported file extensions for configuration files.
        /// </summary>
        public static readonly string[] SupportedFileExtensions =
        [
            ".json5",
            ".json",
            ".yaml",
            ".yml"
        ];

        private static ConfigFileType GetConfigFileType(string filename)
        {
            string ext = Path.GetExtension(filename).ToLowerInvariant();
            return ext switch
            {
                ".json5" => ConfigFileType.Json,
                ".json" => ConfigFileType.Json,
                ".yaml" => ConfigFileType.Yaml,
                ".yml" => ConfigFileType.Yaml,
                _ => ConfigFileType.Unknown
            };
        }

        public class NumberTypeResolver : INodeTypeResolver
        {
            public bool Resolve(NodeEvent? nodeEvent, ref Type currentType)
            {
                if (nodeEvent is Scalar scalar)
                {
                    if (long.TryParse(scalar.Value, out _))
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
