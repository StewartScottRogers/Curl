using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketRepl.Transfer;

/// <summary>Gives one scripted connect result and records the target it was asked for.</summary>
/// <param name="result">The result every connect gives.</param>
internal sealed class RecordingConnector(ConnectResult result) : IConnector
{
    /// <summary>Gets the targets connected to, in order.</summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(result);
    }
}
