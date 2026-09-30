using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet.Fakes;

/// <summary>
/// An <see cref="IDiagnosticLog" /> that records every line written at or below
/// <paramref name="level" />, as the real log's cumulative levels do (ADR-0222).
/// </summary>
/// <param name="level">The most detailed level recorded.</param>
public sealed class RecordingDiagnosticLog(DiagnosticLogLevel level) : IDiagnosticLog
{
    /// <summary>Gets every line recorded, in order.</summary>
    public List<(DiagnosticLogLevel Level, string Component, string Message)> Lines { get; } = [];

    /// <summary>Gets the messages recorded at <paramref name="wanted" />, in order.</summary>
    /// <param name="wanted">The level to pick.</param>
    /// <returns>The messages.</returns>
    public string[] At(DiagnosticLogLevel wanted) =>
        [.. Lines.Where(line => line.Level == wanted).Select(line => line.Message)];

    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLogLevel candidate) => candidate != DiagnosticLogLevel.None && candidate <= level;

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel candidate, string component, string message)
    {
        Assert.IsTrue(IsEnabled(candidate), $"Wrote {candidate} without testing IsEnabled first.");
        Lines.Add((candidate, component, message));
    }
}
