using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>A connector that gives one result and records the target it was asked for.</summary>
internal sealed class ScriptedConnector(ConnectResult result) : IConnector
{
    public ConnectTarget? Target { get; private set; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Target = target;
        return ValueTask.FromResult(result);
    }
}
