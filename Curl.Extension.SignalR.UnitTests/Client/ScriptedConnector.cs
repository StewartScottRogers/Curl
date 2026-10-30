using Curl.Protocol.Abstractions;

namespace Curl.Extension.SignalR.Client;

/// <summary>A connector that gives its scripted results in order, one per connection asked for, and records each target.</summary>
internal sealed class ScriptedConnector(params ConnectResult[] results) : IConnector
{
    private readonly Queue<ConnectResult> pending = new(results);

    public List<ConnectTarget> Targets { get; } = [];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(pending.Dequeue());
    }
}
