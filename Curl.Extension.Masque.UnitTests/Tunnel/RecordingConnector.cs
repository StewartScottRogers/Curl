using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>A connector that records the target it was asked for and answers with a fixed result.</summary>
/// <param name="result">What every connect returns.</param>
internal sealed class RecordingConnector(ConnectResult result) : IConnector
{
    public ConnectTarget? Target { get; private set; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Target = target;
        return ValueTask.FromResult(result);
    }
}
