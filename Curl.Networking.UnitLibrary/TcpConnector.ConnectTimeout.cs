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

    // The most the caller's own limit can run out ahead of this connect's and still be taken for it (CallerLimitLeadFor).
    private static readonly TimeSpan LongestCallerLimitLead = TimeSpan.FromMilliseconds(500);

    private TimeSpan LimitForDestination(ConnectTarget target, ConnectDestination destination, TimeSpan connectLimit) =>
        UnixSocket is null && TunnelProxyOf(target) is null && IsLoopbackDestination(destination.Host) && connectLimit < MinimumLoopbackConnectTimeout
            ? MinimumLoopbackConnectTimeout
            : connectLimit;

    private static bool IsLoopbackDestination(string host) =>
        IsLocalhost(host) || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    /// <summary>
    /// Runs the resolve, dials, any tunnel and any TLS handshake under <paramref name="connectLimit" />
    /// on the injected <see cref="TimeProvider" />, counted from <paramref name="started" /> (ADR-0117).
    /// A cancellation that ends the connect at its limit (<see cref="IsEndedByConnectLimit" />) is
    /// curl 8.21.0's exit 28 <c>Connection timed out after &lt;n&gt; milliseconds</c>, also
    /// reported on the target's events as its <c>-v</c> repeats it (measured, BL-510); any
    /// other cancellation escapes.
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
        catch (CanceledWhileResolvingException) when (IsEndedByConnectLimit(timeout, started, connectLimit))
        {
            // Only a resolve cancelled at the limit is curl's "Resolving timed out" (CGR-284); an earlier cancellation escapes.
            var message = $"Resolving timed out after {TimedOutMilliseconds(started, connectLimit)} milliseconds";
            target.Events.ReportInfo(message);
            return ConnectResult.Failed(CurlExitCode.OperationTimedOut, message);
        }
        catch (OperationCanceledException canceled) when (IsEndedByConnectLimit(timeout, started, connectLimit))
        {
            var message = $"Connection timed out after {TimedOutMilliseconds(started, connectLimit)} milliseconds";
            target.Events.ReportInfo(message);
            return ConnectResult.Failed(CurlExitCode.OperationTimedOut, message, (canceled as CanceledAfterResolveException)?.Timings)
                .WithTcpConnectedBeforeFailure(canceled.Data.Contains(ConnectResult.TcpConnectedOnCancellationKey))
                .WithEndPointsBeforeFailure(ConnectResult.CarriedBy(canceled)?.LocalEndPointBeforeFailure, ConnectResult.CarriedBy(canceled)?.RemoteEndPointBeforeFailure);
        }
    }

    // True when this connect's own timer fired, which on Linux can be a few milliseconds before the clock reads the
    // limit, or when the clock has reached the limit as IsAtConnectLimit judges it (ADR-1457).
    private bool IsEndedByConnectLimit(CancellationTokenSource ownTimer, long started, TimeSpan connectLimit) =>
        ownTimer.IsCancellationRequested || IsAtConnectLimit(timeProvider.GetElapsedTime(started), connectLimit);

    // True when a cancellation this long into a connect is its limit passing: the clock is within CallerLimitLeadFor
    // of the limit or past it. -m is the same limit, but its clock starts before the connect's, so its cancellation
    // arrives with that much of the limit still to run (ADR-1457).
    internal static bool IsAtConnectLimit(TimeSpan elapsed, TimeSpan connectLimit) =>
        elapsed >= connectLimit - CallerLimitLeadFor(connectLimit);

    // A quarter of the limit, at most LongestCallerLimitLead.
    private static TimeSpan CallerLimitLeadFor(TimeSpan connectLimit) =>
        connectLimit / 4 < LongestCallerLimitLead ? connectLimit / 4 : LongestCallerLimitLead;

    // The N of a timed-out message: the time since the connect started, but never less than the limit that passed,
    // since curl's N counts from the transfer's start, where -m's clock started, and so is never below the limit.
    private long TimedOutMilliseconds(long started, TimeSpan connectLimit) =>
        TimedOutMilliseconds(timeProvider.GetElapsedTime(started), connectLimit);

    internal static long TimedOutMilliseconds(TimeSpan elapsed, TimeSpan connectLimit) =>
        (long)(elapsed > connectLimit ? elapsed : connectLimit).TotalMilliseconds;

    private static TimeSpan ConnectTimeoutOrDefault(TimeSpan? connectTimeout) =>
        connectTimeout is { } given && given > TimeSpan.Zero ? given : DefaultConnectTimeout;
}
