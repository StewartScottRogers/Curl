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
/// <para>
/// The idle and leased connections, their numbering and the clock live in a
/// <see cref="ConnectionCache" />. Each <c>-:</c>/<c>--next</c> option group has a connector of
/// its own over the run's one cache, with its own inner connector and its
/// <c>configuration</c>, so a later group reuses an earlier group's connection only when their
/// configurations are equal, as curl 8.21.0 shares its connection cache between groups and
/// reuses a connection only for matching TLS and proxy settings (ADR-0285, BL-754).
/// </para>
/// </remarks>
public sealed class PoolingConnector : IConnector, IAsyncDisposable
{
    /// <summary>
    /// The most idle connections the pool holds, across all keys: curl 8.21.0's
    /// connection cache size, measured as <c>6/5</c> when a sixth was returned.
    /// </summary>
    public const int MaximumIdleConnections = 5;

    private readonly IConnector _innerConnector;
    private readonly ConnectionCache _cache;
    private readonly object? _configuration;
    private readonly bool _ownsCache;

    /// <summary>
    /// Creates a pool of its own, which <see cref="DisposeAsync" /> closes.
    /// </summary>
    /// <param name="innerConnector">Opens a connection when the pool has none for the key.</param>
    /// <param name="timeProvider">Measures how long each connection has been idle.</param>
    public PoolingConnector(IConnector innerConnector, TimeProvider timeProvider)
        : this(innerConnector, new ConnectionCache(timeProvider), configuration: null)
    {
        _ownsCache = true;
    }

    /// <summary>
    /// Creates a connector over <paramref name="cache" />, which other connectors may share and
    /// whose owner closes it; <see cref="DisposeAsync" /> leaves it open.
    /// </summary>
    /// <param name="innerConnector">Opens a connection when the cache has none for the key.</param>
    /// <param name="cache">The connections this connector shares with the others over it.</param>
    /// <param name="configuration">
    /// What else a connection must have been opened with to be handed to this connector's
    /// transfers, compared with <see cref="object.Equals(object?)" />; <see langword="null" /> for nothing.
    /// </param>
    public PoolingConnector(IConnector innerConnector, ConnectionCache cache, object? configuration)
    {
        ArgumentNullException.ThrowIfNull(innerConnector);
        ArgumentNullException.ThrowIfNull(cache);
        _innerConnector = innerConnector;
        _cache = cache;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets a value indicating whether a transfer waits for a connection with its key that is
    /// being opened, or may yet multiplex, instead of opening one of its own: curl's
    /// <c>CURLOPT_PIPEWAIT</c>, which <c>-Z</c> sets unless <c>--parallel-immediate</c> is given
    /// (measured, BL-717 Notes). A connection may multiplex until its session is known when it
    /// agreed <c>h2</c> with ALPN, or when it is plain TCP straight to the origin, which may
    /// speak HTTP/2 with prior knowledge.
    /// </summary>
    public bool WaitsForMultiplexing { get; init; }

    /// <summary>
    /// Gives a connector over the same <see cref="ConnectionCache" />, configuration and
    /// <see cref="WaitsForMultiplexing" /> that opens new connections through
    /// <paramref name="innerConnector" /> instead, so what it opens is numbered in this pool's
    /// sequence; its <see cref="DisposeAsync" /> leaves the cache to this connector's owner. FTP's
    /// passive data connections go through one over <see cref="TcpConnector.WithoutConnectTimeout" /> (BL-797).
    /// </summary>
    /// <param name="innerConnector">Opens a connection when the cache has none for the key.</param>
    /// <returns>The connector.</returns>
    public PoolingConnector Over(IConnector innerConnector) =>
        new(innerConnector, _cache, _configuration) { WaitsForMultiplexing = WaitsForMultiplexing };

    /// <summary>
    /// Gives a datagram connector that opens channels through <paramref name="datagramConnector" />
    /// and numbers each open in this pool's sequence, as curl 8.21.0 numbers a TFTP transfer's
    /// connection with the TCP connections before it (BL-969).
    /// </summary>
    /// <param name="datagramConnector">Opens the channels.</param>
    /// <returns>The numbering connector.</returns>
    public IDatagramConnector NumberingDatagrams(IDatagramConnector datagramConnector)
    {
        ArgumentNullException.ThrowIfNull(datagramConnector);
        return new ConnectionNumberingDatagramConnector(datagramConnector, _cache);
    }

    /// <summary>
    /// Gets the count this pool numbers its connections in, for a handler that numbers a
    /// transfer as a connection without connecting: curl 8.21.0 numbers a <c>file://</c>
    /// transfer with the connections before it (BL-977).
    /// </summary>
    public IConnectionNumbers ConnectionNumbers => _cache;

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
    /// <para>
    /// Before any of that, a connection in use with the key whose session carries several
    /// transfers at once (<see cref="IConnectionSession.ConcurrentTransferLimit" />) and has a
    /// stream to spare is shared: curl 8.21.0's <c>Multiplexed connection found</c> is reported
    /// and the reuse as above. One whose streams are all taken is reported as curl's
    /// <c>MAX_CONCURRENT_STREAMS reached, skip (N)</c>, N the transfers on it, and passed over.
    /// With <see cref="WaitsForMultiplexing" />, a connection with the key that is still being
    /// opened, or that may multiplex and has no session yet, is waited for first (BL-717).
    /// </para>
    /// </remarks>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        return await ConnectThroughPoolAsync(
            target,
            ConnectionPoolKey.Of(target, _configuration),
            token => _innerConnector.ConnectAsync(target, token),
            isQuic: false,
            cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Asks the inner connector for a new QUIC connection every time, unpooled; HTTP/3 transfers
    /// are pooled through <see cref="ConnectMultiplexedSessionAsync" />.
    /// </remarks>
    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        _innerConnector.ConnectMultiplexedAsync(target, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Pools the session as <see cref="ConnectAsync" /> pools a connection, under a key of its own
    /// that no TCP connection shares (BL-735): a session in use with a stream to spare
    /// (<see cref="IConnectionSession.ConcurrentTransferLimit" />) is shared with curl's
    /// <c>Multiplexed connection found</c>, an idle one is reused, and otherwise the inner
    /// connector opens a new one, numbered next. The session is known as soon as it is open,
    /// so transfers waiting for multiplexing (<see cref="WaitsForMultiplexing" />) wait only
    /// for the QUIC handshake. A failed QUIC connect is not numbered: <c>--http3</c> races it
    /// against a TCP connect, which curl numbers as the same connection (ADR-0338).
    /// </remarks>
    public async ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(
        ConnectTarget target,
        Func<IMultiplexedConnection, IConnection> openSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(openSession);
        cancellationToken.ThrowIfCancellationRequested();

        return await ConnectThroughPoolAsync(
            target,
            ConnectionPoolKey.Of(target, _configuration) is { } key ? key with { IsQuic = true } : null,
            token => _innerConnector.ConnectMultiplexedSessionAsync(target, openSession, token),
            isQuic: true,
            cancellationToken);
    }

    /// <summary>
    /// Closes the pool when this connector made it (<see cref="ConnectionCache.DisposeAsync" />);
    /// does nothing to a <see cref="ConnectionCache" /> it was given, which its owner closes.
    /// Wherever the pool closes a connection - there, evicted, expired or found dead - the
    /// protocol session it holds is shut down first (<see cref="IConnectionSession.ShutDownAsync" />),
    /// as curl sends HTTP/2's GOAWAY (BL-817).
    /// </summary>
    /// <returns>A task that completes when every idle connection is closed.</returns>
    public ValueTask DisposeAsync() => _ownsCache ? _cache.DisposeAsync() : ValueTask.CompletedTask;

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

        lock (_cache.Gate)
        {
            if (_cache.IsDisposed)
            {
                closing = [entry];
            }
            else
            {
                closing = RemoveExpired();
                entry.IdleSince = _cache.TimeProvider.GetTimestamp();
                _cache.Idle.Add(entry);
                heldCount = _cache.Idle.Count;
                evicted = heldCount > MaximumIdleConnections ? RemoveOldest() : null;
            }
        }

        if (evicted is not null)
        {
            events.ReportInfo($"Connection pool is full, closing the oldest of {heldCount}/{MaximumIdleConnections}");
            events.ReportInfo($"shutting down connection #{evicted.ConnectionNumber}");
            closing.Add(evicted);
        }

        await ConnectionCache.CloseAllAsync(closing);
    }

    /// <summary>
    /// Ends one lease of <paramref name="entry" />, one that did not mark it reusable stopping
    /// any new transfer from sharing it. While other transfers still hold leases the connection
    /// stays with them; after the last it is no longer in use, and the caller hands it back
    /// with <see cref="HandBackAsync" />.
    /// </summary>
    /// <param name="entry">The connection a <see cref="PooledConnection" /> is handing back.</param>
    /// <param name="isReusable">Whether the lease marked it reusable.</param>
    /// <returns><see langword="true" /> while another transfer still holds a lease of it.</returns>
    internal bool EndLease(PoolEntry entry, bool isReusable)
    {
        lock (_cache.Gate)
        {
            entry.IsShareBarred |= !isReusable;
            entry.LeaseCount--;
            if (entry.LeaseCount > 0)
            {
                return true;
            }

            _cache.Leased.Remove(entry);
            return false;
        }
    }

    /// <summary>
    /// Hands back a connection whose last lease has ended (<see cref="EndLease" />): it returns
    /// to the pool when every lease marked it reusable and it has a key, and closes otherwise.
    /// Transfers waiting to learn whether it multiplexes are woken once it is back in the pool.
    /// </summary>
    /// <param name="entry">The connection.</param>
    /// <param name="events">The events of the transfer handing it back.</param>
    /// <returns>A task that completes when the connection is pooled or closed.</returns>
    internal async ValueTask HandBackAsync(PoolEntry entry, ITransferEvents events)
    {
        MultiplexingNegotiation? negotiation;

        lock (_cache.Gate)
        {
            negotiation = entry.Negotiation;
            entry.Negotiation = null;
        }

        var handingBack = entry.IsShareBarred || entry.Key is null
            ? entry.CloseAsync()
            : ReturnAsync(entry, events);
        Decide(negotiation);
        await handingBack;
    }

    /// <summary>
    /// Records that a transfer handed <paramref name="entry" /> its session, which tells
    /// whether it multiplexes, and wakes the transfers waiting to learn it.
    /// </summary>
    /// <param name="entry">The connection now holding a session.</param>
    internal void SessionHeld(PoolEntry entry)
    {
        MultiplexingNegotiation? negotiation;

        lock (_cache.Gate)
        {
            negotiation = entry.Negotiation;
            entry.Negotiation = null;
        }

        Decide(negotiation);
    }

    /// <summary>Tells whether more than one transfer holds a lease of <paramref name="entry" />.</summary>
    /// <param name="entry">A leased connection.</param>
    /// <returns><see langword="true" /> while another transfer shares it.</returns>
    internal bool IsShared(PoolEntry entry)
    {
        lock (_cache.Gate)
        {
            return entry.LeaseCount > 1;
        }
    }

    private static bool MayMultiplex(ConnectTarget target, ConnectResult connect) =>
        connect.ApplicationProtocol == "h2"
            || (!target.UseTls && !target.IsForwardProxy && target.Proxy is null);

    private static int? ConcurrentTransferLimitOf(PoolEntry entry, ConnectionPoolKey key) =>
        key.Equals(entry.Key) && !entry.IsShareBarred && !entry.HasReadPeerClose
            ? entry.Session?.ConcurrentTransferLimit
            : null;

    private async ValueTask<PoolEntry?> ShareAsync(ConnectionPoolKey? key, ITransferEvents events, CancellationToken cancellationToken)
    {
        if (key is null)
        {
            return null;
        }

        while (true)
        {
            var (shared, fullLeaseCount, negotiation) = FindShareable(key);
            if (fullLeaseCount is { } leaseCount)
            {
                events.ReportInfo($"MAX_CONCURRENT_STREAMS reached, skip ({leaseCount})");
            }

            if (shared is not null || negotiation is null)
            {
                return shared;
            }

            await negotiation.Decided.WaitAsync(cancellationToken);
        }
    }

    private (PoolEntry? Shared, int? FullLeaseCount, MultiplexingNegotiation? Negotiation) FindShareable(ConnectionPoolKey key)
    {
        lock (_cache.Gate)
        {
            int? fullLeaseCount = null;
            foreach (var entry in _cache.Leased)
            {
                var limit = ConcurrentTransferLimitOf(entry, key);
                if (limit > entry.LeaseCount)
                {
                    entry.LeaseCount++;
                    return (entry, null, null);
                }

                fullLeaseCount = limit > 0 ? entry.LeaseCount : fullLeaseCount;
            }

            var negotiation = WaitsForMultiplexing ? _cache.Negotiations.Find(pending => key.Equals(pending.Key)) : null;
            return (null, fullLeaseCount, negotiation);
        }
    }

    private PoolEntry Lease(PoolEntry entry)
    {
        lock (_cache.Gate)
        {
            entry.LeaseCount = 1;
            if (entry.Key is not null)
            {
                _cache.Leased.Add(entry);
            }
        }

        return entry;
    }

    private MultiplexingNegotiation? StartNegotiation(ConnectionPoolKey? key)
    {
        if (key is null)
        {
            return null;
        }

        var negotiation = new MultiplexingNegotiation(key);
        lock (_cache.Gate)
        {
            _cache.Negotiations.Add(negotiation);
        }

        return negotiation;
    }

    private void KeepNegotiating(PoolEntry entry, MultiplexingNegotiation? negotiation)
    {
        lock (_cache.Gate)
        {
            entry.Negotiation = negotiation;
        }
    }

    private void Decide(MultiplexingNegotiation? negotiation)
    {
        if (negotiation is null)
        {
            return;
        }

        lock (_cache.Gate)
        {
            _cache.Negotiations.Remove(negotiation);
        }

        negotiation.Decide();
    }

    private async ValueTask<PoolEntry?> TakeIdleAsync(ConnectionPoolKey? key, ITransferEvents events)
    {
        if (key is null)
        {
            return null;
        }

        var match = await TakeMatchAsync(key);
        while (match is not null && (match.HasReadPeerClose || match.Connection.HasPeerClosed))
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

        lock (_cache.Gate)
        {
            expired = RemoveExpired();
            match = _cache.Idle.Find(entry => key.Equals(entry.Key));

            if (match is not null)
            {
                _cache.Idle.Remove(match);
            }
        }

        await ConnectionCache.CloseAllAsync(expired);

        return match;
    }

    private async ValueTask<ConnectResult> ConnectThroughPoolAsync(
        ConnectTarget target,
        ConnectionPoolKey? key,
        Func<CancellationToken, ValueTask<ConnectResult>> openAsync,
        bool isQuic,
        CancellationToken cancellationToken)
    {
        var shared = await ShareAsync(key, target.Events, cancellationToken);
        if (shared is not null)
        {
            new NetworkDiagnosticLog(target.DiagnosticLog).PoolShare(target, shared.ConnectionNumber);
            target.Events.ReportInfo("Multiplexed connection found");
            return Reuse(target, shared);
        }

        var idle = await TakeIdleAsync(key, target.Events);
        new NetworkDiagnosticLog(target.DiagnosticLog).PoolDecision(target, idle?.ConnectionNumber);

        return idle is null
            ? await OpenAsync(target, key, openAsync, isQuic, cancellationToken)
            : Reuse(target, Lease(idle));
    }

    private async ValueTask<ConnectResult> OpenAsync(
        ConnectTarget target,
        ConnectionPoolKey? key,
        Func<CancellationToken, ValueTask<ConnectResult>> openAsync,
        bool isQuic,
        CancellationToken cancellationToken)
    {
        var negotiation = StartNegotiation(key);
        ConnectResult connect;
        try
        {
            connect = await openAsync(cancellationToken);
        }
        catch
        {
            Decide(negotiation);
            throw;
        }

        if (connect.Connection is null)
        {
            Decide(negotiation);
            return isQuic ? connect : NumberedConnectFailure.Of(connect, _cache.NumberNextConnection());
        }

        var entry = Lease(new PoolEntry(
            key,
            connect.Connection,
            _cache.NumberNextConnection(),
            connect.LocalEndPoint,
            connect.PeerCertificates,
            connect.UnixSocketPath,
            connect.MappedHost,
            connect.MappedPort)
        {
            Session = isQuic ? connect.Connection as IConnectionSession : null,
        });
        if (!isQuic && MayMultiplex(target, connect))
        {
            KeepNegotiating(entry, negotiation);
        }
        else
        {
            Decide(negotiation);
        }

        return ConnectResult.Connected(
            new PooledConnection(this, entry, target.Events),
            connect.Timings,
            connect.LocalEndPoint,
            connect.ProxyConnectResponseCode,
            connect.PeerCertificates,
            isReused: false,
            entry.ConnectionNumber,
            applicationProtocol: connect.ApplicationProtocol,
            unixSocketPath: entry.UnixSocketPath,
            mappedHost: entry.MappedHost,
            mappedPort: entry.MappedPort,
            connectReplyHeadersStored: connect.ConnectReplyHeadersStored,
            proxyConnectHeaderBytes: connect.ProxyConnectHeaderBytes);
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
            entry.ConnectionNumber,
            unixSocketPath: entry.UnixSocketPath,
            mappedHost: entry.MappedHost,
            mappedPort: entry.MappedPort);
    }

    private List<PoolEntry> RemoveExpired()
    {
        var expired = _cache.Idle.FindAll(entry => _cache.TimeProvider.GetElapsedTime(entry.IdleSince) > MaximumIdleTime);
        _cache.Idle.RemoveAll(expired.Contains);

        return expired;
    }

    private PoolEntry RemoveOldest()
    {
        var oldest = _cache.Idle[0];
        _cache.Idle.RemoveAt(0);

        return oldest;
    }
}
