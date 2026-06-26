/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: CommandListener.cs
*
* CommandListener.cs is part of VNLib.WebServer which is part of 
* the larger VNLib collection of libraries and utilities.
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
using System.Threading;

using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Diagnostics;
using VNLib.Net.Http;
using VNLib.Plugins.Runtime.Batteries;
using VNLib.Plugins.Essentials.ServiceStack;

using VNLib.WebServer.Bootstrap;

namespace VNLib.WebServer
{

    internal sealed class CommandListener(ManualResetEvent shutdownEvent, WebserverBase server, ILogProvider log)
    {
        const string MANAGED_HEAP_STATS = @"
         Managed Heap Stats
--------------------------------------
 Collections: 
   Gen0: {g0} Gen1: {g1} Gen2: {g2}

 Heap:
  High Watermark:    {hw} KB
  Last GC Heap Size: {hz} KB
  Current Load:      {ld} KB
  Fragmented:        {fb} KB

 Heap Info:
  Last GC concurrent? {con}
  Last GC compacted?  {comp}
  Pause time:         {pt} %
  Pending finalizers: {pf}
  Pinned objects:     {po}
";

        const string HEAPSTATS = @"
    Unmanaged Heap Stats
---------------------------
 userHeap? {rp}
 Allocated bytes:   {ab}
 Allocated handles: {h}
 Max block size:    {mb}
 Min block size:    {mmb}
 Max heap size:     {hs}
";
        const string HELP = @"
    VNLib.WebServer console help menu

    p <plugin-name> <command> - Sends a command to a plugin   
    cmd <plugin-name> - Enters a command loop for the specified plugin
    reload - Reloads all plugins
    memstats - Prints memory stats
    collect - Flushes server caches, collects, and compacts memory
    stop - Stops the server
    help - Prints this help menu
";


        private readonly HttpServiceStack _serviceStack = server.ServiceStack;

        /// <summary>
        /// Listens for commands and processes them in a continuous loop. This function should always be 
        /// run on a separate thread to avoid blocking as plugins can block the thread to take control of 
        /// the console.
        /// </summary>
        /// <param name="shutdownEvent">A <see cref="ManualResetEvent"/> that is set when the Stop command is received</param>
        /// <param name="server">The webserver for the current process</param>
        public void ListenForCommands(TextReader input, TextWriter output, string name)
        {
            log.Information("Listening for commands on {con}", name);

            // Check that the base server supports the plugin console and get a reference to it if it does
            PluginConsoleEventHandler? pluginConsole = (server is ReleaseWebserver r) ? r.ConsoleEventHandler : null;

            while (shutdownEvent.WaitOne(0) == false)
            {
                output.Write("> ");
                string[]? s = input.ReadLine()?.Split(' ');
                if (s == null)
                {
                    continue;
                }
                switch (s[0].ToLower(null))
                {                   
                    //handle plugin command directly
                    case "p":
                        {
                            if (s.Length < 3)
                            {
                                output.WriteLine("Plugin name and command are required");
                                break;
                            }

                            if (server.Plugins is null)
                            {
                                output.WriteLine("Plugin stack is not initialized");
                                break;
                            }

                            if (pluginConsole == null)
                            {
                                output.WriteLine("Plugin console is not available or supported");
                                break;
                            }

                            string message = string.Join(' ', s[2..]);
                         
                            if (!pluginConsole.SendConsoleCommand(s[1], message))
                            {
                                output.WriteLine("Plugin not found");
                                output.WriteLine(
                                    "Available plugins: {0}", 
                                    string.Join(", ", pluginConsole.GetEnabledNames())
                                );
                            }
                        }
                        break;

                    case "cmd":
                        {
                            if (s.Length < 2)
                            {
                                output.WriteLine("Plugin name is required");
                                break;
                            }

                            if (server.Plugins is null)
                            {
                                output.WriteLine("Plugin stack is not initialized");
                                break;
                            }

                            if (pluginConsole == null)
                            {
                                output.WriteLine("Plugin console is not available or supported");
                                break;
                            }

                            if (!pluginConsole.IsEnabled(s[1]))
                            {
                                output.WriteLine("Plugin not found or does not support console commands");
                                output.WriteLine(
                                   "Available plugins: {0}",
                                   string.Join(", ", pluginConsole.GetEnabledNames())
                                );
                                break;
                            }

                            //Enter plugin command loop
                            EnterPluginLoop(input, output, s[1], pluginConsole);
                        }
                        break;
                    case "reload":
                        {
                            if (server.Plugins is null)
                            {
                                output.WriteLine("Plugin stack is not initialized");
                                break;
                            }

                            try
                            {
                                //Reload all plugins
                                server.Plugins.ReloadPlugins(false);
                                output.WriteLine("Plugins reloaded successfully");
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                            }
                        }
                        break;
                    case "memstats":
                        {

                            //Collect gc info for managed heap stats
                            GCMemoryInfo mi = GC.GetGCMemoryInfo();

                            log.Debug(MANAGED_HEAP_STATS,
                                GC.CollectionCount(0),
                                GC.CollectionCount(1),
                                GC.CollectionCount(2),
                                mi.HighMemoryLoadThresholdBytes / 1024,
                                mi.HeapSizeBytes / 1024,
                                mi.MemoryLoadBytes / 1024,
                                mi.FragmentedBytes / 1024,
                                mi.Concurrent,
                                mi.Compacted,
                                mi.PauseTimePercentage,
                                mi.FinalizationPendingCount,
                                mi.PinnedObjectsCount
                            );

                            //Get heap stats
                            HeapStatistics hs = MemoryUtil.GetSharedHeapStats();

                            //Print unmanaged heap stats
                            log.Debug(HEAPSTATS,
                                MemoryUtil.IsUserDefinedHeap,
                                hs.AllocatedBytes,
                                hs.AllocatedBlocks,
                                hs.MaxBlockSize,
                                hs.MinBlockSize,
                                hs.MaxHeapSize
                            );
                        }
                        break;
                    
                    case "collect":
                        CollectCache(_serviceStack);
                        GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
                        GC.WaitForFullGCComplete();

                        log.Information("Manual garbage collection completed");
                        break;
                    
                    case "stop":
                        shutdownEvent.Set();
                        return;

                    case "": // Print newline on empty input
                        break;

                    case "help":
                        output.WriteLine(HELP);
                        break;

                    default:
                        output.WriteLine("Unknown command");
                        goto case "help";
                }
            }
        }

        /*
         * Function scopes commands as if the user is writing directly to 
         * the plugin. All commands are passed to the plugin manager for
         * processing.
         */
        private static void EnterPluginLoop(
            TextReader input,
            TextWriter output,
            string pluginName, 
            PluginConsoleEventHandler man
        )
        {
            output.WriteLine("Entering plugin {0}. Type 'exit' or 'quit' to leave", pluginName);

            while (true)
            {
                output.Write("{0}> ", pluginName);

                string? cmdText = input.ReadLine();

                if (string.IsNullOrWhiteSpace(cmdText))
                {
                    output.WriteLine("Please enter a command or type 'exit' to leave");
                    continue;
                }

                switch (cmdText.ToLower(null))
                {
                    case "quit": // Support quit but do not advertise it
                    case "exit":
                        output.WriteLine("Exiting plugin {0}", pluginName);
                        return;                         
                }

                // Exec command
                if (!man.SendConsoleCommand(pluginName, cmdText))
                {
                    output.WriteLine("Plugin does not exist or has unloaded exiting loop");
                    break;
                }
            }
        }

        private static void CollectCache(HttpServiceStack controller) 
            => controller.Servers.ForEach(static server => (server as HttpServer)!.CacheClear());
    }
}
