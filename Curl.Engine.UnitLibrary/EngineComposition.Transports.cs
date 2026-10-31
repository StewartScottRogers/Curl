using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Creates the network transports for one run: a <see cref="TcpConnector" /> over the
    /// resolver <see cref="CreateDnsResolver" /> picks, a <see cref="TcpDialer" /> that sets <c>TCP_NODELAY</c> and
    /// <c>SO_KEEPALIVE</c> unless <c>--no-tcp-nodelay</c> or <c>--no-keepalive</c> says not to, and the
    /// TLS provider <see cref="CreateTlsProvider" /> routes to, and a <see cref="UdpDatagramConnector" />. Both
    /// connectors and the TLS provider share the one resolver and <see cref="TimeProvider.System" />. TLS uses
    /// the <see cref="TlsClientOptions" /> mapped from <paramref name="tls" /> by
    /// <see cref="TlsClientOptionsMapping.Of" /> from <paramref name="tls" /> (asking for the OpenSSL build's handshake message
    /// trace under <see cref="EngineToolOptions.TracesTransfer" />, ADR-0985), one set shared by every URL on
    /// the command line. The handshake to an HTTPS proxy runs through a second provider, routed
    /// the same way, on the same clock, with the <see cref="TlsClientOptions" />
    /// <see cref="TlsClientOptionsMapping.ProxyOf" /> maps from the proxy's
    /// TLS options, so <c>-k</c> and <c>--cacert</c> never reach the proxy. The CONNECT request that tunnels through an HTTP proxy carries the
    /// <see cref="HttpProxyTunnelOptions" /> <see cref="CreateProxyTunnelOptions" /> maps from
    /// <paramref name="http" /> and <paramref name="proxy" />, and the TCP connector applies the <c>--resolve</c> and
    /// <c>--connect-to</c> values (<see cref="CreateTcpConnector" />), as the UDP connector does
    /// (<see cref="CreateUdpDatagramConnector" />). One
    /// <see cref="PoolingConnector" /> over the TCP connector, on the same clock, is the option group's
    /// connection pool (ADR-0050).
    /// </summary>
    /// <param name="tool">The curl tool's own settings: <c>-Z</c>, <c>--parallel-immediate</c>, <c>--ssl-sessions</c> and <c>-m</c>.</param>
    /// <param name="network">The option group's network options: its connections, name resolution and connect timeout.</param>
    /// <param name="tls">The option group's TLS options, for the target's, the HTTPS proxy's and the DoH server's handshakes.</param>
    /// <param name="http">The option group's HTTP options, for the HTTP version the ALPN list and the HTTPS-CONNECT attempts follow.</param>
    /// <param name="proxy">The option group's proxy options, for the tunnel, the pre-proxy, SOCKS5 authentication and the HAProxy line.</param>
    /// <param name="authentication">The option group's authentication options, for the proxy's auth methods, the Negotiate service names and <c>--delegation</c>.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components; <see langword="null" /> for none.</param>
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports(EngineToolOptions tool, EasyNetworkOptions network, EasyTlsOptions tls, EasyHttpOptions http, EasyProxyOptions proxy, EasyAuthenticationOptions authentication, EngineTraceComponents? traces = null) =>
        CreateTransports(tool, network, tls, http, proxy, authentication, TimeProvider.System, traces: traces);

    /// <summary>
    /// Creates the TLS provider for handshakes run with <paramref name="options" />: the
    /// <see cref="HandBuiltTlsProvider" /> when <see cref="TlsClientRouting.Choose" /> routes them
    /// to the hand-built client (ADR-0140), otherwise the <see cref="SslStreamTlsProvider" />.
    /// </summary>
    /// <param name="options">The origin's or the HTTPS proxy's TLS options.</param>
    /// <param name="timeProvider">The clock the provider times its handshakes on.</param>
    /// <returns>The provider.</returns>
    /// <param name="sessions">
    /// The run's TLS session cache, which the hand-built provider offers and keeps sessions in,
    /// so a later connection resumes as curl's does, unless <see cref="TlsClientOptions.NoSessionId" />
    /// (BL-713); <c>--ssl-sessions</c> loads and saves it (ADR-0319);
    /// <see langword="null" /> for none.
    /// </param>
    /// <param name="echConfigs">
    /// Finds a host's ECHConfigList for <c>--ech true</c> or <c>hard</c> without <c>ecl:</c>: the run's
    /// <see cref="DohDnsResolver" /> under <c>--doh-url</c> (ADR-0327); <see langword="null" /> for none.
    /// </param>
    internal static ITlsProviderWithWarnings CreateTlsProvider(TlsClientOptions options, TimeProvider timeProvider, TlsSessionCache? sessions = null, IEchConfigListLookup? echConfigs = null) =>
        TlsClientRouting.Choose(options) != TlsClientRoute.HandBuilt ? new SslStreamTlsProvider(options, timeProvider)
            : new HandBuiltTlsProvider(options, timeProvider, sessions, echConfigs);

    /// <summary>
    /// Creates the network transports as <see cref="CreateTransports(EngineToolOptions, EasyNetworkOptions, EasyTlsOptions, EasyHttpOptions, EasyProxyOptions, EasyAuthenticationOptions, EngineTraceComponents?)" /> does,
    /// on <paramref name="timeProvider" /> instead of <see cref="TimeProvider.System" />: the
    /// <see cref="TcpConnector" />, the TLS providers and the
    /// <see cref="UdpDatagramConnector" /> all time on it, so the handshake timestamps the TLS
    /// provider reports are on the connector's clock (ADR-0030).
    /// </summary>
    /// <param name="tool">The curl tool's own settings: <c>-Z</c>, <c>--parallel-immediate</c>, <c>--ssl-sessions</c> and <c>-m</c>.</param>
    /// <param name="network">The option group's network options: its connections, name resolution and connect timeout.</param>
    /// <param name="tls">The option group's TLS options, for the target's, the HTTPS proxy's and the DoH server's handshakes.</param>
    /// <param name="http">The option group's HTTP options, for the HTTP version the ALPN list and the HTTPS-CONNECT attempts follow.</param>
    /// <param name="proxy">The option group's proxy options, for the tunnel, the pre-proxy, SOCKS5 authentication and the HAProxy line.</param>
    /// <param name="authentication">The option group's authentication options, for the proxy's auth methods, the Negotiate service names and <c>--delegation</c>.</param>
    /// <param name="timeProvider">The clock every transport times on.</param>
    /// <returns>The connectors and the pieces they were built from.</returns>
    /// <param name="runConnections">
    /// The run's connection cache, which every option group's pooling connector shares
    /// (<see cref="CreatePoolingConnector" />); <see langword="null" /> for a pool of the group's own.
    /// </param>
    /// <param name="tlsSessions">The run's <c>--ssl-sessions</c> cache, which the origin's TLS provider uses (ADR-0319); <see langword="null" /> for none.</param>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    /// <param name="runDnsCache">The run's DNS cache, which every option group's TCP connector answers from and adds to as curl 8.21.0 shares its cache (BL-1053); <see langword="null" /> for a cache of the group's own.</param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code, which the DoH resolver's <c>--trace-config doh</c> lines carry (BL-1102): <c>CurlEasyErrorText.Of</c> (in <c>Libcurl/Misc/ErrorText</c>) passed in by <c>Curl.Console</c> in production; <see langword="null" /> for an empty text.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components; <see langword="null" /> for none.</param>
    /// <param name="takeDohConnectionId">Takes the run's next <c>%{conn_id}</c> for each DoH connection (CGR-286); <see langword="null" /> to take none.</param>
    internal static CurlTransports CreateTransports(EngineToolOptions tool, EasyNetworkOptions network, EasyTlsOptions tls, EasyHttpOptions http, EasyProxyOptions proxy, EasyAuthenticationOptions authentication, TimeProvider timeProvider, ConnectionCache? runConnections = null, TlsSessionCache? tlsSessions = null, IDiagnosticLog? diagnosticLog = null, DnsCache? runDnsCache = null, Func<CurlExitCode, string>? easyErrorText = null, EngineTraceComponents? traces = null, Func<long>? takeDohConnectionId = null) =>
        CreateTransports(tool, network, tls, http, proxy, authentication, timeProvider, runConnections, tlsSessions, diagnosticLog, runDnsCache, easyErrorText, traces, static resolver => resolver, takeDohConnectionId: takeDohConnectionId);

    /// <summary>
    /// Creates the network transports as the overload without <paramref name="wrapLookUps" /> does, with the
    /// resolver the TCP and UDP connectors ask on a DNS cache miss wrapped by <paramref name="wrapLookUps" />: the
    /// managed engine's <see cref="ResolverStartDnsResolver" />, which calls <c>CURLOPT_RESOLVER_START_FUNCTION</c>
    /// before each look-up (ADR-0611). <see cref="CurlTransports.DnsResolver" /> stays the resolver unwrapped.
    /// </summary>
    /// <param name="tool">The curl tool's own settings.</param>
    /// <param name="network">The option group's network options.</param>
    /// <param name="tls">The option group's TLS options.</param>
    /// <param name="http">The option group's HTTP options.</param>
    /// <param name="proxy">The option group's proxy options.</param>
    /// <param name="authentication">The option group's authentication options.</param>
    /// <param name="timeProvider">The clock every transport times on.</param>
    /// <param name="runConnections">The run's connection cache; <see langword="null" /> for a pool of the group's own.</param>
    /// <param name="tlsSessions">The run's <c>--ssl-sessions</c> cache; <see langword="null" /> for none.</param>
    /// <param name="diagnosticLog">The run's diagnostic log; <see langword="null" /> for none.</param>
    /// <param name="runDnsCache">The run's DNS cache; <see langword="null" /> for a cache of the group's own.</param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code; <see langword="null" /> for an empty text.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components; <see langword="null" /> for none.</param>
    /// <param name="wrapLookUps">Wraps the resolver the connectors look names up through.</param>
    /// <param name="dialing">
    /// What the conformance harness's engine dials and resolves through in place of sockets (ADR-0658): the
    /// <see cref="TcpConnector" /> dials through its dialer, its resolver is <see cref="CurlTransports.DnsResolver" />,
    /// and the datagram protocols and QUIC open their channels through its connector, as <c>Curl.Console</c>'s
    /// dialing runner composes them; <see langword="null" />, in production, for sockets and the resolver the options pick.
    /// </param>
    /// <param name="socketWait">
    /// What a transfer a multi handle drives by <c>curl_multi_socket_action</c> waits for on its socket: the
    /// <see cref="TcpConnector" /> then dials through a <see cref="SocketActionGatedTcpDialer" /> over it, so each
    /// dialed connection reads and writes only once the multi handle signals it (ADR-0917), and the connectors look
    /// names up through a <see cref="SocketActionGatedResolver" /> beneath <paramref name="wrapLookUps" />, so a
    /// look-up waits readable on its wakeup socket (ADR-0984); <see langword="null" />,
    /// outside socket-action mode, for the dialer as it is.
    /// </param>
    /// <param name="takeDohConnectionId">Takes the run's next <c>%{conn_id}</c> for each DoH connection (CGR-286); <see langword="null" /> to take none.</param>
    /// <param name="extensionDatagramConnector">
    /// An activated extension connection hook's datagram connector over curl's UDP connector (ADR-1010): when it
    /// replaces curl's, QUIC opens its channels through it (<see cref="QuicDatagramConnectorOf" />, BL-4318);
    /// <see langword="null" />, or one that keeps curl's, for the socket QUIC dialer. Unused under <paramref name="dialing" />.
    /// <see cref="CurlTransports.UdpDatagramConnector" /> stays curl's, which the dispatch hands the hook again for the datagram protocols.
    /// </param>
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports(EngineToolOptions tool, EasyNetworkOptions network, EasyTlsOptions tls, EasyHttpOptions http, EasyProxyOptions proxy, EasyAuthenticationOptions authentication, TimeProvider timeProvider, ConnectionCache? runConnections, TlsSessionCache? tlsSessions, IDiagnosticLog? diagnosticLog, DnsCache? runDnsCache, Func<CurlExitCode, string>? easyErrorText, EngineTraceComponents? traces, Func<IDnsResolver, IDnsResolver> wrapLookUps, EngineDialing? dialing = null, TransferSocketWait? socketWait = null, Func<long>? takeDohConnectionId = null, Func<IDatagramConnector, IDatagramConnector>? extensionDatagramConnector = null)
    {
        traces ??= EngineTraceComponents.None;
        TcpDialer tcpDialer = new(TcpSocketOptionsOf(network));
        FlowScopedTransferEvents? resolverEvents = TracesDns(traces) ? new() : null;
        IDnsResolver dnsResolver = dialing is null
            ? CreateDnsResolver(network, tls, timeProvider, tcpDialer, resolverEvents, easyErrorText, traces, takeDohConnectionId: takeDohConnectionId)
            : DialingResolverOf(dialing, network, tls, timeProvider, resolverEvents, easyErrorText, traces);
        IDnsResolver lookUps = LookUpsOf(dnsResolver, network, resolver => wrapLookUps(SocketActionGatedResolver.Over(resolver, socketWait)));
        TlsClientOptions tlsClientOptions = TlsClientOptionsMapping.Of(tls, http.SslEnableAlpn, tool.SslSessionsFile, tracesHandshakeMessages: tool.TracesTransfer);
        ITlsProviderWithWarnings tlsProvider = CreateTlsProvider(tlsClientOptions, timeProvider, tlsSessions, dnsResolver as IEchConfigListLookup);
        TlsClientOptions proxyTlsClientOptions = TlsClientOptionsMapping.ProxyOf(tls, http.SslEnableAlpn, tracesHandshakeMessages: tool.TracesTransfer);
        ITlsProviderWithWarnings proxyTlsProvider = CreateTlsProvider(proxyTlsClientOptions, timeProvider);
        LateBoundSecurityContextFactory proxyContexts = new();
        HttpProxyTunnelOptions proxyTunnelOptions = CreateProxyTunnelOptions(http, proxy, authentication, proxyContexts, diagnosticLog);
        (QuicDialer quicDialer, ITcpDialer connectorDialer, IDatagramConnector udpDatagramConnector) = DialersOf(dialing, tcpDialer, tlsClientOptions, traces, timeProvider, () => CreateUdpDatagramConnector(network, lookUps, timeProvider, diagnosticLog), extensionDatagramConnector);
        TcpConnector tcpConnector = CreateTcpConnector(tool, traces, network, http, proxy, authentication, lookUps, SocketActionGatedTcpDialer.Over(connectorDialer, socketWait), tlsProvider, timeProvider, proxyTunnelOptions, proxyTlsProvider, quicDialer, proxyContexts, runDnsCache, resolverEvents);
        PoolingConnector poolingConnector = CreatePoolingConnector(tool, network, tls, http, proxy, tcpConnector, timeProvider, runConnections);

        // The tunnel answers --proxy-ntlm and --proxy-negotiate on the same router the origin's
        // contexts come from, which can only be made once the connectors exist (BL-604).
        proxyContexts.Bind(CreateSecurityContextFactory(poolingConnector, udpDatagramConnector, diagnosticLog));
        return new CurlTransports(
            dnsResolver,
            timeProvider,
            tcpDialer,
            tlsClientOptions,
            tlsProvider,
            proxyTlsClientOptions,
            proxyTlsProvider,
            proxyTunnelOptions,
            quicDialer,
            tcpConnector,
            udpDatagramConnector,
            poolingConnector,
            diagnosticLog,
            TracesFtp(traces),
            TracesSmtp(traces),
            TracesWs(traces),
            TracesSsh(traces),
            TracesHttp2(traces),
            TracesHttp3(traces),
            TracesRead(traces));
    }

    /// <summary>
    /// The QUIC dialer, the dialer the <see cref="TcpConnector" /> dials through and the datagram connector the transports
    /// use: without <paramref name="dialing" />, the QUIC dialer <see cref="NetworkDialersOf" /> picks, <paramref name="tcpDialer" />
    /// and the connector <paramref name="createDatagramConnector" /> builds; with one, a QUIC dialer over its datagram connector,
    /// its dialer and its datagram connector (ADR-0658).
    /// </summary>
    /// <param name="dialing">What the harness's engine dials through, or <see langword="null" />.</param>
    /// <param name="tcpDialer">The socket dialer built from the options.</param>
    /// <param name="tlsClientOptions">The origin's TLS options, for the QUIC dialer.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the HTTP/3 connection lines.</param>
    /// <param name="timeProvider">The clock the network QUIC dialer times on.</param>
    /// <param name="createDatagramConnector">Builds the UDP datagram connector the options pick.</param>
    /// <param name="extensionDatagramConnector">An activated connection hook's datagram connector over curl's, or <see langword="null" />.</param>
    /// <returns>The three.</returns>
    private static (QuicDialer QuicDialer, ITcpDialer TcpDialer, IDatagramConnector DatagramConnector) DialersOf(
        EngineDialing? dialing,
        TcpDialer tcpDialer,
        TlsClientOptions tlsClientOptions,
        EngineTraceComponents traces,
        TimeProvider timeProvider,
        Func<IDatagramConnector> createDatagramConnector,
        Func<IDatagramConnector, IDatagramConnector>? extensionDatagramConnector) =>
        dialing is null
            ? NetworkDialersOf(tcpDialer, tlsClientOptions, traces, timeProvider, createDatagramConnector(), extensionDatagramConnector)
            : (CreateDatagramConnectorQuicDialer(traces, tlsClientOptions, dialing.DatagramConnector), dialing.TcpDialer, dialing.DatagramConnector);

    /// <summary>
    /// The dialers on the real network: a QUIC dialer that opens its channels through the connection hook's datagram
    /// connector when <see cref="QuicDatagramConnectorOf" /> gives one (BL-4318), otherwise the socket
    /// <see cref="QuicDialer" />; <paramref name="tcpDialer" />; and curl's own <paramref name="udpDatagramConnector" />.
    /// </summary>
    /// <param name="tcpDialer">The socket dialer built from the options.</param>
    /// <param name="tlsClientOptions">The origin's TLS options, for the QUIC dialer.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the HTTP/3 connection lines.</param>
    /// <param name="timeProvider">The clock the QUIC dialer times on.</param>
    /// <param name="udpDatagramConnector">Curl's UDP datagram connector.</param>
    /// <param name="extensionDatagramConnector">An activated connection hook's datagram connector over curl's, or <see langword="null" />.</param>
    /// <returns>The three.</returns>
    private static (QuicDialer QuicDialer, ITcpDialer TcpDialer, IDatagramConnector DatagramConnector) NetworkDialersOf(
        TcpDialer tcpDialer,
        TlsClientOptions tlsClientOptions,
        EngineTraceComponents traces,
        TimeProvider timeProvider,
        IDatagramConnector udpDatagramConnector,
        Func<IDatagramConnector, IDatagramConnector>? extensionDatagramConnector) =>
        (QuicDatagramConnectorOf(udpDatagramConnector, extensionDatagramConnector) is { } hooked
            ? new QuicDialer(new DatagramConnectorUdpChannelOpener(hooked), tlsClientOptions, timeProvider) { WritesHttp3ConnectionLines = TracesHttp3(traces) }
            : new QuicDialer(tlsClientOptions, timeProvider) { WritesHttp3ConnectionLines = TracesHttp3(traces) },
            tcpDialer,
            udpDatagramConnector);

    /// <summary>
    /// The datagram connector QUIC opens its channels through in place of its own sockets: the one
    /// <paramref name="extensionDatagramConnector" /> gives over <paramref name="udpDatagramConnector" /> when it is
    /// another connector (BL-4318).
    /// </summary>
    /// <param name="udpDatagramConnector">Curl's UDP datagram connector, handed to the hook.</param>
    /// <param name="extensionDatagramConnector">An activated connection hook's datagram connector over curl's, or <see langword="null" />.</param>
    /// <returns>The hook's connector, or <see langword="null" /> without a hook or when the hook keeps curl's, so QUIC keeps its sockets.</returns>
    internal static IDatagramConnector? QuicDatagramConnectorOf(IDatagramConnector udpDatagramConnector, Func<IDatagramConnector, IDatagramConnector>? extensionDatagramConnector) =>
        extensionDatagramConnector?.Invoke(udpDatagramConnector) is { } hooked && !ReferenceEquals(hooked, udpDatagramConnector) ? hooked : null;

    /// <summary>
    /// The socket options each TCP connection is given: <c>TCP_NODELAY</c>, keepalive with its idle time and
    /// probe count, the IP Type of Service byte, the VLAN priority, TCP Fast Open and Multipath TCP. libcurl's
    /// own keepalive defaults on the record (60 seconds, 9 probes) are <see cref="TcpSocketOptions" />'s
    /// defaults too, so a record that leaves them gives the sockets curl's tool gives them (ADR-0513).
    /// </summary>
    /// <param name="network">The option group's network options.</param>
    /// <returns>The socket options.</returns>
    internal static TcpSocketOptions TcpSocketOptionsOf(EasyNetworkOptions network) =>
        TcpSocketOptions.FromCommandLine(network.TcpNoDelay, network.TcpKeepAlive, (long)network.TcpKeepIdle.TotalSeconds, network.TcpKeepCount)
        with
        {
            TypeOfService = network.IpTypeOfService,
            VlanPriority = network.VlanPriority,
            FastOpen = network.TcpFastOpen,
            MultipathTcp = network.MultipathTcp,
        };

    /// <summary>
    /// Tells whether a transfer waits for a connection to its origin that may yet multiplex rather
    /// than open one of its own: curl sets <c>CURLOPT_PIPEWAIT</c> on every <c>-Z</c> transfer unless
    /// <c>--parallel-immediate</c> is given (measured, BL-717 Notes).
    /// </summary>
    /// <param name="tool">The curl tool's own settings.</param>
    /// <returns><see langword="true" /> for <c>-Z</c> without <c>--parallel-immediate</c>.</returns>
    internal static bool WaitsForMultiplexing(EngineToolOptions tool) => tool.Parallel && !tool.ParallelImmediate;

    /// <summary>
    /// The QUIC dialer of an option group of the dialing runner: its <c>--http3</c> and
    /// <c>--http3-only</c> datagrams go through <paramref name="datagramConnector" />
    /// (<see cref="DatagramConnectorUdpChannelOpener" />), not a real UDP socket, so an in-process
    /// HTTP/3 server answers them (BL-2260). The executable's <see cref="CreateTransports(EngineToolOptions, EasyNetworkOptions, EasyTlsOptions, EasyHttpOptions, EasyProxyOptions, EasyAuthenticationOptions, TimeProvider, ConnectionCache?, TlsSessionCache?, IDiagnosticLog?, DnsCache?, Func{CurlExitCode, string}?, EngineTraceComponents?, Func{long}?)" />
    /// keeps the socket-opening dialer unless an extension connection hook supplies a datagram connector (BL-4318).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the HTTP/3 connection lines.</param>
    /// <param name="tlsClientOptions">The origin's TLS options.</param>
    /// <param name="datagramConnector">Opens the QUIC channels.</param>
    /// <returns>The dialer.</returns>
    internal static QuicDialer CreateDatagramConnectorQuicDialer(EngineTraceComponents traces, TlsClientOptions tlsClientOptions, IDatagramConnector datagramConnector) =>
        new(new DatagramConnectorUdpChannelOpener(datagramConnector), tlsClientOptions, TimeProvider.System) { WritesHttp3ConnectionLines = TracesHttp3(traces) };
}
