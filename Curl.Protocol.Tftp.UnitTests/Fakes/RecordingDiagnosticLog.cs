using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IDiagnosticLog" /> that records every line at or below
/// <paramref name="level" />, as <c>--log-level</c> would write it.
/// </summary>
/// <param name="level">The most detailed level recorded.</param>
public sealed class RecordingDiagnosticLog(DiagnosticLogLevel level = DiagnosticLogLevel.Verbose) : IDiagnosticLog
{
    /// <summary>Gets every line recorded, in order.</summary>
    public List<(DiagnosticLogLevel Level, string Component, string Message)> Lines { get; } = [];

    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLogLevel lineLevel) => lineLevel != DiagnosticLogLevel.None && lineLevel <= level;

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel lineLevel, string component, string message)
    {
        if (IsEnabled(lineLevel))
        {
            Lines.Add((lineLevel, component, message));
        }
    }

    /// <summary>Gets the messages recorded at <paramref name="lineLevel" />, in order.</summary>
    /// <param name="lineLevel">The level to select.</param>
    /// <returns>The messages.</returns>
    public string[] MessagesAt(DiagnosticLogLevel lineLevel) =>
        [.. Lines.Where(line => line.Level == lineLevel).Select(line => line.Message)];
}
