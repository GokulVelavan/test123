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
            var opts       = sp.GetRequiredService<IOptions<AgentOptions>>();
            var channel    = sp.GetRequiredService<LogChannel>();
            var dedupStore = sp.GetRequiredService<IDeduplicationStore>();
            var logFactory = sp.GetRequiredService<ILoggerFactory>();
            var collectors = CollectorFactory.CreateCollectors(opts.Value, channel, dedupStore, logFactory).ToList();
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
