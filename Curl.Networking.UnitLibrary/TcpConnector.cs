using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IConnector" /> for TCP: resolves the host, dials each
/// address in the resolver's order until one connects, and hands the connection to
/// <see cref="ITlsProvider" /> when the target asks for TLS. Given a <see cref="QuicDialer" />,
/// it also resolves the host for a QUIC connection and hands the addresses to the dialer.
/// </summary>
/// <param name="dnsResolver">Resolves the target host to addresses.</param>
/// <param name="tcpDialer">Opens a plaintext connection to one address.</param>
/// <param name="tlsProvider">Upgrades the connection when <see cref="ConnectTarget.UseTls" /> is set.</param>
/// <param name="timeProvider">
/// Takes every timestamp in <see cref="ConnectResult.Timings" />, and measures the connect
/// time curl reports in its exit 7 message.
/// </param>
/// <param name="proxyTunnelOptions">
/// The <c>User-Agent</c> and credential encoding of the CONNECT request that tunnels through
/// an HTTP proxy; <see langword="null" /> for <see cref="HttpProxyTunnelOptions.Default" />.
/// </param>
/// <param name="resolveOverrides">
/// The <c>--resolve</c> entries, consulted for a <c>host:port</c> before
/// <paramref name="dnsResolver" />; <see langword="null" /> for <see cref="ResolveOverrides.None" />.
/// </param>
/// <param name="connectToMappings">
/// The <c>--connect-to</c> mappings, which change the host and port resolved and dialled
/// but not the host TLS verifies; <see langword="null" /> for <see cref="ConnectToMappings.None" />.
/// </param>
/// <param name="proxyTlsProvider">
/// Runs the handshake to an HTTPS proxy, tunnelling (<see cref="ProxyKind.Https" />) or a forward
/// proxy (<see cref="ConnectTarget.IsForwardProxy" /> with <see cref="ConnectTarget.UseTls" />), as curl verifies the
/// proxy with the <c>--proxy-*</c> TLS options and the target with <c>-k</c> and <c>--cacert</c>;
/// <see langword="null" /> for <paramref name="tlsProvider" />.
/// </param>
/// <param name="connectTimeout">
/// The <c>--connect-timeout</c> limit on each connect, its resolve, dials, proxy tunnel and TLS
/// handshakes together (ADR-0117); <see langword="null" />, zero or less for curl's default of
/// 300 seconds.
/// </param>
/// <param name="addressFamily">
/// The family <c>-4</c> (<see cref="AddressFamily.InterNetwork" />) or <c>-6</c>
/// (<see cref="AddressFamily.InterNetworkV6" />) chose, or <see cref="AddressFamily.Unspecified" />
/// for either: only addresses of that family are dialled for a host or proxy name (BL-500).
/// </param>
/// <param name="unixSocket">
/// The Unix domain socket <c>--unix-socket</c> or <c>--abstract-unix-socket</c> named, dialled for
/// every target in place of its host, port and proxy; <see langword="null" /> to dial TCP (BL-507).
/// </param>
/// <param name="httpOverTlsApplicationProtocols">
/// What the handshake for HTTP over TLS to the origin offers through ALPN, one of
/// <see cref="HttpApplicationProtocols" />' lists as the HTTP version options and the platform
/// choose it (ADR-0141); <see langword="null" /> for <see cref="HttpApplicationProtocols.Http11Only" />.
/// </param>
/// <param name="quicDialer">
/// Opens the QUIC connections <see cref="ConnectMultiplexedAsync" /> asks for, over the addresses
/// this connector resolves (ADR-0179); <see langword="null" /> for a connector with no QUIC.
/// </param>
public sealed class TcpConnector(
    IDnsResolver dnsResolver,
    ITcpDialer tcpDialer,
    ITlsProvider tlsProvider,
    TimeProvider timeProvider,
    HttpProxyTunnelOptions? proxyTunnelOptions = null,
    ResolveOverrides? resolveOverrides = null,
    ConnectToMappings? connectToMappings = null,
    ITlsProvider? proxyTlsProvider = null,
    TimeSpan? connectTimeout = null,
    AddressFamily addressFamily = AddressFamily.Unspecified,
    UnixSocketAddress? unixSocket = null,
    IReadOnlyList<string>? httpOverTlsApplicationProtocols = null,
    QuicDialer? quicDialer = null) : IConnector
{
    private const string AnyHost = "*";

    /// <summary>
    /// Gets curl's <c>DEFAULT_CONNECT_TIMEOUT</c>, 300 seconds: the connect timeout when no
    /// <c>--connect-timeout</c>, or 0, was given.
    /// </summary>
    public static TimeSpan DefaultConnectTimeout { get; } = TimeSpan.FromSeconds(300);

    // A Unix domain socket's connection has no IP end points; its opened event carries this for both.
    private static readonly IPEndPoint UnspecifiedEndPoint = new(IPAddress.Any, 0);

    private readonly TimeSpan _connectTimeout = ConnectTimeoutOrDefault(connectTimeout);

    /// <summary>
    /// Gets the Unix domain socket every connect dials in place of the target's host, port and
    /// proxy (<c>--unix-socket</c>, <c>--abstract-unix-socket</c>), or <see langword="null" /> to dial TCP.
    /// </summary>
    public UnixSocketAddress? UnixSocket { get; } = unixSocket;

    /// <summary>
    /// Gets what the handshake for HTTP over TLS to the origin offers through ALPN (ADR-0141).
    /// </summary>
    public IReadOnlyList<string> HttpOverTlsApplicationProtocols { get; } =
        httpOverTlsApplicationProtocols ?? HttpApplicationProtocols.Http11Only;

    private readonly ITlsProvider _proxyTlsProvider = proxyTlsProvider ?? tlsProvider;
    private readonly HttpProxyTunnelOptions _proxyTunnelOptions = proxyTunnelOptions ?? HttpProxyTunnelOptions.Default;
    private readonly ResolveOverrides _resolveOverrides = resolveOverrides ?? ResolveOverrides.None;
    private readonly ConnectToMappings _connectToMappings = connectToMappings ?? ConnectToMappings.None;
    private readonly ConcurrentDictionary<string, DnsCacheEntry> _dnsCache = new(StringComparer.OrdinalIgnoreCase);
    private int _resolveEntriesLoaded;
    private long _nextConnectionNumber;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The messages are curl 8.21.0's, cut to 255 characters as curl cuts them (ADR-0072):
    /// <c>Could not resolve host: &lt;host&gt;</c> for
    /// exit 6, and <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms:
    /// Could not connect to server</c> for exit 7, where <c>n</c> is the time spent
    /// dialing as measured by the injected <see cref="TimeProvider" />. When
    /// <see cref="ConnectTarget.UseTls" /> is set, the <see cref="ITlsProvider" />'s result
    /// is returned as it is, so a failed handshake keeps the exit code and message the
    /// provider chose.
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is an HTTP proxy (<see cref="ProxyKind.Http" />
    /// or <see cref="ProxyKind.Http10" />), the connector resolves and dials the proxy, sends
    /// CONNECT for the target, and applies TLS over the tunnel when
    /// <see cref="ConnectTarget.UseTls" /> is set; the proxy's status code is
    /// <see cref="ConnectResult.ProxyConnectResponseCode" />. The failures are curl 8.21.0's:
    /// exit 5 <c>Could not resolve proxy: &lt;proxy host&gt;</c>, exit 7 <c>Failed to connect
    /// to &lt;host&gt;:&lt;port&gt; over proxy &lt;proxy host&gt; after &lt;n&gt; ms: Could not
    /// connect to server</c>, exit 7 <c>CONNECT tunnel failed, response &lt;code&gt;</c> for a
    /// status outside 200 to 299 (<c>0</c> when the reply is not HTTP), and exit 56
    /// <c>Proxy CONNECT aborted</c> when the proxy closes before its header block ends,
    /// <c>CONNECT response too large</c> for a reply line of 16384 bytes or more, and
    /// <c>Too large response headers: &lt;n&gt; &gt; 307200</c> for a longer header block.
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is an HTTPS proxy (<see cref="ProxyKind.Https" />),
    /// the connector resolves and dials the proxy with the same exit 5 and exit 7 failures,
    /// runs TLS to it through the proxy's <see cref="ITlsProvider" />, verified against the proxy host,
    /// then sends the same CONNECT over that TLS and, when <see cref="ConnectTarget.UseTls" />
    /// is set, runs a second handshake to <see cref="ConnectTarget.Host" /> inside it, as curl
    /// 8.21.0 does. A failed handshake to the proxy or to the target is the provider's result
    /// as it is, and a refused CONNECT fails as through an HTTP proxy.
    /// <see cref="ConnectTimings.TlsHandshakeCompleted" /> and
    /// <see cref="ConnectResult.PeerCertificates" /> are the target's handshake's only, so an
    /// http target has none (measured: <c>%{time_appconnect}</c> is <c>0</c>).
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is a SOCKS proxy (<see cref="ProxyKind.Socks4" />,
    /// <see cref="ProxyKind.Socks4a" />, <see cref="ProxyKind.Socks5" /> or
    /// <see cref="ProxyKind.Socks5Hostname" />), the connector resolves and dials the proxy the
    /// same way, with the same exit 5 and exit 7 failures, runs curl 8.21.0's handshake for
    /// the kind (<see cref="SocksProxyTunnel" />) and applies TLS over the tunnel when
    /// <see cref="ConnectTarget.UseTls" /> is set. SOCKS4 and SOCKS5 resolve the target
    /// locally, <c>--resolve</c> included, and fail with exit 6 when it does not resolve; a
    /// handshake the proxy refuses or cuts short fails with exit 97
    /// (<see cref="CurlExitCode.Proxy" />) and curl's message. The proxy connection is
    /// disposed on every failure. <see cref="ConnectResult.ProxyConnectResponseCode" /> is
    /// <c>0</c>.
    /// </para>
    /// <para>
    /// Before anything is resolved, a <c>--resolve</c> entry that did not parse, or a
    /// <c>--connect-to</c> mapping that matches the target and whose destination does not
    /// parse, fails with exit 49 and curl 8.21.0's message (see <see cref="ResolveOverrides" />
    /// and <see cref="ConnectToMappings" />). A matching mapping's host and port are resolved,
    /// dialled and, through an HTTP proxy, named in the CONNECT request; exit 6 then names the
    /// mapped host and exit 7 reads <c>Failed to connect to &lt;host&gt;:&lt;port&gt; via
    /// &lt;mapped host&gt;:&lt;mapped port&gt; after &lt;n&gt; ms: Could not connect to
    /// server</c> (measured). TLS still verifies <see cref="ConnectTarget.Host" />. A
    /// <c>--resolve</c> entry answers for the host and port being resolved, the proxy's
    /// included, in place of the <see cref="IDnsResolver" />.
    /// </para>
    /// <para>
    /// The resolve, dials, any tunnel and any TLS handshake run under one connect timeout
    /// (<c>--connect-timeout</c>, or 300 seconds) on the injected <see cref="TimeProvider" />,
    /// counted from when this method begins. When it passes, the connect fails with exit 28
    /// (<see cref="CurlExitCode.OperationTimedOut" />) and curl 8.21.0's <c>Connection timed out
    /// after &lt;n&gt; milliseconds</c>, for every scheme (measured, ADR-0117, BL-510).
    /// </para>
    /// <para>
    /// A success carries <see cref="ConnectResult.Timings" />, taken from the injected
    /// <see cref="TimeProvider" />: <see cref="ConnectTimings.Started" /> when this method
    /// begins, <see cref="ConnectTimings.NameResolved" /> when the host (or proxy) has
    /// resolved, <see cref="ConnectTimings.Connected" /> when the TCP connect completes or,
    /// through a proxy, when the tunnel is open, and <see cref="ConnectTimings.TlsHandshakeCompleted" />
    /// from the <see cref="ITlsProvider" />'s own timings, or when it returned if it recorded
    /// none. <see cref="ConnectResult.LocalEndPoint" /> is the local end point
    /// <see cref="ITcpDialer" /> reports; the remote end point is the connection's
    /// <see cref="IConnection.RemoteEndPoint" />. <see cref="ConnectResult.PeerCertificates" />
    /// are the <see cref="ITlsProvider" />'s, and empty without TLS.
    /// </para>
    /// <para>
    /// A dial that reached no address, to the host or to the proxy, carries
    /// <see cref="ConnectTimings.Started" /> and <see cref="ConnectTimings.NameResolved" /> with
    /// <see cref="ConnectTimings.Connected" /> <see langword="null" />, as curl 8.21.0 reports
    /// <c>%{time_namelookup}</c> but a <c>%{time_connect}</c> of <c>0</c> after a refused
    /// connect (measured, ADR-0091). A failed resolve, a bad <c>--resolve</c> or <c>--connect-to</c>
    /// entry and a failed tunnel carry none; a failed handshake is the provider's result as it is.
    /// </para>
    /// <para>
    /// On <see cref="ConnectTarget.Events" /> it reports curl 8.21.0's <c>-v</c> lines for the
    /// connect (ADR-0100): <c>  Trying &lt;address&gt;:&lt;port&gt;...</c> before each dial,
    /// <c>connect to &lt;address&gt; port &lt;port&gt; from 0.0.0.0 port 0 failed:
    /// &lt;reason&gt;</c> after each dial that fails (<see cref="ConnectFailureReason" />), and
    /// the exit 7 message when none reached. A success is reported through
    /// <see cref="ITransferEvents.ReportConnectionOpened" /> once the connection is ready, after
    /// any tunnel and TLS handshake, naming the host dialled and numbering the connections this
    /// connector opened from <c>0</c> (<see cref="ConnectResult.ConnectionNumber" />). A connect
    /// that fails after its options parse takes the next number too, as curl 8.21.0 numbers the
    /// connection it tried (ADR-0109). A host and port answered by a <c>--resolve</c> entry, or
    /// resolved before by this connector (to the host, the proxy or a SOCKS target), are
    /// answered from that cache and reported first as <c>Hostname &lt;host&gt; was found in DNS
    /// cache</c>, as curl 8.21.0 reports them (measured, BL-481). Every answer, cached or
    /// looked up, is then reported as <c>Host &lt;name&gt;:&lt;port&gt; was resolved.</c>,
    /// <c>IPv6: &lt;addresses&gt;</c> and <c>IPv4: &lt;addresses&gt;</c>, naming the host as it
    /// was cached (a <c>--resolve</c> entry's as written, <c>*</c> for a wildcard) and each
    /// family's addresses joined by <c>, </c> or <c>(none)</c>; a name that is an IP address
    /// reports none of the three (measured, BL-482). Before anything else, a connect that no
    /// transfer has preceded with <see cref="LoadResolveEntries" /> loads the <c>--resolve</c>
    /// entries itself, with their <c>Added</c> lines, before a bad entry fails it.
    /// </para>
    /// <para>
    /// Under <c>-4</c> or <c>-6</c> (the constructor's address family) a host or proxy name is
    /// dialled at that family's addresses only, and one with none fails as not resolved: exit 6
    /// <c>Could not resolve host: &lt;host&gt;</c>, or exit 5 for a proxy. An IP address literal
    /// is dialled as written, as curl 8.21.0 dials it (measured, BL-500).
    /// </para>
    /// <para>
    /// A resolver that says why a name did not resolve (<see cref="IDnsResolverWithFailureReason" />,
    /// the <c>--dns-servers</c> client) adds the reason in brackets, <c>Could not resolve host:
    /// &lt;host&gt; (Domain name not found)</c>, and a c-ares option that did not parse is exit 43
    /// <c>Error 43 resolving &lt;host&gt;:&lt;port&gt;</c>, as curl's c-ares build reports them
    /// (<see cref="NameResolutionFailure" />, BL-694).
    /// </para>
    /// </remarks>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var started = timeProvider.GetTimestamp();
        LoadResolveEntriesUnlessLoaded(target.Events);
        var destination = _connectToMappings.Map(target.Host, target.Port);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return ConnectResult.Failed(CurlExitCode.SetoptOptionSyntax, parseError);
        }

        var result = await ConnectWithinTimeoutAsync(target, destination, started, cancellationToken).ConfigureAwait(false);

        return result.Connection is null
            ? NumberedConnectFailure.Of(result, TakeConnectionNumber())
            : result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Without a <see cref="QuicDialer" /> this is the interface's answer: exit 7 <c>QUIC is not
    /// available on this connector</c>. With one, the <c>--resolve</c> entries load, a bad entry
    /// or matching <c>--connect-to</c> mapping fails with exit 49, and the (mapped) host resolves
    /// exactly as <see cref="ConnectAsync" /> resolves it, through the same DNS cache, <c>-4</c> or
    /// <c>-6</c> and <c>-v</c> lines, failing with exit 6 (ADR-0144 section 3). The addresses go
    /// to the <see cref="QuicDialer" /> with the connection's number, taken from the sequence
    /// <see cref="ConnectAsync" /> takes from.
    /// </para>
    /// <para>
    /// A <c>--connect-timeout</c> greater than zero bounds the handshakes; without one each
    /// handshake has QUIC's own 10 seconds (ADR-0144 section 5). A proxy on the target is not
    /// used: curl's ngtcp2 build connects QUIC directly.
    /// </para>
    /// </remarks>
    public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (quicDialer is null)
        {
            return MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "QUIC is not available on this connector");
        }

        var (request, failure) = await ResolveForQuicAsync(target, cancellationToken).ConfigureAwait(false);
        return request is null
            ? failure!
            : await quicDialer.DialAsync(request, cancellationToken).ConfigureAwait(false);
    }

    // Everything ConnectMultiplexedAsync does before the QUIC dial: the --resolve entries, the
    // --connect-to mapping and the resolve, with ConnectAsync's failures for each.
    private async ValueTask<(QuicDialRequest? Request, MultiplexedConnectResult? Failure)> ResolveForQuicAsync(
        ConnectTarget target,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        LoadResolveEntriesUnlessLoaded(target.Events);
        var destination = _connectToMappings.Map(target.Host, target.Port);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return (null, MultiplexedConnectResult.Failed(CurlExitCode.SetoptOptionSyntax, parseError));
        }

        var (addresses, failure) = await ResolveWithFailureReasonAsync(destination.Host, destination.Port, target.Events, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", destination.Host, destination.Port, failure);
            return (null, MultiplexedConnectResult.Failed(exitCode, message));
        }

        return (new QuicDialRequest(
            target,
            destination.Host,
            destination.Port,
            addresses,
            started,
            timeProvider.GetTimestamp(),
            connectTimeout > TimeSpan.Zero ? connectTimeout : null,
            TakeConnectionNumber()), null);
    }

    /// <summary>
    /// Runs the resolve, dials, any tunnel and any TLS handshake under the connect timeout on the
    /// injected <see cref="TimeProvider" />, counted from <paramref name="started" /> (ADR-0117).
    /// A cancellation that arrives once the limit has passed on the clock, whoever cancelled, is
    /// curl 8.21.0's exit 28 <c>Connection timed out after &lt;n&gt; milliseconds</c>, also
    /// reported on the target's events as its <c>-v</c> repeats it (measured, BL-510); any
    /// earlier cancellation escapes.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectWithinTimeoutAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_connectTimeout, timeProvider);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return (UnixSocket, target.Proxy) switch
            {
                ({ } unixSocketAddress, _) => await ConnectOverUnixSocketAsync(target, unixSocketAddress, started, limited.Token).ConfigureAwait(false),
                (null, { } proxy) => await ConnectThroughProxyAsync(target, destination, proxy, started, limited.Token).ConfigureAwait(false),
                _ => await ConnectDirectlyAsync(target, destination, started, limited.Token).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException) when (timeProvider.GetElapsedTime(started) >= _connectTimeout)
        {
            var message = $"Connection timed out after {(long)timeProvider.GetElapsedTime(started).TotalMilliseconds} milliseconds";
            target.Events.ReportInfo(message);
            return ConnectResult.Failed(CurlExitCode.OperationTimedOut, message);
        }
    }

    private static TimeSpan ConnectTimeoutOrDefault(TimeSpan? connectTimeout) =>
        connectTimeout is { } given && given > TimeSpan.Zero ? given : DefaultConnectTimeout;

    private long TakeConnectionNumber() => Interlocked.Increment(ref _nextConnectionNumber) - 1;

    private async ValueTask<ConnectResult> ConnectDirectlyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        CancellationToken cancellationToken)
    {
        var (addresses, failure) = await ResolveWithFailureReasonAsync(destination.Host, destination.Port, target.Events, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", destination.Host, destination.Port, failure);
            return ConnectResult.Failed(exitCode, message);
        }

        var nameResolved = timeProvider.GetTimestamp();
        var (dialed, lastDialError) = await DialFirstReachableAsync(addresses, destination.Port, destination.Host, target.Events, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            var via = destination.IsMapped ? $" via {destination.Host}:{destination.Port}" : string.Empty;
            return DialFailure(
                target.Events,
                lastDialError,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {target.Host}:{target.Port}{via} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        var timings = new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null);
        return await SecureWhenAskedAsync(dialed, target, timings, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Dials <paramref name="unixSocket" /> in place of the target's host, port and proxy, with no
    /// name resolved, then runs TLS to the target's host when it asks for it, as curl 8.21.0 does
    /// (measured, BL-507). <c>-v</c> shows <c>  Trying &lt;name&gt;:0...</c>, where the name is
    /// <see cref="UnixSocketAddress.RemoteIpText" />, and a failed dial <c>Immediate connect fail for
    /// &lt;name&gt;: &lt;reason&gt;</c> and <c>connect to &lt;name&gt; port 0 from  port 0 failed:
    /// &lt;reason&gt;</c> before exit 7 <c>Failed to connect to &lt;host&gt;:&lt;port&gt; over
    /// unix://&lt;path&gt; after &lt;n&gt; ms: Could not connect to server</c>. A path too long for
    /// a <c>sockaddr_un</c> is exit 6 <c>Unix socket path too long: '&lt;path&gt;'</c>.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectOverUnixSocketAsync(
        ConnectTarget target,
        UnixSocketAddress unixSocket,
        long started,
        CancellationToken cancellationToken)
    {
        if (unixSocket.IsTooLong(OperatingSystem.IsMacOS()))
        {
            return ConnectResult.Failed(
                CurlExitCode.CouldntResolveHost,
                CurlErrorBuffer.Truncate($"Unix socket path too long: '{unixSocket.Path}'"));
        }

        var name = unixSocket.RemoteIpText;
        target.Events.ReportInfo($"  Trying {name}:0...");
        var nameResolved = timeProvider.GetTimestamp();
        IConnection connection;
        try
        {
            connection = await tcpDialer.DialUnixSocketAsync(unixSocket, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            var reason = ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows());
            target.Events.ReportInfo($"Immediate connect fail for {name}: {reason}");
            target.Events.ReportInfo($"connect to {name} port 0 from  port 0 failed: {reason}");
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            return DialFailure(
                target.Events,
                exception.SocketErrorCode,
                new ConnectTimings(started, nameResolved, null, null),
                CurlErrorBuffer.Truncate($"Failed to connect to {target.Host}:{target.Port} over unix://{unixSocket.Path} after {elapsedMilliseconds} ms: Could not connect to server"));
        }

        var dialed = new DialedSocket(connection, null, unixSocket.Path, null, name);
        var timings = new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null);
        return await SecureWhenAskedAsync(dialed, target, timings, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads the <c>--resolve</c> entries into the DNS cache, as curl 8.21.0 loads them at the
    /// start of every transfer but not for a redirect it follows (measured, BL-482). Each
    /// addition is reported on <paramref name="events" /> as <c>Added
    /// &lt;host&gt;:&lt;port&gt;:&lt;addresses&gt; to DNS cache</c> (with <c> (non-permanent)</c>
    /// for a <c>+</c> entry, and <c>RESOLVE *:&lt;port&gt; using wildcard</c> after a <c>*</c>
    /// one), after <c>RESOLVE &lt;host&gt;:&lt;port&gt; - old addresses discarded</c> when the
    /// key was cached; a removal drops the key without a line. When no transfer has called
    /// this, <see cref="ConnectAsync" /> calls it on its target's events first.
    /// </summary>
    /// <param name="events">The transfer's events.</param>
    public void LoadResolveEntries(ITransferEvents events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Interlocked.Exchange(ref _resolveEntriesLoaded, 1);
        foreach (var entry in _resolveOverrides.Entries)
        {
            LoadResolveEntry(entry, events);
        }
    }

    private void LoadResolveEntriesUnlessLoaded(ITransferEvents events)
    {
        if (Interlocked.CompareExchange(ref _resolveEntriesLoaded, 1, 0) == 0)
        {
            LoadResolveEntries(events);
        }
    }

    private void LoadResolveEntry(ResolveEntry entry, ITransferEvents events)
    {
        var cacheKey = DnsCacheKey(entry.Host, entry.Port);
        if (entry.IsRemoval)
        {
            _dnsCache.TryRemove(cacheKey, out _);
            return;
        }

        if (_dnsCache.ContainsKey(cacheKey))
        {
            events.ReportInfo($"RESOLVE {entry.Host}:{entry.Port} - old addresses discarded");
        }

        _dnsCache[cacheKey] = new DnsCacheEntry(entry.Host, entry.Port, entry.Addresses);
        events.ReportInfo($"Added {entry.Host}:{entry.Port}:{entry.AddressText} to DNS cache{(entry.IsPermanent ? string.Empty : " (non-permanent)")}");
        if (entry.Host == AnyHost)
        {
            events.ReportInfo($"RESOLVE {AnyHost}:{entry.Port} using wildcard");
        }
    }

    /// <summary>
    /// Resolves <paramref name="host" /> for <paramref name="port" /> as curl 8.21.0's DNS cache
    /// does (BL-481): a <c>--resolve</c> entry for the host or for <c>*</c>, or a host and port
    /// this connector already resolved, answers without <see cref="IDnsResolver" /> and is
    /// reported on <paramref name="events" /> as <c>Hostname &lt;host&gt; was found in DNS
    /// cache</c>. A host that did not resolve is not kept, so it is looked up again. Every
    /// answer is then reported as curl's resolved lines (<see cref="ReportResolved" />).
    /// </summary>
    /// <remarks>
    /// Under <c>-4</c> or <c>-6</c> only the chosen family's addresses are returned for a name
    /// (<see cref="AddressFamilyFilter" />). A looked-up answer is kept and reported with that
    /// family's only, as curl asks the system resolver for the one family; <c>localhost</c> is
    /// kept and reported whole, as curl answers it itself with both. A cached entry is reported
    /// whole, and one left with no address of the family is reported as <c>Negative DNS
    /// entry</c> instead, and so fails the resolve (measured on curl 8.21.0, BL-500).
    /// </remarks>
    private async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, int port, ITransferEvents events, CancellationToken cancellationToken)
    {
        var cacheKey = DnsCacheKey(host, port);
        if ((_dnsCache.GetValueOrDefault(cacheKey) ?? _dnsCache.GetValueOrDefault(DnsCacheKey(AnyHost, port))) is { } cached)
        {
            return new DnsResolution(AnswerFromCache(host, cached, events), DnsLookupFailure.None);
        }

        var (addresses, failure) = await LookUpAsync(host, cancellationToken).ConfigureAwait(false);
        var answered = IsLocalhost(host) ? addresses : AddressFamilyFilter.Dialable(host, addresses, addressFamily);
        if (answered.Count > 0)
        {
            var resolved = new DnsCacheEntry(host, port, answered);
            _dnsCache[cacheKey] = resolved;
            ReportResolved(resolved, events);
        }

        return new DnsResolution(AddressFamilyFilter.Dialable(host, answered, addressFamily), failure);
    }

    /// <summary>Resolves as <see cref="ResolveWithFailureReasonAsync" /> does, for a SOCKS handshake, which needs only the addresses.</summary>
    private async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, int port, ITransferEvents events, CancellationToken cancellationToken) =>
        (await ResolveWithFailureReasonAsync(host, port, events, cancellationToken).ConfigureAwait(false)).Addresses;

    /// <summary>Asks the resolver, with its failure reason when it gives one.</summary>
    private async ValueTask<DnsResolution> LookUpAsync(string host, CancellationToken cancellationToken) =>
        dnsResolver is IDnsResolverWithFailureReason withFailureReason
            ? await withFailureReason.ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)
            : new DnsResolution(await dnsResolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false), DnsLookupFailure.None);

    private IReadOnlyList<IPAddress> AnswerFromCache(string host, DnsCacheEntry cached, ITransferEvents events)
    {
        var dialable = AddressFamilyFilter.Dialable(host, cached.Addresses, addressFamily);
        if (dialable.Count == 0)
        {
            events.ReportInfo("Negative DNS entry");
            return dialable;
        }

        events.ReportInfo($"Hostname {host} was found in DNS cache");
        ReportResolved(cached, events);
        return dialable;
    }

    // curl 8.21.0 answers localhost and every name under .localhost itself, with ::1 and 127.0.0.1.
    private static bool IsLocalhost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reports curl 8.21.0's <c>Host &lt;name&gt;:&lt;port&gt; was resolved.</c>,
    /// <c>IPv6: &lt;addresses&gt;</c> and <c>IPv4: &lt;addresses&gt;</c> lines for a cache
    /// entry, naming the entry's host as it was cached (a <c>--resolve</c> entry's as written,
    /// <c>*</c> for a wildcard) and each family's addresses in order, joined by <c>, </c>, or
    /// <c>(none)</c>. An entry named by an IP address reports none of them (measured, BL-482).
    /// </summary>
    private static void ReportResolved(DnsCacheEntry entry, ITransferEvents events)
    {
        if (IPAddress.TryParse(entry.Name, out _))
        {
            return;
        }

        events.ReportInfo($"Host {entry.Name}:{entry.Port} was resolved.");
        events.ReportInfo($"IPv6: {JoinAddresses(entry.Addresses, AddressFamily.InterNetworkV6)}");
        events.ReportInfo($"IPv4: {JoinAddresses(entry.Addresses, AddressFamily.InterNetwork)}");
    }

    private static string JoinAddresses(IReadOnlyList<IPAddress> addresses, AddressFamily family) =>
        addresses.Where(address => address.AddressFamily == family).Select(address => address.ToString()).ToArray() is { Length: > 0 } inFamily
            ? string.Join(", ", inFamily)
            : "(none)";

    private static string DnsCacheKey(string host, int port) => $"{host}:{port}";

    private async ValueTask<ConnectResult> ConnectThroughProxyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        CancellationToken cancellationToken)
    {
        var (addresses, failure) = await ResolveWithFailureReasonAsync(proxy.Host, proxy.Port, target.Events, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveProxy, "proxy", proxy.Host, proxy.Port, failure);
            return ConnectResult.Failed(exitCode, message);
        }

        var nameResolved = timeProvider.GetTimestamp();
        var (dialed, lastDialError) = await DialFirstReachableAsync(addresses, proxy.Port, proxy.Host, target.Events, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            return DialFailure(
                target.Events,
                lastDialError,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {target.Host}:{target.Port} over proxy {proxy.Host} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        return proxy.Kind switch
        {
            ProxyKind.Http or ProxyKind.Http10 => await OpenTunnelAsync(dialed, target, destination, proxy, started, nameResolved, cancellationToken).ConfigureAwait(false),
            ProxyKind.Https => await OpenTunnelOverTlsAsync(dialed, target, destination, proxy, started, nameResolved, cancellationToken).ConfigureAwait(false),
            _ => await OpenSocksTunnelAsync(dialed, target, destination, proxy, new ConnectTimings(started, nameResolved, 0, null), cancellationToken).ConfigureAwait(false),
        };
    }

    private async ValueTask<ConnectResult> OpenTunnelOverTlsAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        long nameResolved,
        CancellationToken cancellationToken)
    {
        // curl 8.21.0 verifies the proxy against its own host name and reports a failed
        // handshake to it with the same exit code and message as one to a target (measured).
        // It verifies with the --proxy-* TLS options, not -k or --cacert, so the proxy's own
        // provider runs this handshake (ADR-0061).
        var securedProxy = await AuthenticateAsync(_proxyTlsProvider, dialed.Connection, proxy.Host, target.Events, isProxy: true, applicationProtocols: [], cancellationToken).ConfigureAwait(false);
        if (securedProxy.Connection is not { } proxyConnection)
        {
            return securedProxy;
        }

        // CONNECT and the target's TLS run over the proxy's TLS; the socket's local end point stays.
        var securedDialed = dialed with { Connection = proxyConnection };
        return await OpenTunnelAsync(securedDialed, target, destination, proxy, started, nameResolved, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> OpenSocksTunnelAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        ConnectTimings timings,
        CancellationToken cancellationToken)
    {
        var connection = dialed.Connection;
        var (failure, exception) = await RunSocksHandshakeAsync(connection, destination, proxy, target.Events, cancellationToken).ConfigureAwait(false);
        if (exception is not null || failure is not null)
        {
            // The proxy connection is disposed whether the handshake failed or could not be sent or read.
            await connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();
            return failure!;
        }

        // As through an HTTP proxy, %{time_connect} is when the tunnel is open.
        return await SecureWhenAskedAsync(dialed, target, timings with { Connected = timeProvider.GetTimestamp() }, 0, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> OpenTunnelAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        long nameResolved,
        CancellationToken cancellationToken)
    {
        var connection = dialed.Connection;
        var (reply, exception) = await RequestTunnelAsync(connection, destination, proxy, cancellationToken).ConfigureAwait(false);
        if (exception is not null || !reply.OpensTunnel)
        {
            // The proxy connection is disposed whether CONNECT failed or could not be sent or read.
            await connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();
            return TunnelFailure(reply);
        }

        // For a tunnel, %{time_connect} is when the tunnel is open (ConnectTimings.Connected).
        var timings = new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null);
        return await SecureWhenAskedAsync(dialed, target, timings, reply.StatusCode, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<(HttpProxyTunnelReply Reply, ExceptionDispatchInfo? Exception)> RequestTunnelAsync(
        IConnection connection,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(HttpProxyTunnel.BuildConnectRequest(destination.Host, destination.Port, proxy, _proxyTunnelOptions), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return (await HttpProxyTunnel.ReadReplyAsync(connection, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (default, ExceptionDispatchInfo.Capture(exception));
        }
    }

    private async ValueTask<(ConnectResult? Failure, ExceptionDispatchInfo? Exception)> RunSocksHandshakeAsync(
        IConnection connection,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await SocksProxyTunnel.OpenAsync(
                connection,
                proxy,
                destination.Host,
                destination.Port,
                (host, port, token) => ResolveAsync(host, port, events, token),
                cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (null, ExceptionDispatchInfo.Capture(exception));
        }
    }

    private static ConnectResult TunnelFailure(HttpProxyTunnelReply reply) =>
        reply.RecvErrorMessage is { } recvErrorMessage
            ? ConnectResult.Failed(CurlExitCode.RecvError, recvErrorMessage)
            : ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {reply.StatusCode}");

    private async ValueTask<ConnectResult> SecureWhenAskedAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectTimings timings,
        int proxyConnectResponseCode,
        CancellationToken cancellationToken)
    {
        if (!target.UseTls)
        {
            return Opened(dialed, target.Events, dialed.Connection, timings, proxyConnectResponseCode, peerCertificates: null);
        }

        var secured = await AuthenticateTargetAsync(dialed.Connection, target, cancellationToken).ConfigureAwait(false);
        if (secured.Connection is not { } securedConnection)
        {
            return secured;
        }

        // A provider that measured its handshake is trusted for the moment it completed; for
        // one that did not, the moment it returned is that moment.
        var handshakeCompleted = secured.Timings?.TlsHandshakeCompleted ?? timeProvider.GetTimestamp();
        return Opened(
            dialed,
            target.Events,
            securedConnection,
            timings with { TlsHandshakeCompleted = handshakeCompleted },
            proxyConnectResponseCode,
            secured.PeerCertificates);
    }

    /// <summary>
    /// Numbers a connection that is ready for the transfer, reports it opened as curl 8.21.0's
    /// <c>Established connection</c> line, after any tunnel and TLS handshake (BL-228), and
    /// returns it.
    /// </summary>
    private ConnectResult Opened(
        DialedSocket dialed,
        ITransferEvents events,
        IConnection connection,
        ConnectTimings timings,
        int proxyConnectResponseCode,
        IReadOnlyList<ReadOnlyMemory<byte>>? peerCertificates)
    {
        var connectionNumber = TakeConnectionNumber();
        events.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = dialed.HostName,
            RemoteEndPoint = dialed.RemoteEndPoint ?? UnspecifiedEndPoint,
            LocalEndPoint = dialed.LocalEndPoint ?? UnspecifiedEndPoint,
            UnixSocketRemoteIp = dialed.UnixSocketRemoteIp,
            ConnectionNumber = connectionNumber,
        });

        return ConnectResult.Connected(
            connection,
            timings,
            dialed.LocalEndPoint,
            proxyConnectResponseCode,
            peerCertificates,
            isReused: false,
            connectionNumber);
    }

    // A forward proxy is the target itself, so its handshake runs through the proxy's
    // provider, as curl 8.21.0 verifies it with --proxy-insecure and not -k (measured, BL-441),
    // and is reported as a proxy's, as curl's OpenSSL build says Proxy certificate: for it
    // (measured, BL-405).
    private ValueTask<ConnectResult> AuthenticateTargetAsync(
        IConnection plaintext,
        ConnectTarget target,
        CancellationToken cancellationToken) =>
        AuthenticateAsync(
            target.IsForwardProxy ? _proxyTlsProvider : tlsProvider,
            plaintext,
            target.Host,
            target.Events,
            target.IsForwardProxy,
            ApplicationProtocolsFor(target, HttpOverTlsApplicationProtocols),
            cancellationToken);

    /// <summary>
    /// Returns the protocols the handshake with <paramref name="target" /> offers through ALPN:
    /// <paramref name="httpOverTls" /> for HTTP over TLS to the origin, the one target the HTTP
    /// handler pools as <c>https</c> (BL-490, ADR-0141); nothing for a forward proxy or any other
    /// protocol, which curl offers no ALPN.
    /// </summary>
    /// <param name="target">The target whose handshake is about to run.</param>
    /// <param name="httpOverTls">What HTTP over TLS to the origin offers.</param>
    /// <returns>The protocols, in preference order; empty to offer none.</returns>
    internal static IReadOnlyList<string> ApplicationProtocolsFor(ConnectTarget target, IReadOnlyList<string> httpOverTls) =>
        !target.IsForwardProxy && string.Equals(target.PoolScheme, "https", StringComparison.OrdinalIgnoreCase)
            ? httpOverTls
            : [];

    // A provider that can report its handshake reports its trust and handshake on the
    // target's events, marked as the proxy's when it is with an HTTPS proxy (BL-404, BL-452),
    // and offers the target's application protocols through ALPN (BL-490).
    private static ValueTask<ConnectResult> AuthenticateAsync(
        ITlsProvider provider,
        IConnection plaintext,
        string host,
        ITransferEvents events,
        bool isProxy,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken) =>
        provider is IHandshakeReportingTlsProvider reportingProvider
            ? reportingProvider.AuthenticateAsClientAsync(plaintext, host, events, isProxy, applicationProtocols, cancellationToken)
            : provider.AuthenticateAsClientAsync(plaintext, host, cancellationToken);

    /// <summary>
    /// Dials each address in turn and returns the first connection, or <see langword="null" />
    /// with the <see cref="SocketError" /> of the last attempt, which curl keeps as
    /// <c>CURLINFO_OS_ERRNO</c>. Each attempt is reported on <paramref name="events" /> as
    /// curl 8.21.0's <c>-v</c> reports it: <c>Trying</c> before it, and the
    /// <c>connect to ... failed</c> line when it fails (measured, BL-408).
    /// </summary>
    private async ValueTask<(DialedSocket? Dialed, SocketError LastError)> DialFirstReachableAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        string hostName,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        var lastError = SocketError.Success;
        foreach (var address in addresses)
        {
            var remoteEndPoint = new IPEndPoint(address, port);
            events.ReportInfo($"  Trying {remoteEndPoint}...");
            try
            {
                var dialed = await tcpDialer.DialAsync(remoteEndPoint, cancellationToken).ConfigureAwait(false);
                return (new DialedSocket(dialed.Connection, dialed.LocalEndPoint, hostName, remoteEndPoint), SocketError.Success);
            }
            catch (SocketException exception)
            {
                // curl moves on to the next address; only when every one fails is it exit 7.
                lastError = exception.SocketErrorCode;
                events.ReportInfo(ConnectFailedLine(remoteEndPoint, exception));
            }
        }

        return (null, lastError);
    }

    /// <summary>
    /// curl 8.21.0's line for one failed dial, such as <c>connect to 127.0.0.1 port 1 from
    /// 0.0.0.0 port 56585 failed: Connection refused</c>. A failed dial reports no local end
    /// point, so it names the unspecified address of the family and port <c>0</c> (ADR-0100).
    /// </summary>
    private static string ConnectFailedLine(IPEndPoint remoteEndPoint, SocketException exception)
    {
        var unspecified = remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        var reason = ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows());
        return $"connect to {remoteEndPoint.Address} port {remoteEndPoint.Port} from {unspecified} port 0 failed: {reason}";
    }

    /// <summary>
    /// The exit 7 for a dial that reached no address, its message also reported on
    /// <paramref name="events" /> as curl's <c>-v</c> repeats it: marked refused when the last
    /// attempt was refused, as <c>--retry-connrefused</c> reads curl's <c>CURLINFO_OS_ERRNO</c>.
    /// It carries the start and lookup timestamps, as curl 8.21.0 still reports
    /// <c>%{time_namelookup}</c> after a refused connect (measured, ADR-0091).
    /// </summary>
    private static ConnectResult DialFailure(ITransferEvents events, SocketError lastError, ConnectTimings timings, string errorMessage)
    {
        events.ReportInfo(errorMessage);
        return lastError == SocketError.ConnectionRefused
            ? ConnectResult.Refused(errorMessage, timings)
            : ConnectResult.Failed(CurlExitCode.CouldntConnect, errorMessage, timings);
    }

    /// <summary>
    /// A dialled TCP connection with the name and address it was dialled for: the connection
    /// the transfer talks over, which becomes the proxy's TLS stream through an HTTPS proxy.
    /// </summary>
    /// <remarks>
    /// Through a Unix domain socket the host name is the socket's path, the two end points are
    /// <see langword="null" /> and <paramref name="UnixSocketRemoteIp" /> is what curl shows instead.
    /// </remarks>
    private sealed record DialedSocket(
        IConnection Connection,
        IPEndPoint? LocalEndPoint,
        string HostName,
        IPEndPoint? RemoteEndPoint,
        string? UnixSocketRemoteIp = null);

    /// <summary>
    /// One key of curl's DNS cache: the host as it was cached (the name looked up, or a
    /// <c>--resolve</c> entry's host as written), the port and the addresses.
    /// </summary>
    private sealed record DnsCacheEntry(string Name, int Port, IReadOnlyList<IPAddress> Addresses);
}
