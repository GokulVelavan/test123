namespace SyslogAgent;

/// <summary>
/// Thread-safe console writer. All colored console output must go through this class
/// to prevent color bleeding between threads.
/// </summary>
public static class ConsoleWriter
{
    private static readonly object Lock = new();

    /// <summary>
    /// Write a line with a single color.
    /// </summary>
    public static void WriteLine(string text, ConsoleColor? color = null)
    {
        lock (Lock)
        {
            if (color.HasValue) Console.ForegroundColor = color.Value;
            Console.WriteLine(text);
            if (color.HasValue) Console.ResetColor();
        }
    }

    /// <summary>
    /// Write text (no newline) with a single color.
    /// </summary>
    public static void Write(string text, ConsoleColor? color = null)
    {
        lock (Lock)
        {
            if (color.HasValue) Console.ForegroundColor = color.Value;
            Console.Write(text);
            if (color.HasValue) Console.ResetColor();
        }
    }

    /// <summary>
    /// Write multiple colored segments followed by a newline.
    /// Each segment is a (text, color?) tuple.
    /// </summary>
    public static void WriteLineSegments(params (string Text, ConsoleColor? Color)[] segments)
    {
        lock (Lock)
        {
            foreach (var (text, color) in segments)
            {
                if (color.HasValue) Console.ForegroundColor = color.Value;
                Console.Write(text);
                if (color.HasValue) Console.ResetColor();
            }
            Console.WriteLine();
        }
    }
}
