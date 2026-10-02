using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The run's diagnostic log for services composed before the command line is read, such as the
/// authenticators (BL-923): it writes nothing, as <see cref="NoDiagnosticLog.Instance" />, until
/// the runner opens the run's log and <see cref="Bind" />s it, then forwards every line to it.
/// </summary>
internal sealed class LateBoundDiagnosticLog : IDiagnosticLog
{
    private volatile IDiagnosticLog bound = NoDiagnosticLog.Instance;

    /// <summary>Forwards every later line to <paramref name="log" />.</summary>
    /// <param name="log">The run's log.</param>
    public void Bind(IDiagnosticLog log) => bound = log;

    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLogLevel level) => bound.IsEnabled(level);

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel level, string component, string message) => bound.Write(level, component, message);
}
