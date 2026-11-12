using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A connector that records every target it is asked for and fails each connect with the
/// exit code and message it was built with, so no socket is ever opened.
/// </summary>
internal sealed class RecordingConnector(CurlExitCode exitCode, string message) : IConnector
{
    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>Gets or sets what to do with the connect target's diagnostic log when a connect is asked for.</summary>
    public Action<IDiagnosticLog>? WritesOnConnect { get; set; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        WritesOnConnect?.Invoke(target.DiagnosticLog);

        return ValueTask.FromResult(ConnectResult.Failed(exitCode, message));
    }
}
