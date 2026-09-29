using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Writes each enabled diagnostic line through another <see cref="IDiagnosticLog" /> while holding
/// the run's <see cref="WriteGate" />, so a log line on standard error never lands inside a line a
/// <c>-Z</c> transfer is writing there, and the gate is always taken before the log's own lock.
/// </summary>
/// <param name="inner">The log that formats and writes the line.</param>
/// <param name="gate">The run's write gate.</param>
internal sealed class GatedDiagnosticLog(IDiagnosticLog inner, WriteGate gate) : IDiagnosticLog
{
    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLogLevel level) => inner.IsEnabled(level);

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel level, string component, string message)
    {
        if (inner.IsEnabled(level))
        {
            gate.RunExclusive(() => inner.Write(level, component, message));
        }
    }
}
