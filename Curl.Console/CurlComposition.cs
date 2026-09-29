using System.Net.Sockets;
using System.Text;
using Curl.Authentication;
using Curl.Cli;
using Curl.Cookies;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Kerberos;
using Curl.Networking;
using Curl.Ntlm;
using Curl.Output;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict;
using Curl.Protocol.File;
using Curl.Protocol.Ftp;
using Curl.Protocol.Gopher;
using Curl.Protocol.Http;
using Curl.Protocol.Imap;
using Curl.Protocol.Ldap;
using Curl.Protocol.Mqtt;
using Curl.Protocol.Pop3;
using Curl.Protocol.Rtsp;
using Curl.Protocol.Smtp;
using Curl.Protocol.Telnet;
using Curl.Protocol.Tftp;
using Curl.Protocol.Ws;

namespace Curl.Console;

/// <summary>
/// The composition root: builds every service the executable needs with plain constructor
/// calls. There is no container, no reflection and no assembly scanning, so native
/// ahead-of-time publishing sees every type that is used.
/// </summary>
internal static class CurlComposition
{
    /// <summary>
    /// Creates the protocol handlers the executable registers: <c>file</c> over the real
    /// disk; <c>dict</c>, <c>gopher</c> and <c>gophers</c>, <c>telnet</c>, <c>mqtt</c>
    /// and <c>mqtts</c>, <c>imap</c> and <c>imaps</c>, <c>pop3</c> and <c>pop3s</c>, <c>smtp</c> and <c>smtps</c> (the mail schemes authenticating with
    /// <see cref="CreateSaslAuthenticator" />'s authenticator and upgrading with
    /// <paramref name="tlsProvider" /> after <c>STARTTLS</c> or <c>STLS</c>), <c>ldap</c> and <c>ldaps</c>
    /// answering as WinLDAP's build on Windows and as the OpenLDAP build elsewhere (ADR-0166), and <c>http</c> and <c>https</c> over <paramref name="connector" />,
    /// the last two answering authentication with <see cref="CreateHttpAuthenticator" />'s
    /// authenticator, answering a forward proxy with <paramref name="proxyAuthSchemes" /> (ADR-0187),
    /// and keeping cookies in <paramref name="cookieStore" />; <c>ws</c> and <c>wss</c>
    /// over <paramref name="connector" />, sending a pre-emptive <c>Authorization</c> from
    /// <see cref="CreateHttpAuthenticator" />'s authenticator and drawing each
    /// <c>Sec-WebSocket-Key</c> and frame mask from <see cref="SystemWebSocketRandomSource" />
    /// (ADR-0128); <c>rtsp</c> over <paramref name="connector" />, sending one <c>OPTIONS *</c>
    /// request per transfer with a pre-emptive <c>Authorization</c> from the same authenticator
    /// (ADR-0169); and <c>tftp</c> over
    /// <paramref name="datagramConnector" />, sending its MASQUE request through an HTTP or HTTPS
    /// proxy over <paramref name="connector" /> with the proxy credential in the platform's
    /// encoding (ADR-0056, rule 4); and <c>ftp</c> and <c>ftps</c>, which
    /// <see cref="RoutingFtpProtocolHandler" /> hands to the HTTP handler when an <c>ftp</c>
    /// transfer is forwarded through an HTTP proxy without <c>-p</c> (ADR-0056, rule 3) and
    /// otherwise to <see cref="CreateFtpProtocolHandler" />'s handler (ADR-0093, ADR-0102).
    /// Each scheme is claimed by exactly one handler. Every handler connects through an
    /// <see cref="EndPointRecordingConnector" /> and an <see cref="EndPointRecordingDatagramConnector" />
    /// sharing one <see cref="ConnectionEndPointRecorder" />, and is wrapped in an
    /// <see cref="EndPointReportingProtocolHandler" />, so every scheme's report carries the end
    /// points of the first connection its transfer opened (ADR-0119).
    /// </summary>
    /// <param name="connector">Connects the TCP protocols, with TLS for <c>gophers</c>, <c>imaps</c>, <c>mqtts</c>, <c>pop3s</c>, <c>smtps</c>, <c>ldaps</c>, <c>wss</c> and <c>ftps</c>.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="tlsProvider">
    /// Upgrades an FTP connection after an accepted <c>AUTH</c> or <c>PROT P</c>, an IMAP connection after an accepted <c>STARTTLS</c>, a POP3 connection
    /// after an accepted <c>STLS</c>, and an SMTP
    /// connection after an accepted <c>STARTTLS</c>.
    /// </param>
    /// <param name="dnsResolver">Resolves a host name given to <c>-P</c>.</param>
    /// <param name="cookieStore">
    /// The cookies the HTTP handler sends and stores, or <see langword="null" /> to keep none.
    /// </param>
    /// <param name="securityContexts">
    /// Makes the HTTP handler's Negotiate and NTLM contexts and the mail handlers' SASL GSSAPI and
    /// NTLM contexts, or <see langword="null" /> for
    /// <see cref="CreateSecurityContextFactory" />'s router over <paramref name="connector" /> and
    /// <paramref name="datagramConnector" />.
    /// </param>
    /// <param name="proxyAuthSchemes">
    /// The scheme the <c>--proxy-*</c> auth switches pick (<see cref="CommandLineOptions.ProxyAuthSchemes" />),
    /// which the HTTP handler answers a forward proxy with; Basic, curl's default, when not given.
    /// </param>
    /// <param name="negotiateOptions">
    /// The <c>--service-name</c>, <c>--proxy-service-name</c> and <c>--delegation</c> the HTTP
    /// handler's Negotiate answers with (<see cref="NegotiateOptionsMapping.FromCommandLine" />);
    /// <see cref="NegotiateOptions.Default" /> when not given.
    /// </param>
    /// <param name="signingClock">
    /// The clock <c>--aws-sigv4</c> signs with (<see cref="AwsSigV4HttpAuthenticator" />, which
    /// the HTTP handler alone is given); <see cref="TimeProvider.System" /> when not given.
    /// </param>
    /// <returns>Every registered handler.</returns>
    internal static IReadOnlyList<IProtocolHandler> CreateProtocolHandlers(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ITlsProvider tlsProvider,
        IDnsResolver dnsResolver,
        ICookieStore? cookieStore = null,
        ISecurityContextFactory? securityContexts = null,
        HttpAuthSchemes proxyAuthSchemes = HttpAuthSchemes.Basic,
        NegotiateOptions? negotiateOptions = null,
        TimeProvider? signingClock = null)
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector recordingConnector = new(connector, recorder);
        EndPointRecordingDatagramConnector recordingDatagramConnector = new(datagramConnector, recorder);
        ISecurityContextFactory contexts = securityContexts ?? CreateSecurityContextFactory(connector, datagramConnector);
        RankedHttpAuthenticator httpAuthenticator = CreateHttpAuthenticator(contexts, negotiateOptions);
        AwsSigV4Signer signer = new(signingClock ?? TimeProvider.System, CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()));
        HttpProtocolHandler http = new(recordingConnector, new AwsSigV4HttpAuthenticator(httpAuthenticator, signer), cookieStore, proxyAuthSchemes);

        IProtocolHandler[] handlers =
        [
            new FileProtocolHandler(new PhysicalFileSystem()),
            new DictProtocolHandler(recordingConnector),
            new GopherProtocolHandler(recordingConnector),
            new TelnetProtocolHandler(recordingConnector),
            new TftpProtocolHandler(recordingDatagramConnector, recordingConnector, CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())),
            new MqttProtocolHandler(recordingConnector),
            new ImapProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts)),
            new Pop3ProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts)),
            new SmtpProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts)),
            new LdapProtocolHandler(recordingConnector, OperatingSystem.IsWindows() ? LdapDialect.WinLdap : LdapDialect.OpenLdap),
            new WsProtocolHandler(recordingConnector, httpAuthenticator, new SystemWebSocketRandomSource()),
            new RtspProtocolHandler(recordingConnector, httpAuthenticator),
            http,
            new RoutingFtpProtocolHandler(http, CreateFtpProtocolHandler(recordingConnector, tlsProvider, dnsResolver)),
        ];

        return [.. handlers.Select(handler => new EndPointReportingProtocolHandler(handler, recorder))];
    }

    /// <summary>
    /// Creates the FTP handler for <c>ftp</c> and <c>ftps</c> (ADR-0102): passive data
    /// connections through <paramref name="connector" />, active ones (<c>-P</c>) on a
    /// <see cref="TcpConnectionListener" />, a <c>-P</c> interface name looked up with
    /// <see cref="SystemNetworkInterfaceLookup" /> (ADR-0110) and a <c>-P</c> host name
    /// resolved with <paramref name="dnsResolver" /> (ADR-0108), and TLS from
    /// <paramref name="tlsProvider" />.
    /// </summary>
    /// <param name="connector">Supplies the control connection and the passive data connection.</param>
    /// <param name="tlsProvider">Upgrades a connection after an accepted <c>AUTH</c> or <c>PROT P</c>.</param>
    /// <param name="dnsResolver">Resolves a host name given to <c>-P</c>.</param>
    /// <returns>The handler.</returns>
    internal static FtpProtocolHandler CreateFtpProtocolHandler(IConnector connector, ITlsProvider tlsProvider, IDnsResolver dnsResolver) =>
        new(connector, new TcpConnectionListener(), tlsProvider, dnsResolver, new SystemNetworkInterfaceLookup());

    /// <summary>
    /// Creates the HTTP authenticator: a <see cref="RankedHttpAuthenticator" /> that answers the
    /// scheme curl 8.21.0 picks among those <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c> and <c>--anyauth</c>
    /// allow, with a <see cref="BasicAndBearerAuthenticator" /> for Basic, Bearer
    /// (<c>--oauth2-bearer</c>) and the first request, and a <see cref="DigestAuthenticator" />
    /// drawing each client nonce from <see cref="DigestClientNonce.CreateRandom" />, both encoding
    /// credentials in the platform's encoding (<see cref="CredentialEncoding.ForPlatform" />),
    /// and a <see cref="NegotiateHttpAuthenticator" /> and an <see cref="NtlmHttpAuthenticator" /> over <paramref name="securityContexts" />
    /// for <c>--negotiate</c> and <c>--ntlm</c> (ADR-0176, ADR-0181), a refused NTLM Type 2 message failing the transfer
    /// on Windows, as curl's SSPI build fails it, Negotiate naming the service and delegating as
    /// <paramref name="negotiateOptions" /> says (ADR-0188).
    /// </summary>
    /// <param name="securityContexts">Makes Negotiate's and NTLM's contexts: <see cref="CreateSecurityContextFactory" />'s in production.</param>
    /// <param name="negotiateOptions">The service names and delegation level; <see cref="NegotiateOptions.Default" /> when <see langword="null" />.</param>
    /// <returns>The authenticator.</returns>
    internal static RankedHttpAuthenticator CreateHttpAuthenticator(ISecurityContextFactory securityContexts, NegotiateOptions? negotiateOptions = null)
    {
        Encoding credentialEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows());

        return new RankedHttpAuthenticator(
            new BasicAndBearerAuthenticator(credentialEncoding),
            new DigestAuthenticator(credentialEncoding, DigestClientNonce.CreateRandom),
            new NegotiateHttpAuthenticator(securityContexts, negotiateOptions),
            new NtlmHttpAuthenticator(securityContexts, refusedChallengeFailsTransfer: OperatingSystem.IsWindows()));
    }

    /// <summary>
    /// Creates ADR-0142's router for NTLM, Negotiate and Kerberos contexts: SSPI on Windows,
    /// elsewhere the system GSS-API with the hand-built SPNEGO and Kerberos behind it, whose KDC
    /// exchanges go through a <see cref="KerberosKdcSocketTransport" /> over
    /// <paramref name="datagramConnector" /> and <paramref name="connector" /> (waiting
    /// <see cref="KdcReplyTimeout" /> for a UDP reply) and whose SRV lookups go through a
    /// <see cref="DnsServerResolver" /> asking the system's DNS servers (ADR-0176).
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <returns>The router.</returns>
    internal static RoutingSecurityContextFactory CreateSecurityContextFactory(IConnector connector, IDatagramConnector datagramConnector)
    {
        KerberosKdcSocketTransport kdcTransport = new(datagramConnector, connector, KdcReplyTimeout, TimeProvider.System);
        DnsServerResolver srvResolver = new(new DnsServerResolverOptions(null, null, null, null), TimeProvider.System);
        HandBuiltKerberosSources sources = new(
            new KerberosDiskFileReader(),
            HandBuiltKerberosSources.ReadProcessEnvironmentVariable,
            ProcessUserId.Read,
            new KerberosDnsSrvLookup(srvResolver.ResolveServiceAsync),
            kdcTransport,
            TimeProvider.System);
        return new RoutingSecurityContextFactory(
            OperatingSystem.IsWindows(),
            new SystemSecurityContextFactory(),
            new HandBuiltSecurityContextFactory(sources.CreateTicketSource(), TimeProvider.System, new SystemKerberosRandomSource(), new SystemNtlmRandomSource()));
    }

    /// <summary>
    /// How long a UDP request to a KDC waits for its reply before the next KDC is tried: MIT's
    /// first per-KDC wait (<c>krb5_sendto_kdc</c>), one second.
    /// </summary>
    internal static TimeSpan KdcReplyTimeout { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Creates the SASL authenticator the SMTP, POP3 and IMAP handlers share (ADR-0121): a
    /// <see cref="SaslAuthenticator" /> encoding credentials in the platform's encoding
    /// (<see cref="CredentialEncoding.ForPlatform" />), as the HTTP authenticator does, and answering
    /// GSSAPI and NTLM on the contexts <paramref name="securityContexts" /> makes (ADR-0184, BL-852).
    /// </summary>
    /// <param name="securityContexts">Makes GSSAPI's and NTLM's contexts: <see cref="CreateSecurityContextFactory" />'s in production.</param>
    /// <returns>The authenticator.</returns>
    internal static ISaslAuthenticator CreateSaslAuthenticator(ISecurityContextFactory securityContexts) =>
        new SaslAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()), securityContexts);

    /// <summary>
    /// Creates the network transports for one run: a <see cref="TcpConnector" /> over the
    /// resolver <see cref="CreateDnsResolver" /> picks, a <see cref="TcpDialer" /> that sets <c>TCP_NODELAY</c> and
    /// <c>SO_KEEPALIVE</c> unless <c>--no-tcp-nodelay</c> or <c>--no-keepalive</c> says not to, and the
    /// TLS provider <see cref="CreateTlsProvider" /> routes to, and a <see cref="UdpDatagramConnector" />. Both
    /// connectors and the TLS provider share the one resolver and <see cref="TimeProvider.System" />. TLS uses
    /// the <see cref="TlsClientOptions" /> mapped from <paramref name="options" /> by
    /// <see cref="TlsClientOptionsMapping.FromCommandLine" />, one set shared by every URL on
    /// the command line. The handshake to an HTTPS proxy runs through a second provider, routed
    /// the same way, on the same clock, with the <see cref="TlsClientOptions" />
    /// <see cref="TlsClientOptionsMapping.ProxyFromCommandLine" /> maps from the <c>--proxy-*</c>
    /// TLS options, so <c>-k</c> and <c>--cacert</c> never reach the proxy. The CONNECT request that tunnels through an HTTP proxy carries the
    /// <see cref="HttpProxyTunnelOptions" /> <see cref="CreateProxyTunnelOptions" /> maps from
    /// <paramref name="options" />, and the TCP connector applies the <c>--resolve</c> and
    /// <c>--connect-to</c> values (<see cref="CreateTcpConnector" />), as the UDP connector does
    /// (<see cref="CreateUdpDatagramConnector" />). One
    /// <see cref="PoolingConnector" /> over the TCP connector, on the same clock, is the run's
    /// connection pool (ADR-0050).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports(CommandLineOptions options) =>
        CreateTransports(options, TimeProvider.System);

    /// <summary>
    /// Creates the run's resolver: the <see cref="CreateDohResolver" /> one when <c>--doh-url</c> is
    /// given, since curl asks the DoH server whichever resolver it was built with (BL-642); otherwise
    /// the hand-built <see cref="DnsServerResolver" /> when any of
    /// <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> and <c>--dns-ipv6-addr</c>
    /// is given, as curl's c-ares build resolves then (ADR-0170, BL-694), asking only the
    /// <c>-4</c> or <c>-6</c> family's records; otherwise the <see cref="SystemDnsResolver" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="timeProvider">The clock the hand-built resolver and the DoH connections time on.</param>
    /// <param name="tcpDialer">Opens the plaintext TCP connections to the DoH server.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver CreateDnsResolver(CommandLineOptions options, TimeProvider timeProvider, ITcpDialer tcpDialer)
    {
        if (options.DohUrl is { } dohUrl)
        {
            return CreateDohResolver(dohUrl, CreateDohConnector(options, tcpDialer, timeProvider));
        }


        DnsServerResolverOptions resolverOptions = new(
            options.DnsServers,
            options.DnsInterface,
            options.DnsIPv4Address,
            options.DnsIPv6Address,
            AddressFamilyOf(options));
        return resolverOptions.IsAnyGiven
            ? new DnsServerResolver(resolverOptions, timeProvider)
            : new SystemDnsResolver();
    }

    /// <summary>
    /// Creates the resolver <c>--doh-url</c> asks for: a <see cref="DohDnsResolver" /> over
    /// <paramref name="connector" /> for the URL <see cref="DohUrlOf" /> makes of the value, or, when it
    /// makes none, an <see cref="UnusableDohUrlResolver" />, so every name fails to resolve with exit 6
    /// as curl 8.21.0 fails <c>--doh-url bogus</c> and an <c>ftp://</c> DoH URL (measured, BL-642).
    /// </summary>
    /// <param name="dohUrl">The <c>--doh-url</c> value.</param>
    /// <param name="connector">Opens each DoH connection: <see cref="CreateDohConnector" />'s in production.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver CreateDohResolver(string dohUrl, IConnector connector) =>
        DohUrlOf(dohUrl) is { } url ? new DohDnsResolver(connector, url) : new UnusableDohUrlResolver();

    /// <summary>
    /// The DoH URL curl makes of a <c>--doh-url</c> value: the value as it is when it names a scheme,
    /// and with <c>http://</c> in front when it does not, as curl guesses the scheme of any URL (a
    /// scheme-less DoH URL was measured to reach its server as plain HTTP, BL-642).
    /// </summary>
    /// <param name="dohUrl">The <c>--doh-url</c> value.</param>
    /// <returns>
    /// The absolute <c>http</c> or <c>https</c> URL, or <see langword="null" /> when the value does not
    /// parse as one.
    /// </returns>
    internal static Uri? DohUrlOf(string dohUrl)
    {
        string withScheme = dohUrl.Contains("://", StringComparison.Ordinal) ? dohUrl : "http://" + dohUrl;
        return Uri.TryCreate(withScheme, UriKind.Absolute, out Uri? url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;
    }

    /// <summary>
    /// Creates the connector the DoH queries are sent through (ADR-0152): a <see cref="TcpConnector" />
    /// of its own over the <see cref="SystemDnsResolver" /> (the DoH server's own name is resolved as any
    /// host is), <paramref name="tcpDialer" />, and a TLS provider routed as
    /// <see cref="CreateTlsProvider" /> routes the options <see cref="TlsClientOptionsMapping.DohFromCommandLine" />
    /// maps. No <c>--resolve</c>, <c>--connect-to</c>, proxy or <c>-4</c>/<c>-6</c> applies to it.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="tcpDialer">Opens each plaintext connection.</param>
    /// <param name="timeProvider">The clock the connector and its TLS provider time on.</param>
    /// <returns>The connector.</returns>
    internal static TcpConnector CreateDohConnector(CommandLineOptions options, ITcpDialer tcpDialer, TimeProvider timeProvider) =>
        new(
            new SystemDnsResolver(),
            tcpDialer,
            CreateTlsProvider(TlsClientOptionsMapping.DohFromCommandLine(options), timeProvider),
            timeProvider);

    /// <summary>
    /// Creates the TLS provider for handshakes run with <paramref name="options" />: the
    /// <see cref="HandBuiltTlsProvider" /> when <see cref="TlsClientRouting.Choose" /> routes them
    /// to the hand-built client (ADR-0140), otherwise the <see cref="SslStreamTlsProvider" />.
    /// </summary>
    /// <param name="options">The origin's or the HTTPS proxy's TLS options.</param>
    /// <param name="timeProvider">The clock the provider times its handshakes on.</param>
    /// <returns>The provider.</returns>
    internal static ITlsProviderWithWarnings CreateTlsProvider(TlsClientOptions options, TimeProvider timeProvider) =>
        TlsClientRouting.Choose(options) == TlsClientRoute.HandBuilt
            ? new HandBuiltTlsProvider(options, timeProvider)
            : new SslStreamTlsProvider(options, timeProvider);

    /// <summary>
    /// Creates the network transports as <see cref="CreateTransports(CommandLineOptions)" /> does,
    /// on <paramref name="timeProvider" /> instead of <see cref="TimeProvider.System" />: the
    /// <see cref="TcpConnector" />, the TLS providers and the
    /// <see cref="UdpDatagramConnector" /> all time on it, so the handshake timestamps the TLS
    /// provider reports are on the connector's clock (ADR-0030).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="timeProvider">The clock every transport times on.</param>
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports(CommandLineOptions options, TimeProvider timeProvider)
    {
        TcpDialer tcpDialer = new(new TcpSocketOptions(options.TcpNoDelay, options.TcpKeepAlive));
        IDnsResolver dnsResolver = CreateDnsResolver(options, timeProvider, tcpDialer);
        TlsClientOptions tlsClientOptions = TlsClientOptionsMapping.FromCommandLine(options);
        ITlsProviderWithWarnings tlsProvider = CreateTlsProvider(tlsClientOptions, timeProvider);
        TlsClientOptions proxyTlsClientOptions = TlsClientOptionsMapping.ProxyFromCommandLine(options);
        ITlsProviderWithWarnings proxyTlsProvider = CreateTlsProvider(proxyTlsClientOptions, timeProvider);
        HttpProxyTunnelOptions proxyTunnelOptions = CreateProxyTunnelOptions(options);
        QuicDialer quicDialer = new(tlsClientOptions, timeProvider);
        TcpConnector tcpConnector = CreateTcpConnector(options, dnsResolver, tcpDialer, tlsProvider, timeProvider, proxyTunnelOptions, proxyTlsProvider, quicDialer);

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
            CreateUdpDatagramConnector(options, dnsResolver, timeProvider),
            new PoolingConnector(tcpConnector, timeProvider));
    }

    /// <summary>
    /// Creates the run's <see cref="TcpConnector" /> over the given pieces, with the
    /// <c>--resolve</c> entries parsed by <see cref="ResolveOverrides.Parse" /> and the
    /// <c>--connect-to</c> mappings of <paramref name="options" />, so a transfer dials the
    /// mapped host and port at the overridden addresses. An entry that does not parse fails
    /// each transfer with exit 49 when it connects, as curl 8.21.0 fails it. Under
    /// <c>--unix-socket</c> or <c>--abstract-unix-socket</c> it dials that socket for every
    /// transfer of the option group instead (<see cref="UnixSocketOf" />, BL-507). HTTP over TLS
    /// offers the ALPN list the version option and the platform choose
    /// (<see cref="HttpVersionMapping.HttpOverTlsApplicationProtocolsOf(RequestedHttpVersion?)" />, ADR-0141).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
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
    /// <returns>The connector.</returns>
    internal static TcpConnector CreateTcpConnector(
        CommandLineOptions options,
        IDnsResolver dnsResolver,
        ITcpDialer tcpDialer,
        ITlsProvider tlsProvider,
        TimeProvider timeProvider,
        HttpProxyTunnelOptions proxyTunnelOptions,
        ITlsProvider? proxyTlsProvider = null,
        QuicDialer? quicDialer = null) =>
        new(
            dnsResolver,
            tcpDialer,
            tlsProvider,
            timeProvider,
            proxyTunnelOptions,
            ResolveOverrides.Parse(options.ResolveEntries),
            new ConnectToMappings(options.ConnectToEntries),
            proxyTlsProvider,
            ConnectTimeoutOf(options),
            AddressFamilyOf(options),
            UnixSocketOf(options),
            HttpVersionMapping.HttpOverTlsApplicationProtocolsOf(options.HttpVersion),
            quicDialer);

    /// <summary>
    /// The Unix domain socket the TCP connector dials in place of each URL's host: the last of
    /// <c>--unix-socket</c> and <c>--abstract-unix-socket</c>, or <see langword="null" /> for
    /// neither (BL-507). Each option group builds its own transports, so each group's pool
    /// holds only connections to its own socket.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The socket, or <see langword="null" /> to dial TCP.</returns>
    internal static UnixSocketAddress? UnixSocketOf(CommandLineOptions options) =>
        options.UnixSocketPath is { } path ? new UnixSocketAddress(path, options.UnixSocketIsAbstract) : null;

    /// <summary>
    /// The address family both connectors dial:<see cref="AddressFamily.InterNetwork" /> under
    /// <c>-4</c>, <see cref="AddressFamily.InterNetworkV6" /> under <c>-6</c>, and
    /// <see cref="AddressFamily.Unspecified" /> (either) when neither was given (BL-500).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The family.</returns>
    internal static AddressFamily AddressFamilyOf(CommandLineOptions options) => options.IpAddressFamily switch
    {
        IpAddressFamilyChoice.IPv4Only => AddressFamily.InterNetwork,
        IpAddressFamilyChoice.IPv6Only => AddressFamily.InterNetworkV6,
        _ => AddressFamily.Unspecified,
    };

    /// <summary>
    /// The connect timeout <see cref="CreateTcpConnector" /> gives the connector: the
    /// <c>--connect-timeout</c> value (curl's 300 seconds when none or 0 was given), or a positive
    /// <c>-m</c> when it runs out sooner, as curl 8.21.0 ends a connect with its connect message
    /// at whichever runs out first (measured, BL-510).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The limit on each connect.</returns>
    internal static TimeSpan ConnectTimeoutOf(CommandLineOptions options)
    {
        TimeSpan connectTimeout = options.ConnectTimeout is { } given && given > TimeSpan.Zero ? given : TcpConnector.DefaultConnectTimeout;
        return options.MaxTime is { } maxTime && maxTime > TimeSpan.Zero && maxTime < connectTimeout ? maxTime : connectTimeout;
    }

    /// <summary>
    /// Creates the run's <see cref="UdpDatagramConnector" />, which TFTP opens its channel
    /// through, with the same <c>--resolve</c> entries and <c>--connect-to</c> mappings
    /// <see cref="CreateTcpConnector" /> applies: curl 8.21.0 applies both to a
    /// <c>tftp://</c> transfer and fails it with exit 49 for an entry that does not parse
    /// (measured).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="dnsResolver">Resolves a host no <c>--resolve</c> entry answers for.</param>
    /// <param name="timeProvider">The clock the connector times on.</param>
    /// <returns>The connector.</returns>
    internal static UdpDatagramConnector CreateUdpDatagramConnector(
        CommandLineOptions options,
        IDnsResolver dnsResolver,
        TimeProvider timeProvider) =>
        new(
            dnsResolver,
            timeProvider,
            ResolveOverrides.Parse(options.ResolveEntries),
            new ConnectToMappings(options.ConnectToEntries),
            AddressFamilyOf(options));

    /// <summary>
    /// Maps the command line to what the CONNECT request through an HTTP proxy carries: the
    /// <c>-A</c> value as its <c>User-Agent</c>, no <c>User-Agent</c> header for <c>-A ""</c>,
    /// <c>curl/8.21.0</c> without <c>-A</c>; the proxy credential encoded as the server
    /// credential is (<see cref="CredentialEncoding.ForPlatform" />, ADR-0022); and the
    /// <c>--proxy-header</c> values verbatim, never the <c>-H</c> ones (ADR-0077); the
    /// <c>-A</c> and <c>--proxy-header</c> text encoded in that same platform encoding (ADR-0067);
    /// and the proxy authenticated with the scheme the <c>--proxy-*</c> auth switches pick
    /// (<see cref="CommandLineOptions.ProxyAuthSchemes" />), answered by the same
    /// <see cref="CreateHttpAuthenticator" /> the origin uses: Basic up front, Digest and
    /// <c>--proxy-anyauth</c> after a <c>407</c> (ADR-0186). Its Negotiate and NTLM contexts come
    /// from a <see cref="SystemSecurityContextFactory" />, with the <c>--proxy-service-name</c> and
    /// <c>--delegation</c> of <see cref="NegotiateOptionsMapping.FromCommandLine" />, though the
    /// authenticator answers neither for a proxy yet (BL-604).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The tunnel's options.</returns>
    internal static HttpProxyTunnelOptions CreateProxyTunnelOptions(CommandLineOptions options) =>
        new(
            options.UserAgent switch
            {
                null => HttpProxyTunnelOptions.Default.UserAgent,
                "" => null,
                string userAgent => userAgent,
            },
            CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()))
        {
            ProxyHeaders = options.ProxyHeaders,
            CommandLineTextEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()),
            ProxyAuthSchemes = options.ProxyAuthSchemes,
            ProxyAuthenticator = CreateHttpAuthenticator(new SystemSecurityContextFactory(), NegotiateOptionsMapping.FromCommandLine(options)),
        };

    /// <summary>
    /// Creates the runner that parses a command line and performs its transfers against
    /// the real disk, the real network and the given standard streams, wrapping warnings at
    /// the width <see cref="TerminalColumns.Resolve()" /> gives. The network
    /// transports are built by <see cref="CreateTransports(CommandLineOptions)" /> once the
    /// command line is parsed, because their TLS settings come from it.
    /// </summary>
    /// <param name="standardOutput">Where a transfer without <c>-o</c> writes its bytes.</param>
    /// <param name="standardError">Where the <c>curl: (N) message</c> lines go.</param>
    /// <param name="standardInput">What a <c>telnet</c> transfer sends to the server.</param>
    /// <param name="standardOutputIsTerminal">
    /// Whether standard output is a terminal, where the progress meter of a transfer with no
    /// <c>-o</c> is hidden, as curl hides it.
    /// </param>
    /// <param name="terminalRendersStyles">
    /// Whether that terminal renders bold, so <c>-i</c> and <c>-I</c> header lines are styled under
    /// <c>--styled-output</c> (<see cref="StandardOutputVirtualTerminal.RendersStyles" />, ADR-0246).
    /// </param>
    /// <returns>The runner, which writes curl's progress meter, opens the <c>-w</c> <c>%output{file}</c> targets on disk (<see cref="DiskWriteOutFileOpener" />), reads the default config file (<c>.curlrc</c>) where <see cref="DefaultConfigFileSearch.ForProcess" /> finds it, and reads the process's environment for the IPFS gateway.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        bool standardOutputIsTerminal,
        bool terminalRendersStyles = false) =>
        new(
            SharingRunCookies((options, cookies) => CreateTransferDispatch(CreateTransports(options), cookies, NegotiateOptionsMapping.FromCommandLine(options))),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            TerminalColumns.Resolve(),
            writesProgressMeter: true,
            standardOutputIsTerminal,
            writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows()),
            writeOutTimeDialect: WriteOutTimeDialectFor(OperatingSystem.IsWindows()),
            outputPaths: new PhysicalOutputPaths(),
            defaultConfigFileSearch: DefaultConfigFileSearch.ForProcess,
            readEnvironmentVariable: name => Environment.GetEnvironmentVariable(name),
            terminalRendersStyles: terminalRendersStyles);

    /// <summary>
    /// Creates the runner with the production handler set built around the given
    /// connectors instead of the real network, so the wiring can be checked without
    /// opening a socket.
    /// </summary>
    /// <param name="standardOutput">Where a transfer without <c>-o</c> writes its bytes.</param>
    /// <param name="standardError">Where the <c>curl: (N) message</c> lines go.</param>
    /// <param name="standardInput">What a <c>telnet</c> transfer sends to the server.</param>
    /// <param name="connector">Connects the TCP protocols.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="proxySelector">
    /// Chooses each transfer's proxy, or <see langword="null" /> for one that reads no
    /// environment variables.
    /// </param>
    /// <param name="securityContexts">
    /// Makes the HTTP handler's Negotiate and NTLM contexts and the mail handlers' GSSAPI and NTLM contexts, or <see langword="null" /> for the production router.
    /// </param>
    /// <param name="signingClock">The clock <c>--aws-sigv4</c> signs with; <see cref="TimeProvider.System" /> when not given.</param>
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        IConnector connector,
        IDatagramConnector datagramConnector,
        ProxySelector? proxySelector = null,
        ISecurityContextFactory? securityContexts = null,
        TimeProvider? signingClock = null) =>
        new(
            SharingRunCookies((options, cookies) => CreateTransferDispatch(connector, datagramConnector, CreateTlsProvider(TlsClientOptionsMapping.FromCommandLine(options), TimeProvider.System), cookies, proxySelector, securityContexts, options, signingClock)),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            writeOutTimeDialect: WriteOutTimeDialectFor(OperatingSystem.IsWindows()),
            outputPaths: new PhysicalOutputPaths());

    /// <summary>
    /// The C runtime whose <c>strftime</c> a <c>-w</c> <c>%time{format}</c> follows on the
    /// platform: the Windows one, as the Windows curl 8.21.0 (mingw, Schannel) prints it, or
    /// glibc, as the Linux curl 8.21.0 does and the macOS build is matched to (ADR-0078).
    /// </summary>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <returns><see cref="WriteOutTimeDialect.WindowsCRuntime" /> on Windows, otherwise <see cref="WriteOutTimeDialect.Glibc" />.</returns>
    internal static WriteOutTimeDialect WriteOutTimeDialectFor(bool runsOnWindows) =>
        runsOnWindows ? WriteOutTimeDialect.WindowsCRuntime : WriteOutTimeDialect.Glibc;

    /// <summary>
    /// Wraps a dispatch factory so every <c>-:</c> / <c>--next</c> option group of one run gets its own
    /// <see cref="CookieEngine" /> over the one <see cref="CookieStore" /> the run shares, as curl 8.21.0
    /// shares its cookie list between groups (measured 2026-09-28, BL-509 Notes).
    /// </summary>
    /// <param name="createTransferDispatch">Builds a group's dispatch from its options and its cookies.</param>
    /// <returns>The factory the runner calls once per option group.</returns>
    internal static Func<CommandLineOptions, TransferDispatch> SharingRunCookies(
        Func<CommandLineOptions, CookieEngine?, TransferDispatch> createTransferDispatch)
    {
        CookieStore runCookies = new();
        return options => createTransferDispatch(options, CookieEngine.FromCommandLine(options, runCookies));
    }

    /// <summary>
    /// Creates the dispatcher over the production handler set, connecting the TCP protocols
    /// through <paramref name="transports" />' <see cref="CurlTransports.PoolingConnector" />
    /// and keeping no cookies.
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <returns>The dispatcher.</returns>
    internal static ProtocolDispatcher CreateDispatcher(CurlTransports transports) =>
        new(CreateProtocolHandlers(transports.PoolingConnector, transports.UdpDatagramConnector, transports.TlsProvider, transports.DnsResolver, proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes));

    /// <summary>
    /// Creates what one run transfers through: the production handler set, every TCP handler
    /// connecting through <paramref name="transports" />' one
    /// <see cref="CurlTransports.PoolingConnector" />, its HTTP handler keeping cookies in
    /// <paramref name="cookies" />; the proxy TLS provider's <see cref="ITlsProviderWithWarnings.Warnings" />
    /// as the lines printed before each transfer, as curl 8.21.0 prints its one Schannel warning,
    /// about the proxy's CA path, once per URL for <c>--capath</c>, <c>--proxy-capath</c> or both
    /// (measured, proxy or not), and the proxy's CA path is <c>--proxy-capath</c> or else
    /// <c>--capath</c>; <paramref name="cookies" /> for the runner to load and save; a
    /// <see cref="ProxySelector" /> reading the process's proxy environment variables; the
    /// pooling connector as the connection pool the runner disposes when the run ends (ADR-0050);
    /// and the TCP connector's <see cref="TcpConnector.LoadResolveEntries" />, which the runner
    /// calls at the start of every transfer (BL-486).
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <param name="cookies">The run's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <param name="negotiateOptions">The service names and delegation the HTTP handler's Negotiate answers with; <see cref="NegotiateOptions.Default" /> when <see langword="null" />.</param>
    /// <returns>The dispatcher, the warning lines, the cookies, the proxy selector, the connection pool and the <c>--resolve</c> loader.</returns>
    internal static TransferDispatch CreateTransferDispatch(CurlTransports transports, CookieEngine? cookies = null, NegotiateOptions? negotiateOptions = null) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(transports.PoolingConnector, transports.UdpDatagramConnector, transports.TlsProvider, transports.DnsResolver, cookies?.HandlerStore, proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes, negotiateOptions: negotiateOptions)),
            transports.ProxyTlsProvider.Warnings,
            cookies,
            new ProxySelector(Environment.GetEnvironmentVariable),
            transports.PoolingConnector,
            transports.TcpConnector.LoadResolveEntries);

    /// <summary>
    /// Creates what one run transfers through over the given connectors instead of the real
    /// network: the production handler set, its HTTP handler keeping cookies in
    /// <paramref name="cookies" />, no warning lines, <paramref name="cookies" /> and <paramref name="proxySelector" />.
    /// </summary>
    /// <param name="connector">Connects the TCP protocols.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="tlsProvider">Upgrades an FTP connection after an accepted <c>AUTH</c> or <c>PROT P</c>.</param>
    /// <param name="cookies">The run's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <param name="proxySelector">Chooses each transfer's proxy, or <see langword="null" /> for one that reads no environment variables.</param>
    /// <param name="securityContexts">Makes the HTTP handler's Negotiate and NTLM contexts and the mail handlers' GSSAPI and NTLM contexts, or <see langword="null" /> for the production router.</param>
    /// <param name="options">
    /// The option group: its <c>--proxy-*</c> auth switches pick the scheme the HTTP handler answers a
    /// forward proxy with, and its <c>--service-name</c>, <c>--proxy-service-name</c> and
    /// <c>--delegation</c> shape its Negotiate answers.
    /// </param>
    /// <param name="signingClock">The clock <c>--aws-sigv4</c> signs with; <see cref="TimeProvider.System" /> when <see langword="null" />.</param>
    /// <returns>The dispatcher, no warning lines, the cookies and the proxy selector.</returns>
    private static TransferDispatch CreateTransferDispatch(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ITlsProvider tlsProvider,
        CookieEngine? cookies,
        ProxySelector? proxySelector,
        ISecurityContextFactory? securityContexts,
        CommandLineOptions options,
        TimeProvider? signingClock) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(connector, datagramConnector, tlsProvider, new SystemDnsResolver(), cookies?.HandlerStore, securityContexts, options.ProxyAuthSchemes, NegotiateOptionsMapping.FromCommandLine(options), signingClock)),
            [],
            cookies,
            proxySelector);
}
