namespace Curl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IDiagnosticLog" /> for tests: every level is enabled and every line is
/// recorded.
/// </summary>
internal sealed class StubDiagnosticLog : IDiagnosticLog
{
    public List<string> Lines { get; } = [];

    public bool IsEnabled(DiagnosticLogLevel level) => true;

    public void Write(DiagnosticLogLevel level, string component, string message) =>
        Lines.Add($"{level} {component} {message}");
}
