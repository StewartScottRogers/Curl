using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    // Runs a connect and, each time one of its handshakes asks to be run again by the hand-built
    // client (ADR-1459), runs it once more from the dial in what is left of the connect's limit. A
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
        catch (HandBuiltRedialRequiredException redial)
        {
            return await RedialByHandAsync(target, destination, started, connectLimit, redial.IsProxy, cancellationToken).ConfigureAwait(false);
        }
    }

    // The connect run again, its events showing only what the attempts before it did not report.
    // Each redial wraps the target's own events, so a second one (the origin's, after the proxy's)
    // holds back up to its own handshake.
    private async ValueTask<ConnectResult> RedialByHandAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        TimeSpan connectLimit,
        bool redialedIsProxy,
        CancellationToken cancellationToken)
    {
        var left = Math.Max(0, (connectLimit - timeProvider.GetElapsedTime(started)).Ticks);
        using var timeout = new CancellationTokenSource(TimeSpan.FromTicks(left), timeProvider);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var events = RedialedEventsOf(target, redialedIsProxy);
        try
        {
            return await ConnectWithinTimeoutAsync(target with { Events = events }, destination, started, connectLimit, limited.Token).ConfigureAwait(false);
        }
        catch (HandBuiltRedialRequiredException redial)
        {
            return await RedialByHandAsync(target, destination, started, connectLimit, redial.IsProxy, limited.Token).ConfigureAwait(false);
        }
        finally
        {
            events.ReportHeldLines();
        }
    }

    // An origin reached through an HTTPS proxy handshakes after the proxy, so the proxy's trust is
    // held back too before the origin's.
    private RedialedConnectTransferEvents RedialedEventsOf(ConnectTarget target, bool redialedIsProxy)
    {
        var followsTheProxysHandshake = !redialedIsProxy && UnixSocket is null && target.Proxy?.Kind == ProxyKind.Https;
        return new RedialedConnectTransferEvents(target.Events, followsTheProxysHandshake ? 1 : 0);
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
        catch (HandBuiltRedialRequiredException redial)
        {
            var events = RedialedEventsOf(target, redial.IsProxy);
            try
            {
                return await ConnectMultiplexedThroughProxyAsync(target with { Events = events }, proxy, dialer, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                events.ReportHeldLines();
            }
        }
    }
}
