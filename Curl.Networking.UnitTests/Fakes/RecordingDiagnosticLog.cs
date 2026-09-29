using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IDiagnosticLog" /> that records every line written at or below
/// <paramref name="level" />, as the real log's cumulative levels do (ADR-0222).
/// </summary>
/// <param name="level">The most detailed level recorded.</param>
public sealed class RecordingDiagnosticLog(DiagnosticLogLevel level) : IDiagnosticLog
{
    /// <summary>Gets every line recorded, in order.</summary>
    public List<(DiagnosticLogLevel Level, string Component, string Message)> Lines { get; } = [];

    /// <summary>Gets the lines recorded at <paramref name="wanted" /> for <paramref name="component" />, in order.</summary>
    /// <param name="wanted">The level to pick.</param>
    /// <param name="component">The component to pick.</param>
    /// <returns>The messages.</returns>
    public string[] At(DiagnosticLogLevel wanted, string component) =>
        [.. Lines.Where(line => line.Level == wanted && line.Component == component).Select(line => line.Message)];

    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLogLevel candidate) => candidate != DiagnosticLogLevel.None && candidate <= level;

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel candidate, string component, string message)
    {
        Assert.IsTrue(IsEnabled(candidate), $"Wrote {candidate} without testing IsEnabled first.");
        Lines.Add((candidate, component, message));
    }

    /// <summary>Asserts that no recorded message contains <paramref name="secret" />.</summary>
    /// <param name="secret">The value that must never be logged.</param>
    public void AssertNeverContains(string secret)
    {
        Assert.IsNotEmpty(Lines, "Nothing was logged, so the check proves nothing.");
        foreach (var line in Lines)
        {
            Assert.DoesNotContain(secret, line.Message);
        }
    }
}
