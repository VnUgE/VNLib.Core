/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: JsonJobberConfig.cs
*
* JsonJobberConfig.cs is part of jobber which is part of the larger 
* VNLib collection of libraries and utilities.
*
* jobber is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* jobber is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with jobber. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text;
using System.Text.Json;

using YamlDotNet.Serialization;
using YamlDotNet.Core.Events;

using VNLib.Utils.IO;

namespace Jobber.Config;


internal sealed class JsonJobberConfig
{
    private readonly JsonDocument _doc;
    public JobberConfig Config { get; }
    public JsonElement Root => _doc.RootElement;

    private JsonJobberConfig(JsonDocument doc, JobberConfig cfg)
    {
        _doc = doc;
        Config = cfg;
    }

    public static JsonJobberConfig? FromFile(string path)
    {
        if (!FileOperations.FileExists(path))
        {
            Console.WriteLine("Config file does not exist: {0}", path);
            return null;
        }

        bool yaml = path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
        using FileStream fs = File.OpenRead(path);
        return FromStream(fs, yaml);
    }
 
    private static JsonJobberConfig? FromStream(Stream stream, bool yaml)
    {
        try
        {
            JsonDocumentOptions jdo = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            JsonDocument doc;
            if (yaml)
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, leaveOpen:true);
                object? yamlObj = new DeserializerBuilder()
                    .WithNodeTypeResolver(new NumberTypeResolver())
                    .Build()
                    .Deserialize(reader);

                ISerializer serializer = new SerializerBuilder()
                    .JsonCompatible()
                    .Build();

                using VnMemoryStream ms = new ();

                using (StreamWriter sw = new (ms, leaveOpen:true))
                {
                    serializer.Serialize(sw, yamlObj);
                }

                ms.Seek(0, SeekOrigin.Begin);
                doc = JsonDocument.Parse(ms, jdo);
            }
            else
            {
                doc = JsonDocument.Parse(stream, jdo);
            }

            JobberConfig? cfg = doc.RootElement.Deserialize<JobberConfig>();
            if (cfg is null)
            {
                Console.WriteLine("Failed to deserialize configuration");
                return null;
            }

            // triggers validation
            cfg.OnDeserialized();

            return new JsonJobberConfig(doc, cfg);
        }
        catch (JsonException je)
        {
            Console.WriteLine("JSON parse error line {0} pos {1}", je.LineNumber, je.BytePositionInLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to read configuration: {0}", ex.Message);
        }
        return null;
    }

    internal sealed class NumberTypeResolver : INodeTypeResolver
    {
        public bool Resolve(NodeEvent? nodeEvent, ref Type currentType)
        {
            if (nodeEvent is Scalar sc)
            {
                if (int.TryParse(sc.Value, out _))
                {
                    currentType = typeof(int);
                    return true;
                }
                if (long.TryParse(sc.Value, out _))
                {
                    currentType = typeof(long);
                    return true;
                }
                if (double.TryParse(sc.Value, out _))
                {
                    currentType = typeof(double);
                    return true;
                }
                if (bool.TryParse(sc.Value, out _))
                {
                    currentType = typeof(bool);
                    return true;
                }
            }
            return false;
        }
    }
}