namespace Curl.Api.Options.Network;

/// <summary>
/// The connection, name resolution and connection-reuse options of one easy handle: the
/// <c>curl_easy_setopt</c> options of the network group (ADR-0506), each defaulting to
/// libcurl 8.22.0's default. Entry syntaxes (<c>CURLOPT_RESOLVE</c>, <c>CURLOPT_CONNECT_TO</c>,
/// <c>CURLOPT_PROTOCOLS_STR</c>) are carried as libcurl takes them; parsing them is the
/// engine's job.
/// </summary>
public sealed record EasyNetworkOptions
{
    /// <summary><c>CURLOPT_URL</c>: the URL to transfer. Default <see langword="null"/> (no URL).</summary>
    public string? Url { get; init; }

    /// <summary>
    /// <c>CURLOPT_CURLU</c>: the full URL of the <c>CURLU</c> handle set on the easy handle, which
    /// libcurl uses in place of <c>CURLOPT_URL</c> when set. Default <see langword="null"/> (no handle).
    /// </summary>
    public string? UrlFromUrlHandle { get; init; }

    /// <summary>
    /// <c>CURLOPT_PATH_AS_IS</c>: send the URL path without squashing <c>/../</c> and <c>/./</c>.
    /// Default <see langword="false"/>.
    /// </summary>
    public bool PathAsIs { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROTOCOLS_STR</c>: the comma-separated protocols a transfer may use. Default
    /// <see langword="null"/>, meaning every protocol built in.
    /// </summary>
    public string? Protocols { get; init; }

    /// <summary>
    /// <c>CURLOPT_REDIR_PROTOCOLS_STR</c>: the comma-separated protocols a redirect may go to.
    /// Default <see langword="null"/>, meaning HTTP, HTTPS, FTP and FTPS.
    /// </summary>
    public string? RedirectProtocols { get; init; }

    /// <summary>
    /// <c>CURLOPT_DEFAULT_PROTOCOL</c>: the scheme to use for a URL without one. Default
    /// <see langword="null"/>, meaning libcurl guesses from the host name and falls back to HTTP.
    /// </summary>
    public string? DefaultProtocol { get; init; }

    /// <summary><c>CURLOPT_PORT</c>: the remote port to connect to. Default 0, meaning the URL's or the protocol's port.</summary>
    public int Port { get; init; }

    /// <summary>
    /// <c>CURLOPT_INTERFACE</c>: the outgoing interface, address or host name (with the
    /// <c>if!</c>, <c>host!</c> and <c>ifhost!</c> prefixes). Default <see langword="null"/>, meaning any.
    /// </summary>
    public string? Interface { get; init; }

    /// <summary><c>CURLOPT_LOCALPORT</c>: the first local port to bind to. Default 0, meaning any.</summary>
    public int LocalPort { get; init; }

    /// <summary><c>CURLOPT_LOCALPORTRANGE</c>: how many local ports from <see cref="LocalPort"/> to try. Default 1.</summary>
    public int LocalPortRange { get; init; } = 1;

    /// <summary>
    /// <c>CURLOPT_CONNECT_TO</c>: <c>HOST:PORT:CONNECT-TO-HOST:CONNECT-TO-PORT</c> entries. Default
    /// empty (libcurl's <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> ConnectTo { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_RESOLVE</c>: <c>[+]HOST:PORT:ADDRESS[,ADDRESS]</c> and <c>-HOST:PORT</c> entries.
    /// Default empty (libcurl's <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> Resolve { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_DNS_CACHE_TIMEOUT</c> (and <c>CURLOPT_DNS_CACHE_TIMEOUT_MS</c>): how long a resolved
    /// name stays cached. Default 60 seconds; zero disables the cache and
    /// <see cref="Timeout.InfiniteTimeSpan"/> (libcurl's -1) keeps entries forever.
    /// </summary>
    public TimeSpan DnsCacheTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_DNS_SERVERS</c>: comma-separated DNS servers. Default <see langword="null"/>, meaning the system's.</summary>
    public string? DnsServers { get; init; }

    /// <summary><c>CURLOPT_DNS_INTERFACE</c>: the interface DNS queries go out on. Default <see langword="null"/>.</summary>
    public string? DnsInterface { get; init; }

    /// <summary><c>CURLOPT_DNS_LOCAL_IP4</c>: the local IPv4 address DNS queries bind to. Default <see langword="null"/>.</summary>
    public string? DnsLocalIPv4 { get; init; }

    /// <summary><c>CURLOPT_DNS_LOCAL_IP6</c>: the local IPv6 address DNS queries bind to. Default <see langword="null"/>.</summary>
    public string? DnsLocalIPv6 { get; init; }

    /// <summary><c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c>: shuffle a name's addresses before use. Default <see langword="false"/>.</summary>
    public bool DnsShuffleAddresses { get; init; }

    /// <summary><c>CURLOPT_DOH_URL</c>: the DNS-over-HTTPS server URL. Default <see langword="null"/>, meaning no DoH.</summary>
    public string? DohUrl { get; init; }

    /// <summary><c>CURLOPT_IPRESOLVE</c>: the IP versions a name may resolve to. Default <see cref="IpResolve.Whatever"/>.</summary>
    public IpResolve IpResolve { get; init; } = IpResolve.Whatever;

    /// <summary><c>CURLOPT_TCP_NODELAY</c>: disable Nagle's algorithm. Default <see langword="true"/>.</summary>
    public bool TcpNoDelay { get; init; } = true;

    /// <summary><c>CURLOPT_TCP_FASTOPEN</c>: use TCP Fast Open. Default <see langword="false"/>.</summary>
    public bool TcpFastOpen { get; init; }

    /// <summary><c>CURLOPT_TCP_KEEPALIVE</c>: send TCP keepalive probes. Default <see langword="false"/>.</summary>
    public bool TcpKeepAlive { get; init; }

    /// <summary><c>CURLOPT_TCP_KEEPIDLE</c>: idle time before the first keepalive probe. Default 60 seconds.</summary>
    public TimeSpan TcpKeepIdle { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_TCP_KEEPINTVL</c>: time between keepalive probes. Default 60 seconds.</summary>
    public TimeSpan TcpKeepInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_TCP_KEEPCNT</c>: unanswered keepalive probes before the connection is dropped. Default 9.</summary>
    public int TcpKeepCount { get; init; } = 9;

    /// <summary>
    /// The IP Type of Service byte each socket is given (the curl tool's <c>--ip-tos</c>, which it sets
    /// through <c>CURLOPT_SOCKOPTFUNCTION</c> rather than an option of its own; ADR-0513). Default 0,
    /// meaning the socket's own.
    /// </summary>
    public int IpTypeOfService { get; init; }

    /// <summary>
    /// The VLAN priority each socket is given (the curl tool's <c>--vlan-priority</c>, set through
    /// <c>CURLOPT_SOCKOPTFUNCTION</c>; ADR-0513). Default 0, meaning the socket's own.
    /// </summary>
    public int VlanPriority { get; init; }

    /// <summary>
    /// Open TCP sockets as Multipath TCP (the curl tool's <c>--mptcp</c>, set through
    /// <c>CURLOPT_OPENSOCKETFUNCTION</c>; ADR-0513). Default <see langword="false"/>.
    /// </summary>
    public bool MultipathTcp { get; init; }

    /// <summary><c>CURLOPT_ADDRESS_SCOPE</c>: the IPv6 scope id for link-local addresses. Default 0.</summary>
    public long AddressScope { get; init; }

    /// <summary><c>CURLOPT_UNIX_SOCKET_PATH</c>: connect through this Unix domain socket. Default <see langword="null"/>.</summary>
    public string? UnixSocketPath { get; init; }

    /// <summary>
    /// <c>CURLOPT_ABSTRACT_UNIX_SOCKET</c>: connect through this abstract Unix domain socket.
    /// Default <see langword="null"/>.
    /// </summary>
    public string? AbstractUnixSocket { get; init; }

    /// <summary>
    /// <c>CURLOPT_CONNECTTIMEOUT</c> (and <c>CURLOPT_CONNECTTIMEOUT_MS</c>): the longest the connect
    /// phase may take. Default zero, libcurl's stored default, which it takes as 300 seconds and for
    /// which it sets no <c>CONNECTTIMEOUT</c> timer (ADR-0513).
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS</c>: the head start the first IP family gets before the
    /// other is tried. Default 200 milliseconds.
    /// </summary>
    public TimeSpan HappyEyeballsTimeout { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary><c>CURLOPT_FRESH_CONNECT</c>: use a new connection instead of a cached one. Default <see langword="false"/>.</summary>
    public bool FreshConnect { get; init; }

    /// <summary><c>CURLOPT_FORBID_REUSE</c>: close the connection after the transfer. Default <see langword="false"/>.</summary>
    public bool ForbidReuse { get; init; }

    /// <summary><c>CURLOPT_MAXCONNECTS</c>: the size of the connection cache. Default 5.</summary>
    public int MaxConnects { get; init; } = 5;

    /// <summary><c>CURLOPT_MAXAGE_CONN</c>: the longest a cached connection may sit idle and still be reused. Default 118 seconds.</summary>
    public TimeSpan MaxIdleConnectionAge { get; init; } = TimeSpan.FromSeconds(118);

    /// <summary>
    /// <c>CURLOPT_MAXLIFETIME_CONN</c>: the oldest a connection may be and still be reused. Default
    /// 24 hours (since curl 8.17.0); zero means no limit.
    /// </summary>
    public TimeSpan MaxConnectionLifetime { get; init; } = TimeSpan.FromHours(24);

    /// <summary><c>CURLOPT_CONNECT_ONLY</c>: stop after connecting. Default <see cref="ConnectOnly.Disabled"/>.</summary>
    public ConnectOnly ConnectOnly { get; init; } = ConnectOnly.Disabled;

    /// <summary>
    /// <c>CURLOPT_ACCEPTTIMEOUT_MS</c>: how long to wait for the server to connect back on an active
    /// FTP data connection. Default 60 seconds.
    /// </summary>
    public TimeSpan AcceptTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_UPKEEP_INTERVAL_MS</c>: the interval between connection upkeep calls. Default 60 seconds.</summary>
    public TimeSpan UpkeepInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// <c>CURLOPT_SERVER_RESPONSE_TIMEOUT</c> (and <c>CURLOPT_SERVER_RESPONSE_TIMEOUT_MS</c>): how
    /// long to wait for a server's response on an FTP, IMAP, POP3, SMTP or SFTP control connection.
    /// Default 60 seconds.
    /// </summary>
    public TimeSpan ServerResponseTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_USE_SSL</c>: whether the transfer must upgrade to TLS. Default <see cref="UseSsl.None"/>.</summary>
    public UseSsl UseSsl { get; init; } = UseSsl.None;
}
