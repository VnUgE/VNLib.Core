/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: HttpCompressor.cs 
*
* HttpCompressor.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Text.Json;
using System.Reflection;
using System.Runtime.Loader;

using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Net.Http;

using VNLib.WebServer.Config;
using VNLib.WebServer.RuntimeLoading;
using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer.Compression
{

    internal static class HttpCompressor
    {
        /*
         * A function delegate that is invoked on the user-defined http compressor library
         * when loaded
         */
        private delegate void OnHttpLibLoad(ILogProvider log, JsonElement? configData);

        /// <summary>
        /// Attempts to load a user-defined http compressor library from the specified path in the config,
        /// otherwise falls back to the default http compressor, unless the command line disabled compression.
        /// </summary>
        /// <param name="args">Process wide- argument list</param>
        /// <param name="config">The top-level config element</param>
        /// <param name="logger">The application logger to write logging events to</param>
        /// <returns>The <see cref="IHttpCompressorManager"/> that the user configured, or null if disabled</returns>
        public static IHttpCompressorManager? LoadOrDefaultCompressor(ProcessArguments args, HttpCompressorConfig compConfig, IServerConfig config, ILogProvider logger)
        {
            const string EXTERN_LIB_LOAD_METHOD_NAME = "OnLoad";

            if (args.HasArgument("--compression-off"))
            {
                logger.Information("Http compression disabled by cli args");
                return null;
            }

            if(!compConfig.Enabled)
            {
                logger.Information("Http compression disabled by config");
                return null;
            }

            if (string.IsNullOrWhiteSpace(compConfig.AssemblyPath))
            {
                logger.Information("Falling back to default http compressor");
                return new FallbackCompressionManager();
            }

            //Make sure the file exists
            if (!FileOperations.FileExists(compConfig.AssemblyPath))
            {
                logger.Warn("The specified http compressor assembly file does not exist, falling back to default http compressor");
                return new FallbackCompressionManager();
            }

            //Try to load the assembly into our process alc, we dont need to worry about unloading
            ManagedLibrary lib = ManagedLibrary.LoadManagedAssembly(compConfig.AssemblyPath, AssemblyLoadContext.Default);

            logger.Debug("Loading user defined compressor assembly: {asm}", Path.GetFileName(lib.AssemblyPath));

            try
            {
                //Load the compressor manager type from the assembly
                IHttpCompressorManager instance = lib.LoadTypeFromAssembly<IHttpCompressorManager>();

                /*
                 * We can provide some optional library initialization functions if the library 
                 * supports it. First we can allow the library to write logs to our log provider
                 * and second we can provide the library with the raw configuration data as a byte array
                 */

                //Invoke the on load method with the logger and config data
                OnHttpLibLoad? onlibLoadConfig = ManagedLibrary.TryGetMethod<OnHttpLibLoad>(instance, EXTERN_LIB_LOAD_METHOD_NAME);
                onlibLoadConfig?.Invoke(logger, config.GetDocumentRoot());

                //Invoke parameterless on load method
                Action? onLibLoad = ManagedLibrary.TryGetMethod<Action>(instance, EXTERN_LIB_LOAD_METHOD_NAME);
                onLibLoad?.Invoke();

                logger.Information("Custom compressor library loaded");

                return instance;
            }
            //Catch TIE and throw the inner exception for cleaner debug
            catch (TargetInvocationException te) when (te.InnerException != null)
            {
                throw te.InnerException;
            }
        }
    }
}
