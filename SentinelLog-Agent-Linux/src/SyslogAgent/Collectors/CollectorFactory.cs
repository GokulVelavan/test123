using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Pipeline;

namespace SyslogAgent.Collectors;

/// <summary>
/// Factory for creating platform-specific log collectors.
/// Single point of OperatingSystem.IsWindows/IsLinux branching.
/// </summary>
public static class CollectorFactory
{
    public static IEnumerable<ILogCollector> CreateCollectors(
        AgentOptions options,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILoggerFactory loggerFactory)
    {
        var collectors = new List<ILogCollector>();

        // Windows collectors
        if (OperatingSystem.IsWindows())
        {
            if (options.Collectors.WindowsEventLog)
            {
                if (options.EnableRealtime)
                {
                    collectors.Add(new Windows.WindowsEventLogCollector(
                        options, channel, dedupStore, loggerFactory.CreateLogger<Windows.WindowsEventLogCollector>()));
                }
                if (options.EnableBatch)
                {
                    collectors.Add(new Windows.WindowsEventLogBatchCollector(
                        options, channel, dedupStore, loggerFactory.CreateLogger<Windows.WindowsEventLogBatchCollector>()));
                }
            }
        }

        // Linux collectors
        if (OperatingSystem.IsLinux())
        {
            if (options.Collectors.LinuxJournald)
            {
                if (options.EnableRealtime)
                {
                    collectors.Add(new Linux.JournaldCollector(
                        options, channel, dedupStore, loggerFactory.CreateLogger<Linux.JournaldCollector>()));
                }
                if (options.EnableBatch)
                {
                    collectors.Add(new Linux.JournaldBatchCollector(
                        options, channel, dedupStore, loggerFactory.CreateLogger<Linux.JournaldBatchCollector>()));
                }
            }

            var syslogPatterns = options.Collectors.LinuxSyslogFilePatterns;
            var applyFilter = options.Collectors.ApplyFilter;
            foreach (var syslogPath in options.Collectors.LinuxSyslogFiles)
            {
                if (options.EnableRealtime)
                {
                    collectors.Add(new Linux.SyslogFileCollector(
                        syslogPath, channel, dedupStore, syslogPatterns, applyFilter, loggerFactory.CreateLogger<Linux.SyslogFileCollector>()));
                }
                if (options.EnableBatch)
                {
                    collectors.Add(new Linux.SyslogFileBatchCollector(
                        syslogPath, channel, dedupStore, syslogPatterns, applyFilter, loggerFactory.CreateLogger<Linux.SyslogFileBatchCollector>()));
                }
            }

            // dmesg — kernel ring buffer fallback for non-journald systems
            if (options.Collectors.LinuxDmesg && options.EnableBatch)
            {
                collectors.Add(new Linux.DmesgBatchCollector(
                    channel, dedupStore, loggerFactory.CreateLogger<Linux.DmesgBatchCollector>()));
            }

            // Binary login records (wtmp / btmp / lastlog) via last / lastb / lastlog commands
            if (options.Collectors.LinuxBinaryLogs && options.EnableBatch)
            {
                collectors.Add(new Linux.LinuxBinaryLogCollector(
                    channel, dedupStore, loggerFactory.CreateLogger<Linux.LinuxBinaryLogCollector>()));
            }

            // Auto-discovery: watches /var/log/ recursively, picks up any text log file
            // not already covered by the explicit LinuxSyslogFiles list.
            // Implements both IRealtimeCollector and IBatchCollector — registered once.
            if (options.Collectors.LinuxAutoDiscoverLogs)
            {
                var discovery = new Linux.LinuxLogDiscoveryCollector(
                    options.Collectors.LinuxSyslogFiles,
                    channel,
                    dedupStore,
                    loggerFactory.CreateLogger<Linux.LinuxLogDiscoveryCollector>());
                collectors.Add(discovery);
            }
        }

        // Cross-platform file watchers
        foreach (var fw in options.Collectors.FileWatchers)
        {
            if (options.EnableRealtime)
            {
                collectors.Add(new Common.FileWatcherCollector(
                    fw, channel, dedupStore, loggerFactory.CreateLogger<Common.FileWatcherCollector>()));
            }
            if (options.EnableBatch)
            {
                collectors.Add(new Common.FileBatchCollector(
                    fw, channel, dedupStore, loggerFactory.CreateLogger<Common.FileBatchCollector>()));
            }
        }

        return collectors;
    }
}
