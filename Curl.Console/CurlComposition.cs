using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
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
using Curl.Protocol.Smb;
using Curl.Protocol.Smtp;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Telnet;
using Curl.Protocol.Tftp;
using Curl.Protocol.Ws;
using Curl.Engine;
using static Curl.Engine.EngineComposition;

namespace Curl.Console;

/// <summary>
/// The composition root: builds every service the executable needs with plain constructor
/// calls. There is no container, no reflection and no assembly scanning, so native
/// ahead-of-time publishing sees every type that is used.
/// </summary>
internal static class CurlComposition
{
    /// <summary>
    /// <see cref="CurlEasyErrorText.Of" /> as one delegate made once, so no call site re-checks a
    /// compiler-cached method-group delegate (BL-2291).
    /// </summary>
    private static readonly Func<CurlExitCode, string> EasyErrorText = new(CurlEasyErrorText.Of);

    /// <summary>
    /// Creates the runner that parses a command line and performs its transfers against
    /// the real disk, the real network and the given standard streams, wrapping warnings at
    /// the width <see cref="TerminalColumns.Resolve()" /> gives. The network
    /// transports are built for each option group by
    /// <see cref="CreateTransports(CommandLineOptions, EasyNetworkOptions, EasyTlsOptions, EasyHttpOptions, EasyProxyOptions, TimeProvider, ConnectionCache?, TlsSessionCache?, IDiagnosticLog?, DnsCache?, Func{CurlExitCode, string}?)" /> once the
    /// command line is parsed, because their TLS settings come from it, over one
    /// <see cref="ConnectionCache" /> the runner closes when the run ends, so a later group reuses
    /// an earlier group's connection when their settings match (ADR-0285, BL-754).
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
        bool terminalRendersStyles = false)
    {
        ConnectionCache runConnections = new(TimeProvider.System);
        TlsSessionCache tlsSessions = new(TimeProvider.System);
        DnsCache runDnsCache = new();
        LateBoundDiagnosticLog runLog = new();
        return new(
            SharingRunCookies((options, cookies) => CreateTransferDispatch(CreateTransports(options, EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), TimeProvider.System, runConnections, tlsSessions, runLog, runDnsCache, EasyErrorText), cookies, NegotiateOptionsMapping.FromCommandLine(options))),
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
            terminalRendersStyles: terminalRendersStyles,
            accountHomeDirectory: AccountHomeDirectory.ForProcess,
            runConnectionCache: runConnections,
            tlsSessions: tlsSessions,
            extendedAttributeWriter: NativeExtendedAttributeWriter.ForCurrentPlatform(),
            lateBoundDiagnosticLog: runLog,
            transferPerformer: new TransferPerformer(TimeProvider.System, EasyErrorText));
    }

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
    /// <param name="runConnections">
    /// The run's connection cache, which each option group then reaches <paramref name="connector" />
    /// through (<see cref="CreatePoolingConnector" />) and the runner closes when the run ends; or
    /// <see langword="null" /> to connect through <paramref name="connector" /> itself.
    /// </param>
    /// <param name="parsesAsWindowsBuild">
    /// Whether the runner reads the command line as curl's Windows Schannel build does; <see langword="null" />
    /// for this process's platform. Tests pass <see langword="false" /> to reach <c>--http2</c>, <c>--http3</c> and
    /// the TLS-SRP options on every platform (ADR-0397).
    /// </param>
    /// <param name="writesProgressMeter">
    /// Whether the runner writes curl's progress meter and the <c>-#</c> bar, as the executable's runner
    /// does; off by default, so tests that pin standard error see none. The conformance harness turns it on (BL-1445).
    /// </param>
    /// <param name="writeOutFileOpener">
    /// Opens the <c>-w</c> <c>%output{file}</c> targets, such as a <see cref="DiskWriteOutFileOpener" />; or
    /// <see langword="null" /> for the runner's <see cref="RefusingWriteOutFileOpener" />, which opens none (BL-1445).
    /// </param>
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        IConnector connector,
        IDatagramConnector datagramConnector,
        ProxySelector? proxySelector = null,
        ISecurityContextFactory? securityContexts = null,
        TimeProvider? signingClock = null,
        ConnectionCache? runConnections = null,
        bool? parsesAsWindowsBuild = null,
        bool writesProgressMeter = false,
        IWriteOutFileOpener? writeOutFileOpener = null)
    {
        LateBoundDiagnosticLog runLog = new();
        return new(
            SharingRunCookies((options, cookies) => CreateTransferDispatch(GroupConnectorOf(options, connector, runConnections), datagramConnector, CreateTlsProvider(TlsClientOptionsMapping.Of(EasyOptionsFromCommandLine.TlsOf(options), options.UseAlpn, options.SslSessionsFile), TimeProvider.System), cookies, proxySelector, securityContexts, options, signingClock, runLog)),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            writesProgressMeter: writesProgressMeter,
            writeOutFileOpener: writeOutFileOpener,
            writeOutTimeDialect: WriteOutTimeDialectFor(OperatingSystem.IsWindows()),
            outputPaths: new PhysicalOutputPaths(),
            runConnectionCache: runConnections,
            lateBoundDiagnosticLog: runLog,
            parsesAsWindowsBuild: parsesAsWindowsBuild,
            transferPerformer: new TransferPerformer(TimeProvider.System, EasyErrorText));
    }

    /// <summary>
    /// Creates the runner with the production <see cref="TcpConnector" /> each option group gets -
    /// <see cref="CreateTcpConnector" /> over the group's options, its CONNECT tunnel, HAProxy line,
    /// <c>.onion</c> refusal, <c>--resolve</c> and <c>--connect-to</c> - with only the TCP dial and
    /// the name resolver replaced, so those reach an in-process server as they reach a real one (BL-1831).
    /// Each group's connector sits behind <see cref="CreatePoolingConnector" /> over one run cache.
    /// </summary>
    /// <param name="standardOutput">Where a transfer without <c>-o</c> writes its bytes.</param>
    /// <param name="standardError">Where the <c>curl: (N) message</c> lines go.</param>
    /// <param name="standardInput">What a <c>telnet</c> transfer sends to the server.</param>
    /// <param name="tcpDialer">Opens every plaintext TCP connection the connector dials.</param>
    /// <param name="dnsResolver">Resolves every host name the connector looks up.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="writesProgressMeter">Whether the runner writes curl's progress meter and the <c>-#</c> bar.</param>
    /// <param name="writeOutFileOpener">Opens the <c>-w</c> <c>%output{file}</c> targets; <see langword="null" /> for none.</param>
    /// <param name="usesHandBuiltNtlm">
    /// Whether NTLM, to the origin and to a CONNECT proxy, is answered by the hand-built context on
    /// every platform, as curl's non-SSPI build answers it (BL-1858); otherwise ADR-0142's router
    /// sends it to SSPI on Windows.
    /// </param>
    /// <param name="readEnvironmentVariable">
    /// Reads every environment variable the run reads - the proxy variables, <c>IPFS_GATEWAY</c>,
    /// <c>HOME</c> for the netrc file, <c>COLUMNS</c> for the width warnings and notes wrap at
    /// (<see cref="TerminalColumns" />, no console consulted; BL-2017) and the rest - so a test gives one run its own environment
    /// without touching the process's (BL-1928); <see langword="null" /> for a run that reads none.
    /// </param>
    /// <param name="ftpListener">Listens for FTP's active-mode (<c>-P</c>) data connections; a <see cref="TcpConnectionListener" /> when <see langword="null" /> (BL-1907).</param>
    /// <param name="defaultConfigFileSearch">Where the run looks for the default config file (<c>.curlrc</c>); <see langword="null" /> for a run that reads none (BL-1977).</param>
    /// <param name="signingClock">The clock <c>--aws-sigv4</c> and the <c>--httpsig-*</c> options sign with; <see cref="TimeProvider.System" /> when not given (BL-2183).</param>
    /// <param name="honoursDnsServerVariable">
    /// Whether a non-empty <c>CURL_DNS_SERVER</c> makes each option group resolve through
    /// <see cref="DnsServerVariableResolverOf" />, as curl's debug build does; off by default, as in
    /// curl's release build, and turned on by the conformance harness (ADR-0492, BL-2248).
    /// </param>
    /// <param name="honoursHstsTimeVariable">
    /// Whether a <c>CURL_TIME</c> holding whole seconds since the epoch fixes the HSTS clock at that
    /// instant (<see cref="HstsClockOf" />), as curl's debug build reads it in <c>lib/hsts.c</c>; off by
    /// default, as in curl's release build, and turned on by the conformance harness (ADR-0493, BL-2234).
    /// </param>
    /// <param name="honoursHstsHttpVariable">
    /// Whether a set <c>CURL_HSTS_HTTP</c> (any value, empty included) makes the run's HSTS cache learn
    /// a <c>Strict-Transport-Security</c> header received over plain <c>http</c>, as curl's debug build
    /// does in <c>lib/http.c</c>; off by default, as in curl's release build, and turned on by the
    /// conformance harness (ADR-0494, BL-2250).
    /// </param>
    /// <param name="honoursAltSvcHttpVariable">
    /// Whether a set <c>CURL_ALTSVC_HTTP</c> (any value, empty included) makes each <c>--alt-svc</c>
    /// cache learn <c>Alt-Svc</c> headers received over plain <c>http</c> and look up a plain
    /// <c>http</c> origin's alternatives, as curl's debug build does in <c>lib/http.c</c> and
    /// <c>lib/url.c</c>; off by default, as in curl's release build, and turned on by the conformance
    /// harness (ADR-0497, BL-2263).
    /// </param>
    /// <param name="parsesAsWindowsBuild">
    /// Whether the runner reads the command line as curl's Windows Schannel build does; <see langword="null" />
    /// for this process's platform. Tests pass <see langword="false" /> to reach <c>--http3</c> on every platform (ADR-0397, BL-2260).
    /// </param>
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        ITcpDialer tcpDialer,
        IDnsResolver dnsResolver,
        IDatagramConnector datagramConnector,
        bool writesProgressMeter = false,
        IWriteOutFileOpener? writeOutFileOpener = null,
        bool usesHandBuiltNtlm = false,
        Func<string, string?>? readEnvironmentVariable = null,
        IConnectionListener? ftpListener = null,
        DefaultConfigFileSearch? defaultConfigFileSearch = null,
        TimeProvider? signingClock = null,
        bool honoursDnsServerVariable = false,
        bool honoursHstsTimeVariable = false,
        bool honoursHstsHttpVariable = false,
        bool honoursAltSvcHttpVariable = false,
        bool? parsesAsWindowsBuild = null)
    {
        ConnectionCache runConnections = new(TimeProvider.System);
        LateBoundDiagnosticLog runLog = new();
        Func<string, string?> environment = readEnvironmentVariable ?? (_ => null);
        string? dnsServer = honoursDnsServerVariable ? environment(DnsServerVariableName) : null;
        return new(
            SharingRunCookies((options, cookies) => CreateDialingTransferDispatch(tcpDialer, DnsServerVariableResolverOf(EasyOptionsFromCommandLine.NetworkOf(options), dnsServer, dnsResolver, datagramConnector), datagramConnector, cookies, options, runConnections, runLog, usesHandBuiltNtlm, environment, ftpListener, signingClock)),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            terminalColumns: TerminalColumns.Resolve(environment(TerminalColumns.ColumnsVariableName), () => null),
            writesProgressMeter: writesProgressMeter,
            writeOutFileOpener: writeOutFileOpener,
            writeOutTimeDialect: WriteOutTimeDialectFor(OperatingSystem.IsWindows()),
            outputPaths: new PhysicalOutputPaths(),
            defaultConfigFileSearch: defaultConfigFileSearch,
            readEnvironmentVariable: readEnvironmentVariable,
            runConnectionCache: runConnections,
            lateBoundDiagnosticLog: runLog,
            hstsClock: honoursHstsTimeVariable ? HstsClockOf(environment(HstsTimeVariableName)) : null,
            hstsLearnsOverHttp: LearnsHstsOverHttp(honoursHstsHttpVariable, environment),
            altSvcUsesOverHttp: UsesAltSvcOverHttp(honoursAltSvcHttpVariable, environment),
            parsesAsWindowsBuild: parsesAsWindowsBuild,
            transferPerformer: new TransferPerformer(TimeProvider.System, EasyErrorText));
    }

    /// <summary>
    /// Builds one option group's dispatch for the dialing <see cref="CreateRunner(Stream, Stream, Stream, ITcpDialer, IDnsResolver, IDatagramConnector, bool, IWriteOutFileOpener?, bool, Func{string, string?}?, IConnectionListener?, DefaultConfigFileSearch?, TimeProvider?, bool, bool, bool, bool, bool?)" />:
    /// the group's <see cref="TcpConnector" /> over <paramref name="tcpDialer" /> and
    /// <paramref name="dnsResolver" />, pooled in <paramref name="runConnections" />, with a proxy
    /// selector that reads the environment through <paramref name="readEnvironmentVariable" />, and the connector's <c>--resolve</c> entries
    /// loaded before each URL as the executable loads them.
    /// </summary>
    /// <param name="tcpDialer">Opens every plaintext TCP connection.</param>
    /// <param name="dnsResolver">Resolves every host name.</param>
    /// <param name="datagramConnector">Opens the UDP channels.</param>
    /// <param name="cookies">The run's cookie engine, or <see langword="null" />.</param>
    /// <param name="options">The option group.</param>
    /// <param name="runConnections">The run's connection cache.</param>
    /// <param name="diagnosticLog">The run's diagnostic log.</param>
    /// <param name="usesHandBuiltNtlm">Whether NTLM is answered by the hand-built context on every platform (<see cref="CreateDialingSecurityContextFactory" />).</param>
    /// <param name="readEnvironmentVariable">Reads the proxy environment variables.</param>
    /// <param name="ftpListener">Listens for FTP's active-mode data connections, or <see langword="null" /> for a <see cref="TcpConnectionListener" />.</param>
    /// <param name="signingClock">The clock <c>--aws-sigv4</c> and the <c>--httpsig-*</c> options sign with; <see cref="TimeProvider.System" /> when <see langword="null" />.</param>
    /// <returns>The dispatch.</returns>
    private static TransferDispatch CreateDialingTransferDispatch(
        ITcpDialer tcpDialer,
        IDnsResolver dnsResolver,
        IDatagramConnector datagramConnector,
        CookieEngine? cookies,
        CommandLineOptions options,
        ConnectionCache runConnections,
        IDiagnosticLog diagnosticLog,
        bool usesHandBuiltNtlm,
        Func<string, string?> readEnvironmentVariable,
        IConnectionListener? ftpListener,
        TimeProvider? signingClock)
    {
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(options);
        EasyTlsOptions tls = EasyOptionsFromCommandLine.TlsOf(options);
        EasyHttpOptions http = EasyOptionsFromCommandLine.HttpOf(options);
        EasyProxyOptions proxy = EasyOptionsFromCommandLine.ProxyOf(options);
        TlsClientOptions tlsClientOptions = TlsClientOptionsMapping.Of(tls, options.UseAlpn, options.SslSessionsFile);
        ITlsProviderWithWarnings tlsProvider = CreateTlsProvider(tlsClientOptions, TimeProvider.System);
        ITlsProviderWithWarnings proxyTlsProvider = CreateTlsProvider(TlsClientOptionsMapping.ProxyOf(tls, options.UseAlpn), TimeProvider.System);
        LateBoundSecurityContextFactory proxyContexts = new() { UsesHandBuiltNtlm = usesHandBuiltNtlm };
        HttpProxyTunnelOptions proxyTunnelOptions = CreateProxyTunnelOptions(options, proxy, proxyContexts, diagnosticLog);
        QuicDialer quicDialer = CreateDatagramConnectorQuicDialer(options, tlsClientOptions, datagramConnector);
        TcpConnector tcpConnector = CreateTcpConnector(options, network, http, proxy, dnsResolver, tcpDialer, tlsProvider, TimeProvider.System, proxyTunnelOptions, proxyTlsProvider, quicDialer, socks5SecurityContexts: proxyContexts);
        PoolingConnector poolingConnector = CreatePoolingConnector(options, network, tls, http, proxy, tcpConnector, TimeProvider.System, runConnections);
        ISecurityContextFactory securityContexts = CreateDialingSecurityContextFactory(poolingConnector, datagramConnector, diagnosticLog, usesHandBuiltNtlm);
        proxyContexts.Bind(securityContexts);
        return new(
            new ProtocolDispatcher(CreateProtocolHandlers(poolingConnector, datagramConnector, tlsProvider, dnsResolver, cookies?.HandlerStore, securityContexts, proxyAuthSchemes: proxyTunnelOptions.ProxyAuthSchemes, negotiateOptions: NegotiateOptionsMapping.FromCommandLine(options), signingClock: signingClock, ftpDataConnector: poolingConnector.Over(tcpConnector.ForFtpDataConnections()), diagnosticLog: diagnosticLog, ftpListener: ftpListener)),
            [],
            cookies,
            new ProxySelector(readEnvironmentVariable),
            poolingConnector,
            tcpConnector.LoadResolveEntries);
    }

    /// <summary>
    /// The connector an option group of a runner over fake connectors connects through:
    /// <paramref name="connector" /> itself, or, with the run's <paramref name="runConnections" />,
    /// the group's <see cref="CreatePoolingConnector" /> over it.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="connector">The fake connector.</param>
    /// <param name="runConnections">The run's connection cache, or <see langword="null" />.</param>
    /// <returns>The group's connector.</returns>
    private static IConnector GroupConnectorOf(CommandLineOptions options, IConnector connector, ConnectionCache? runConnections) =>
        runConnections is null ? connector : CreatePoolingConnector(options, EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), connector, TimeProvider.System, runConnections);

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
        return options => createTransferDispatch(options, CookieEngine.FromHttpOptions(EasyOptionsFromCommandLine.HttpOf(options), runCookies));
    }

    /// <summary>
    /// Creates the dispatcher over the production handler set, connecting the TCP protocols
    /// through <paramref name="transports" />' <see cref="CurlTransports.PoolingConnector" />
    /// and keeping no cookies.
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <returns>The dispatcher.</returns>
    internal static ProtocolDispatcher CreateDispatcher(CurlTransports transports) =>
        new(CreateProtocolHandlers(transports.PoolingConnector, transports.UdpDatagramConnector, transports.TlsProvider, transports.DnsResolver, proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes, ftpDataConnector: FtpDataConnectorOf(transports), diagnosticLog: transports.DiagnosticLog, tracesFtp: transports.TracesFtp, tracesSmtp: transports.TracesSmtp, tracesWs: transports.TracesWs, tracesSsh: transports.TracesSsh, tracesHttp2: transports.TracesHttp2, tracesHttp3: transports.TracesHttp3, tracesRead: transports.TracesRead));

    /// <summary>
    /// Creates what one run transfers through: the production handler set, every TCP handler
    /// connecting through <paramref name="transports" />' one
    /// <see cref="CurlTransports.PoolingConnector" />, its HTTP handler keeping cookies in
    /// <paramref name="cookies" />; <see cref="WarningLinesBeforeEachTransfer" /> on this platform's
    /// curl build as the lines printed before each transfer; <paramref name="cookies" /> for the runner to load and save; a
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
        CreateTransferDispatch(transports, OperatingSystem.IsWindows(), cookies, negotiateOptions);

    /// <summary>
    /// Returns the lines curl 8.21.0 prints to standard error before each URL's transfer, unless
    /// <c>-s</c> is given, for TLS options its build ignores (measured, BL-1034): the proxy TLS
    /// provider's <see cref="ITlsProviderWithWarnings.Warnings" />, the one Schannel warning about
    /// the proxy's CA path for <c>--capath</c>, <c>--proxy-capath</c> or both (the proxy's CA path
    /// is <c>--proxy-capath</c> or else <c>--capath</c>); then, in the Schannel build only, one
    /// line for <c>--tls13-ciphers</c> and then one for <c>--proxy-tls13-ciphers</c>, each when
    /// given, whatever the URL's scheme and whether or not a proxy is used (ADR-0349).
    /// </summary>
    /// <param name="transports">The run's connectors and the TLS options they were built from.</param>
    /// <param name="matchesSchannelBuild">Whether the run behaves like curl's Schannel build.</param>
    /// <returns>The warning lines, each unwrapped and without its line ending.</returns>
    internal static IReadOnlyList<string> WarningLinesBeforeEachTransfer(CurlTransports transports, bool matchesSchannelBuild)
    {
        if (!matchesSchannelBuild)
        {
            return transports.ProxyTlsProvider.Warnings;
        }

        List<string> lines = [.. transports.ProxyTlsProvider.Warnings];
        if (transports.TlsClientOptions.Tls13Ciphers is not null)
        {
            lines.Add(SchannelIgnoredOptionWarning("--tls13-ciphers"));
        }

        if (transports.ProxyTlsClientOptions.Tls13Ciphers is not null)
        {
            lines.Add(SchannelIgnoredOptionWarning("--proxy-tls13-ciphers"));
        }

        return lines;
    }

    private static string SchannelIgnoredOptionWarning(string option) =>
        $"Warning: ignoring {option}, not supported by libcurl with Schannel";

    /// <summary>
    /// Creates what one run transfers through as <see cref="CreateTransferDispatch(CurlTransports, CookieEngine?, NegotiateOptions?)" />
    /// does, warning as the named curl build does.
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <param name="matchesSchannelBuild">Whether the run warns as curl's Schannel build does.</param>
    /// <param name="cookies">The run's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <param name="negotiateOptions">The service names and delegation the HTTP handler's Negotiate answers with; <see cref="NegotiateOptions.Default" /> when <see langword="null" />.</param>
    /// <returns>The dispatcher, the warning lines, the cookies, the proxy selector, the connection pool and the <c>--resolve</c> loader.</returns>
    internal static TransferDispatch CreateTransferDispatch(CurlTransports transports, bool matchesSchannelBuild, CookieEngine? cookies = null, NegotiateOptions? negotiateOptions = null) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(transports.PoolingConnector, transports.UdpDatagramConnector, transports.TlsProvider, transports.DnsResolver, cookies?.HandlerStore, proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes, negotiateOptions: negotiateOptions, ftpDataConnector: FtpDataConnectorOf(transports), diagnosticLog: transports.DiagnosticLog, tracesFtp: transports.TracesFtp, tracesSmtp: transports.TracesSmtp, tracesWs: transports.TracesWs, tracesSsh: transports.TracesSsh, tracesHttp2: transports.TracesHttp2, tracesHttp3: transports.TracesHttp3, tracesRead: transports.TracesRead)),
            WarningLinesBeforeEachTransfer(transports, matchesSchannelBuild),
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
    /// <param name="diagnosticLog">The run's diagnostic log, bound by the runner once the command line is read (BL-923).</param>
    private static TransferDispatch CreateTransferDispatch(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ITlsProvider tlsProvider,
        CookieEngine? cookies,
        ProxySelector? proxySelector,
        ISecurityContextFactory? securityContexts,
        CommandLineOptions options,
        TimeProvider? signingClock,
        IDiagnosticLog diagnosticLog) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(connector, datagramConnector, tlsProvider, new SystemDnsResolver(), cookies?.HandlerStore, securityContexts, options.ProxyAuthSchemes, NegotiateOptionsMapping.FromCommandLine(options), signingClock, diagnosticLog: diagnosticLog)),
            [],
            cookies,
            proxySelector);
}
