using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

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
/// <param name="haproxyProtocol">
/// The HAProxy PROXY protocol v1 line <c>--haproxy-protocol</c> or <c>--haproxy-clientip</c> asks for,
/// written first on every new connection once it is dialled and any proxy tunnel is open, before the
/// target's TLS handshake, from the socket's own ends (BL-616); <see langword="null" /> to send none.
/// </param>
/// <param name="dnsCache">
/// The DNS cache this connector answers from and adds to, which the connectors of every
/// <c>-:</c>/<c>--next</c> option group of one run share as curl 8.21.0 shares its cache (BL-1053);
/// <see langword="null" /> for a cache of this connector's own.
/// </param>
public sealed partial class TcpConnector(
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
    Socks5AuthenticationOptions? socks5Authentication = null,
    HaproxyProtocolHeader? haproxyProtocol = null,
    DnsCache? dnsCache = null) : IConnector
{
    private const string AnyHost = "*";

    // curl 8.21.0 dials an HTTP proxy that keeps closing on a 407 at most five more times, then
    // fails with "Could not connect to server" (measured with stale Digest challenges, BL-864 Notes).
    private const int MaxProxyReconnects = 5;

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

    /// <summary>
    /// Gets the HAProxy PROXY protocol v1 line written first on every new connection, or
    /// <see langword="null" /> for none (<c>--haproxy-protocol</c>, <c>--haproxy-clientip</c>, BL-616).
    /// </summary>
    public HaproxyProtocolHeader? HaproxyProtocol => haproxyProtocol;

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
    public IReadOnlyList<string> HttpOverTlsApplicationProtocols =>
        httpOverTlsApplicationProtocols ?? HttpApplicationProtocols.Http11Only;

    private readonly ITlsProvider _proxyTlsProvider = proxyTlsProvider ?? tlsProvider;
    private readonly HttpProxyTunnelOptions _proxyTunnelOptions = proxyTunnelOptions ?? HttpProxyTunnelOptions.Default;
    private readonly ResolveOverrides _resolveOverrides = resolveOverrides ?? ResolveOverrides.None;
    private readonly ConnectToMappings _connectToMappings = connectToMappings ?? ConnectToMappings.None;
    private readonly DnsCache _dnsCache = dnsCache ?? new DnsCache();

    // The host:port keys an IHttpsOriginDnsResolver has answered an HTTPS-RR resolve for: curl 8.22.0
    // caches that answer as an entry of its own, beside the addresses (ADR-0727's BL-2986 amendment).
    private readonly ConcurrentDictionary<string, bool> _httpsRecordsResolved = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the DNS cache this connector answers from and adds to: the <c>dnsCache</c> it was
    /// given, or one of its own.
    /// </summary>
    public DnsCache DnsCache => _dnsCache;

    /// <summary>
    /// Gets a value indicating whether a <c>--resolve</c> entry's addresses are shuffled with
    /// <see cref="DnsAddressShuffle" /> as <see cref="LoadResolveEntries" /> puts them in the DNS cache, for
    /// <c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c>, as curl 8.22.0's <c>dnsc_entry_assign_addr</c> shuffles every
    /// address entry (BL-2977). The <c>Added H:P:A to DNS cache</c> line keeps the entry as written.
    /// </summary>
    public bool ShufflesResolveEntryAddresses { get; init; }

    /// <summary>
    /// Gets a value indicating whether a direct connect's <c>https://</c> origin is resolved through
    /// <see cref="IHttpsOriginDnsResolver.ResolveHttpsOriginAsync" />, asking its HTTPS record first,
    /// as curl 8.22.0's HTTPSRR build does (ADR-0727). The default is off on every platform, as neither
    /// reference build (Schannel on Windows, OpenSSL elsewhere) has <c>HTTPSRR</c>: each sends only the A and AAAA DoH
    /// queries (ADR-0856, ADR-0957, CGR-157).
    /// </summary>
    public bool AsksHttpsRecords { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>-4</c> or <c>-6</c> also filters an IP address literal host, so
    /// <c>-6 http://127.0.0.1/</c> has nothing to dial and fails with exit 7, as curl 8.18.0 on Linux does
    /// (CGR-186). The default is the platform build's: off on Windows, where the literal is dialled as written.
    /// </summary>
    public bool FiltersAddressLiterals { get; init; } = !OperatingSystem.IsWindows();

    // Gives a random index below its argument for ShufflesResolveEntryAddresses; tests pin it.
    internal Func<int, int> ResolveEntryShuffleRandomBelow { get; init; } = DnsAddressShuffle.CryptographicRandomBelow;

    /// <summary>
    /// Gets a value indicating whether a direct connect writes the <c>[DNS]</c> lines curl 8.21.0
    /// writes for its DNS connection filter under <c>--trace-config dns</c>, <c>doh</c> or <c>all</c>
    /// (<see cref="DnsFilterTraceEvents" />, BL-1102).
    /// </summary>
    public bool TracesDnsFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a direct connect writes the <c>[SETUP]</c> lines curl 8.21.0
    /// writes for its connection setup filter under <c>-vv</c> and up, <c>--trace-config setup</c> or
    /// <c>all</c> (<see cref="SetupFilterTraceEvents" />, BL-1103).
    /// </summary>
    public bool TracesSetupFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a connection that writes the <see cref="HaproxyProtocol" />
    /// line reports curl 8.21.0's <c>[HAPROXY] removing connected setup filter</c> and
    /// <c>[HAPROXY] destroy</c> after <c>Established connection</c>, under <c>--trace-config
    /// haproxy</c>, <c>proxy</c> or <c>all</c> (measured, BL-1160 Notes). The setup filter's own
    /// <see cref="HaproxyFilterAddedLine" /> follows <see cref="TracesSetupFilter" /> instead.
    /// </summary>
    public bool TracesHaproxyFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a SOCKS handshake, to a SOCKS proxy or a <c>--preproxy</c>,
    /// writes curl 8.21.0's <c>[SOCKS]</c> lines as it runs, under <c>--trace-config socks</c>,
    /// <c>proxy</c> or <c>all</c> (<see cref="SocksProxyTunnel" />, measured, BL-1191 Notes).
    /// </summary>
    public bool TracesSocksFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a CONNECT tunnel through a plain HTTP or HTTPS proxy writes curl
    /// 8.21.0's <c>[HTTP-PROXY]</c> lines under <c>--trace-config http-proxy</c>, <c>proxy</c> or a
    /// named <c>all</c> (<see cref="HttpProxyTunnelTrace" />, measured, BL-1193 Notes).
    /// </summary>
    public bool TracesHttpProxyFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a CONNECT tunnel through a plain HTTP or HTTPS proxy writes curl
    /// 8.21.0's <c>[H1-PROXY]</c> lines under <c>--trace-config h1-proxy</c>, <c>proxy</c> or a
    /// named <c>all</c> (<see cref="HttpProxyTunnelTrace" />, measured, BL-1193 Notes).
    /// </summary>
    public bool TracesH1ProxyFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether an origin's TLS handshake writes curl 8.21.0's <c>[SSL]</c> lines
    /// under <c>--trace-config ssl</c>, <c>network</c> or <c>all</c> (<see cref="SslFilterTrace" />,
    /// measured, BL-1287 Notes).
    /// </summary>
    public bool TracesSslFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether an HTTPS proxy's TLS handshake writes curl 8.21.0's
    /// <c>[SSL-PROXY]</c> lines under <c>--trace-config proxy</c> or a named <c>all</c>, but not
    /// <c>ssl</c> or <c>network</c> (<see cref="SslFilterTrace" />, measured, BL-1287 Notes).
    /// </summary>
    public bool TracesSslProxyFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a direct connect writes the <c>[HAPPY-EYEBALLS]</c> lines curl
    /// 8.21.0 writes around its connect attempts under <c>--trace-config happy-eyeballs</c>,
    /// <c>network</c> or <c>all</c> (<see cref="ConnectAttemptTraceEvents" />, BL-1161).
    /// </summary>
    public bool TracesHappyEyeballsFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a direct connect writes the <c>[TCP]</c> lines curl 8.21.0
    /// writes for its sockets as they open, connect or fail under <c>--trace-config tcp</c>,
    /// <c>network</c> or <c>all</c> (<see cref="ConnectAttemptTraceEvents" />, BL-1161).
    /// </summary>
    public bool TracesTcpFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a direct connect writes the <c>[TIMER] [HAPPY_EYEBALLS]</c>
    /// lines curl 8.21.0 writes as it sets and clears its second-family timer, and the
    /// <c>[TIMER] [CONNECTTIMEOUT]</c> lines of a given <c>--connect-timeout</c>, under
    /// <c>--trace-config timer</c>, <c>network</c> or <c>all</c> (<see cref="ConnectAttemptTraceEvents" />,
    /// BL-1186, BL-1210).
    /// </summary>
    public bool TracesTimers { get; init; }

    /// <summary>
    /// Gets a value indicating whether, under <see cref="TracesTimers" />, a direct connect also writes
    /// the <c>[TIMER] ... expires in</c> lines curl 8.21.0 writes only when its multi is traced too:
    /// <c>timer</c> with <c>multi</c>, <c>network</c> or <c>all</c> (measured, BL-1210 Notes).
    /// </summary>
    public bool TracesTimerExpiry { get; init; }

    /// <summary>
    /// Gets the <c>--connect-timeout</c> whose <c>[TIMER] [CONNECTTIMEOUT]</c> lines a direct connect
    /// writes under <see cref="TracesTimers" />, or <see langword="null" /> when none, or 0, was given:
    /// curl's default connect timeout writes no such line (measured, BL-1210 Notes), a longer or shorter
    /// <c>-m</c> leaves it written (measured, BL-1258 Notes).
    /// </summary>
    public TimeSpan? TracedConnectTimeout { get; init; }

    /// <summary>
    /// Gets the <c>-m</c> whose <c>[TIMER] [TIMEOUT]</c> lines a direct connect writes under
    /// <see cref="TracesTimers" />, or <see langword="null" /> when none, or 0, was given (measured,
    /// BL-1258 Notes).
    /// </summary>
    public TimeSpan? TracedTransferTimeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether a direct connect to an <c>https://</c> origin writes the
    /// <c>[HTTPS-CONNECT]</c> lines curl 8.21.0 writes for its ALPN connect filter under
    /// <c>--trace-config https-connect</c> or <c>all</c> (<see cref="HttpsConnectFilterTraceEvents" />,
    /// measured, BL-1192 Notes).
    /// </summary>
    public bool TracesHttpsConnectFilter { get; init; }

    /// <summary>
    /// Gets the HTTP version the <c>[HTTPS-CONNECT] 1st attempt uses &lt;version&gt; from wanted
    /// versions</c> line names under <see cref="TracesHttpsConnectFilter" />: <c>h2</c> (the default,
    /// even where the handshake offers only <c>http/1.1</c>), <c>h1</c> or <c>h3</c>.
    /// </summary>
    public string HttpsConnectFirstAttemptVersion { get; init; } = "h2";

    /// <summary>
    /// Gets the events a resolver built once per run reports to, such as the
    /// <see cref="DohDnsResolver" />'s <c>--trace-config doh</c> lines: before each look-up the
    /// connector points them at the resolving transfer's events (BL-1102); <see langword="null" />
    /// for none.
    /// </summary>
    public FlowScopedTransferEvents? ResolverEvents { get; init; }

    private readonly ConditionalWeakTable<ConnectTarget, object> _altSvcReported = new();
    private int _resolveEntriesLoaded;
    private long _nextConnectionNumber;

    private static TimeSpan HappyEyeballsTimeoutOrDefault(TimeSpan? happyEyeballsTimeout) =>
        happyEyeballsTimeout switch
        {
            null => DefaultHappyEyeballsTimeout,
            { } given when given < TimeSpan.Zero => TimeSpan.Zero,
            { } given when given > LongestTimerDelay => LongestTimerDelay,
            { } given => given,
        };
}
