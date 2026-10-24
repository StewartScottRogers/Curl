using System.Globalization;

namespace Curl.Mcp;

/// <summary>
/// The MCP server's log: writes each message to standard error as one line, prefixed with its UTC time
/// (<c>2026-10-23T12:00:00.000Z message</c>), and flushes it. Standard output carries only the protocol, so
/// nothing the server logs ever goes there. Safe to call from several threads at once: lines never interleave.
/// </summary>
internal sealed class McpStandardErrorLog
{
    private readonly TextWriter error;

    private readonly TimeProvider timeProvider;

    private readonly Lock gate = new();

    /// <summary>Initializes a new instance of the <see cref="McpStandardErrorLog" /> class.</summary>
    /// <param name="error">The standard error writer the lines go to; the caller keeps ownership.</param>
    /// <param name="timeProvider">The clock each line's time is read from.</param>
    internal McpStandardErrorLog(TextWriter error, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.error = error;
        this.timeProvider = timeProvider;
    }

    /// <summary>Writes <paramref name="message" /> as one timestamped line and flushes it.</summary>
    /// <param name="message">The message, without a line terminator.</param>
    internal void WriteLine(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        string line = string.Create(CultureInfo.InvariantCulture, $"{timeProvider.GetUtcNow().UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.fff'Z'} {message}");
        lock (gate)
        {
            error.WriteLine(line);
            error.Flush();
        }
    }
}
