using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    // Runs a connect and, each time one of its handshakes asks to be run again by the hand-built
    // client (ADR-1453), runs it once more from the dial in what is left of the connect's limit. A
    // provider that asked for a host never asks for it again, so this ends.
    private async ValueTask<ConnectResult> ConnectRedialingByHandAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        TimeSpan connectLimit,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ConnectWithinTimeoutAsync(target, destination, started, connectLimit, cancellationToken).ConfigureAwait(false);
        }
        catch (HandBuiltRedialRequiredException)
        {
            var left = Math.Max(0, (connectLimit - timeProvider.GetElapsedTime(started)).Ticks);
            using var timeout = new CancellationTokenSource(TimeSpan.FromTicks(left), timeProvider);
            using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            return await ConnectRedialingByHandAsync(target, destination, started, connectLimit, limited.Token).ConfigureAwait(false);
        }
    }

    // The same for a QUIC connect through an HTTPS proxy, whose handshake is the one over TCP; the
    // second attempt runs under a connect limit of its own.
    private async ValueTask<MultiplexedConnectResult> ConnectMultiplexedThroughProxyRedialingByHandAsync(
        ConnectTarget target,
        ProxyEndpoint proxy,
        QuicDialer dialer,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ConnectMultiplexedThroughProxyAsync(target, proxy, dialer, cancellationToken).ConfigureAwait(false);
        }
        catch (HandBuiltRedialRequiredException)
        {
            return await ConnectMultiplexedThroughProxyAsync(target, proxy, dialer, cancellationToken).ConfigureAwait(false);
        }
    }
}
