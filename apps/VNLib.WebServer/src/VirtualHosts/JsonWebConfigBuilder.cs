/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: JsonWebConfigBuilder.cs 
*
* JsonWebConfigBuilder.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
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
using System.Net;
using System.Data;
using System.Linq;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

using VNLib.WebServer.Config;
using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer
{   

    internal sealed partial class JsonWebConfigBuilder(VirtualHostServerConfig VhConfig, TimeSpan execTimeout, ILogProvider logger) 
        : IVirtualHostConfigBuilder
    {
        //Use pre-compiled default regex
        private static readonly Regex DefaultRootRegex = MyRegex();
       
        ///<inheritdoc/>
        public VirtualHostConfig GetBaseConfig()
        {
            //Declare the vh config
            return new()
            {
                //File root is required
                RootDir                 = new(VhConfig.DirPath!),
                LogProvider             = logger,
                ExecutionTimeout        = execTimeout,
                WhiteList               = GetIpWhitelist(VhConfig),
                DownStreamServers       = GetDownStreamServers(VhConfig),
                ExcludedExtensions      = GetExlcudedExtensions(VhConfig),
                DefaultFiles            = GetDefaultFiles(VhConfig),
                PathFilter              = GetPathFilter(VhConfig),
                CacheDefault            = TimeSpan.FromSeconds(VhConfig.CacheDefaultTimeSeconds),
                AdditionalHeaders       = GetConfigHeaders(VhConfig),
                SpecialHeaders          = GetSpecialHeaders(VhConfig),
                FailureFiles            = GetFailureFiles(VhConfig),
                FilePathCacheMaxAge     = TimeSpan.MaxValue,
                Hostnames               = GetHostnames(VhConfig),
                Transports              = GetInterfaces(VhConfig),
                BlackList               = GetIpBlacklist(VhConfig),
            };
        }

        private static string[] GetHostnames(VirtualHostServerConfig conf)
        {
            Validate.EnsureNotNull(conf.Hostnames, "Hostnames array was set to null, you must define at least one hostname");

            foreach (string hostname in conf.Hostnames)
            {
                Validate.EnsureNotNull(hostname, "Hostname is null, all hostnames must be defined");
            }

            return conf.Hostnames;
        }

        private static TransportInterface[] GetInterfaces(VirtualHostServerConfig conf)
        {
            Validate.EnsureNotNull(conf.Interfaces, "Interfaces array was set to null, you must define at least one network interface");
            Validate.Assert(conf.Interfaces.Length > 0, $"You must define at least one interface for host");

            for(int i = 0; i < conf.Interfaces.Length; i++)
            {
                TransportInterface iFace = conf.Interfaces[i];

                Validate.EnsureNotNull(iFace, $"Vrtual host interface [{i}] is undefined");

                Validate.EnsureNotNull(iFace.Address, $"The interface IP address is required for interface [{i}]");
                Validate.EnsureValidIp(iFace.Address, $"The interface IP address is invalid for interface [{i}]");
                Validate.EnsureRange(iFace.Port, 1, 65535, "Interface port");
            }

            return conf.Interfaces;
        }

        private static Regex GetPathFilter(VirtualHostServerConfig conf)
        {
            //Allow site to define a regex filter pattern
            return conf.PathFilter is not null ? new(conf.PathFilter!) : DefaultRootRegex;
        }

        private FrozenDictionary<HttpStatusCode, FileCache> GetFailureFiles(VirtualHostServerConfig conf)
        {
            //if a failure file array is specified, capure all files and
            if (conf.ErrorFiles is null || conf.ErrorFiles.Length < 1)
            {
                return new Dictionary<HttpStatusCode, FileCache>().ToFrozenDictionary();
            }

            //Get the error files
            IEnumerable<KeyValuePair<HttpStatusCode, string>> ffs = conf.ErrorFiles
                        .Select(static f => new KeyValuePair<HttpStatusCode, string>((HttpStatusCode)f.Code, f.Path!));

            //Create the file cache dictionary
            (HttpStatusCode, string, FileCache?)[] loadCache = ffs.Select(static kv =>
                {
                    FileCache? cached = FileCache.Create(kv.Key, kv.Value);
                    return (kv.Key, kv.Value, cached);

                }).ToArray();

            //Only include files that exist and were loaded
            int loadedFiles = loadCache
                .Where(static loadCache => loadCache.Item3 != null)
                .Count();

            string[] notFoundFiles = loadCache
                .Where(static loadCache => loadCache.Item3 == null)
                .Select(static l => Path.GetFileName(l.Item2))
                .ToArray();

            if (notFoundFiles.Length > 0)
            {
                logger.Warn("Failed to load error files {files} for host {hosts}", notFoundFiles, conf.Hostnames);
            }

            //init frozen dictionary from valid cached files
            return loadCache
                .Where(static kv => kv.Item3 != null)
                .ToDictionary(static kv => kv.Item1, static kv => kv.Item3!)
                .ToFrozenDictionary();
        }

        private static FrozenSet<IPAddress> GetDownStreamServers(VirtualHostServerConfig conf)
        {
            //Find downstream servers
            HashSet<IPAddress>? downstreamServers = null;

            //See if element is set
            if (conf.DownstreamServers is not null)
            {
                //hash addresses, make is distinct
                downstreamServers = conf.DownstreamServers
                    .Where(static addr => !string.IsNullOrWhiteSpace(addr))
                    .Select(static addr => IPAddress.Parse(addr))
                    .Distinct()
                    .ToHashSet();
            }

            return (downstreamServers ?? []).ToFrozenSet();
        }

        private static FrozenSet<IPAddress>? GetIpWhitelist(VirtualHostServerConfig conf)
        {
            if(conf.Whitelist is null)
            {
                return null;
            }

            //See if whitelist is defined, if so, get a distinct list of addresses
            return conf.Whitelist
                    .Where(static addr => !string.IsNullOrWhiteSpace(addr))
                    .Select(static addr => IPAddress.Parse(addr))
                    .Distinct()
                    .ToHashSet()
                    .ToFrozenSet();
        }

        private static FrozenSet<IPAddress>? GetIpBlacklist(VirtualHostServerConfig conf)
        {
            if (conf.Blacklist is null)
            {
                return null;
            }

            //See if whitelist is defined, if so, get a distinct list of addresses
            return conf.Blacklist
                    .Where(static addr => !string.IsNullOrWhiteSpace(addr))
                    .Select(static addr => IPAddress.Parse(addr))
                    .Distinct()
                    .ToHashSet()
                    .ToFrozenSet();
        }

        private static FrozenSet<string> GetExlcudedExtensions(VirtualHostServerConfig conf)
        {
            //Get exlucded/denied extensions from config, ignore null strings
            if (conf.DenyExtensions is not null)
            {
                return conf.DenyExtensions
                    .Where(static s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToHashSet()
                    .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                return new HashSet<string>().ToFrozenSet();
            }
        }

        private static IReadOnlyCollection<string> GetDefaultFiles(VirtualHostServerConfig conf)
        {
            if(conf.DefaultFiles is null)
            {
                return Array.Empty<string>();
            }

            //Get blocked extensions for the root
            return conf.DefaultFiles
                        .Where(static s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .ToList();
        }

        private static KeyValuePair<string, string>[] GetConfigHeaders(VirtualHostServerConfig conf)
        {
            if (conf.Headers is null)
            {
                return [];
            }

            //Enumerate kv headers
            return conf.Headers
                    //Ignore empty keys or values                        
                    .Where(static p => !string.IsNullOrWhiteSpace(p.Key) && string.IsNullOrWhiteSpace(p.Value))
                    //Exclude special headers
                    .Where(static p => !SpecialHeaders.SpecialHeader.Contains(p.Key, StringComparer.OrdinalIgnoreCase))
                    .Select(static p => new KeyValuePair<string, string>(p.Key!, p.Value))
                    .ToArray();
        }

        private static FrozenDictionary<string, string> GetSpecialHeaders(VirtualHostServerConfig conf)
        {
            //get the headers array property
            if (conf.Headers is null)
            {
                return new Dictionary<string, string>().ToFrozenDictionary();
            }

            //Enumerate kv header
            return conf.Headers
                    //Ignore empty keys or values
                    .Where(static p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value))
                    //Only include special headers
                    .Where(static p => SpecialHeaders.SpecialHeader.Contains(p.Key, StringComparer.OrdinalIgnoreCase))
                    //Create the special dictionary
                    .ToDictionary(static k => k.Key, static k => k.Value, StringComparer.OrdinalIgnoreCase)
                    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }


        [GeneratedRegex(@"(\/\.\.)|(\\\.\.)|[\[\]^*<>|`~'\n\r\t\n]|(\s$)|^(\s)", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}
