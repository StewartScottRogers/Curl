using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="IConnector" /> that keeps connections open after a transfer and hands one
/// to the next transfer with the same <see cref="ConnectionPoolKey" />, as curl's connection
/// cache does for one command line (ADR-0050).
/// </summary>
/// <remarks>
/// <para>
/// Every connection it returns is a <see cref="PooledConnection" />. A handler that calls
/// <see cref="IConnection.MarkReusable" /> before disposing it returns it to the pool; any
/// other dispose closes it. A connection in use is not in the pool, so no idle connection is
/// ever handed to two transfers, and concurrent calls are safe.
/// </para>
/// <para>
/// The pool holds at most <see cref="MaximumIdleConnections" /> idle connections across all
/// keys, closing the oldest when one more is returned, and drops a connection idle for longer
/// than <see cref="MaximumIdleTime" />, measured on the injected <see cref="TimeProvider" />.
/// Connections are numbered from <c>0</c> in the order the inner connector opens them, or
/// fails to (ADR-0109).
/// </para>
/// </remarks>
/// <param name="innerConnector">Opens a connection when the pool has none for the key.</param>
/// <param name="timeProvider">Measures how long each connection has been idle.</param>
public sealed class PoolingConnector(IConnector innerConnector, TimeProvider timeProvider) : IConnector, IAsyncDisposable
{
    /// <summary>
    /// The most idle connections the pool holds, across all keys: curl 8.21.0's
    /// connection cache size, measured as <c>6/5</c> when a sixth was returned.
    /// </summary>
    public const int MaximumIdleConnections = 5;

    private readonly Lock _gate = new();
    private readonly List<PoolEntry> _idle = [];
    private long _nextConnectionNumber;
    private bool _isDisposed;

    /// <summary>
    /// Gets the longest a connection may sit idle and still be reused: 118 seconds, curl's
    /// <c>CURLOPT_MAXAGE_CONN</c> default.
    /// </summary>
    public static TimeSpan MaximumIdleTime { get; } = TimeSpan.FromSeconds(118);

    /// <inheritdoc />
    /// <remarks>
    /// When the pool holds an idle connection with the target's key it reports
    /// <see cref="ITransferEvents.ReportConnectionReused" /> on the target's
    /// <see cref="ConnectTarget.Events" /> and returns it at once with
    /// <see cref="ConnectResult.IsReused" /> set, its original
    /// <see cref="ConnectResult.ConnectionNumber" />, <see cref="ConnectResult.LocalEndPoint" />
    /// and <see cref="ConnectResult.PeerCertificates" />, and no
    /// <see cref="ConnectResult.Timings" />. Otherwise it asks the inner connector and gives
    /// its result the next number, a failure's included, as curl 8.21.0 numbers a failed
    /// connection too (ADR-0109). A target without
    /// <see cref="ConnectTarget.PoolScheme" /> is never served from the pool. A reuse is
    /// reported <c>with proxy</c> for a forward-proxy target and for a tunnelled one, which
    /// names the proxy's host, as curl 8.21.0 prints it (BL-360). An idle connection on which a
    /// read found the server's close is not handed out: it is reported with curl 8.21.0's
    /// <c>Connection N seems to be dead</c> and <c>shutting down connection #N</c>, closed,
    /// and the next idle one with the key is tried (ADR-0112).
    /// </remarks>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var key = ConnectionPoolKey.Of(target);
        var idle = await TakeIdleAsync(key, target.Events);

        return idle is null
            ? await OpenAsync(target, key, cancellationToken)
            : Reuse(target, idle);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Asks the inner connector for a new QUIC connection every time; keeping one per origin
    /// for later transfers and <c>-Z</c> streams is BL-735's.
    /// </remarks>
    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        innerConnector.ConnectMultiplexedAsync(target, cancellationToken);

    /// <summary>
    /// Closes every idle connection without reporting anything; a connection returned
    /// afterwards is closed instead of pooled. Wherever the pool closes a connection - here,
    /// evicted, expired or found dead - the protocol session it holds is shut down first
    /// (<see cref="IConnectionSession.ShutDownAsync" />), as curl sends HTTP/2's GOAWAY
    /// (BL-817).
    /// </summary>
    /// <returns>A task that completes when every idle connection is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        List<PoolEntry> closing;

        lock (_gate)
        {
            _isDisposed = true;
            closing = [.. _idle];
            _idle.Clear();
        }

        await CloseAllAsync(closing);
    }

    /// <summary>
    /// Puts <paramref name="entry" /> back in the pool, closing the oldest idle connection
    /// when that makes one more than <see cref="MaximumIdleConnections" /> and reporting it on
    /// <paramref name="events" /> as curl does.
    /// </summary>
    /// <param name="entry">The connection a <see cref="PooledConnection" /> is handing back.</param>
    /// <param name="events">The events of the transfer handing it back.</param>
    /// <returns>A task that completes when any connection being dropped is closed.</returns>
    internal async ValueTask ReturnAsync(PoolEntry entry, ITransferEvents events)
    {
        List<PoolEntry> closing;
        PoolEntry? evicted = null;
        var heldCount = 0;

        lock (_gate)
        {
            if (_isDisposed)
            {
                closing = [entry];
            }
            else
            {
                closing = RemoveExpired();
                entry.IdleSince = timeProvider.GetTimestamp();
                _idle.Add(entry);
                heldCount = _idle.Count;
                evicted = heldCount > MaximumIdleConnections ? RemoveOldest() : null;
            }
        }

        if (evicted is not null)
        {
            events.ReportInfo($"Connection pool is full, closing the oldest of {heldCount}/{MaximumIdleConnections}");
            events.ReportInfo($"shutting down connection #{evicted.ConnectionNumber}");
            closing.Add(evicted);
        }

        await CloseAllAsync(closing);
    }

    private static async ValueTask CloseAllAsync(List<PoolEntry> entries)
    {
        foreach (var entry in entries)
        {
            await entry.CloseAsync();
        }
    }

    private async ValueTask<PoolEntry?> TakeIdleAsync(ConnectionPoolKey? key, ITransferEvents events)
    {
        if (key is null)
        {
            return null;
        }

        var match = await TakeMatchAsync(key);
        while (match is { HasReadPeerClose: true })
        {
            events.ReportInfo($"Connection {match.ConnectionNumber} seems to be dead");
            events.ReportInfo($"shutting down connection #{match.ConnectionNumber}");
            await match.CloseAsync();
            match = await TakeMatchAsync(key);
        }

        return match;
    }

    private async ValueTask<PoolEntry?> TakeMatchAsync(ConnectionPoolKey key)
    {
        List<PoolEntry> expired;
        PoolEntry? match;

        lock (_gate)
        {
            expired = RemoveExpired();
            match = _idle.Find(entry => key.Equals(entry.Key));

            if (match is not null)
            {
                _idle.Remove(match);
            }
        }

        await CloseAllAsync(expired);

        return match;
    }

    private async ValueTask<ConnectResult> OpenAsync(
        ConnectTarget target,
        ConnectionPoolKey? key,
        CancellationToken cancellationToken)
    {
        var connect = await innerConnector.ConnectAsync(target, cancellationToken);

        var connectionNumber = Interlocked.Increment(ref _nextConnectionNumber) - 1;
        if (connect.Connection is null)
        {
            return NumberedConnectFailure.Of(connect, connectionNumber);
        }

        var entry = new PoolEntry(
            key,
            connect.Connection,
            connectionNumber,
            connect.LocalEndPoint,
            connect.PeerCertificates);

        return ConnectResult.Connected(
            new PooledConnection(this, entry, target.Events),
            connect.Timings,
            connect.LocalEndPoint,
            connect.ProxyConnectResponseCode,
            connect.PeerCertificates,
            isReused: false,
            entry.ConnectionNumber);
    }

    private ConnectResult Reuse(ConnectTarget target, PoolEntry entry)
    {
        target.Events.ReportConnectionReused(new ConnectionReusedEvent
        {
            Scheme = target.PoolScheme!,
            IsProxy = target.IsForwardProxy || target.Proxy is not null,
            HostName = target.Proxy?.Host ?? target.Host,
            Port = target.Proxy?.Port ?? target.Port,
            ConnectionNumber = entry.ConnectionNumber,
        });

        return ConnectResult.Connected(
            new PooledConnection(this, entry, target.Events),
            timings: null,
            entry.LocalEndPoint,
            proxyConnectResponseCode: 0,
            entry.PeerCertificates,
            isReused: true,
            entry.ConnectionNumber);
    }

    private List<PoolEntry> RemoveExpired()
    {
        var expired = _idle.FindAll(entry => timeProvider.GetElapsedTime(entry.IdleSince) > MaximumIdleTime);
        _idle.RemoveAll(expired.Contains);

        return expired;
    }

    private PoolEntry RemoveOldest()
    {
        var oldest = _idle[0];
        _idle.RemoveAt(0);

        return oldest;
    }
}
