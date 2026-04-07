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
