using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.McpClient.Client;

/// <summary>A fake <see cref="IConnector"/> that hands out its connections in order, one per connect.</summary>
internal sealed class SequencedMcpConnector(params IConnection[] connections) : IConnector
{
    private int next;

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(connections[next++]));
}
