using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A connector whose QUIC connects all return one scripted result and whose TCP connects go to
/// <paramref name="tcp" />, recording each QUIC target, so <c>--http3</c> and
/// <c>--http3-only</c> run end to end with no socket.
/// </summary>
/// <param name="quic">What every <see cref="ConnectMultiplexedAsync" /> returns.</param>
/// <param name="tcp">Answers every <see cref="ConnectAsync" />.</param>
internal sealed class ScriptedQuicConnector(MultiplexedConnectResult quic, IConnector tcp) : IConnector
{
    /// <summary>Gets each target a QUIC connection was asked for, in order.</summary>
    public List<ConnectTarget> QuicTargets { get; } = [];

    /// <summary>Gets how many TCP connections were asked for.</summary>
    public int TcpConnectCount { get; private set; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        TcpConnectCount++;
        return tcp.ConnectAsync(target, cancellationToken);
    }

    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        QuicTargets.Add(target);
        return ValueTask.FromResult(quic);
    }
}
