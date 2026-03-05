/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: FileCompressionMiddleware.cs 
*
* FileCompressionMiddleware.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Collections.Frozen;
using System.IO;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Middleware;

using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer.Middlewares
{
    internal sealed class FileCompressionMiddleware(FileCompressionConfig config) : IHttpMiddleware
    {
        private readonly FrozenSet<string> _exlcudedFilePaths = config.DisabledFileTypes!.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity) 
            => ValueTask.FromResult(FileProcessArgs.Continue);

        /// <inheritdoc/>
        public void VolatilePostProcess(HttpEntity entity, ref FileProcessArgs currentArgs)
        {
            /*
             * If the server is responding with a file, ServeOther or continue 
             * routines, then we can see if the file has an extension set.
             * 
             * If so and the extension is not in the excluded list, then we can set 
             * a flag on the entity to disable automatic response compression.
             * 
             * Running under PostProcess because we can see the results of normal 
             * processing. 
             */

            string filePath;

            switch (currentArgs.Routine)
            {
                case FpRoutine.ServeOtherFQ:
                case FpRoutine.ServeOther:
                    filePath = currentArgs.Alternate;
                    break;

                case FpRoutine.Continue:
                    filePath = entity.Server.Path;
                    break;

                default:
                    return;
            }

            if (!Path.HasExtension(filePath))
            {
                return;
            }
           
            string extension = Path.GetExtension(filePath);            
            if(_exlcudedFilePaths.Contains(extension))
            {
                // Disable compression if extension is in the excluded list
                entity.SetControlFlag(HttpControlMask.CompressionDisabled);
            }
        }
    }
}
