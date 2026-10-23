using System.Net.Sockets;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Creates an option group's pooling connector over <paramref name="connector" />, waiting for
    /// multiplexing as <see cref="WaitsForMultiplexing" /> says: over the run's
    /// <paramref name="runConnections" /> with the group's <see cref="OptionGroupConnectionSettings" />,
    /// so a later group reuses an earlier group's connection when their settings are equal, as curl
    /// 8.21.0 does (ADR-0285, BL-754); or, without one, over a pool of its own.
    /// </summary>
    /// <param name="tool">The curl tool's own settings, whose <c>-Z</c> and <c>--ssl-sessions</c> the pool's settings hold.</param>
    /// <param name="network">The option group's network options, whose <c>--connect-to</c> entries, binding, socket and IP version the pool's settings hold, and whose <c>CURLOPT_MAXLIFETIME_CONN</c> the connector retires connections by (BL-2730).</param>
    /// <param name="tls">The option group's TLS options, which the pool's settings hold.</param>
    /// <param name="http">The option group's HTTP options, whose HTTP version the pool's settings hold.</param>
    /// <param name="proxy">The option group's proxy options, whose pre-proxy and HAProxy line the pool's settings hold.</param>
    /// <param name="connector">Opens a connection when the pool has none to reuse.</param>
    /// <param name="timeProvider">The clock a pool of its own measures idle time on.</param>
    /// <param name="runConnections">The run's connection cache, or <see langword="null" />.</param>
    /// <param name="featureMask">The features the run behaves as built without, which its <c>--connect-to</c> mappings parse under (<see cref="ConnectToMappingsOf" />).</param>
    /// <returns>The pooling connector.</returns>
    internal static PoolingConnector CreatePoolingConnector(
        EngineToolOptions tool,
        EasyNetworkOptions network,
        EasyTlsOptions tls,
        EasyHttpOptions http,
        EasyProxyOptions proxy,
        IConnector connector,
        TimeProvider timeProvider,
        ConnectionCache? runConnections,
        EngineFeatureMask featureMask = EngineFeatureMask.None)
    {
        bool waitsForMultiplexing = WaitsForMultiplexing(tool);
        return runConnections is null
            ? new PoolingConnector(connector, timeProvider) { WaitsForMultiplexing = waitsForMultiplexing, MaximumConnectionLifetime = network.MaxConnectionLifetime, FreshConnect = network.FreshConnect, ForbidReuse = network.ForbidReuse }
            : new PoolingConnector(connector, runConnections, OptionGroupConnectionSettings.Of(tool, network, tls, http, proxy), ConnectToMappingsOf(network, featureMask)) { WaitsForMultiplexing = waitsForMultiplexing, MaximumConnectionLifetime = network.MaxConnectionLifetime, FreshConnect = network.FreshConnect, ForbidReuse = network.ForbidReuse };
    }

    /// <summary>
    /// The <c>--connect-to</c> mappings of <paramref name="network" />, parsed as a curl built without
    /// IPv6 parses them when <paramref name="featureMask" /> hides <see cref="EngineFeatureMask.Ipv6" />:
    /// a matching bracketed destination then fails with exit 4 (upstream test1454, BL-2996).
    /// </summary>
    /// <param name="network">The option group's network options, whose <c>--connect-to</c> values are mapped.</param>
    /// <param name="featureMask">The features the run behaves as built without.</param>
    /// <returns>The mappings.</returns>
    internal static ConnectToMappings ConnectToMappingsOf(EasyNetworkOptions network, EngineFeatureMask featureMask) =>
        new(network.ConnectTo, hidesIpv6: featureMask.HasFlag(EngineFeatureMask.Ipv6));

    /// <summary>
    /// Creates the run's <see cref="TcpConnector" /> over the given pieces, with the
    /// <c>--resolve</c> entries parsed by <see cref="ResolveOverrides.Parse" /> and the
    /// <c>--connect-to</c> mappings of <paramref name="network" />, so a transfer dials the
    /// mapped host and port at the overridden addresses. An entry that does not parse fails
    /// each transfer with exit 49 when it connects, as curl 8.21.0 fails it. Under
    /// <c>--unix-socket</c> or <c>--abstract-unix-socket</c> it dials that socket for every
    /// transfer of the option group instead (<see cref="UnixSocketOf" />, BL-507). HTTP over TLS
    /// offers the ALPN list the version option and the platform choose
    /// (<see cref="HttpVersionMapping.HttpOverTlsApplicationProtocolsOf(Curl.Api.Options.Http.HttpVersion)" />, ADR-0141).
    /// <c>--happy-eyeballs-timeout-ms</c> is how long it dials the first address family alone before
    /// the other, curl's 200 ms when not given (ADR-0254, BL-889).
    /// </summary>
    /// <param name="tool">The curl tool's own settings, for its <c>-m</c>.</param>
    /// <param name="network">The option group's network options: <c>--resolve</c>, <c>--connect-to</c>, the binding, the socket, the IP version and the connect timeouts.</param>
    /// <param name="http">The option group's HTTP options: the HTTP version the ALPN list and the HTTPS-CONNECT attempts follow.</param>
    /// <param name="proxy">The option group's proxy options: the pre-proxy, SOCKS5 authentication and the HAProxy line.</param>
    /// <param name="authentication">The option group's authentication options: the <c>--proxy-service-name</c> and <c>--delegation</c> SOCKS5 GSS-API runs with.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the connector's filter lines.</param>
    /// <param name="dnsResolver">Resolves a host no <c>--resolve</c> entry answers for.</param>
    /// <param name="tcpDialer">Opens each plaintext connection.</param>
    /// <param name="tlsProvider">Upgrades a connection whose target asks for TLS.</param>
    /// <param name="timeProvider">The clock the connector times on.</param>
    /// <param name="proxyTunnelOptions">What the CONNECT request through an HTTP proxy carries.</param>
    /// <param name="proxyTlsProvider">
    /// Runs the handshake to an HTTPS proxy; <see langword="null" /> for <paramref name="tlsProvider" />.
    /// </param>
    /// <param name="quicDialer">
    /// Opens the QUIC connections <c>--http3</c> and <c>--http3-only</c> ask for (ADR-0144, BL-732);
    /// <see langword="null" /> for a connector with no QUIC.
    /// </param>
    /// <param name="socks5SecurityContexts">
    /// Makes the Kerberos contexts SOCKS5 GSS-API runs on, with the <c>--socks5-*</c> options of
    /// <see cref="Socks5AuthenticationMapping.Of" /> (BL-615); <see langword="null" /> for none.
    /// </param>
    /// <param name="runDnsCache">
    /// The run's DNS cache, shared by every option group's connector (BL-1053); <see langword="null" /> for
    /// a cache of the connector's own.
    /// </param>
    /// <param name="resolverEvents">
    /// The events <paramref name="dnsResolver" /> reports its <c>--trace-config doh</c> lines to, which
    /// the connector points at each resolving transfer's events (BL-1102); <see langword="null" /> for none.
    /// </param>
    /// <param name="featureMask">
    /// The features the run behaves as built without, which its <c>--connect-to</c> mappings parse under
    /// (<see cref="ConnectToMappingsOf" />); <see cref="EngineFeatureMask.None" />, the default, hides none.
    /// </param>
    /// <returns>
    /// The connector, writing curl's <c>[DNS]</c> filter lines when <see cref="TracesDns" /> says so.
    /// </returns>
    internal static TcpConnector CreateTcpConnector(
        EngineToolOptions tool,
        EngineTraceComponents traces,
        EasyNetworkOptions network,
        EasyHttpOptions http,
        EasyProxyOptions proxy,
        EasyAuthenticationOptions authentication,
        IDnsResolver dnsResolver,
        ITcpDialer tcpDialer,
        ITlsProvider tlsProvider,
        TimeProvider timeProvider,
        HttpProxyTunnelOptions proxyTunnelOptions,
        ITlsProvider? proxyTlsProvider = null,
        QuicDialer? quicDialer = null,
        ISecurityContextFactory? socks5SecurityContexts = null,
        DnsCache? runDnsCache = null,
        FlowScopedTransferEvents? resolverEvents = null,
        EngineFeatureMask featureMask = EngineFeatureMask.None) =>
        new(
            dnsResolver,
            tcpDialer,
            tlsProvider,
            timeProvider,
            proxyTunnelOptions,
            ResolveOverrides.Parse(network.Resolve),
            ConnectToMappingsOf(network, featureMask),
            proxyTlsProvider,
            ConnectTimeoutOf(network, tool.MaxTime),
            AddressFamilyOf(network),
            UnixSocketOf(network),
            HttpVersionMapping.HttpOverTlsApplicationProtocolsOf(http.HttpVersion),
            quicDialer,
            network.HappyEyeballsTimeout,
            localBinding: LocalBindingOf(network),
            preProxy: PreProxyOf(proxy),
            socks5Authentication: Socks5AuthenticationMapping.Of(proxy, NegotiateOptionsMapping.Of(authentication), socks5SecurityContexts, OperatingSystem.IsWindows()),
            haproxyProtocol: HaproxyProtocolOf(proxy),
            dnsCache: runDnsCache)
        {
            TracesDnsFilter = TracesDns(traces),
            TracesSetupFilter = TracesSetup(traces),
            TracesHaproxyFilter = TracesHaproxy(traces),
            TracesSocksFilter = TracesSocks(traces),
            TracesHttpProxyFilter = TracesHttpProxy(traces),
            TracesH1ProxyFilter = TracesH1Proxy(traces),
            TracesSslFilter = TracesSsl(traces),
            TracesSslProxyFilter = TracesSslProxy(traces),
            TracesHappyEyeballsFilter = TracesHappyEyeballs(traces),
            TracesTcpFilter = TracesTcp(traces),
            TracesTimers = TracesTimer(traces),
            TracesTimerExpiry = TracesMulti(traces),
            TracedConnectTimeout = network.ConnectTimeout > TimeSpan.Zero ? network.ConnectTimeout : null,
            TracedTransferTimeout = tool.MaxTime > TimeSpan.Zero ? tool.MaxTime : null,
            TracesHttpsConnectFilter = TracesHttpsConnect(traces),
            HttpsConnectFirstAttemptVersion = HttpsConnectFirstAttemptVersionOf(http.HttpVersion),
            HttpsConnectSecondAttemptVersion = http.HttpVersion == HttpVersion.Http3 ? "h2" : null,
            ResolverEvents = resolverEvents,
            ShufflesResolveEntryAddresses = network.DnsShuffleAddresses,
        };

    /// <summary>
    /// What the TCP connector binds each connection's local end to, from <c>--interface</c> and
    /// <c>--local-port</c>, or <see langword="null" /> for neither (BL-600): a plain name is tried as an
    /// interface and then as a host, <c>if!</c> as an interface only, <c>host!</c> as a host only, and
    /// <c>ifhost!</c> binds its host with its interface part kept as the device name; the port range is
    /// <see cref="EasyNetworkOptions.LocalPort" /> and <see cref="EasyNetworkOptions.LocalPortRange" />, or any port. A value
    /// libcurl refuses at setopt never reaches a connect, so its empty parts are not looked at. The
    /// binding is part of each option group's <see cref="OptionGroupConnectionSettings" />, so a bound
    /// connection is never reused unbound.
    /// </summary>
    /// <param name="network">The option group's network options: <see cref="EasyNetworkOptions.Interface" />, <see cref="EasyNetworkOptions.LocalPort" /> and <see cref="EasyNetworkOptions.LocalPortRange" />, a port of 0 meaning any.</param>
    /// <returns>The binding, or <see langword="null" />.</returns>
    internal static LocalBinding? LocalBindingOf(EasyNetworkOptions network)
    {
        if (network.Interface is null && network.LocalPort == 0)
        {
            return null;
        }

        (string? interfaceName, string? hostName, string? deviceName) = LocalBindingNamesOf(network.Interface is { } named ? EngineInterfaceBinding.Parse(named) : null);
        return new LocalBinding(interfaceName, hostName, deviceName, network.LocalPort, network.LocalPortRange);
    }

    /// <summary>
    /// The names <see cref="LocalBindingOf" /> binds for a <c>--interface</c> value: a plain name as both
    /// interface and host, <c>ifhost!</c>'s parts as device and host, <c>if!</c> and <c>host!</c> as given.
    /// </summary>
    private static (string? InterfaceName, string? HostName, string? DeviceName) LocalBindingNamesOf(EngineInterfaceBinding? named)
    {
        if (named is null)
        {
            return (null, null, null);
        }

        if (named.InterfaceOrHostName is { } name)
        {
            return (name, name, null);
        }

        return named.InterfaceName is not null && named.HostName is not null
            ? (null, named.HostName, named.InterfaceName)
            : (named.InterfaceName, named.HostName, null);
    }

    /// <summary>
    /// The Unix domain socket the TCP connector dials in place of each URL's host: the last of
    /// <c>--unix-socket</c> and <c>--abstract-unix-socket</c>, or <see langword="null" /> for
    /// neither (BL-507). Each option group builds its own transports, so each group's pool
    /// holds only connections to its own socket.
    /// </summary>
    /// <param name="network">The option group's network options.</param>
    /// <returns>The socket, or <see langword="null" /> to dial TCP.</returns>
    internal static UnixSocketAddress? UnixSocketOf(EasyNetworkOptions network) =>
        network.UnixSocketPath is { } path ? new UnixSocketAddress(path, false)
        : network.AbstractUnixSocket is { } name ? new UnixSocketAddress(name, true)
        : null;

    /// <summary>
    /// The address family both connectors dial:<see cref="AddressFamily.InterNetwork" /> under
    /// <c>-4</c>, <see cref="AddressFamily.InterNetworkV6" /> under <c>-6</c>, and
    /// <see cref="AddressFamily.Unspecified" /> (either) when neither was given (BL-500).
    /// </summary>
    /// <param name="network">The option group's network options.</param>
    /// <returns>The family.</returns>
    internal static AddressFamily AddressFamilyOf(EasyNetworkOptions network) => network.IpResolve switch
    {
        IpResolve.V4 => AddressFamily.InterNetwork,
        IpResolve.V6 => AddressFamily.InterNetworkV6,
        _ => AddressFamily.Unspecified,
    };

    /// <summary>
    /// The connect timeout <see cref="CreateTcpConnector" /> gives the connector: the
    /// <c>--connect-timeout</c> value (curl's 300 seconds when none or 0 was given), or a positive
    /// <c>-m</c> when it runs out sooner, as curl 8.21.0 ends a connect with its connect message
    /// at whichever runs out first (measured, BL-510).
    /// </summary>
    /// <param name="network">The option group's network options, whose <see cref="EasyNetworkOptions.ConnectTimeout" /> of zero means not set.</param>
    /// <param name="maxTime">The <c>-m</c> value, if any (the transfer group's timeout until its own retarget).</param>
    /// <returns>The limit on each connect.</returns>
    internal static TimeSpan ConnectTimeoutOf(EasyNetworkOptions network, TimeSpan? maxTime)
    {
        TimeSpan connectTimeout = network.ConnectTimeout > TimeSpan.Zero ? network.ConnectTimeout : TcpConnector.DefaultConnectTimeout;
        return maxTime is { } given && given > TimeSpan.Zero && given < connectTimeout ? given : connectTimeout;
    }

    /// <summary>
    /// Creates the run's <see cref="UdpDatagramConnector" />, which TFTP opens its channel
    /// through, with the same <c>--resolve</c> entries and <c>--connect-to</c> mappings
    /// <see cref="CreateTcpConnector" /> applies: curl 8.21.0 applies both to a
    /// <c>tftp://</c> transfer and fails it with exit 49 for an entry that does not parse
    /// (measured).
    /// </summary>
    /// <param name="network">The option group's network options.</param>
    /// <param name="dnsResolver">Resolves a host no <c>--resolve</c> entry answers for.</param>
    /// <param name="timeProvider">The clock the connector times on.</param>
    /// <param name="diagnosticLog">Where the resolve and the channel are logged (<c>--log-level</c>, BL-968); <see langword="null" /> logs nothing.</param>
    /// <returns>The connector.</returns>
    internal static UdpDatagramConnector CreateUdpDatagramConnector(
        EasyNetworkOptions network,
        IDnsResolver dnsResolver,
        TimeProvider timeProvider,
        IDiagnosticLog? diagnosticLog = null) =>
        new(
            dnsResolver,
            timeProvider,
            ResolveOverrides.Parse(network.Resolve),
            new ConnectToMappings(network.ConnectTo),
            AddressFamilyOf(network),
            diagnosticLog);
}
