using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A connector that records every target it is asked for and fails each connect with the
/// exit code and message it was built with, so no socket is ever opened.
/// </summary>
internal sealed class RecordingConnector(CurlExitCode exitCode, string message) : IConnector
{
    public List<ConnectTarget> Targets { get; } = [];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);

        return ValueTask.FromResult(ConnectResult.Failed(exitCode, message));
    }
}
