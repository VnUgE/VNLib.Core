/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: ServiceManager.cs
*
* ServiceManager.cs is part of jobber which is part of the larger 
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jobber.Config;
using VNLib.Utils.Logging;

namespace Jobber.Runtime;

internal sealed class ServiceManager
{
    private readonly JobberConfig _cfg;
    private readonly ILogProvider _log;
    private readonly ProcessArguments _args;
    private readonly CancellationToken _token;
    private readonly Dictionary<string, ServiceNode> _nodes;
    private readonly List<ServiceNode> _topology;

    private sealed class ServiceNode
    {
        public ServiceConfig Config { get; }
        public List<ServiceNode> Dependents { get; } = new List<ServiceNode>();
        public int RemainingDeps;
        public Process? Process;
        public int? ExitCode;
        public Task? OutputTask;
        public Task? ErrorTask;
        public CancellationTokenSource KillCts = new CancellationTokenSource();
        public ServiceNode(ServiceConfig cfg)
        {
            Config = cfg;
        }
    }

    public ServiceManager(JobberConfig cfg, ILogProvider log, ProcessArguments args, CancellationToken token)
    {
        _cfg = cfg;
        _log = log;
        _args = args;
        _token = token;
        _nodes = cfg.Services.ToDictionary(s => s.Name!, s => new ServiceNode(s), StringComparer.OrdinalIgnoreCase);
        BuildGraph();
        _topology = TopologicalSort();
    }

    private void BuildGraph()
    {
        foreach (ServiceNode n in _nodes.Values)
        {
            foreach (string dep in n.Config.DependsOn)
            {
                if (!_nodes.ContainsKey(dep))
                {
                    throw new InvalidOperationException($"Service '{n.Config.Name}' depends on unknown service '{dep}'");
                }
            }
        }
        // build dependents + counts
        foreach (ServiceNode n in _nodes.Values)
        {
            n.RemainingDeps = n.Config.DependsOn.Length;
            foreach (string dep in n.Config.DependsOn)
            {
                _nodes[dep].Dependents.Add(n);
            }
        }
    }

    private List<ServiceNode> TopologicalSort()
    {
        Queue<ServiceNode> ready = new Queue<ServiceNode>(_nodes.Values.Where(n => n.RemainingDeps == 0));
        List<ServiceNode> order = new List<ServiceNode>(_nodes.Count);
        int visited = 0;
        while (ready.Count > 0)
        {
            ServiceNode n = ready.Dequeue();
            order.Add(n);
            visited++;
            foreach (ServiceNode d in n.Dependents)
            {
                if (--d.RemainingDeps == 0)
                {
                    ready.Enqueue(d);
                }
            }
        }
        if (visited != _nodes.Count)
        {
            throw new InvalidOperationException("Dependency cycle detected in services");
        }
        // reset RemainingDeps for runtime use
        foreach (ServiceNode n in _nodes.Values)
        {
            n.RemainingDeps = n.Config.DependsOn.Length;
        }
        return order;
    }

    public void ListServices()
    {
        foreach (ServiceNode n in _topology)
        {
            Console.WriteLine($"- {n.Config.Name} (deps: {string.Join(',', n.Config.DependsOn)}) primary={n.Config.Primary} wait={n.Config.WaitForExit}");
        }
    }

    public async Task<int> RunAsync()
    {
        string? waitName = _args.WaitForService ?? _cfg.Services.FirstOrDefault(s => s.Primary)?.Name;
        ServiceNode? found;
        ServiceNode? waitNode = (waitName != null && _nodes.TryGetValue(waitName, out found)) ? found : null;

        if (waitName != null && waitNode == null)
        {
            _log.Error("Specified wait service '{name}' not found", waitName);
            return -1;
        }

        if (_args.DryRun)
        {
            _log.Information("Dry run: topology order: {order}", string.Join(" -> ", _topology.Select(t => t.Config.Name)));
            return 0;
        }

        using CancellationTokenRegistration reg = _token.Register(() =>
        {
            _log.Warn("Cancellation requested, initiating shutdown");
            _ = StopAllAsync(false);
        });

        // Start services in topo order when dependencies started
        foreach (ServiceNode node in _topology)
        {
            await StartNodeAsync(node);
        }

        if (waitNode != null)
        {
            _log.Information("Waiting for primary service '{name}' to exit", waitNode.Config.Name);
            while (waitNode.ExitCode is null && !_token.IsCancellationRequested)
            {
                await Task.Delay(250, _token).ContinueWith(_ => { });
            }
            _log.Information("Primary service exited with {code}", waitNode.ExitCode);
            await StopAllAsync(force: false);
            return waitNode.ExitCode ?? 0;
        }

        // Wait for all wait_for_exit flagged services then exit
        ServiceNode[] waitables = _nodes.Values.Where(n => n.Config.WaitForExit).ToArray();
        if (waitables.Length == 0)
        {
            _log.Information("No primary or wait services specified; press Ctrl+C to terminate");
            while (!_token.IsCancellationRequested)
            {
                await Task.Delay(500, _token).ContinueWith(_ => { });
            }
            await StopAllAsync(false);
            return 0;
        }
        _log.Information("Waiting for {count} designated wait services", waitables.Length);
        while (!_token.IsCancellationRequested && waitables.Any(w => w.ExitCode is null))
        {
            await Task.Delay(300, _token).ContinueWith(_ => { });
        }
        await StopAllAsync(false);
        ServiceNode? failed = waitables.FirstOrDefault(w => w.ExitCode != 0);
        return failed != null ? failed.ExitCode ?? 0 : 0;
    }

    private async Task StartNodeAsync(ServiceNode node)
    {
        if (_token.IsCancellationRequested) return;

        foreach (string dep in node.Config.DependsOn)
        {
            // Wait for dependency process to have started (simple spin)
            while (_nodes[dep].Process == null && !_token.IsCancellationRequested)
            {
                await Task.Delay(50);
            }
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = node.Config.Command!,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(node.Config.WorkingDirectory)
                    ? Environment.CurrentDirectory
                    : node.Config.WorkingDirectory
        };

        if (node.Config.Args.Length > 0)
        {
            psi.ArgumentList.Clear();
            foreach (string a in node.Config.Args)
            {
                psi.ArgumentList.Add(a);
            }
        }

        if (node.Config.Environment != null)
        {
            foreach (var kvp in node.Config.Environment)
            {
                psi.Environment[kvp.Key] = kvp.Value;
            }
        }

        Process proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!proc.Start())
        {
            throw new InvalidOperationException($"Failed to start service '{node.Config.Name}'");
        }
        node.Process = proc;

        _log.Information("Started service {name} pid={pid}", node.Config.Name, proc.Id);

        node.OutputTask = HandleStreamAsync(node, proc.StandardOutput, node.Config.Tee?.StdOutPath, false);
        node.ErrorTask = HandleStreamAsync(node, proc.StandardError, node.Config.Tee?.StdErrPath, true);

        proc.Exited += (_, _) =>
            {
                try
                {
                    node.ExitCode = proc.ExitCode;
                    _log.Information("Service {name} exited code={code}", node.Config.Name, proc.ExitCode);

                    if (node.Config.ShutdownWithDependents)
                    {
                        foreach (ServiceNode d in node.Dependents)
                        {
                            _ = StopNodeAsync(d, false);
                        }
                    }
                }
                catch { }
            };
    }

    private async Task HandleStreamAsync(ServiceNode node, StreamReader reader, string? teePath, bool isErr)
    {
        FileStream? tee = null;
        StreamWriter? teeWriter = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(teePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(teePath))!);
                tee = new FileStream(teePath, node.Config.Tee!.Append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
                teeWriter = new StreamWriter(tee, Encoding.UTF8) { AutoFlush = true };
            }

            while (!reader.EndOfStream && !node.KillCts.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync();
                if (line == null) break;
                if (!_args.Quiet)
                {
                    if (isErr)
                        Console.Error.WriteLine($"[{node.Config.Name}] {line}");
                    else
                        Console.WriteLine($"[{node.Config.Name}] {line}");
                }
                if (teeWriter != null)
                {
                    await teeWriter.WriteLineAsync(line);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Stream handler error for {name}", node.Config.Name);
        }
        finally
        {
            teeWriter?.Dispose();
            tee?.Dispose();
        }
    }

    private async Task StopAllAsync(bool force)
    {
        List<Task> stops = new List<Task>();
        foreach (ServiceNode n in _nodes.Values)
        {
            stops.Add(StopNodeAsync(n, force));
        }
        await Task.WhenAll(stops);
    }

    private async Task StopNodeAsync(ServiceNode node, bool force)
    {
        if (node.Process == null) return;

        try
        {
            node.KillCts.Cancel();

            if (!node.Process.HasExited)
            {
                try
                {
                    node.Process.CloseMainWindow();
                }
                catch { }

                int timeoutMs = (_cfg.StopTimeoutSeconds * 1000);
                if (_args.StopTimeoutOverride.HasValue)
                    timeoutMs = _args.StopTimeoutOverride.Value * 1000;

                using CancellationTokenSource cts = new(timeoutMs);
                while (!node.Process.HasExited && !cts.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                if (!node.Process.HasExited)
                {
                    if (force)
                    {
                        node.Process.Kill(entireProcessTree: true);
                        _log.Warn("Force killed service {name}", node.Config.Name);
                    }
                    else
                    {
                        node.Process.Kill(entireProcessTree: true);
                        _log.Warn("Killed service {name} after timeout", node.Config.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn(ex, "Failed to stop service {name}", node.Config.Name);
        }
    }
}