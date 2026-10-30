using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>A fake <see cref="IConnector"/> that gives its results in order and records every target asked for.</summary>
internal sealed class ScriptedConnector(params ConnectResult[] results) : IConnector
{
    private readonly Queue<ConnectResult> pending = new(results);

    public List<ConnectTarget> Targets { get; } = [];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(pending.TryDequeue(out var result) ? result : throw new AssertFailedException("No connection was scripted."));
    }
}
