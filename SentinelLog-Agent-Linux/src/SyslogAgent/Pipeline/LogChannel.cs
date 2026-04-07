using System.Threading.Channels;
using SyslogAgent.Models;

namespace SyslogAgent.Pipeline;

/// <summary>
/// Thread-safe bounded channel for log events flowing from collectors to the pipeline.
/// Uses DropOldest strategy when full.
/// </summary>
public sealed class LogChannel
{
    private readonly Channel<LogEvent> _channel;

    public ChannelWriter<LogEvent> Writer => _channel.Writer;
    public ChannelReader<LogEvent> Reader => _channel.Reader;

    public LogChannel(int capacity)
    {
        _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }
}
