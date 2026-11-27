using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    private async ValueTask<ConnectResult> ConnectDirectlyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        CancellationToken cancellationToken)
    {
        (target, var trace, var httpsConnect, var attempt) = TracingConnectionFilters(target, destination);
        var result = await ConnectDirectlyTracedAsync(target, destination, started, trace, cancellationToken).ConfigureAwait(false);
        ReportHttpsConnectFailure(httpsConnect, result, attempt);
        return result;
    }

    private static void ReportHttpsConnectFailure(HttpsConnectFilterTraceEvents? httpsConnect, ConnectResult result, FirstHttpsConnectAttempt? attempt = null)
    {
        if (result.ExitCode != CurlExitCode.Ok)
        {
            ReportHttpsConnectAttemptFailure(httpsConnect, attempt, result.ExitCode);
        }
    }

    private ValueTask<ConnectResult> ConnectDirectlyTracedAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        ConnectAttemptTraceEvents? trace,
        CancellationToken cancellationToken)
    {
        if (OnionAddress.IsRefused(destination.Host))
        {
            ReportOnionRefused(target.Events, destination);
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntResolveHost, OnionAddress.RefusalMessage));
        }

        return ResolveAndDialDirectlyAsync(target, destination, started, trace, cancellationToken);
    }

    private async ValueTask<ConnectResult> ResolveAndDialDirectlyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        ConnectAttemptTraceEvents? trace,
        CancellationToken cancellationToken)
    {
        var ((addresses, failure), fromCache) = await ResolveNotingCacheAsync(destination.Host, destination.Port, target, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return NothingToDial(target, destination, failure, fromCache);
        }

        var nameResolved = timeProvider.GetTimestamp();
        var (dialed, lastDialError, lastBindFailure) = await DialFirstReachableAsync(addresses, destination.Port, destination.Host, target, cancellationToken, trace).ConfigureAwait(false);
        if (dialed is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            var via = destination.IsMapped ? $" via {destination.Host}:{destination.Port}" : string.Empty;
            var overProxy = target.IsForwardProxy ? $" over proxy {target.Host}" : string.Empty;
            return DialFailure(
                target.Events,
                lastDialError,
                lastBindFailure,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {HostWithoutZoneId(target.Host)}:{target.Port}{via}{overProxy} after {elapsedMilliseconds} ms: {DialFailureText(lastBindFailure)}");
        }

        var timings = new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null);
        var named = destination.IsMapped ? dialed with { MappedDestination = destination } : dialed;
        var secured = await SecureWhenAskedAsync(named, target, timings, 0, cancellationToken, WritesSslFilterAddedFor(target)).ConfigureAwait(false);
        return WithTimingsWhenFailedWithout(secured, timings);
    }
}
