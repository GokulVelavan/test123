# Syslog Agent Cleanup & Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove ASP.NET Core, deduplicate shared parsers, fix 3 Linux collector bugs, add unit tests for pure functions, and publish a self-contained linux-x64 binary.

**Architecture:** Pure `IHost`/`BackgroundService` console daemon — no open ports, no web framework. Shared static helpers (`JournaldParser`, `WindowsEventLogMapper`) eliminate duplicated parsing logic. New `SyslogAgent.Tests` xunit project covers all pure functions.

**Tech Stack:** .NET 8, `Microsoft.NET.Sdk` (not Web), xunit 2.x, self-contained linux-x64 publish

---

## File Map

| Action | Path |
|---|---|
| Modify | `src/SyslogAgent/SyslogAgent.csproj` |
| Modify | `src/SyslogAgent/Program.cs` |
| Modify | `src/SyslogAgent/Configuration/AgentOptions.cs` |
| Modify | `src/SyslogAgent/appsettings.json` |
| Modify | `src/SyslogAgent/Pipeline/LogPipeline.cs` |
| Delete | `src/SyslogAgent/Api/` (entire folder) |
| Create | `src/SyslogAgent/Collectors/Linux/JournaldParser.cs` |
| Modify | `src/SyslogAgent/Collectors/Linux/JournaldCollector.cs` |
| Modify | `src/SyslogAgent/Collectors/Linux/JournaldBatchCollector.cs` |
| Create | `src/SyslogAgent/Collectors/Windows/WindowsEventLogMapper.cs` |
| Modify | `src/SyslogAgent/Collectors/Windows/WindowsEventLogCollector.cs` |
| Modify | `src/SyslogAgent/Collectors/Windows/WindowsEventLogBatchCollector.cs` |
| Fix | `src/SyslogAgent/Collectors/Linux/LinuxLogDiscoveryCollector.cs` |
| Delete | `publish/`, `publish-linux/`, `publish-new/`, `publish2/` (repo root) |
| Create | `src/SyslogAgent.Tests/SyslogAgent.Tests.csproj` |
| Create | `src/SyslogAgent.Tests/JournaldParserTests.cs` |
| Create | `src/SyslogAgent.Tests/SyslogFormatterTests.cs` |
| Modify | `SyslogAgent.sln` |

---

## Task 1: Switch SDK and remove ASP.NET Core from csproj

**Files:**
- Modify: `src/SyslogAgent/SyslogAgent.csproj`

- [ ] **Step 1: Update the csproj**

Replace the entire file content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>SyslogAgent</RootNamespace>
    <AssemblyName>SyslogAgent</AssemblyName>
    <Version>1.0.0</Version>
    <Platforms>AnyCPU</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.*" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
      <CopyToPublishDirectory>Always</CopyToPublishDirectory>
    </None>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Verify build still compiles (will fail — expected — because Program.cs still uses WebApplication)**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent -c Debug 2>&1 | head -30
```

Expected: errors about `WebApplication`, `Results`, `IResult` — that's fine, we fix those next.

---

## Task 2: Rewrite Program.cs — replace WebApplication with IHost

**Files:**
- Modify: `src/SyslogAgent/Program.cs`

- [ ] **Step 1: Replace the entire file**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyslogAgent.Collectors;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

using SyslogAgent;

ConsoleWriter.WriteLine("╔══════════════════════════════════════════════════╗", ConsoleColor.Cyan);
ConsoleWriter.WriteLine("║          Syslog Machine Agent  v1.0.0           ║", ConsoleColor.Cyan);
ConsoleWriter.WriteLine("║     Cross-Platform Log Collection & Forwarding  ║", ConsoleColor.Cyan);
ConsoleWriter.WriteLine("╚══════════════════════════════════════════════════╝", ConsoleColor.Cyan);
Console.WriteLine();

ConsoleWriter.WriteLine("── Syslog Server Configuration ──", ConsoleColor.Yellow);

Console.Write("  Syslog Server IP/Hostname [127.0.0.1]: ");
var inputHost = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(inputHost)) inputHost = "127.0.0.1";

int inputPort = 514;
while (true)
{
    Console.Write("  Syslog Server Port       [514]: ");
    var inputPortStr = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(inputPortStr)) break;
    if (int.TryParse(inputPortStr, out var parsedPort) && parsedPort >= 1 && parsedPort <= 65535)
    {
        inputPort = parsedPort;
        break;
    }
    ConsoleWriter.WriteLine("    Invalid port. Enter a number between 1 and 65535.", ConsoleColor.Red);
}

Console.Write("  Protocol — 1) UDP  2) TCP    [1]: ");
var protoInput = Console.ReadLine()?.Trim()?.ToUpperInvariant();
var inputProtocol = protoInput switch
{
    "2" or "TCP" => "TCP",
    _ => "UDP"
};
ConsoleWriter.WriteLine($"  Selected: {inputProtocol}", ConsoleColor.DarkGray);
Console.WriteLine();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, config) =>
    {
        config.Sources.Clear();
        config
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:SyslogServer:Host"]     = inputHost,
                ["Agent:SyslogServer:Port"]     = inputPort.ToString(),
                ["Agent:SyslogServer:Protocol"] = inputProtocol
            });
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<AgentOptions>(ctx.Configuration.GetSection(AgentOptions.SectionName));

        services
            .AddSingleton<LogChannel>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AgentOptions>>();
                return new LogChannel(opts.Value.ChannelCapacity);
            })
            .AddSingleton<IDeduplicationStore, FileOffsetStore>()
            .AddSingleton<ISyslogSender, SyslogSender>();

        services.AddSingleton(sp =>
        {
            var opts        = sp.GetRequiredService<IOptions<AgentOptions>>();
            var channel     = sp.GetRequiredService<LogChannel>();
            var dedupStore  = sp.GetRequiredService<IDeduplicationStore>();
            var logFactory  = sp.GetRequiredService<ILoggerFactory>();
            var collectors  = CollectorFactory.CreateCollectors(opts.Value, channel, dedupStore, logFactory).ToList();
            return new CollectorRegistry(collectors);
        });

        services.AddSingleton<IEnumerable<ILogCollector>>(sp =>
            sp.GetRequiredService<CollectorRegistry>().All);

        services.AddSingleton<IEnumerable<IBatchCollector>>(sp =>
            sp.GetRequiredService<CollectorRegistry>().BatchCollectors);

        services
            .AddHostedService<LogPipeline>()
            .AddHostedService<BatchScheduler>()
            .AddHostedService<CollectorHost>();
    })
    .Build();

var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    var dedupStore = host.Services.GetRequiredService<IDeduplicationStore>();
    if (dedupStore is FileOffsetStore fileStore)
        await fileStore.InitializeAsync(CancellationToken.None);

    var agentOpts = host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;

    if (string.IsNullOrWhiteSpace(agentOpts.SyslogServer.Host))
    {
        startupLogger.LogCritical("SyslogServer.Host is not configured. Set it in appsettings.json");
        return;
    }
    if (agentOpts.SyslogServer.Port < 1 || agentOpts.SyslogServer.Port > 65535)
    {
        startupLogger.LogCritical("SyslogServer.Port must be between 1 and 65535. Got: {Port}", agentOpts.SyslogServer.Port);
        return;
    }
    if (!agentOpts.SyslogServer.Protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase) &&
        !agentOpts.SyslogServer.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
    {
        startupLogger.LogCritical("SyslogServer.Protocol must be 'UDP' or 'TCP'. Got: {Protocol}", agentOpts.SyslogServer.Protocol);
        return;
    }

    var registry       = host.Services.GetRequiredService<CollectorRegistry>();
    var collectorCount = registry.All.Count;
    if (collectorCount == 0)
        startupLogger.LogWarning("No log collectors were configured. Check appsettings.json");

    ConsoleWriter.WriteLine("── Agent Status ─────────────────────────────────", ConsoleColor.Green);
    ConsoleWriter.WriteLine($"  Hostname:       {Environment.MachineName}");
    ConsoleWriter.WriteLine($"  Platform:       {(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Unknown")}");
    ConsoleWriter.WriteLine($"  Realtime Mode:  {(agentOpts.EnableRealtime ? "Enabled" : "Disabled")}");
    ConsoleWriter.WriteLine($"  Batch Mode:     {(agentOpts.EnableBatch ? "Enabled" : "Disabled")}");
    ConsoleWriter.WriteLine($"  Batch Interval: {agentOpts.BatchIntervalSeconds}s");
    ConsoleWriter.WriteLine($"  Filter Mode:    {(agentOpts.Collectors.ApplyFilter ? "Filtered (matching event IDs only)" : "All Logs (collecting everything)")}");
    ConsoleWriter.WriteLine("");

    ConsoleWriter.WriteLine("── Target Syslog Server ─────────────────────────", ConsoleColor.Green);
    ConsoleWriter.WriteLine($"  Sending to:     {agentOpts.SyslogServer.Protocol}://{agentOpts.SyslogServer.Host}:{agentOpts.SyslogServer.Port}");
    ConsoleWriter.WriteLine($"  Max Message:    {agentOpts.SyslogServer.MaxMessageBytes} bytes");
    if (agentOpts.SyslogServer.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        ConsoleWriter.WriteLine($"  TCP Timeout:    {agentOpts.SyslogServer.TcpTimeoutSeconds}s");
    ConsoleWriter.WriteLine("");

    ConsoleWriter.WriteLine($"── Log Collectors ({collectorCount}) ──────────────────────────", ConsoleColor.Green);
    foreach (var collector in registry.All)
    {
        var isBatch    = collector is IBatchCollector;
        var isRealtime = collector is IRealtimeCollector;
        var mode       = (isBatch && isRealtime) ? "Both" : isBatch ? "Batch" : "Realtime";
        ConsoleWriter.WriteLine($"  [{mode,-8}] {collector.Name}");
    }
    ConsoleWriter.WriteLine("");

    ConsoleWriter.WriteLine("══════════════════════════════════════════════════", ConsoleColor.Cyan);
    ConsoleWriter.WriteLine("  Agent is running. Press Ctrl+C to stop.",         ConsoleColor.Cyan);
    ConsoleWriter.WriteLine("══════════════════════════════════════════════════", ConsoleColor.Cyan);
    ConsoleWriter.WriteLine("");

    await host.RunAsync();
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Application terminated unexpectedly");
    throw;
}
```

---

## Task 3: Remove API folder and clean AgentOptions + appsettings

**Files:**
- Delete: `src/SyslogAgent/Api/` (entire folder)
- Modify: `src/SyslogAgent/Configuration/AgentOptions.cs`
- Modify: `src/SyslogAgent/appsettings.json`

- [ ] **Step 1: Delete the Api folder**

```bash
rm -rf "d:/Syslog-Machine-agent/src/SyslogAgent/Api"
```

- [ ] **Step 2: Update AgentOptions.cs — remove ApiPort and EnableApi**

Replace the entire file:

```csharp
namespace SyslogAgent.Configuration;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public SyslogServerOptions SyslogServer { get; set; } = new();
    public CollectorOptions Collectors { get; set; } = new();
    public int BatchIntervalSeconds { get; set; } = 60;
    public int ChannelCapacity { get; set; } = 10_000;
    public bool EnableRealtime { get; set; } = true;
    public bool EnableBatch { get; set; } = true;
    public string DeduplicationStorePath { get; set; } = "offsets.json";
}
```

- [ ] **Step 3: Remove ApiPort and EnableApi from appsettings.json**

Find these two lines at the bottom of the `"Agent"` section and delete them:
```json
"ApiPort": 5100,
"EnableApi": true
```

- [ ] **Step 4: Update LogPipeline.cs — remove AgentMetrics and RecentLogBuffer**

Replace the entire file at `src/SyslogAgent/Pipeline/LogPipeline.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyslogAgent.Configuration;
using SyslogAgent.Syslog;

namespace SyslogAgent.Pipeline;

public sealed class LogPipeline : BackgroundService
{
    private readonly LogChannel _channel;
    private readonly ISyslogSender _sender;
    private readonly ILogger<LogPipeline> _logger;
    private readonly SyslogServerOptions _serverOpts;

    public LogPipeline(
        LogChannel channel,
        ISyslogSender sender,
        IOptions<AgentOptions> options,
        ILogger<LogPipeline> logger)
    {
        _channel    = channel;
        _sender     = sender;
        _serverOpts = options.Value.SyslogServer;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogPipeline started, waiting for log events");

        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _sender.SendAsync(evt, stoppingToken);

                    var msg = evt.Message.Length > 120 ? evt.Message[..117] + "..." : evt.Message;
                    msg = msg.ReplaceLineEndings(" ");

                    ConsoleWriter.WriteLineSegments(
                        ($"  [{evt.Timestamp:HH:mm:ss}] ", ConsoleColor.DarkGray),
                        (evt.Source,                       ConsoleColor.White),
                        (" → ",                            ConsoleColor.DarkGray),
                        ($"{_serverOpts.Protocol}://{_serverOpts.Host}:{_serverOpts.Port}", ConsoleColor.Green),
                        ($" | {evt.Severity} | ",          ConsoleColor.DarkGray),
                        (msg,                              null));
                }
                catch (Exception ex)
                {
                    ConsoleWriter.WriteLine($"  [ERROR] Failed to send: {ex.Message}", ConsoleColor.Red);
                    _logger.LogError(ex, "Failed to send log event");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("LogPipeline stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LogPipeline encountered an unexpected error");
            throw;
        }

        while (_channel.Reader.TryRead(out var remaining))
        {
            try
            {
                await _sender.SendAsync(remaining, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send remaining event during shutdown");
                break;
            }
        }

        await _sender.FlushAsync(CancellationToken.None);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Verify the build compiles cleanly**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent -c Debug 2>&1
```

Expected: `Build succeeded. 0 Error(s) 0 Warning(s)`

- [ ] **Step 6: Commit**

```bash
cd d:/Syslog-Machine-agent
git add src/SyslogAgent/SyslogAgent.csproj src/SyslogAgent/Program.cs src/SyslogAgent/Configuration/AgentOptions.cs src/SyslogAgent/appsettings.json src/SyslogAgent/Pipeline/LogPipeline.cs
git commit -m "refactor: remove ASP.NET Core, replace WebApplication with IHost"
```

---

## Task 4: Create JournaldParser shared helper

**Files:**
- Create: `src/SyslogAgent/Collectors/Linux/JournaldParser.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Text.Json;
using SyslogAgent.Models;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Linux;

/// <summary>
/// Shared pure-function helpers for parsing journald JSON output.
/// Used by both JournaldCollector (realtime) and JournaldBatchCollector (batch).
/// </summary>
internal static class JournaldParser
{
    public static LogEvent? Parse(string json, CollectionMode mode)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            if (!root.TryGetProperty("MESSAGE", out var messageEl))
                return null;

            var message = messageEl.GetString() ?? string.Empty;

            var timestamp = DateTimeOffset.UtcNow;
            if (root.TryGetProperty("__REALTIME_TIMESTAMP", out var tsEl) &&
                long.TryParse(tsEl.GetString(), out var tsMicros))
            {
                timestamp = new DateTimeOffset(
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMicroseconds(tsMicros));
            }

            var hostname = "localhost";
            if (root.TryGetProperty("_HOSTNAME", out var hostEl))
                hostname = hostEl.GetString() ?? hostname;

            var source = "journal";
            if (root.TryGetProperty("_COMM", out var commEl))
                source = commEl.GetString() ?? source;

            var facility = SyslogFacility.Local0;
            if (root.TryGetProperty("SYSLOG_FACILITY", out var facilEl) &&
                int.TryParse(facilEl.GetString(), out var facil))
                facility = (SyslogFacility)facil;

            var severity = SyslogSeverity.Informational;
            if (root.TryGetProperty("PRIORITY", out var priorEl) &&
                int.TryParse(priorEl.GetString(), out var prior))
                severity = (SyslogSeverity)(prior % 8);

            var cursor = string.Empty;
            if (root.TryGetProperty("__CURSOR", out var cursorEl))
                cursor = cursorEl.GetString() ?? string.Empty;

            return new LogEvent
            {
                Timestamp        = timestamp,
                Hostname         = hostname,
                Source           = source,
                Message          = message,
                Facility         = facility,
                Severity         = severity,
                CollectedBy      = mode,
                DeduplicationKey = cursor
            };
        }
        catch
        {
            return null;
        }
    }

    public static string ExtractCursor(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("__CURSOR", out var el))
                return el.GetString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    public static bool IsValidMatch(string match)
    {
        if (match.Length > 512 || !match.Contains('='))
            return false;
        foreach (var c in match)
        {
            if (!char.IsLetterOrDigit(c) && c != '=' && c != '_' && c != '-' && c != '.' && c != '/' && c != ':')
                return false;
        }
        return true;
    }

    public static bool IsValidCursor(string cursor)
    {
        foreach (var c in cursor)
        {
            if (!char.IsLetterOrDigit(c) && c != '=' && c != ';' && c != ' ' && c != '_' && c != '-')
                return false;
        }
        return cursor.Length > 0 && cursor.Length < 1024;
    }
}
```

---

## Task 5: Update JournaldCollector and JournaldBatchCollector to use JournaldParser

**Files:**
- Modify: `src/SyslogAgent/Collectors/Linux/JournaldCollector.cs`
- Modify: `src/SyslogAgent/Collectors/Linux/JournaldBatchCollector.cs`

- [ ] **Step 1: Update JournaldCollector.cs**

Replace the `ReadJournalctlOutputAsync` method and remove the private `ParseJournaldJson` and `IsValidJournaldMatch` methods. The updated file:

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;

namespace SyslogAgent.Collectors.Linux;

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class JournaldCollector : IRealtimeCollector, IAsyncDisposable
{
    private readonly AgentOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<JournaldCollector> _logger;
    private Process? _journalProcess;
    private Task? _readTask;

    public string Name => "Journald.Realtime";

    public JournaldCollector(AgentOptions options, LogChannel channel, IDeduplicationStore dedupStore, ILogger<JournaldCollector> logger)
    {
        _options    = options;
        _channel    = channel;
        _dedupStore = dedupStore;
        _logger     = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var argList = BuildJournalctlArgList();
            _logger.LogInformation("Journald realtime starting: journalctl {Args}", string.Join(" ", argList));

            var psi = new ProcessStartInfo
            {
                FileName             = "journalctl",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute      = false,
                CreateNoWindow       = true
            };
            foreach (var arg in argList)
                psi.ArgumentList.Add(arg);

            _journalProcess = new Process { StartInfo = psi };
            _journalProcess.Start();
            _readTask = Task.Run(() => ReadJournalctlOutputAsync(cancellationToken), cancellationToken);

            _logger.LogInformation("Journald collector started");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start JournaldCollector");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_journalProcess is not null)
        {
            try { _journalProcess.Kill(); } catch { }
            await _journalProcess.WaitForExitAsync(cancellationToken);
            _journalProcess.Dispose();
            _journalProcess = null;
        }

        if (_readTask is not null)
        {
            try { await _readTask; } catch { }
            _readTask = null;
        }
    }

    private List<string> BuildJournalctlArgList()
    {
        var args = new List<string> { "-f", "--output=json", "--since=now" };

        if (_options.Collectors.ApplyFilter)
        {
            var maxPriority = _options.Collectors.LinuxJournaldMaxPriority;
            if (maxPriority >= 0 && maxPriority < 7)
                args.Add($"--priority=0..{maxPriority}");

            foreach (var unit in _options.Collectors.LinuxJournaldUnits)
                args.Add($"--unit={unit}");

            foreach (var match in _options.Collectors.LinuxJournaldMatches)
            {
                if (!string.IsNullOrWhiteSpace(match) && JournaldParser.IsValidMatch(match))
                    args.Add(match);
                else
                    _logger.LogWarning("Skipping invalid journald match: {Match}", match);
            }
        }

        return args;
    }

    private async Task ReadJournalctlOutputAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_journalProcess?.StandardOutput is null)
                return;

            var stream = _journalProcess.StandardOutput;
            while (!stream.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await stream.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var evt = JournaldParser.Parse(line, CollectionMode.Realtime);
                if (evt is null)
                    continue;

                await _channel.Writer.WriteAsync(evt, cancellationToken);

                if (!string.IsNullOrEmpty(evt.DeduplicationKey))
                    await _dedupStore.SetCursorAsync("journald", evt.DeduplicationKey, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading journalctl output");
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_journalProcess is not null)
        {
            try
            {
                if (!_journalProcess.HasExited)
                {
                    _journalProcess.Kill();
                    await _journalProcess.WaitForExitAsync();
                }
            }
            catch { }
            _journalProcess.Dispose();
        }
    }
}
```

- [ ] **Step 2: Update JournaldBatchCollector.cs — use JournaldParser and add process timeout**

Replace the entire file:

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;

namespace SyslogAgent.Collectors.Linux;

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class JournaldBatchCollector : IBatchCollector
{
    private readonly AgentOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<JournaldBatchCollector> _logger;

    public string Name => "Journald.Batch";

    public JournaldBatchCollector(
        AgentOptions options,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<JournaldBatchCollector> logger)
    {
        _options    = options;
        _channel    = channel;
        _dedupStore = dedupStore;
        _logger     = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken)  => Task.CompletedTask;

    public async Task CollectBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lastCursor = await _dedupStore.GetCursorAsync("journald", cancellationToken);

            if (lastCursor is not null && !JournaldParser.IsValidCursor(lastCursor))
            {
                _logger.LogWarning("Invalid journald cursor detected, ignoring: {Cursor}", lastCursor);
                lastCursor = null;
            }

            var argList = BuildBatchArgList(lastCursor);
            var output  = await RunJournalctlAsync(argList, cancellationToken);
            string? latestCursor = null;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var evt = JournaldParser.Parse(line, CollectionMode.Batch);
                if (evt is null)
                    continue;

                latestCursor = JournaldParser.ExtractCursor(line);
                await _channel.Writer.WriteAsync(evt, cancellationToken);
            }

            if (latestCursor is not null)
                await _dedupStore.SetCursorAsync("journald", latestCursor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in journald batch collection");
        }
    }

    private List<string> BuildBatchArgList(string? cursor)
    {
        var args = new List<string> { "--output=json" };

        if (cursor is not null)
            args.Add($"--after-cursor={cursor}");
        else
            args.Add("--boot");

        if (_options.Collectors.ApplyFilter)
        {
            var maxPriority = _options.Collectors.LinuxJournaldMaxPriority;
            if (maxPriority >= 0 && maxPriority < 7)
                args.Add($"--priority=0..{maxPriority}");

            foreach (var unit in _options.Collectors.LinuxJournaldUnits)
                args.Add($"--unit={unit}");

            foreach (var match in _options.Collectors.LinuxJournaldMatches)
            {
                if (!string.IsNullOrWhiteSpace(match) && JournaldParser.IsValidMatch(match))
                    args.Add(match);
                else
                    _logger.LogWarning("Skipping invalid journald match: {Match}", match);
            }
        }

        return args;
    }

    private static async Task<string> RunJournalctlAsync(List<string> argList, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName             = "journalctl",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute      = false,
            CreateNoWindow       = true
        };
        foreach (var arg in argList)
            psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // BUG FIX: add 60s timeout so a hung journalctl never blocks the batch cycle
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);
            return output;
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(); } catch { }
            return string.Empty;
        }
    }
}
```

- [ ] **Step 3: Build and confirm 0 errors**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent -c Debug 2>&1
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
cd d:/Syslog-Machine-agent
git add src/SyslogAgent/Collectors/Linux/
git commit -m "refactor: extract JournaldParser, fix batch collector timeout"
```

---

## Task 6: Create WindowsEventLogMapper shared helper

**Files:**
- Create: `src/SyslogAgent/Collectors/Windows/WindowsEventLogMapper.cs`
- Modify: `src/SyslogAgent/Collectors/Windows/WindowsEventLogCollector.cs`
- Modify: `src/SyslogAgent/Collectors/Windows/WindowsEventLogBatchCollector.cs`

- [ ] **Step 1: Create WindowsEventLogMapper.cs**

```csharp
using System.Diagnostics.Eventing.Reader;
using SyslogAgent.Models;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Windows;

/// <summary>
/// Shared pure-function helper for mapping Windows EventRecord to LogEvent.
/// Used by both WindowsEventLogCollector (realtime) and WindowsEventLogBatchCollector (batch).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class WindowsEventLogMapper
{
    public static LogEvent Map(EventRecord record, CollectionMode mode)
    {
        var level    = record.Level ?? 0;
        var severity = level switch
        {
            1 => SyslogSeverity.Critical,
            2 => SyslogSeverity.Error,
            3 => SyslogSeverity.Warning,
            4 => SyslogSeverity.Informational,
            5 => SyslogSeverity.Debug,
            _ => SyslogSeverity.Notice
        };

        var logName  = record.LogName ?? "Unknown";
        var facility = logName switch
        {
            "Security"    => SyslogFacility.Security,
            "System"      => SyslogFacility.System,
            "Application" => SyslogFacility.UserLevel,
            _ when logName.Contains("Security",         StringComparison.OrdinalIgnoreCase)
                || logName.Contains("AppLocker",        StringComparison.OrdinalIgnoreCase)
                || logName.Contains("CodeIntegrity",    StringComparison.OrdinalIgnoreCase)
                || logName.Contains("NTLM",             StringComparison.OrdinalIgnoreCase)
                || logName.Contains("SMB",              StringComparison.OrdinalIgnoreCase)
                || logName.Contains("Defender",         StringComparison.OrdinalIgnoreCase) => SyslogFacility.Security,
            _ when logName.Contains("TerminalServices", StringComparison.OrdinalIgnoreCase)
                || logName.Contains("RemoteDesktop",    StringComparison.OrdinalIgnoreCase) => SyslogFacility.AuthPriv,
            _ when logName.Contains("TaskScheduler",    StringComparison.OrdinalIgnoreCase) => SyslogFacility.Cron,
            _ when logName.Contains("PrintService",     StringComparison.OrdinalIgnoreCase) => SyslogFacility.LinePrinter,
            _ when logName.Contains("DNS",              StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local7,
            _ when logName.Contains("Sysmon",           StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local1,
            _ when logName.Contains("PowerShell",       StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local2,
            _ when logName.Contains("Firewall",         StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local4,
            _ when logName.Contains("WMI",              StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local5,
            _ when logName.Contains("Bits",             StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local6,
            _ when logName.Contains("Driver",           StringComparison.OrdinalIgnoreCase)
                || logName.Contains("Kernel",           StringComparison.OrdinalIgnoreCase) => SyslogFacility.Kernel,
            _ => SyslogFacility.Local0
        };

        string message;
        try   { message = record.FormatDescription() ?? record.ToXml(); }
        catch { message = record.ToXml(); }

        return new LogEvent
        {
            Timestamp        = record.TimeCreated ?? DateTimeOffset.Now,
            Hostname         = Environment.MachineName,
            Source           = record.ProviderName ?? "Unknown",
            Message          = message,
            Facility         = facility,
            Severity         = severity,
            CollectedBy      = mode,
            DeduplicationKey = $"{logName}:{record.RecordId}"
        };
    }
}
```

- [ ] **Step 2: Update WindowsEventLogCollector.cs — remove private MapToLogEvent method, call mapper instead**

In `WindowsEventLogCollector.cs`, find and replace the call to `MapToLogEvent`:

Change line in `OnEventRecordWritten`:
```csharp
var evt = MapToLogEvent(record, CollectionMode.Realtime);
```
To:
```csharp
var evt = WindowsEventLogMapper.Map(record, CollectionMode.Realtime);
```

Then delete the entire private `MapToLogEvent` static method (lines 139–201 in the original file).

- [ ] **Step 3: Update WindowsEventLogBatchCollector.cs — same change**

In `WindowsEventLogBatchCollector.cs`, find and replace:
```csharp
var evt = MapToLogEvent(record, CollectionMode.Batch);
```
To:
```csharp
var evt = WindowsEventLogMapper.Map(record, CollectionMode.Batch);
```

Then delete the entire private `MapToLogEvent` static method (lines 170–232 in the original file).

- [ ] **Step 4: Build and confirm 0 errors**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent -c Debug 2>&1
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
cd d:/Syslog-Machine-agent
git add src/SyslogAgent/Collectors/Windows/
git commit -m "refactor: extract WindowsEventLogMapper, remove duplicated MapToLogEvent"
```

---

## Task 7: Fix LinuxLogDiscoveryCollector — wrong CollectionMode in batch

**Files:**
- Modify: `src/SyslogAgent/Collectors/Linux/LinuxLogDiscoveryCollector.cs`

- [ ] **Step 1: Add a `mode` parameter to ReadFileAsync**

Find the method signature:
```csharp
private async Task ReadFileAsync(string path, CancellationToken cancellationToken)
```

Replace with:
```csharp
private async Task ReadFileAsync(string path, CancellationToken cancellationToken, CollectionMode mode = CollectionMode.Realtime)
```

- [ ] **Step 2: Use the mode parameter when creating LogEvent**

Inside `ReadFileAsync`, find:
```csharp
CollectedBy = CollectionMode.Realtime,
```

Replace with:
```csharp
CollectedBy = mode,
```

- [ ] **Step 3: Pass CollectionMode.Batch from ScanDirectoryAsync**

Find the call inside `ScanDirectoryAsync`:
```csharp
await ReadFileAsync(path, cancellationToken);
```

Replace with:
```csharp
await ReadFileAsync(path, cancellationToken, CollectionMode.Batch);
```

The FSW `OnFileEvent` callback already uses the default `CollectionMode.Realtime` — no change needed there.

- [ ] **Step 4: Build**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent -c Debug 2>&1
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
cd d:/Syslog-Machine-agent
git add src/SyslogAgent/Collectors/Linux/LinuxLogDiscoveryCollector.cs
git commit -m "fix: LinuxLogDiscoveryCollector passes correct CollectionMode in batch scan"
```

---

## Task 8: Delete stale publish folders

**Files:**
- Delete: `publish/`, `publish-linux/`, `publish-new/`, `publish2/` at repo root

- [ ] **Step 1: Delete the folders**

```bash
cd d:/Syslog-Machine-agent
rm -rf publish publish-linux publish-new publish2
```

- [ ] **Step 2: Add publish output to .gitignore**

Check if `.gitignore` exists:
```bash
ls d:/Syslog-Machine-agent/.gitignore 2>/dev/null && echo "exists" || echo "not found"
```

If it exists, append to it. If not, create it:
```bash
cat >> d:/Syslog-Machine-agent/.gitignore << 'EOF'

# Publish output
out/
publish*/
EOF
```

- [ ] **Step 3: Commit**

```bash
cd d:/Syslog-Machine-agent
git add -A
git commit -m "chore: remove stale publish output folders, add to .gitignore"
```

---

## Task 9: Create unit test project

**Files:**
- Create: `src/SyslogAgent.Tests/SyslogAgent.Tests.csproj`
- Modify: `SyslogAgent.sln`

- [ ] **Step 1: Create the test project**

```bash
cd d:/Syslog-Machine-agent
dotnet new xunit -n SyslogAgent.Tests -o src/SyslogAgent.Tests --framework net8.0
```

- [ ] **Step 2: Add project reference to main project**

```bash
cd d:/Syslog-Machine-agent
dotnet add src/SyslogAgent.Tests/SyslogAgent.Tests.csproj reference src/SyslogAgent/SyslogAgent.csproj
```

- [ ] **Step 3: Add test project to solution**

```bash
cd d:/Syslog-Machine-agent
dotnet sln add src/SyslogAgent.Tests/SyslogAgent.Tests.csproj
```

- [ ] **Step 4: Verify the test project builds**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent.Tests 2>&1
```

Expected: `Build succeeded. 0 Error(s)`

---

## Task 10: Write JournaldParser unit tests

**Files:**
- Create: `src/SyslogAgent.Tests/JournaldParserTests.cs`

- [ ] **Step 1: Delete the placeholder UnitTest1.cs generated by the template**

```bash
rm d:/Syslog-Machine-agent/src/SyslogAgent.Tests/UnitTest1.cs
```

- [ ] **Step 2: Create JournaldParserTests.cs**

```csharp
using SyslogAgent.Collectors.Linux;
using SyslogAgent.Models;
using SyslogAgent.Syslog;

namespace SyslogAgent.Tests;

public class JournaldParserTests
{
    private const string ValidJson = """
        {
            "__CURSOR": "s=abc;i=1",
            "__REALTIME_TIMESTAMP": "1705316625000000",
            "MESSAGE": "sshd started",
            "_HOSTNAME": "ubuntu22",
            "_COMM": "sshd",
            "SYSLOG_FACILITY": "4",
            "PRIORITY": "6"
        }
        """;

    [Fact]
    public void Parse_ValidJson_ReturnsLogEvent()
    {
        var result = JournaldParser.Parse(ValidJson, CollectionMode.Realtime);

        Assert.NotNull(result);
        Assert.Equal("sshd started", result.Message);
        Assert.Equal("ubuntu22", result.Hostname);
        Assert.Equal("sshd", result.Source);
        Assert.Equal(CollectionMode.Realtime, result.CollectedBy);
    }

    [Fact]
    public void Parse_ValidJson_MapsTimestampFromMicroseconds()
    {
        // 1705316625000000 microseconds = 2024-01-15 10:23:45 UTC
        var result = JournaldParser.Parse(ValidJson, CollectionMode.Realtime);

        Assert.NotNull(result);
        Assert.Equal(2024, result.Timestamp.Year);
        Assert.Equal(1, result.Timestamp.Month);
        Assert.Equal(15, result.Timestamp.Day);
    }

    [Fact]
    public void Parse_ValidJson_MapsPriorityToSeverity()
    {
        // PRIORITY "6" = Informational
        var result = JournaldParser.Parse(ValidJson, CollectionMode.Realtime);

        Assert.NotNull(result);
        Assert.Equal(SyslogSeverity.Informational, result.Severity);
    }

    [Fact]
    public void Parse_ValidJson_MapsFacility()
    {
        // SYSLOG_FACILITY "4" = Security (per SyslogFacility enum)
        var result = JournaldParser.Parse(ValidJson, CollectionMode.Realtime);

        Assert.NotNull(result);
        Assert.Equal(SyslogFacility.Security, result.Facility);
    }

    [Fact]
    public void Parse_ValidJson_SetsCursor()
    {
        var result = JournaldParser.Parse(ValidJson, CollectionMode.Realtime);

        Assert.NotNull(result);
        Assert.Equal("s=abc;i=1", result.DeduplicationKey);
    }

    [Fact]
    public void Parse_MissingMessage_ReturnsNull()
    {
        const string json = """{"__CURSOR": "s=abc", "_HOSTNAME": "host"}""";

        var result = JournaldParser.Parse(json, CollectionMode.Realtime);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var result = JournaldParser.Parse("not json at all", CollectionMode.Realtime);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        var result = JournaldParser.Parse("", CollectionMode.Realtime);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_BatchMode_SetsCollectedByBatch()
    {
        var result = JournaldParser.Parse(ValidJson, CollectionMode.Batch);

        Assert.NotNull(result);
        Assert.Equal(CollectionMode.Batch, result.CollectedBy);
    }

    [Fact]
    public void ExtractCursor_ValidJson_ReturnsCursor()
    {
        var cursor = JournaldParser.ExtractCursor(ValidJson);

        Assert.Equal("s=abc;i=1", cursor);
    }

    [Fact]
    public void ExtractCursor_NoCursor_ReturnsEmpty()
    {
        const string json = """{"MESSAGE": "hello"}""";
        var cursor = JournaldParser.ExtractCursor(json);

        Assert.Equal(string.Empty, cursor);
    }

    [Fact]
    public void IsValidMatch_ValidKeyValue_ReturnsTrue()
    {
        Assert.True(JournaldParser.IsValidMatch("_TRANSPORT=audit"));
        Assert.True(JournaldParser.IsValidMatch("SYSLOG_IDENTIFIER=sshd"));
    }

    [Fact]
    public void IsValidMatch_NoEquals_ReturnsFalse()
    {
        Assert.False(JournaldParser.IsValidMatch("_TRANSPORTaudit"));
    }

    [Fact]
    public void IsValidMatch_InjectionAttempt_ReturnsFalse()
    {
        Assert.False(JournaldParser.IsValidMatch("_COMM=foo; rm -rf /"));
    }

    [Fact]
    public void IsValidCursor_ValidCursor_ReturnsTrue()
    {
        Assert.True(JournaldParser.IsValidCursor("s=abc123; i=456; b=def"));
    }

    [Fact]
    public void IsValidCursor_EmptyString_ReturnsFalse()
    {
        Assert.False(JournaldParser.IsValidCursor(""));
    }

    [Fact]
    public void IsValidCursor_TooLong_ReturnsFalse()
    {
        Assert.False(JournaldParser.IsValidCursor(new string('a', 1025)));
    }
}
```

- [ ] **Step 3: Run the tests**

```bash
cd d:/Syslog-Machine-agent
dotnet test src/SyslogAgent.Tests -v normal 2>&1
```

Expected: All tests pass. If `JournaldParser` methods are `internal`, you may see "inaccessible" errors — fix by adding to `SyslogAgent.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="SyslogAgent.Tests" />
</ItemGroup>
```

Then re-run until all pass.

- [ ] **Step 4: Commit**

```bash
cd d:/Syslog-Machine-agent
git add src/SyslogAgent.Tests/ src/SyslogAgent/SyslogAgent.csproj SyslogAgent.sln
git commit -m "test: add JournaldParser unit tests"
```

---

## Task 11: Write SyslogFormatter unit tests

**Files:**
- Create: `src/SyslogAgent.Tests/SyslogFormatterTests.cs`

- [ ] **Step 1: Create SyslogFormatterTests.cs**

```csharp
using System.Text;
using SyslogAgent.Models;
using SyslogAgent.Syslog;

namespace SyslogAgent.Tests;

public class SyslogFormatterTests
{
    private static SyslogMessage MakeMessage(string content = "test message", int maxBytes = 1024) =>
        new()
        {
            Facility  = SyslogFacility.UserLevel,
            Severity  = SyslogSeverity.Informational,
            Timestamp = new DateTimeOffset(2024, 1, 15, 10, 23, 45, TimeSpan.Zero),
            Hostname  = "myhost",
            Tag       = "myapp",
            Content   = content
        };

    [Fact]
    public void FormatRfc3164_ReturnsExpectedPrefix()
    {
        // Facility=1 (UserLevel), Severity=6 (Informational) → PRI = (1*8)+6 = 14
        var bytes = SyslogFormatter.FormatRfc3164(MakeMessage());
        var result = Encoding.UTF8.GetString(bytes);

        Assert.StartsWith("<14>Jan 15 10:23:45 myhost myapp: test message", result);
    }

    [Fact]
    public void FormatRfc3164_SingleDigitDay_PaddedWithSpace()
    {
        var bytes = SyslogFormatter.FormatRfc3164(MakeMessage());
        var result = Encoding.UTF8.GetString(bytes);

        // Day 15 is double-digit so no padding; use day=5 to test padding
        var msg = new SyslogMessage
        {
            Facility  = SyslogFacility.UserLevel,
            Severity  = SyslogSeverity.Informational,
            Timestamp = new DateTimeOffset(2024, 1, 5, 10, 23, 45, TimeSpan.Zero),
            Hostname  = "myhost",
            Tag       = "myapp",
            Content   = "test"
        };
        var bytes2 = SyslogFormatter.FormatRfc3164(msg);
        var result2 = Encoding.UTF8.GetString(bytes2);

        Assert.Contains("Jan  5", result2); // note double-space for single digit day
    }

    [Fact]
    public void FormatRfc3164_MessageWithinLimit_NotTruncated()
    {
        var content = new string('A', 100);
        var bytes   = SyslogFormatter.FormatRfc3164(MakeMessage(content), maxBytes: 1024);
        var result  = Encoding.UTF8.GetString(bytes);

        Assert.Contains(content, result);
    }

    [Fact]
    public void FormatRfc3164_MessageExceedsLimit_Truncated()
    {
        var content = new string('A', 2000);
        var bytes   = SyslogFormatter.FormatRfc3164(MakeMessage(content), maxBytes: 512);

        Assert.True(bytes.Length <= 512);
    }

    [Fact]
    public void FormatRfc3164_TruncationDoesNotSplitUtf8Char()
    {
        // A 3-byte UTF-8 char: '中' = 0xE4 0xB8 0xAD
        var content = new string('中', 300); // 300 * 3 = 900 bytes
        var bytes   = SyslogFormatter.FormatRfc3164(MakeMessage(content), maxBytes: 512);

        // Must be valid UTF-8 — if truncation split a char this would throw
        var ex = Record.Exception(() => Encoding.UTF8.GetString(bytes));
        Assert.Null(ex);
        Assert.True(bytes.Length <= 512);
    }

    [Fact]
    public void FormatRfc3164_TagTruncatedTo32Chars()
    {
        // SyslogMessage.Tag is the responsibility of the caller (SyslogSender.MapToSyslogMessage),
        // not SyslogFormatter — this test verifies the formatter doesn't alter the tag
        var msg = new SyslogMessage
        {
            Facility  = SyslogFacility.UserLevel,
            Severity  = SyslogSeverity.Informational,
            Timestamp = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Hostname  = "h",
            Tag       = "MyTag",
            Content   = "msg"
        };
        var bytes  = SyslogFormatter.FormatRfc3164(msg);
        var result = Encoding.UTF8.GetString(bytes);

        Assert.Contains("MyTag:", result);
    }
}
```

- [ ] **Step 2: Run all tests**

```bash
cd d:/Syslog-Machine-agent
dotnet test src/SyslogAgent.Tests -v normal 2>&1
```

Expected: All tests pass (both `JournaldParserTests` and `SyslogFormatterTests`).

- [ ] **Step 3: Commit**

```bash
cd d:/Syslog-Machine-agent
git add src/SyslogAgent.Tests/SyslogFormatterTests.cs
git commit -m "test: add SyslogFormatter unit tests"
```

---

## Task 12: Final build verification and publish linux-x64

- [ ] **Step 1: Full clean build of the agent**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent -c Release 2>&1
```

Expected: `Build succeeded. 0 Error(s) 0 Warning(s)`

- [ ] **Step 2: Run all tests one final time**

```bash
cd d:/Syslog-Machine-agent
dotnet test src/SyslogAgent.Tests -c Release 2>&1
```

Expected: All tests pass.

- [ ] **Step 3: Publish linux-x64 self-contained single file**

```bash
cd d:/Syslog-Machine-agent
dotnet publish src/SyslogAgent -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -o out/linux-x64
```

Expected output in `out/linux-x64/`:
- `SyslogAgent` — single executable binary (~80-100MB including .NET runtime)
- `appsettings.json` — copy alongside the binary on the target machine

- [ ] **Step 4: Verify binary exists and is executable**

```bash
ls -lh d:/Syslog-Machine-agent/out/linux-x64/
```

Expected: `SyslogAgent` file present, size ~80-100MB.

- [ ] **Step 5: Verify Windows build still compiles (no publish, just build)**

```bash
cd d:/Syslog-Machine-agent
dotnet build src/SyslogAgent -c Release -r win-x64 2>&1
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Final commit**

```bash
cd d:/Syslog-Machine-agent
git add out/.gitkeep 2>/dev/null || true
git commit -m "chore: final verified build — linux-x64 publish ready"
```

---

## Deployment on Ubuntu 22

Copy two files to the target server:

```bash
scp out/linux-x64/SyslogAgent user@ubuntu22:/opt/syslog-agent/
scp out/linux-x64/appsettings.json user@ubuntu22:/opt/syslog-agent/
```

Run:
```bash
chmod +x /opt/syslog-agent/SyslogAgent
sudo /opt/syslog-agent/SyslogAgent
```

No `dotnet` install required. Ubuntu 22 standard packages (`libicu70`, `libssl3`, `libc6`) are sufficient.
