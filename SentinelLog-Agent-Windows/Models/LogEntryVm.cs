namespace SyslogAgent.Desktop.Models;

public sealed class LogEntryVm
{
    public string Time { get; init; } = "";
    public string Source { get; init; } = "";
    public string Facility { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Message { get; init; } = "";
    public string SeverityColor { get; init; } = "#333";
}
