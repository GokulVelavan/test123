namespace SyslogAgent.Desktop.Models;

public sealed class FilterChannelModel
{
    public string ChannelName { get; init; } = "";
    public List<EventIdModel> EventIds { get; init; } = new();
    public string Summary => $"{ChannelName}  ({EventIds.Count} event IDs)";
}

public sealed class EventIdModel
{
    public int Id { get; init; }
    public string Description { get; init; } = "";
    public string Display => $"{Id} — {Description}";
}
