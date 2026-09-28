using System.Text;
using Curl.Authentication;
using Curl.Cli;
using Curl.Cookies;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Networking;
using Curl.Output;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict;
using Curl.Protocol.File;
using Curl.Protocol.Ftp;
using Curl.Protocol.Gopher;
using Curl.Protocol.Http;
using Curl.Protocol.Mqtt;
using Curl.Protocol.Telnet;
using Curl.Protocol.Tftp;

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
    /// and <c>mqtts</c>, and <c>http</c> and <c>https</c> over <paramref name="connector" />,
    /// the last two answering authentication with <see cref="CreateHttpAuthenticator" />'s
    /// authenticator and keeping cookies in <paramref name="cookieStore" />; and <c>tftp</c> over
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
    /// <param name="connector">Connects the TCP protocols, with TLS for <c>gophers</c>, <c>mqtts</c> and <c>ftps</c>.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="tlsProvider">Upgrades an FTP connection after an accepted <c>AUTH</c> or <c>PROT P</c>.</param>
    /// <param name="dnsResolver">Resolves a host name given to <c>-P</c>.</param>
    /// <param name="cookieStore">
    /// The cookies the HTTP handler sends and stores, or <see langword="null" /> to keep none.
    /// </param>
    /// <returns>Every registered handler.</returns>
    internal static IReadOnlyList<IProtocolHandler> CreateProtocolHandlers(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ITlsProvider tlsProvider,
        IDnsResolver dnsResolver,
        ICookieStore? cookieStore = null)
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector recordingConnector = new(connector, recorder);
        EndPointRecordingDatagramConnector recordingDatagramConnector = new(datagramConnector, recorder);
        HttpProtocolHandler http = new(recordingConnector, CreateHttpAuthenticator(), cookieStore);

        IProtocolHandler[] handlers =
        [
            new FileProtocolHandler(new PhysicalFileSystem()),
            new DictProtocolHandler(recordingConnector),
            new GopherProtocolHandler(recordingConnector),
            new TelnetProtocolHandler(recordingConnector),
            new TftpProtocolHandler(recordingDatagramConnector, recordingConnector, CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())),
            new MqttProtocolHandler(recordingConnector),
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
    /// scheme curl 8.21.0 picks among those <c>--basic</c>, <c>--digest</c> and <c>--anyauth</c>
    /// allow, with a <see cref="BasicAndBearerAuthenticator" /> for Basic, Bearer
    /// (<c>--oauth2-bearer</c>) and the first request, and a <see cref="DigestAuthenticator" />
    /// drawing each client nonce from <see cref="DigestClientNonce.CreateRandom" />; both encode
    /// credentials in the platform's encoding (<see cref="CredentialEncoding.ForPlatform" />).
    /// </summary>
    /// <returns>The authenticator.</returns>
    internal static RankedHttpAuthenticator CreateHttpAuthenticator()
    {
        Encoding credentialEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows());

        return new RankedHttpAuthenticator(
            new BasicAndBearerAuthenticator(credentialEncoding),
            new DigestAuthenticator(credentialEncoding, DigestClientNonce.CreateRandom));
    }

    /// <summary>
    /// Creates the SASL authenticator the SMTP, POP3 and IMAP handlers share (ADR-0121): a
    /// <see cref="SaslAuthenticator" /> encoding credentials in the platform's encoding
    /// (<see cref="CredentialEncoding.ForPlatform" />), as the HTTP authenticator does.
    /// </summary>
    /// <returns>The authenticator.</returns>
    internal static ISaslAuthenticator CreateSaslAuthenticator() =>
        new SaslAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()));

    /// <summary>
    /// Creates the network transports for one run: a <see cref="TcpConnector" /> over a
    /// <see cref="SystemDnsResolver" />, a <see cref="TcpDialer" /> that sets <c>TCP_NODELAY</c> and
    /// <c>SO_KEEPALIVE</c> unless <c>--no-tcp-nodelay</c> or <c>--no-keepalive</c> says not to, and an
    /// <see cref="SslStreamTlsProvider" />, and a <see cref="UdpDatagramConnector" />. Both
    /// connectors and the TLS provider share the one resolver and <see cref="TimeProvider.System" />. TLS uses
    /// the <see cref="TlsClientOptions" /> mapped from <paramref name="options" /> by
    /// <see cref="TlsClientOptionsMapping.FromCommandLine" />, one set shared by every URL on
    /// the command line. The handshake to an HTTPS proxy runs through a second
    /// <see cref="SslStreamTlsProvider" /> on the same clock, with the <see cref="TlsClientOptions" />
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
    /// Creates the network transports as <see cref="CreateTransports(CommandLineOptions)" /> does,
    /// on <paramref name="timeProvider" /> instead of <see cref="TimeProvider.System" />: the
    /// <see cref="TcpConnector" />, the <see cref="SslStreamTlsProvider" /> and the
    /// <see cref="UdpDatagramConnector" /> all time on it, so the handshake timestamps the TLS
    /// provider reports are on the connector's clock (ADR-0030).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="timeProvider">The clock every transport times on.</param>
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports(CommandLineOptions options, TimeProvider timeProvider)
    {
        SystemDnsResolver dnsResolver = new();
        TcpDialer tcpDialer = new(new TcpSocketOptions(options.TcpNoDelay, options.TcpKeepAlive));
        TlsClientOptions tlsClientOptions = TlsClientOptionsMapping.FromCommandLine(options);
        SslStreamTlsProvider tlsProvider = new(tlsClientOptions, timeProvider);
        TlsClientOptions proxyTlsClientOptions = TlsClientOptionsMapping.ProxyFromCommandLine(options);
        SslStreamTlsProvider proxyTlsProvider = new(proxyTlsClientOptions, timeProvider);
        HttpProxyTunnelOptions proxyTunnelOptions = CreateProxyTunnelOptions(options);
        TcpConnector tcpConnector = CreateTcpConnector(options, dnsResolver, tcpDialer, tlsProvider, timeProvider, proxyTunnelOptions, proxyTlsProvider);

        return new CurlTransports(
            dnsResolver,
            timeProvider,
            tcpDialer,
            tlsClientOptions,
            tlsProvider,
            proxyTlsClientOptions,
            proxyTlsProvider,
            proxyTunnelOptions,
            tcpConnector,
            CreateUdpDatagramConnector(options, dnsResolver, timeProvider),
            new PoolingConnector(tcpConnector, timeProvider));
    }

    /// <summary>
    /// Creates the run's <see cref="TcpConnector" /> over the given pieces, with the
    /// <c>--resolve</c> entries parsed by <see cref="ResolveOverrides.Parse" /> and the
    /// <c>--connect-to</c> mappings of <paramref name="options" />, so a transfer dials the
    /// mapped host and port at the overridden addresses. An entry that does not parse fails
    /// each transfer with exit 49 when it connects, as curl 8.21.0 fails it.
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
    /// <returns>The connector.</returns>
    internal static TcpConnector CreateTcpConnector(
        CommandLineOptions options,
        IDnsResolver dnsResolver,
        ITcpDialer tcpDialer,
        ITlsProvider tlsProvider,
        TimeProvider timeProvider,
        HttpProxyTunnelOptions proxyTunnelOptions,
        ITlsProvider? proxyTlsProvider = null) =>
        new(
            dnsResolver,
            tcpDialer,
            tlsProvider,
            timeProvider,
            proxyTunnelOptions,
            ResolveOverrides.Parse(options.ResolveEntries),
            new ConnectToMappings(options.ConnectToEntries),
            proxyTlsProvider,
            ConnectTimeoutOf(options));

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
            new ConnectToMappings(options.ConnectToEntries));

    /// <summary>
    /// Maps the command line to what the CONNECT request through an HTTP proxy carries: the
    /// <c>-A</c> value as its <c>User-Agent</c>, no <c>User-Agent</c> header for <c>-A ""</c>,
    /// <c>curl/8.21.0</c> without <c>-A</c>; the proxy credential encoded as the server
    /// credential is (<see cref="CredentialEncoding.ForPlatform" />, ADR-0022); and the
    /// <c>--proxy-header</c> values verbatim, never the <c>-H</c> ones (ADR-0077); the
    /// <c>-A</c> and <c>--proxy-header</c> text encoded in that same platform encoding (ADR-0067).
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
    /// <returns>The runner, which writes curl's progress meter, opens the <c>-w</c> <c>%output{file}</c> targets on disk (<see cref="DiskWriteOutFileOpener" />), reads the default config file (<c>.curlrc</c>) where <see cref="DefaultConfigFileSearch.ForProcess" /> finds it, and reads the process's environment for the IPFS gateway.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        bool standardOutputIsTerminal) =>
        new(
            SharingRunCookies((options, cookies) => CreateTransferDispatch(CreateTransports(options), cookies)),
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
            readEnvironmentVariable: name => Environment.GetEnvironmentVariable(name));

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
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        IConnector connector,
        IDatagramConnector datagramConnector,
        ProxySelector? proxySelector = null) =>
        new(
            SharingRunCookies((options, cookies) => CreateTransferDispatch(connector, datagramConnector, new SslStreamTlsProvider(TlsClientOptionsMapping.FromCommandLine(options), TimeProvider.System), cookies, proxySelector)),
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
        new(CreateProtocolHandlers(transports.PoolingConnector, transports.UdpDatagramConnector, transports.TlsProvider, transports.DnsResolver));

    /// <summary>
    /// Creates what one run transfers through: the production handler set, every TCP handler
    /// connecting through <paramref name="transports" />' one
    /// <see cref="CurlTransports.PoolingConnector" />, its HTTP handler keeping cookies in
    /// <paramref name="cookies" />; the proxy TLS provider's <see cref="SslStreamTlsProvider.Warnings" />
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
    /// <returns>The dispatcher, the warning lines, the cookies, the proxy selector, the connection pool and the <c>--resolve</c> loader.</returns>
    internal static TransferDispatch CreateTransferDispatch(CurlTransports transports, CookieEngine? cookies = null) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(transports.PoolingConnector, transports.UdpDatagramConnector, transports.TlsProvider, transports.DnsResolver, cookies?.HandlerStore)),
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
    /// <returns>The dispatcher, no warning lines, the cookies and the proxy selector.</returns>
    private static TransferDispatch CreateTransferDispatch(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ITlsProvider tlsProvider,
        CookieEngine? cookies,
        ProxySelector? proxySelector) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(connector, datagramConnector, tlsProvider, new SystemDnsResolver(), cookies?.HandlerStore)),
            [],
            cookies,
            proxySelector);
}
