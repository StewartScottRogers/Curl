using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A datagram connector that records every host and port it is asked for and fails each
/// open with the exit code and message it was built with, so no socket is ever opened.
/// </summary>
internal sealed class RecordingDatagramConnector(CurlExitCode exitCode, string message) : IDatagramConnector
{
    public List<(string Host, int Port)> Opens { get; } = [];

    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        Opens.Add((host, port));

        return ValueTask.FromResult(DatagramOpenResult.Failed(exitCode, message));
    }
}
