/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: InMemoryTemplate.cs 
*
* InMemoryTemplate.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Represents a lazily loaded file stored in memory, with a change mointor 
    /// that reloads the template if the file was modified in the filesystem
    /// </summary>
    public abstract class InMemoryTemplate : VnDisposeable
    {
        protected ManualResetEventSlim TemplateLock;
        private readonly FileSystemWatcher? Watcher;
        private bool Modified;
        private VnMemoryStream templateBuffer;
        protected readonly FileInfo TemplateFile;

        /// <summary>
        /// Gets the name of the template
        /// </summary>
        public abstract string TemplateName { get; }

        /// <summary>
        /// Creates a new in-memory copy of a file that will detect changes and refresh
        /// </summary>
        /// <param name="listenForChanges">Should changes to the template file be moniored for changes, and reloaded as necessary</param>
        /// <param name="path">The path of the file template</param>
        protected InMemoryTemplate(string path, bool listenForChanges = true)
        {
            TemplateFile = new FileInfo(path);
            TemplateLock = new(true);
            //Make sure the file exists
            if (!TemplateFile.Exists)
            {
                throw new FileNotFoundException("Template file does not exist");
            }
            if (listenForChanges)
            {
                //Setup a watcher to reload the template when modified
                Watcher = new FileSystemWatcher(TemplateFile.DirectoryName!)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                Watcher.Changed += Watcher_Changed;
            }
            //Set modified flag to make sure the template is read on first use
            this.Modified = true;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            //Make sure the event was raied for this template
            if (!e.FullPath.Equals(TemplateFile.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            TemplateLock.Reset();
            try
            {
                //Set modified flag
                Modified = true;
                //Refresh the fileinfo object
                TemplateFile.Refresh();
                //Invoke onmodifed function
                OnModifed();
            }
            finally
            {
                TemplateLock.Set();
            }
        }

        /// <summary>
        /// Gets a cached copy of the template data
        /// </summary>
        protected VnMemoryStream GetTemplateData()
        {
            //Make sure access is synchronized incase the file gets updated during access on another thread
            TemplateLock.Wait();
            //Determine if the file has been modified and needs to be reloaded
            if (Modified)
            {
                TemplateLock.Reset();
                try
                {
                    //Read a new copy of the templte into mem
                    ReadFile();
                }
                finally
                {
                    TemplateLock.Set();
                }
            }
            //Return a copy of the memory stream
            return templateBuffer.GetReadonlyShallowCopy();
        }
        /// <summary>
        /// Updates the internal copy of the file to its memory representation
        /// </summary>
        protected void ReadFile()
        {
            //Open the file stream
            using FileStream fs = TemplateFile.OpenRead();
            //Dispose the old template buffer
            templateBuffer?.Dispose();
            //Create a new stream for storing the cached copy
            VnMemoryStream newBuf = new();
            try
            {
                fs.CopyTo(newBuf, null);
            }
            catch
            {
                newBuf.Dispose();
                throw;
            }
            //Create the readonly copy 
            templateBuffer = VnMemoryStream.CreateReadonly(newBuf);
            //Clear the modified flag
            Modified = false;
        }
        /// <summary>
        /// Updates the internal copy of the file to its memory representation, asynchronously
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>A task that completes when the file has been copied into memory</returns>
        protected async Task ReadFileAsync(CancellationToken cancellationToken = default)
        {
            //Open the file stream
            await using FileStream fs = TemplateFile.OpenRead();
            //Dispose the old template buffer
            templateBuffer?.Dispose();
            //Create a new stream for storing the cached copy
            VnMemoryStream newBuf = new();
            try
            {
                //Copy async
                await fs.CopyToAsync(newBuf, 8192, Memory.MemoryUtil.Shared, cancellationToken);
            }
            catch
            {
                newBuf.Dispose();
                throw;
            }
            //Create the readonly copy 
            templateBuffer = VnMemoryStream.CreateReadonly(newBuf);
            //Clear the modified flag
            Modified = false;
        }

        /// <summary>
        /// Invoked when the template file has been modifed. Note: This event is raised 
        /// while the <see cref="TemplateLock"/> is held.
        /// </summary>
        protected abstract void OnModifed();

        ///<inheritdoc/>
        protected override void Free()
        {
            //Dispose the watcher
            Watcher?.Dispose();
            //free the stream
            templateBuffer?.Dispose();
        }
    }
}