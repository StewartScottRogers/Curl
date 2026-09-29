using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Writes Curl's own diagnostic log (ADR-0222): each line at or below the configured
/// level, as <c>[&lt;UTC timestamp&gt;] [&lt;level&gt;] [&lt;component&gt;] &lt;message&gt;</c>
/// and the given line end, one whole line per call, safely from parallel transfers.
/// </summary>
/// <remarks>
/// A carriage return or line feed in a message is written as the two characters <c>\r</c>
/// or <c>\n</c>, so one call is always one line. Each line is flushed as it is written, so
/// a crash keeps the log up to the failure. The first <see cref="IOException" /> from the
/// target stops the writer: it and every later line are dropped, because the log must never
/// change a transfer's exit code.
/// </remarks>
public sealed class DiagnosticLogWriter : IDiagnosticLog
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    // Indexed by level - 1: Error, Warning, Info, Verbose.
    private static readonly string[] LevelTexts = ["error", "warning", "info", "verbose"];

    private readonly TextWriter target;
    private readonly DiagnosticLogLevel configuredLevel;
    private readonly TimeProvider timeProvider;
    private readonly string lineEnd;
    private readonly Lock writeLock = new();

    private bool targetFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticLogWriter" /> class.
    /// </summary>
    /// <param name="target">The writer the lines go to, not owned: standard error or the <c>--log-file</c>.</param>
    /// <param name="level">The most detailed level written, from <see cref="DiagnosticLogLevel.Error" /> to <see cref="DiagnosticLogLevel.Verbose" />.</param>
    /// <param name="timeProvider">The clock read for each line's timestamp.</param>
    /// <param name="lineEnd">The line end written after each line, the one the runner uses for its own standard-error text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target" />, <paramref name="timeProvider" /> or <paramref name="lineEnd" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="level" /> is <see cref="DiagnosticLogLevel.None" /> or not a defined level; at
    /// <c>none</c> no writer exists and components get <see cref="NoDiagnosticLog.Instance" />.
    /// </exception>
    public DiagnosticLogWriter(TextWriter target, DiagnosticLogLevel level, TimeProvider timeProvider, string lineEnd)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(lineEnd);
        ArgumentOutOfRangeException.ThrowIfLessThan((int)level, (int)DiagnosticLogLevel.Error, nameof(level));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)level, (int)DiagnosticLogLevel.Verbose, nameof(level));

        this.target = target;
        configuredLevel = level;
        this.timeProvider = timeProvider;
        this.lineEnd = lineEnd;
    }

    /// <inheritdoc />
    /// <returns>
    /// <see langword="true" /> when <paramref name="level" /> is a level other than
    /// <see cref="DiagnosticLogLevel.None" /> at or below the configured one.
    /// </returns>
    public bool IsEnabled(DiagnosticLogLevel level) => level > DiagnosticLogLevel.None && level <= configuredLevel;

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel level, string component, string message)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        lock (writeLock)
        {
            if (targetFailed)
            {
                return;
            }

            string line = FormatLine(level, component, message);
            try
            {
                target.Write(line);
                target.Flush();
            }
            catch (IOException)
            {
                targetFailed = true;
            }
        }
    }

    private string FormatLine(DiagnosticLogLevel level, string component, string message)
    {
        string timestamp = timeProvider.GetUtcNow().UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        string escapedMessage = message.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
        return $"[{timestamp}] [{LevelTexts[(int)level - 1]}] [{component}] {escapedMessage}{lineEnd}";
    }
}
