/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: Entry.cs
*
* Entry.cs is part of jobber which is part of the larger 
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
using System.Text;
using System.Text.Json;
using Jobber.ConfigLoading;
using Jobber.Runtime;
using VNLib.Utils.Logging;

namespace Jobber;

/// <summary>
/// Application entry point for jobber
/// </summary>
internal static class Entry
{
    private const string STARTUP =
@"jobber - lightweight multi-service supervisor
Copyright (c) 2025 Vaughn Nugent
Starting...
";

    private const string HELP_TEXT = @"
jobber usage:
    --config <path>        Path to config file (.json | .yaml/.yml) or '--' for stdin
    --quiet | -q           Suppress console output from services (still tees to files)
    --dump-config          Pretty print merged configuration
    --wait <service>       Override primary service to wait on
    --stop-timeout <sec>   Override stop_timeout_sec
    --list                 List services and exit
    --dry-run              Parse and validate only
    -v / --verbose         Verbose logging
    -d / --debug           Debug logging
    -h / --help            Help
Config schema:
{
    ""stop_timeout_sec"": 15,
    ""services"": [
         {
            ""name"": ""api"",
            ""command"": ""dotnet"",
            ""args"": [""run""],
            ""working_dir"": ""./src/api"",
            ""env"": { ""ASPNETCORE_ENVIRONMENT"": ""Development"" },
            ""depends_on"": [ ""db"" ],
            ""primary"": true,
            ""wait_for_exit"": false,
            ""shutdown_with_dependents"": false,
            ""tee"": { ""stdout"": ""logs/api.out"", ""stderr"": ""logs/api.err"", ""append"": true }
         }
    ]
}
";

    internal static int Main(string[] args)
    {
        ProcessArguments procArgs = new ProcessArguments(args);
        if (args.Length == 0 || procArgs.HasArgument("-h") || procArgs.HasArgument("--help"))
        {
            PrintHelp();
            return 0;
        }

        Console.WriteLine(STARTUP);

        ILogProvider log = BuildLog(procArgs);

        IJobberConfigProvider? provider = LoadConfig(procArgs);
        if (provider == null)
        {
            log.Fatal("Failed to load configuration");
            return -1;
        }

        if (procArgs.DumpConfig)
        {
            DumpConfig(provider.Root, log);
        }

        if (procArgs.List)
        {
            ServiceManager listManager = new ServiceManager(provider.Config, log, procArgs, CancellationToken.None);
            listManager.ListServices();
            return 0;
        }

        CancellationTokenSource cts = new CancellationTokenSource();
        Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            ServiceManager manager = new ServiceManager(provider.Config, log, procArgs, cts.Token);
            // Avoid Task.Result deadlocks and respects sync exit code pattern
            int exitCode = manager.RunAsync().GetAwaiter().GetResult();
            return exitCode;
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "Fatal error");
            return -1;
        }
        finally
        {
            cts.Dispose();
        }
    }

    private static ILogProvider BuildLog(ProcessArguments args)
    {
        LoggerConfiguration cfg = new LoggerConfiguration();
        if (args.Verbose)
        {
            cfg.MinimumLevel.Verbose();
        }
        else if (args.Debug)
        {
            cfg.MinimumLevel.Debug();
        }
        else
        {
            cfg.MinimumLevel.Information();
        }

        if (!args.Quiet)
        {
            cfg.WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] jobber {Message:lj}{NewLine}{Exception}");
        }
        return new VLogProvider(cfg);
    }

    private static IJobberConfigProvider? LoadConfig(ProcessArguments args)
    {
        string path = args.ConfigPath ?? "jobber.json";
        if (path == "--")
        {
            return JsonJobberConfig.FromStdin();
        }
        return JsonJobberConfig.FromFile(path);
    }

    private static void DumpConfig(JsonElement root, ILogProvider log)
    {
        MemoryStream ms = new MemoryStream();
        Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        try
        {
            root.WriteTo(writer);
        }
        finally
        {
            writer.Dispose();
        }
        string json = Encoding.UTF8.GetString(ms.ToArray());
        log.Information("Loaded configuration:\n{json}", json);
        ms.Dispose();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(HELP_TEXT);
    }
}