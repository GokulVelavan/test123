namespace DRS.Scaffold.Syslog.Worker.Extensions;

/// <summary>General-purpose string helpers used by the worker.</summary>
public static class StringExtensions
{
    /// <summary>Returns at most <paramref name="maxLength"/> characters, appending "…" if truncated.</summary>
    public static string Truncate(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;
        return string.Concat(value.AsSpan(0, maxLength), "…");
    }
}
