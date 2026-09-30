using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp.Fakes;

/// <summary>
/// An <see cref="IDiagnosticLog" /> that records every line at or below
/// <paramref name="level" />, the levels being cumulative as <c>--log-level</c> makes them.
/// </summary>
/// <param name="level">The most detailed level recorded.</param>
public sealed class RecordingDiagnosticLog(DiagnosticLogLevel level = DiagnosticLogLevel.Verbose) : IDiagnosticLog
{
    /// <summary>Gets every line written, in order.</summary>
    public List<(DiagnosticLogLevel Level, string Component, string Message)> Lines { get; } = [];

    /// <summary>Gets the messages written at <paramref name="at" />, in order.</summary>
    /// <param name="at">The level to pick.</param>
    /// <returns>The messages.</returns>
    public string[] MessagesAt(DiagnosticLogLevel at) => [.. Lines.Where(line => line.Level == at).Select(line => line.Message)];

    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLogLevel candidate) => candidate != DiagnosticLogLevel.None && candidate <= level;

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel candidate, string component, string message)
    {
        Assert.IsTrue(IsEnabled(candidate), "A line was written at a level that is not enabled.");
        Lines.Add((candidate, component, message));
    }
}
