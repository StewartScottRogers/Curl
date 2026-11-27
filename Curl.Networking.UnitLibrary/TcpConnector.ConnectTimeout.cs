using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Gets the shortest connect limit a loopback destination is held to, 100 ms. curl's non-blocking
    /// connect to loopback completes before its timer is first checked, so <c>--connect-timeout 0.01</c>
    /// connects; here the runtime's own overhead can pass 10 ms first (CGR-187, BL-3716).
    /// </summary>
    public static TimeSpan MinimumLoopbackConnectTimeout { get; } = TimeSpan.FromMilliseconds(100);

    private TimeSpan LimitForDestination(ConnectTarget target, ConnectDestination destination, TimeSpan connectLimit) =>
        UnixSocket is null && TunnelProxyOf(target) is null && IsLoopbackDestination(destination.Host) && connectLimit < MinimumLoopbackConnectTimeout
            ? MinimumLoopbackConnectTimeout
            : connectLimit;

    private static bool IsLoopbackDestination(string host) =>
        IsLocalhost(host) || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    /// <summary>
    /// Runs the resolve, dials, any tunnel and any TLS handshake under <paramref name="connectLimit" />
    /// on the injected <see cref="TimeProvider" />, counted from <paramref name="started" /> (ADR-0117).
    /// A cancellation that arrives once the limit has passed on the clock, whoever cancelled, is
    /// curl 8.21.0's exit 28 <c>Connection timed out after &lt;n&gt; milliseconds</c>, also
    /// reported on the target's events as its <c>-v</c> repeats it (measured, BL-510); any
    /// earlier cancellation escapes.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectWithinTimeoutAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        TimeSpan connectLimit,
        CancellationToken cancellationToken)
    {
        connectLimit = LimitForDestination(target, destination, connectLimit);
        using var timeout = new CancellationTokenSource(connectLimit, timeProvider);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            if (UnixSocket is { } unixSocketAddress)
            {
                return await ConnectOverUnixSocketAsync(target, unixSocketAddress, started, limited.Token).ConfigureAwait(false);
            }

            return TunnelProxyOf(target) is { } proxy
                ? await ConnectThroughProxyAsync(target, destination, proxy, started, limited.Token).ConfigureAwait(false)
                : await ConnectDirectlyAsync(target, destination, started, limited.Token).ConfigureAwait(false);
        }
        catch (CanceledWhileResolvingException) when (timeProvider.GetElapsedTime(started) >= connectLimit)
        {
            // Only a resolve cancelled once the limit has passed is curl's "Resolving timed out" (CGR-284); a cancellation before the limit escapes.
            var message = $"Resolving timed out after {(long)timeProvider.GetElapsedTime(started).TotalMilliseconds} milliseconds";
            target.Events.ReportInfo(message);
            return ConnectResult.Failed(CurlExitCode.OperationTimedOut, message);
        }
        catch (OperationCanceledException canceled) when (timeProvider.GetElapsedTime(started) >= connectLimit)
        {
            var message = $"Connection timed out after {(long)timeProvider.GetElapsedTime(started).TotalMilliseconds} milliseconds";
            target.Events.ReportInfo(message);
            return ConnectResult.Failed(CurlExitCode.OperationTimedOut, message, (canceled as CanceledAfterResolveException)?.Timings);
        }
    }

    private static TimeSpan ConnectTimeoutOrDefault(TimeSpan? connectTimeout) =>
        connectTimeout is { } given && given > TimeSpan.Zero ? given : DefaultConnectTimeout;
}
