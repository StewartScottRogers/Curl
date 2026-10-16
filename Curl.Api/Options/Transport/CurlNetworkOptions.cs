using Curl.Api.Options.Network;

namespace Curl.Api;

/// <summary>
/// Connection, name resolution, timeout and connection-reuse settings, mapped one to one onto
/// <see cref="EasyNetworkOptions"/> (BL-2502, ADR-0636). Each member starts at libcurl 8.22.0's
/// default, so <c>new CurlNetworkOptions()</c> changes nothing. Set on <see cref="CurlClientOptions.Network"/>
/// for every request or on <see cref="CurlRequest.Network"/> for one; a request's record replaces the
/// client's whole. An invalid value throws as it is set. <c>CURLOPT_URL</c> and <c>CURLOPT_CURLU</c>
/// are left out because <see cref="CurlRequest.Url"/> owns the URL, and <c>CURLOPT_CONNECT_ONLY</c>
/// because <c>CurlClient.ConnectWebSocketAsync</c> owns it.
/// </summary>
public sealed record CurlNetworkOptions
{
    /// <summary><c>CURLOPT_PATH_AS_IS</c>: send the URL path without squashing <c>/../</c> and <c>/./</c>. Default <see langword="false"/>.</summary>
    public bool PathAsIs { get; init; }

    /// <summary><c>CURLOPT_PROTOCOLS_STR</c>: the comma-separated protocols a transfer may use. Default <see langword="null"/>: every protocol.</summary>
    public string? Protocols { get; init; }

    /// <summary><c>CURLOPT_REDIR_PROTOCOLS_STR</c>: the comma-separated protocols a redirect may go to. Default <see langword="null"/>: HTTP, HTTPS, FTP and FTPS.</summary>
    public string? RedirectProtocols { get; init; }

    /// <summary><c>CURLOPT_DEFAULT_PROTOCOL</c>: the scheme for a URL without one. Default <see langword="null"/>: guessed from the host name.</summary>
    public string? DefaultProtocol { get; init; }

    /// <summary><c>CURLOPT_PORT</c>: the remote port, 0 to 65535. Default 0: the URL's or the protocol's port.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0 to 65535.</exception>
    public int Port
    {
        get;
        init => field = TransportOptionGuard.Port(value, nameof(Port));
    }

    /// <summary><c>CURLOPT_INTERFACE</c>: the outgoing interface, address or host name. Default <see langword="null"/>: any.</summary>
    public string? Interface { get; init; }

    /// <summary><c>CURLOPT_LOCALPORT</c>: the first local port to bind to, 0 to 65535. Default 0: any.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0 to 65535.</exception>
    public int LocalPort
    {
        get;
        init => field = TransportOptionGuard.Port(value, nameof(LocalPort));
    }

    /// <summary><c>CURLOPT_LOCALPORTRANGE</c>: how many local ports from <see cref="LocalPort"/> to try, 1 to 65535. Default 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 1 to 65535.</exception>
    public int LocalPortRange
    {
        get;
        init => field = (int)TransportOptionGuard.InRange(value, 1, 65535, nameof(LocalPortRange));
    } = 1;

    /// <summary><c>CURLOPT_CONNECT_TO</c>: <c>HOST:PORT:CONNECT-TO-HOST:CONNECT-TO-PORT</c> entries. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> ConnectTo
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(ConnectTo));
    } = [];

    /// <summary><c>CURLOPT_RESOLVE</c>: <c>HOST:PORT:ADDRESS[,ADDRESS]</c> entries. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> Resolve
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(Resolve));
    } = [];

    /// <summary>
    /// <c>CURLOPT_DNS_CACHE_TIMEOUT</c>: how long a resolved name stays cached. Default 60 seconds;
    /// zero disables the cache and <see cref="Timeout.InfiniteTimeSpan"/> keeps entries forever.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative and not <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
    public TimeSpan DnsCacheTimeout
    {
        get;
        init => field = TransportOptionGuard.NonNegativeOrInfinite(value, nameof(DnsCacheTimeout));
    } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_DNS_SERVERS</c>: the comma-separated name servers to use. Default <see langword="null"/>: the system's.</summary>
    public string? DnsServers { get; init; }

    /// <summary><c>CURLOPT_DNS_INTERFACE</c>: the interface name lookups go out on. Default <see langword="null"/>.</summary>
    public string? DnsInterface { get; init; }

    /// <summary><c>CURLOPT_DNS_LOCAL_IP4</c>: the local IPv4 address name lookups bind to. Default <see langword="null"/>.</summary>
    public string? DnsLocalIPv4 { get; init; }

    /// <summary><c>CURLOPT_DNS_LOCAL_IP6</c>: the local IPv6 address name lookups bind to. Default <see langword="null"/>.</summary>
    public string? DnsLocalIPv6 { get; init; }

    /// <summary><c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c>: shuffle a name's addresses before use. Default <see langword="false"/>.</summary>
    public bool DnsShuffleAddresses { get; init; }

    /// <summary><c>CURLOPT_DOH_URL</c>: the DNS-over-HTTPS server. Default <see langword="null"/>: ordinary name resolution.</summary>
    public string? DohUrl { get; init; }

    /// <summary><c>CURLOPT_IPRESOLVE</c>: which IP versions names resolve to. Default <see cref="IpResolve.Whatever"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not an <see cref="Options.Network.IpResolve"/> value.</exception>
    public IpResolve IpResolve
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(IpResolve));
    } = IpResolve.Whatever;

    /// <summary><c>CURLOPT_TCP_NODELAY</c>: disable Nagle's algorithm. Default <see langword="true"/>.</summary>
    public bool TcpNoDelay { get; init; } = true;

    /// <summary><c>CURLOPT_TCP_FASTOPEN</c>: use TCP Fast Open. Default <see langword="false"/>.</summary>
    public bool TcpFastOpen { get; init; }

    /// <summary><c>CURLOPT_TCP_KEEPALIVE</c>: send TCP keepalive probes. Default <see langword="false"/>.</summary>
    public bool TcpKeepAlive { get; init; }

    /// <summary><c>CURLOPT_TCP_KEEPIDLE</c>: idle time before the first keepalive probe. Default 60 seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan TcpKeepIdle
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(TcpKeepIdle));
    } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_TCP_KEEPINTVL</c>: time between keepalive probes. Default 60 seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan TcpKeepInterval
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(TcpKeepInterval));
    } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_TCP_KEEPCNT</c>: unanswered keepalive probes before the connection is dropped. Default 9.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int TcpKeepCount
    {
        get;
        init => field = (int)TransportOptionGuard.InRange(value, 0, int.MaxValue, nameof(TcpKeepCount));
    } = 9;

    /// <summary><c>CURLOPT_IP_TOS</c>: the IP type of service byte, 0 to 255. Default 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0 to 255.</exception>
    public int IpTypeOfService
    {
        get;
        init => field = (int)TransportOptionGuard.InRange(value, 0, 255, nameof(IpTypeOfService));
    }

    /// <summary><c>CURLOPT_VLAN_PRIORITY</c>: the VLAN priority, 0 to 7. Default 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0 to 7.</exception>
    public int VlanPriority
    {
        get;
        init => field = (int)TransportOptionGuard.InRange(value, 0, 7, nameof(VlanPriority));
    }

    /// <summary>Use Multipath TCP (curl's <c>--mptcp</c>). Default <see langword="false"/>.</summary>
    public bool MultipathTcp { get; init; }

    /// <summary><c>CURLOPT_ADDRESS_SCOPE</c>: the IPv6 scope id for link-local addresses. Default 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long AddressScope
    {
        get;
        init => field = TransportOptionGuard.InRange(value, 0, long.MaxValue, nameof(AddressScope));
    }

    /// <summary><c>CURLOPT_UNIX_SOCKET_PATH</c>: connect through this Unix domain socket. Default <see langword="null"/>.</summary>
    public string? UnixSocketPath { get; init; }

    /// <summary><c>CURLOPT_ABSTRACT_UNIX_SOCKET</c>: connect through this abstract Unix domain socket. Default <see langword="null"/>.</summary>
    public string? AbstractUnixSocket { get; init; }

    /// <summary>
    /// <c>CURLOPT_CONNECTTIMEOUT_MS</c>. Default <see langword="null"/>: keep the client's
    /// <see cref="CurlClientOptions.ConnectTimeout"/>; <see cref="TimeSpan.Zero"/> means libcurl's 300 seconds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan? ConnectTimeout
    {
        get;
        init => field = value is { } timeout ? TransportOptionGuard.NonNegative(timeout, nameof(ConnectTimeout)) : null;
    }

    /// <summary><c>CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS</c>: the head start IPv6 gets over IPv4. Default 200 milliseconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan HappyEyeballsTimeout
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(HappyEyeballsTimeout));
    } = TimeSpan.FromMilliseconds(200);

    /// <summary><c>CURLOPT_FRESH_CONNECT</c>: use a new connection, not a cached one. Default <see langword="false"/>.</summary>
    public bool FreshConnect { get; init; }

    /// <summary><c>CURLOPT_FORBID_REUSE</c>: close the connection after the transfer. Default <see langword="false"/>.</summary>
    public bool ForbidReuse { get; init; }

    /// <summary><c>CURLOPT_MAXCONNECTS</c>: the size of the connection cache. Default 5.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxConnects
    {
        get;
        init => field = (int)TransportOptionGuard.InRange(value, 0, int.MaxValue, nameof(MaxConnects));
    } = 5;

    /// <summary><c>CURLOPT_MAXAGE_CONN</c>: how long an idle cached connection may be reused. Default 118 seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan MaxIdleConnectionAge
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(MaxIdleConnectionAge));
    } = TimeSpan.FromSeconds(118);

    /// <summary><c>CURLOPT_MAXLIFETIME_CONN</c>: how long after it was made a connection may be reused. Default 24 hours.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan MaxConnectionLifetime
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(MaxConnectionLifetime));
    } = TimeSpan.FromHours(24);

    /// <summary><c>CURLOPT_ACCEPTTIMEOUT_MS</c>: how long to wait for an active FTP server to connect back. Default 60 seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan AcceptTimeout
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(AcceptTimeout));
    } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_UPKEEP_INTERVAL_MS</c>: how often connection upkeep runs. Default 60 seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan UpkeepInterval
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(UpkeepInterval));
    } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_SERVER_RESPONSE_TIMEOUT_MS</c>: how long to wait for a command response. Default 60 seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan ServerResponseTimeout
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(ServerResponseTimeout));
    } = TimeSpan.FromSeconds(60);

    /// <summary><c>CURLOPT_USE_SSL</c>: TLS for protocols that upgrade to it (FTP, IMAP, POP3, SMTP, LDAP). Default <see cref="UseSsl.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="Options.Network.UseSsl"/> value.</exception>
    public UseSsl UseSsl
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(UseSsl));
    } = UseSsl.None;

    /// <summary><paramref name="network"/> with every member of this record set on it; its URL and connect-only mode are kept.</summary>
    /// <param name="network">The handle's network options so far.</param>
    /// <returns>The options with this record applied.</returns>
    internal EasyNetworkOptions ApplyTo(EasyNetworkOptions network) => network with
    {
        PathAsIs = PathAsIs,
        Protocols = Protocols,
        RedirectProtocols = RedirectProtocols,
        DefaultProtocol = DefaultProtocol,
        Port = Port,
        Interface = Interface,
        LocalPort = LocalPort,
        LocalPortRange = LocalPortRange,
        ConnectTo = ConnectTo,
        Resolve = Resolve,
        DnsCacheTimeout = DnsCacheTimeout,
        DnsServers = DnsServers,
        DnsInterface = DnsInterface,
        DnsLocalIPv4 = DnsLocalIPv4,
        DnsLocalIPv6 = DnsLocalIPv6,
        DnsShuffleAddresses = DnsShuffleAddresses,
        DohUrl = DohUrl,
        IpResolve = IpResolve,
        TcpNoDelay = TcpNoDelay,
        TcpFastOpen = TcpFastOpen,
        TcpKeepAlive = TcpKeepAlive,
        TcpKeepIdle = TcpKeepIdle,
        TcpKeepInterval = TcpKeepInterval,
        TcpKeepCount = TcpKeepCount,
        IpTypeOfService = IpTypeOfService,
        VlanPriority = VlanPriority,
        MultipathTcp = MultipathTcp,
        AddressScope = AddressScope,
        UnixSocketPath = UnixSocketPath,
        AbstractUnixSocket = AbstractUnixSocket,
        ConnectTimeout = ConnectTimeout ?? network.ConnectTimeout,
        HappyEyeballsTimeout = HappyEyeballsTimeout,
        FreshConnect = FreshConnect,
        ForbidReuse = ForbidReuse,
        MaxConnects = MaxConnects,
        MaxIdleConnectionAge = MaxIdleConnectionAge,
        MaxConnectionLifetime = MaxConnectionLifetime,
        AcceptTimeout = AcceptTimeout,
        UpkeepInterval = UpkeepInterval,
        ServerResponseTimeout = ServerResponseTimeout,
        UseSsl = UseSsl,
    };
}
