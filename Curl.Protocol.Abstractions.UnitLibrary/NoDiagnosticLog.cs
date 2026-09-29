namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="IDiagnosticLog" /> used at <c>--log-level none</c>, the default: no
/// level is enabled and nothing is written (ADR-0222).
/// </summary>
public sealed class NoDiagnosticLog : IDiagnosticLog
{
    private NoDiagnosticLog()
    {
    }

    /// <summary>
    /// Gets the one instance.
    /// </summary>
    public static NoDiagnosticLog Instance { get; } = new();

    /// <inheritdoc />
    /// <returns>Always <see langword="false" />.</returns>
    public bool IsEnabled(DiagnosticLogLevel level) => false;

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel level, string component, string message)
    {
    }
}
