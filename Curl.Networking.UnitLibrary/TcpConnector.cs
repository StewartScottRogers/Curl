using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IConnector" /> for TCP: resolves the host, dials its addresses
/// with happy eyeballs (<see cref="AddressFamilyRace" />) until one connects, and hands the connection to
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
/// this connector resolves (ADR-0180); <see langword="null" /> for a connector with no QUIC.
/// </param>
/// <param name="happyEyeballsTimeout">
/// The <c>--happy-eyeballs-timeout-ms</c> delay: how long the first address family is dialled alone
/// before the other is dialled beside it (<see cref="AddressFamilyRace" />, ADR-0254);
/// <see langword="null" /> for curl's default of 200 milliseconds.
/// </param>
/// <param name="localBinding">
/// What the local end of every TCP connection, to a host or a proxy, is bound to before it connects
/// (<c>--interface</c>, <c>--local-port</c>, BL-600); <see langword="null" /> to leave it to the system.
/// A dial that cannot bind moves on to the next address, and when the last one failed so the connect
/// is exit 45 <c>Failed binding local connection end</c>, or exit 43 for an <c>ifhost!</c> interface
/// part too long. Unix domain sockets are never bound.
/// </param>
/// <param name="networkInterfaceLookup">
/// Finds the interface <paramref name="localBinding" /> names; <see langword="null" /> for
/// <see cref="SystemNetworkInterfaceLookup" />, which finds none on Windows, as the Schannel build does.
/// </param>
/// <param name="preProxy">
/// The SOCKS proxy <c>--preproxy</c> names, which every connection to an HTTP or HTTPS proxy - a
/// tunnelling <see cref="ConnectTarget.Proxy" /> or a <see cref="ConnectTarget.IsForwardProxy" />
/// target - is opened through (BL-614); <see langword="null" /> for none. A SOCKS
/// <see cref="ConnectTarget.Proxy" /> and a direct target never pass through it.
/// </param>
/// <param name="socks5Authentication">
/// The methods a SOCKS5 greeting offers and how GSS-API authenticates (<c>--socks5-basic</c>,
/// <c>--socks5-gssapi</c>, <c>--socks5-gssapi-service</c>, <c>--socks5-gssapi-nec</c>, BL-615);
/// <see langword="null" /> for <see cref="Socks5AuthenticationOptions.Default" />.
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
    QuicDialer? quicDialer = null,
    TimeSpan? happyEyeballsTimeout = null,
    LocalBinding? localBinding = null,
    INetworkInterfaceLookup? networkInterfaceLookup = null,
    ProxyEndpoint? preProxy = null,
    Socks5AuthenticationOptions? socks5Authentication = null) : IConnector
{
    private const string AnyHost = "*";

    /// <summary>
    /// Gets curl's <c>DEFAULT_CONNECT_TIMEOUT</c>, 300 seconds: the connect timeout when no
    /// <c>--connect-timeout</c>, or 0, was given.
    /// </summary>
    public static TimeSpan DefaultConnectTimeout { get; } = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Gets curl's <c>CURL_HET_DEFAULT</c>, 200 milliseconds: the happy-eyeballs delay when no
    /// <c>--happy-eyeballs-timeout-ms</c> was given.
    /// </summary>
    public static TimeSpan DefaultHappyEyeballsTimeout { get; } = TimeSpan.FromMilliseconds(200);

    // The longest delay a .NET timer takes, about 49.7 days; a longer happy-eyeballs delay is held to it.
    private static readonly TimeSpan LongestTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <summary>
    /// Gets how long the first address family is dialled alone before the other is dialled beside
    /// it: the <c>happyEyeballsTimeout</c> given, or <see cref="DefaultHappyEyeballsTimeout" />,
    /// held to between zero and the longest delay a .NET timer takes.
    /// </summary>
    public TimeSpan HappyEyeballsTimeout { get; } = HappyEyeballsTimeoutOrDefault(happyEyeballsTimeout);

    // A Unix domain socket's connection has no IP end points; its opened event carries this for both.
    private static readonly IPEndPoint UnspecifiedEndPoint = new(IPAddress.Any, 0);

    private readonly TimeSpan _connectTimeout = ConnectTimeoutOrDefault(connectTimeout);

    /// <summary>
    /// Gets what the local end of every TCP connection is bound to (<c>--interface</c>,
    /// <c>--local-port</c>), or <see langword="null" /> to leave it to the system.
    /// </summary>
    public LocalBinding? LocalBinding => localBinding;

    /// <summary>
    /// Gets the methods a SOCKS5 greeting offers and how GSS-API authenticates (BL-615):
    /// <see cref="Socks5AuthenticationOptions.Default" /> when none were given.
    /// </summary>
    public Socks5AuthenticationOptions Socks5Authentication => socks5Authentication ?? Socks5AuthenticationOptions.Default;

    // Every TCP dial goes through this one: bound as localBinding asks, or the dialer as given.
    private ITcpDialer BindingDialer() => localBinding is null
        ? tcpDialer
        : new LocalBindingTcpDialer(tcpDialer, localBinding, networkInterfaceLookup ?? new SystemNetworkInterfaceLookup(), dnsResolver);

    /// <summary>
    /// Gets the Unix domain socket every connect dials in place of the target's host, port and
    /// proxy (<c>--unix-socket</c>, <c>--abstract-unix-socket</c>), or <see langword="null" /> to dial TCP.
    /// </summary>
    public UnixSocketAddress? UnixSocket { get; } = unixSocket;

    /// <summary>
    /// Gets the SOCKS proxy every connection to an HTTP or HTTPS proxy is opened through
    /// (<c>--preproxy</c>), or <see langword="null" /> for none.
    /// </summary>
    public ProxyEndpoint? PreProxy { get; } = preProxy;

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
    /// With a <see cref="PreProxy" />, an HTTP or HTTPS <see cref="ConnectTarget.Proxy" /> and an
    /// <see cref="ConnectTarget.IsForwardProxy" /> target are reached through it, as curl 8.21.0
    /// does for <c>--preproxy</c> (measured, BL-614): the pre-proxy is resolved (exit 5
    /// <c>Could not resolve proxy: &lt;pre-proxy&gt;</c>) and dialled (exit 7 <c>Failed to connect
    /// to &lt;proxy&gt;:&lt;port&gt; over proxy &lt;pre-proxy&gt; after &lt;n&gt; ms: ...</c>), its SOCKS
    /// handshake opens a tunnel to the HTTP proxy with the SOCKS failures above, and the CONNECT,
    /// the HTTPS proxy's TLS or the forwarded request then run inside that tunnel.
    /// </para>
    /// <para>
    /// Before anything is resolved, a <c>--resolve</c> entry that did not parse, or a
    /// <c>--connect-to</c> mapping that matches the target and whose destination does not
    /// parse, fails with exit 49 and curl 8.21.0's message (see <see cref="ResolveOverrides" />
    /// and <see cref="ConnectToMappings" />). A matching mapping's host and port are resolved,
    /// dialled and, through an HTTP proxy, named in the CONNECT request; exit 6 then names the
    /// mapped host and exit 7 reads <c>Failed to connect to &lt;host&gt;:&lt;port&gt; via
    /// &lt;mapped host&gt;:&lt;mapped port&gt; after &lt;n&gt; ms: Could not connect to
    /// server</c> (measured). When no mapping matches, a <see cref="ConnectTarget.AltSvcRoute" />
    /// is dialled the same way, after curl's <c>Alt-svc connecting from</c> line. TLS still
    /// verifies <see cref="ConnectTarget.Host" />. A
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

        // Every failure, and every exception that escapes, is written to the diagnostic log at
        // error (BL-920).
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        try
        {
            var result = await ConnectAndNumberAsync(target, cancellationToken).ConfigureAwait(false);
            if (result.Connection is null)
            {
                log.Failed(DiagnosticLogComponents.Connect, result.ExitCode, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception exception)
        {
            log.Threw(exception);
            throw;
        }
    }

    private async ValueTask<ConnectResult> ConnectAndNumberAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        LoadResolveEntriesUnlessLoaded(target.Events);
        var destination = DestinationOf(target);
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

        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        var (request, failure) = await ResolveForQuicAsync(target, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            log.Failed(DiagnosticLogComponents.Quic, failure!.ExitCode, failure.ErrorMessage);
            return failure;
        }

        // The QUIC steps are logged around the dial, so Curl.Quic needs no diagnostic log (BL-920).
        log.QuicDialling(request.DestinationHost, request.Port, request.Addresses);
        var dialled = await quicDialer.DialAsync(request, cancellationToken).ConfigureAwait(false);
        log.QuicDialled(request.DestinationHost, request.Port, dialled);
        return dialled;
    }

    // Everything ConnectMultiplexedAsync does before the QUIC dial: the --resolve entries, the
    // --connect-to mapping and the resolve, with ConnectAsync's failures for each.
    private async ValueTask<(QuicDialRequest? Request, MultiplexedConnectResult? Failure)> ResolveForQuicAsync(
        ConnectTarget target,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        LoadResolveEntriesUnlessLoaded(target.Events);
        var destination = DestinationOf(target);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return (null, MultiplexedConnectResult.Failed(CurlExitCode.SetoptOptionSyntax, parseError));
        }

        var (addresses, failure) = await ResolveWithFailureReasonAsync(destination.Host, destination.Port, target, cancellationToken).ConfigureAwait(false);
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
            return (UnixSocket, TunnelProxyOf(target)) switch
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

    /// <summary>
    /// Gives where a connection to <paramref name="target" /> goes: the <c>--connect-to</c>
    /// mapping that matches it, else its <see cref="ConnectTarget.AltSvcRoute" />'s alternative,
    /// reported first as curl 8.21.0's <c>Alt-svc connecting from [&lt;id&gt;]&lt;host&gt;:&lt;port&gt;
    /// to [&lt;id&gt;]&lt;host&gt;:&lt;port&gt;</c> (measured, BL-623 Notes), else the target itself.
    /// An alternative counts as mapped, so exit 7 names it after <c>via</c>, as curl's does.
    /// </summary>
    private ConnectDestination DestinationOf(ConnectTarget target)
    {
        var mapped = _connectToMappings.Map(target.Host, target.Port);
        if (mapped.IsMapped || mapped.ParseError is not null || target.AltSvcRoute is not { } route)
        {
            return mapped;
        }

        var alternative = route.Alternative;
        target.Events.ReportInfo(
            $"Alt-svc connecting from [{route.OriginAlpn}]{target.Host}:{target.Port} to [{alternative.Alpn}]{alternative.Host}:{alternative.Port}");
        return new ConnectDestination(alternative.Host, alternative.Port, IsMapped: true, ParseError: null);
    }

    private static TimeSpan ConnectTimeoutOrDefault(TimeSpan? connectTimeout) =>
        connectTimeout is { } given && given > TimeSpan.Zero ? given : DefaultConnectTimeout;

    private static TimeSpan HappyEyeballsTimeoutOrDefault(TimeSpan? happyEyeballsTimeout) =>
        happyEyeballsTimeout switch
        {
            null => DefaultHappyEyeballsTimeout,
            { } given when given < TimeSpan.Zero => TimeSpan.Zero,
            { } given when given > LongestTimerDelay => LongestTimerDelay,
            { } given => given,
        };

    private long TakeConnectionNumber() => Interlocked.Increment(ref _nextConnectionNumber) - 1;

    private async ValueTask<ConnectResult> ConnectDirectlyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        CancellationToken cancellationToken)
    {
        var (addresses, failure) = await ResolveWithFailureReasonAsync(destination.Host, destination.Port, target, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", destination.Host, destination.Port, failure);
            return ConnectResult.Failed(exitCode, message);
        }

        var nameResolved = timeProvider.GetTimestamp();
        var (dialed, lastDialError, lastBindFailure) = await DialFirstReachableAsync(addresses, destination.Port, destination.Host, target, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            var via = destination.IsMapped ? $" via {destination.Host}:{destination.Port}" : string.Empty;
            return DialFailure(
                target.Events,
                lastDialError,
                lastBindFailure,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {target.Host}:{target.Port}{via} after {elapsedMilliseconds} ms: {DialFailureText(lastBindFailure)}");
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
                null,
                new ConnectTimings(started, nameResolved, null, null),
                CurlErrorBuffer.Truncate($"Failed to connect to {target.Host}:{target.Port} over unix://{unixSocket.Path} after {elapsedMilliseconds} ms: Could not connect to server"));
        }

        var dialed = new DialedSocket(connection, null, unixSocket.Path, null, name, unixSocket.Path);
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
    /// reported on the target's events as <c>Hostname &lt;host&gt; was found in DNS
    /// cache</c>. A host that did not resolve is not kept, so it is looked up again. Every
    /// answer is then reported as curl's resolved lines (<see cref="ReportResolved" />), and
    /// written to the target's diagnostic log (<see cref="LogResolution" />, BL-920).
    /// </summary>
    /// <remarks>
    /// Under <c>-4</c> or <c>-6</c> only the chosen family's addresses are returned for a name
    /// (<see cref="AddressFamilyFilter" />). A looked-up answer is kept and reported with that
    /// family's only, as curl asks the system resolver for the one family; <c>localhost</c> is
    /// kept and reported whole, as curl answers it itself with both. A cached entry is reported
    /// whole, and one left with no address of the family is reported as <c>Negative DNS
    /// entry</c> instead, and so fails the resolve (measured on curl 8.21.0, BL-500).
    /// </remarks>
    private async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, int port, ConnectTarget target, CancellationToken cancellationToken)
    {
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        var resolveStarted = log.IsEnabled(DiagnosticLogLevel.Info) ? timeProvider.GetTimestamp() : 0;
        var cacheKey = DnsCacheKey(host, port);
        if ((_dnsCache.GetValueOrDefault(cacheKey) ?? _dnsCache.GetValueOrDefault(DnsCacheKey(AnyHost, port))) is { } cached)
        {
            var fromCache = AnswerFromCache(host, cached, target.Events);
            LogResolution(log, host, port, fromCache, fromCache: true, resolveStarted);
            return new DnsResolution(fromCache, DnsLookupFailure.None);
        }

        var (addresses, failure) = await LookUpAsync(host, cancellationToken).ConfigureAwait(false);
        var answered = IsLocalhost(host) ? addresses : AddressFamilyFilter.Dialable(host, addresses, addressFamily);
        if (answered.Count > 0)
        {
            var resolved = new DnsCacheEntry(host, port, answered);
            _dnsCache[cacheKey] = resolved;
            ReportResolved(resolved, target.Events);
        }

        var dialable = AddressFamilyFilter.Dialable(host, answered, addressFamily);
        LogResolution(log, host, port, dialable, fromCache: false, resolveStarted);
        return new DnsResolution(dialable, failure);
    }

    /// <summary>
    /// Writes a resolve's answer to the diagnostic log: the addresses and elapsed milliseconds at
    /// <c>info</c>, or a <c>warning</c> when none can be dialled. The clock is read only when
    /// <c>info</c> is enabled, so a disabled log takes no timestamp.
    /// </summary>
    private void LogResolution(NetworkDiagnosticLog log, string host, int port, IReadOnlyList<IPAddress> dialable, bool fromCache, long resolveStarted)
    {
        if (dialable.Count == 0)
        {
            log.NotResolved(host, port);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            log.Resolved(host, port, dialable, fromCache, timeProvider.GetElapsedTime(resolveStarted));
        }
    }

    /// <summary>Resolves as <see cref="ResolveWithFailureReasonAsync" /> does, for a SOCKS handshake, which needs only the addresses.</summary>
    private async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, int port, ConnectTarget target, CancellationToken cancellationToken) =>
        (await ResolveWithFailureReasonAsync(host, port, target, cancellationToken).ConfigureAwait(false)).Addresses;

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
    internal static bool IsLocalhost(string host) =>
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
        var firstHop = FirstHopTo(proxy);
        var (addresses, failure) = await ResolveWithFailureReasonAsync(firstHop.Host, firstHop.Port, target, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveProxy, "proxy", firstHop.Host, firstHop.Port, failure);
            return ConnectResult.Failed(exitCode, message);
        }

        var nameResolved = timeProvider.GetTimestamp();
        var proxyAuthorization = await CreateProxyAuthorizationAsync(destination, proxy, [], cancellationToken).ConfigureAwait(false);
        var answersChallenge = false;
        while (true)
        {
            // A 407 answered on a connection the proxy closes is sent again on a new one, as
            // curl 8.21.0 connects again ("Connect me again please", BL-602 Notes).
            var (result, redialAuthorization) = await DialAndOpenThroughProxyAsync(
                addresses, new TunnelRequest(target, destination, proxy, started, nameResolved, answersChallenge), proxyAuthorization, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }

            proxyAuthorization = redialAuthorization;
            answersChallenge = true;
        }
    }

    /// <summary>
    /// Dials the proxy and opens the tunnel through it, sending
    /// <paramref name="proxyAuthorization" /> on the first CONNECT: the connect's result, or
    /// <see langword="null" /> with the <c>Proxy-Authorization</c> to send on a new connection
    /// when the proxy challenged and closed this one.
    /// </summary>
    private async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> DialAndOpenThroughProxyAsync(
        IReadOnlyList<IPAddress> addresses,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        var (target, destination, proxy, started, nameResolved, _) = tunnel;
        var firstHop = FirstHopTo(proxy);
        var (dialed, lastDialError, lastBindFailure) = await DialFirstReachableAsync(addresses, firstHop.Port, firstHop.Host, target, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            // Through a pre-proxy curl 8.21.0 names the HTTP proxy as what it failed to reach (measured, BL-614).
            var reached = ReferenceEquals(firstHop, proxy) ? $"{target.Host}:{target.Port}" : $"{proxy.Host}:{proxy.Port}";
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            return (DialFailure(
                target.Events,
                lastDialError,
                lastBindFailure,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {reached} over proxy {firstHop.Host} after {elapsedMilliseconds} ms: {DialFailureText(lastBindFailure)}"), null);
        }

        if (!ReferenceEquals(firstHop, proxy)
            && await OpenSocksHopAsync(dialed.Connection, target, new ConnectDestination(proxy.Host, proxy.Port, IsMapped: false, ParseError: null), firstHop, cancellationToken).ConfigureAwait(false) is { } preProxyFailure)
        {
            return (preProxyFailure, null);
        }

        return proxy.Kind switch
        {
            ProxyKind.Http or ProxyKind.Http10 => await OpenTunnelAsync(dialed, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false),
            ProxyKind.Https => await OpenTunnelOverTlsAsync(dialed, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false),
            _ => (await OpenSocksTunnelAsync(dialed, target, destination, proxy, new ConnectTimings(started, nameResolved, 0, null), cancellationToken).ConfigureAwait(false), null),
        };
    }

    /// <summary>
    /// Asks <see cref="HttpProxyTunnelOptions.ProxyAuthenticator" /> for the CONNECT's
    /// <c>Proxy-Authorization</c> to <paramref name="destination" />: with no challenges for the
    /// first CONNECT, with a <c>407</c>'s <c>Proxy-Authenticate</c> values after one.
    /// </summary>
    private ValueTask<string?> CreateProxyAuthorizationAsync(
        ConnectDestination destination,
        ProxyEndpoint proxy,
        IReadOnlyList<string> challenges,
        CancellationToken cancellationToken) =>
        ProxyAuthenticator.CreateAuthorizationAsync(ProxyAuthRequestOf(destination, proxy), challenges, cancellationToken);

    /// <summary>
    /// The authenticator that answers the proxy: <see cref="HttpProxyTunnelOptions.ProxyAuthenticator" />,
    /// or pre-emptive Basic without one.
    /// </summary>
    private IHttpAuthenticator ProxyAuthenticator =>
        _proxyTunnelOptions.ProxyAuthenticator ?? new PreemptiveBasicProxyAuthenticator(_proxyTunnelOptions.CredentialEncoding);

    /// <summary>
    /// The request the authenticator is asked about for a CONNECT to <paramref name="destination" />:
    /// the proxy's own URL, so NTLM and Negotiate ask for <c>HTTP</c> on the proxy's host as
    /// curl 8.21.0 does (BL-604 Notes), and the CONNECT's authority as the request target.
    /// </summary>
    private HttpAuthRequest ProxyAuthRequestOf(ConnectDestination destination, ProxyEndpoint proxy)
    {
        var scheme = proxy.Kind == ProxyKind.Https ? "https" : "http";
        return new HttpAuthRequest(
            "CONNECT",
            CurlUrl.Parse($"{scheme}://{HttpProxyTunnel.FormatAuthority(proxy.Host, proxy.Port)}/"),
            HttpProxyTunnel.FormatAuthority(destination.Host, destination.Port),
            proxy.Credential,
            null,
            _proxyTunnelOptions.ProxyAuthSchemes,
            IsProxy: true);
    }

    private async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> OpenTunnelOverTlsAsync(
        DialedSocket dialed,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        // curl 8.21.0 verifies the proxy against its own host name and reports a failed
        // handshake to it with the same exit code and message as one to a target (measured).
        // It verifies with the --proxy-* TLS options, not -k or --cacert, so the proxy's own
        // provider runs this handshake (ADR-0061). It offers http/1.1 through ALPN whatever the
        // HTTP version options say, and none under --no-alpn (measured on both builds, ADR-0190).
        var securedProxy = await AuthenticateAsync(_proxyTlsProvider, dialed.Connection, tunnel.Proxy.Host, tunnel.Target, isProxy: true, applicationProtocols: HttpApplicationProtocols.Http11Only, cancellationToken).ConfigureAwait(false);
        if (securedProxy.Connection is not { } proxyConnection)
        {
            return (securedProxy, null);
        }

        // Both builds say what the proxy's ALPN agreed before the CONNECT, with and without
        // --no-alpn (measured, BL-872).
        tunnel.Target.Events.ReportInfo(securedProxy.ApplicationProtocol is { } agreed
            ? $"CONNECT: '{agreed}' negotiated"
            : "CONNECT: no ALPN negotiated");

        // CONNECT and the target's TLS run over the proxy's TLS; the socket's local end point stays.
        var securedDialed = dialed with { Connection = proxyConnection };
        return await OpenTunnelAsync(securedDialed, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> OpenSocksTunnelAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        ConnectTimings timings,
        CancellationToken cancellationToken)
    {
        if (await OpenSocksHopAsync(dialed.Connection, target, destination, proxy, cancellationToken).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        // As through an HTTP proxy, %{time_connect} is when the tunnel is open.
        return await SecureWhenAskedAsync(dialed, target, timings with { Connected = timeProvider.GetTimestamp() }, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The proxy a connect to <paramref name="target" /> tunnels through: its own
    /// <see cref="ConnectTarget.Proxy" />, else the <see cref="PreProxy" /> for a forward proxy,
    /// which is opened as a SOCKS tunnel to the forward proxy (BL-614), else none.
    /// </summary>
    private ProxyEndpoint? TunnelProxyOf(ConnectTarget target) =>
        target.Proxy ?? (target.IsForwardProxy ? PreProxy : null);

    /// <summary>
    /// The proxy <paramref name="proxy" /> is reached through: the <see cref="PreProxy" /> for an
    /// HTTP or HTTPS proxy when there is one, as curl 8.21.0 opens the SOCKS connection first and
    /// speaks to the HTTP proxy inside it (measured, BL-614); else the proxy itself.
    /// </summary>
    private ProxyEndpoint FirstHopTo(ProxyEndpoint proxy) =>
        PreProxy is { } socks && proxy.Kind is ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https ? socks : proxy;

    /// <summary>
    /// Runs the SOCKS handshake to <paramref name="destination" /> over <paramref name="connection" />:
    /// <see langword="null" /> once the tunnel is open, else the failure, with the connection disposed.
    /// </summary>
    private async ValueTask<ConnectResult?> OpenSocksHopAsync(
        IConnection connection,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint socks,
        CancellationToken cancellationToken)
    {
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        log.SocksHandshakeStarting(socks, destination.Host, destination.Port);
        var (failure, exception) = await RunSocksHandshakeAsync(connection, destination, socks, target, cancellationToken).ConfigureAwait(false);
        if (exception is not null || failure is not null)
        {
            // The proxy connection is disposed whether the handshake failed or could not be sent or read.
            await connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();
            return failure!;
        }

        log.TunnelEstablished(socks, destination.Host, destination.Port, statusCode: 0);
        return null;
    }

    /// <summary>
    /// Sends CONNECT with <paramref name="proxyAuthorization" /> and, when the proxy answers
    /// <c>407</c> and the authenticator answers its challenge, sends it again with that answer,
    /// as curl 8.21.0 does (BL-602): on this connection when the reply leaves it reusable, else
    /// by returning the answer for a new one. A <c>407</c> to a CONNECT that sent a credential
    /// is answered only by a handshake of more than one leg - NTLM's Type 3 for its Type 2, a
    /// Negotiate context's next token (BL-604, ADR-0270) - and is else the tunnel's failure.
    /// </summary>
    private async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> OpenTunnelAsync(
        DialedSocket dialed,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        var connection = dialed.Connection;
        var log = new NetworkDiagnosticLog(tunnel.Target.DiagnosticLog);
        var answersChallenge = tunnel.AuthorizationAnswersChallenge;
        while (true)
        {
            log.TunnelRequested(tunnel.Proxy, tunnel.Destination.Host, tunnel.Destination.Port);
            var (reply, exception) = await RequestTunnelAsync(connection, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false);
            if (exception is null && reply.OpensTunnel)
            {
                EndProxyAuthorization(proxyAuthorization);

                // For a tunnel, %{time_connect} is when the tunnel is open (ConnectTimings.Connected).
                var timings = new ConnectTimings(tunnel.Started, tunnel.NameResolved, timeProvider.GetTimestamp(), null);
                log.TunnelEstablished(tunnel.Proxy, tunnel.Destination.Host, tunnel.Destination.Port, reply.StatusCode);
                return (await SecureWhenAskedAsync(dialed, tunnel.Target, timings, reply.StatusCode, cancellationToken).ConfigureAwait(false), null);
            }

            var (answer, onThisConnection, failure) = exception is null
                ? await AnswerProxyChallengeAsync(connection, reply, tunnel, proxyAuthorization, answersChallenge, cancellationToken).ConfigureAwait(false)
                : (null, false, null);
            if (failure is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                return (failure, null);
            }

            if (!onThisConnection)
            {
                return await CloseUnopenedTunnelAsync(connection, reply, exception, answer).ConfigureAwait(false);
            }

            proxyAuthorization = answer;
            answersChallenge = true;
        }
    }

    /// <summary>
    /// Tells the authenticator the handshake behind <paramref name="sentAuthorization" /> is
    /// over because the tunnel opened, so a Negotiate context kept for its next leg is disposed
    /// of, as the HTTP handler does for a response that is not a <c>407</c> (ADR-0248).
    /// </summary>
    private void EndProxyAuthorization(string? sentAuthorization)
    {
        if (sentAuthorization is not null)
        {
            ProxyAuthenticator.EndAuthorization(sentAuthorization);
        }
    }

    /// <summary>
    /// Disposes the proxy connection whether CONNECT failed, could not be sent or read, or is
    /// to be sent again on a new connection; then rethrows, or returns the tunnel's failure, or
    /// <paramref name="redialAuthorization" /> for the new connection.
    /// </summary>
    private static async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> CloseUnopenedTunnelAsync(
        IConnection connection,
        HttpProxyTunnelReply reply,
        ExceptionDispatchInfo? exception,
        string? redialAuthorization)
    {
        await connection.DisposeAsync().ConfigureAwait(false);
        exception?.Throw();
        return redialAuthorization is null ? (TunnelFailure(reply), null) : (null, redialAuthorization);
    }

    /// <summary>
    /// The <c>Proxy-Authorization</c> that answers <paramref name="reply" />, and whether it goes
    /// on the same connection, its body discarded: none unless the reply is a <c>407</c> the
    /// authenticator answers - afresh when the CONNECT sent nothing, and through
    /// <see cref="IHttpAuthenticator.ContinueAuthorizationAsync" /> when it sent a credential, which
    /// only NTLM and Negotiate go on from, so curl 8.21.0's giving up on Basic or Digest sent and
    /// challenged again stays. An empty answer (<c>--proxy-anyauth</c> picking a Negotiate
    /// context that makes no token) sends nothing more. A failure the authenticator reports,
    /// such as an NTLM Type 2 SSPI cannot answer, is the tunnel's failure with its exit code.
    /// </summary>
    private async ValueTask<(string? Answer, bool OnThisConnection, ConnectResult? Failure)> AnswerProxyChallengeAsync(
        IConnection connection,
        HttpProxyTunnelReply reply,
        TunnelRequest tunnel,
        string? sentAuthorization,
        bool sentAnswersChallenge,
        CancellationToken cancellationToken)
    {
        if (reply.StatusCode != 407)
        {
            EndProxyAuthorization(sentAuthorization);
            return (null, false, null);
        }

        string? answer;
        try
        {
            answer = sentAuthorization is null
                ? await CreateProxyAuthorizationAsync(tunnel.Destination, tunnel.Proxy, reply.ProxyAuthenticate, cancellationToken).ConfigureAwait(false)
                : await ProxyAuthenticator.ContinueAuthorizationAsync(ProxyAuthRequestOf(tunnel.Destination, tunnel.Proxy), sentAuthorization, !sentAnswersChallenge, reply.ProxyAuthenticate, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpAuthenticationFailedException failure)
        {
            return (null, false, ConnectResult.Failed(failure.ExitCode, failure.Message));
        }

        if (string.IsNullOrEmpty(answer))
        {
            return (null, false, null);
        }

        return (answer, reply.LeavesConnectionReusable
            && await HttpProxyTunnel.DiscardBodyAsync(connection, reply.ContentLength, cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>
    /// Sends one CONNECT and reads the proxy's reply, whose complete head goes to the
    /// transfer's header output when its events take it (<see cref="IConnectReplyHeadWritingEvents" />),
    /// whatever the status, as curl 8.21.0 writes a <c>407</c>'s and a <c>403</c>'s too (BL-613 Notes).
    /// </summary>
    private async ValueTask<(HttpProxyTunnelReply Reply, ExceptionDispatchInfo? Exception)> RequestTunnelAsync(
        IConnection connection,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        try
        {
            var destination = tunnel.Destination;
            await connection.WriteAsync(HttpProxyTunnel.BuildConnectRequest(destination.Host, destination.Port, tunnel.Proxy, _proxyTunnelOptions, proxyAuthorization), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            var reply = await HttpProxyTunnel.ReadReplyAsync(connection, cancellationToken).ConfigureAwait(false);
            if (tunnel.Target.Events is IConnectReplyHeadWritingEvents headOutput && !reply.Head.IsEmpty)
            {
                await headOutput.WriteConnectReplyHeadAsync(reply.Head, cancellationToken).ConfigureAwait(false);
            }

            return (reply, null);
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
        ConnectTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await SocksProxyTunnel.OpenAsync(
                connection,
                proxy,
                destination.Host,
                destination.Port,
                (host, port, token) => ResolveAsync(host, port, target, token),
                Socks5Authentication,
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
            connectionNumber,
            unixSocketPath: dialed.UnixSocketPath);
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
            target,
            target.IsForwardProxy,
            ApplicationProtocolsFor(target, HttpOverTlsApplicationProtocols),
            cancellationToken);

    /// <summary>
    /// Returns the protocols the handshake with <paramref name="target" /> offers through ALPN:
    /// <paramref name="httpOverTls" /> for HTTP over TLS to the origin, the one target the HTTP
    /// handler pools as <c>https</c> (BL-490, ADR-0141); <c>http/1.1</c> alone for an HTTPS
    /// forward proxy, whatever the HTTP version options say, as both curl 8.21.0 builds offer it
    /// (measured, BL-753, ADR-0190); nothing for any other protocol, which curl offers no ALPN.
    /// </summary>
    /// <param name="target">The target whose handshake is about to run.</param>
    /// <param name="httpOverTls">What HTTP over TLS to the origin offers.</param>
    /// <returns>The protocols, in preference order; empty to offer none.</returns>
    internal static IReadOnlyList<string> ApplicationProtocolsFor(ConnectTarget target, IReadOnlyList<string> httpOverTls) =>
        target.IsForwardProxy
            ? HttpApplicationProtocols.Http11Only
            : string.Equals(target.PoolScheme, "https", StringComparison.OrdinalIgnoreCase)
                ? httpOverTls
                : [];

    // A provider that can report its handshake reports its trust and handshake on the
    // target's events, marked as the proxy's when it is with an HTTPS proxy (BL-404, BL-452),
    // and offers the target's application protocols through ALPN (BL-490).
    // Either way the handshake's outcome goes to the diagnostic log (BL-920): the events are
    // wrapped to catch the reported handshake only when info is enabled, so with the log off the
    // provider receives the target's own events.
    private static async ValueTask<ConnectResult> AuthenticateAsync(
        ITlsProvider provider,
        IConnection plaintext,
        string host,
        ConnectTarget target,
        bool isProxy,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken)
    {
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        if (provider is not IHandshakeReportingTlsProvider reportingProvider)
        {
            var unreported = await provider.AuthenticateAsClientAsync(plaintext, host, cancellationToken).ConfigureAwait(false);
            LogHandshake(log, host, route: null, handshake: null, unreported);
            return unreported;
        }

        var capturing = log.IsEnabled(DiagnosticLogLevel.Info) ? new HandshakeCapturingTransferEvents(target.Events) : null;
        var secured = await reportingProvider.AuthenticateAsClientAsync(plaintext, host, capturing ?? target.Events, isProxy, applicationProtocols, cancellationToken).ConfigureAwait(false);
        LogHandshake(log, host, reportingProvider.Route, capturing?.Handshake, secured);
        return secured;
    }

    private static void LogHandshake(NetworkDiagnosticLog log, string host, TlsClientRoute? route, TlsHandshakeEvent? handshake, ConnectResult secured)
    {
        if (secured.Connection is null)
        {
            log.Failed(DiagnosticLogComponents.Tls, secured.ExitCode, secured.ErrorMessage);
        }
        else
        {
            log.HandshakeCompleted(host, route, handshake, secured.ApplicationProtocol);
        }
    }

    /// <summary>
    /// Races the address families of <paramref name="addresses" /> through an
    /// <see cref="AddressFamilyRace" /> and returns the first connection, or <see langword="null" />
    /// with the <see cref="SocketError" /> of the attempt that failed last, which curl keeps as
    /// <c>CURLINFO_OS_ERRNO</c>, and its <see cref="LocalBindFailure" /> when its local end could not be bound.
    /// </summary>
    private async ValueTask<(DialedSocket? Dialed, SocketError LastError, LocalBindFailure? LastBindFailure)> DialFirstReachableAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        string hostName,
        ConnectTarget target,
        CancellationToken cancellationToken)
    {
        var race = new AddressFamilyRace(BindingDialer(), timeProvider, HappyEyeballsTimeout, target.Events, new NetworkDiagnosticLog(target.DiagnosticLog));
        var (dialed, remoteEndPoint, lastError, lastBindFailure) = await race.DialAsync(addresses, port, cancellationToken).ConfigureAwait(false);
        return dialed is null
            ? (null, lastError, lastBindFailure)
            : (new DialedSocket(dialed.Connection, dialed.LocalEndPoint, hostName, remoteEndPoint), SocketError.Success, null);
    }

    /// <summary>
    /// The reason that ends a failed connect's message: curl's text for <c>CURLE_INTERFACE_FAILED</c> or
    /// <c>CURLE_BAD_FUNCTION_ARGUMENT</c> when the last address failed to bind its local end with
    /// one of them, <c>Could not connect to server</c> otherwise (measured, BL-600 Notes).
    /// </summary>
    private static string DialFailureText(LocalBindFailure? lastBindFailure) => lastBindFailure switch
    {
        LocalBindFailure.InterfaceFailed => "Failed binding local connection end",
        LocalBindFailure.BadArgument => "A libcurl function was given a bad argument",
        _ => "Could not connect to server",
    };

    /// <summary>
    /// The failure for a dial that reached no address, its message also reported on
    /// <paramref name="events" /> as curl's <c>-v</c> repeats it: exit 45 or exit 43 when the last
    /// address failed to bind its local end with <see cref="LocalBindFailure.InterfaceFailed" /> or
    /// <see cref="LocalBindFailure.BadArgument" />, else exit 7, marked refused when the last
    /// attempt was refused, as <c>--retry-connrefused</c> reads curl's <c>CURLINFO_OS_ERRNO</c>.
    /// It carries the start and lookup timestamps, as curl 8.21.0 still reports
    /// <c>%{time_namelookup}</c> after a refused connect (measured, ADR-0091).
    /// </summary>
    private static ConnectResult DialFailure(ITransferEvents events, SocketError lastError, LocalBindFailure? lastBindFailure, ConnectTimings timings, string errorMessage)
    {
        events.ReportInfo(errorMessage);
        return lastBindFailure switch
        {
            LocalBindFailure.InterfaceFailed => ConnectResult.Failed(CurlExitCode.InterfaceFailed, errorMessage, timings),
            LocalBindFailure.BadArgument => ConnectResult.Failed(CurlExitCode.BadFunctionArgument, errorMessage, timings),
            _ when lastError == SocketError.ConnectionRefused => ConnectResult.Refused(errorMessage, timings),
            _ => ConnectResult.Failed(CurlExitCode.CouldntConnect, errorMessage, timings),
        };
    }

    /// <summary>
    /// A dialled TCP connection with the name and address it was dialled for: the connection
    /// the transfer talks over, which becomes the proxy's TLS stream through an HTTPS proxy.
    /// </summary>
    /// <remarks>
    /// Through a Unix domain socket the host name is the socket's path, the two end points are
    /// <see langword="null" />, <paramref name="UnixSocketRemoteIp" /> is what curl shows instead and
    /// <paramref name="UnixSocketPath" /> is the whole path, which the HTTP handler names in its
    /// left-intact line (BL-884).
    /// </remarks>
    private sealed record DialedSocket(
        IConnection Connection,
        IPEndPoint? LocalEndPoint,
        string HostName,
        IPEndPoint? RemoteEndPoint,
        string? UnixSocketRemoteIp = null,
        string? UnixSocketPath = null);

    /// <summary>
    /// What a CONNECT tunnel is opened for: the target, the destination named in the CONNECT,
    /// the proxy, the connect's start and lookup timestamps, and whether the first CONNECT's
    /// <c>Proxy-Authorization</c> answers a <c>407</c> the proxy sent on a connection it closed,
    /// rather than being the value made before any challenge.
    /// </summary>
    private sealed record TunnelRequest(
        ConnectTarget Target,
        ConnectDestination Destination,
        ProxyEndpoint Proxy,
        long Started,
        long NameResolved,
        bool AuthorizationAnswersChallenge);

    /// <summary>
    /// One key of curl's DNS cache: the host as it was cached (the name looked up, or a
    /// <c>--resolve</c> entry's host as written), the port and the addresses.
    /// </summary>
    private sealed record DnsCacheEntry(string Name, int Port, IReadOnlyList<IPAddress> Addresses);
}
