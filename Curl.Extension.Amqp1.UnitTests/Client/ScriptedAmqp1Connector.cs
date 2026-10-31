using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp1.Client;

/// <summary>A fake <see cref="IConnector"/> that records the target it is asked for and returns a set result.</summary>
internal sealed class ScriptedAmqp1Connector(ConnectResult result) : IConnector
{
    public ConnectTarget? Target { get; private set; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Target = target;
        return ValueTask.FromResult(result);
    }
}
