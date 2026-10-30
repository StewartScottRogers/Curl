using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Authentication;
using Curl.Cli;
using Curl.Cookies;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Resolution;
using Curl.Extension.Abstractions.Scheme;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
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
using Libcurl.Misc.ErrorText;
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
    /// <see cref="Environment.GetEnvironmentVariable(string)" /> as one delegate made once, so no call site
    /// re-checks a compiler-cached method-group delegate, whose two sides coverage counted by test order (BL-4083).
    /// </summary>
    private static readonly Func<string, string?> ProcessEnvironmentVariable = new(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Creates the runner that parses a command line and performs its transfers against
    /// the real disk, the real network and the given standard streams, wrapping warnings at
    /// the width <see cref="TerminalColumns.Resolve()" /> gives. The network
    /// transports are built for each option group by
    /// <see cref="CreateTransports(EngineToolOptions, EasyNetworkOptions, EasyTlsOptions, EasyHttpOptions, EasyProxyOptions, EasyAuthenticationOptions, TimeProvider, ConnectionCache?, TlsSessionCache?, IDiagnosticLog?, DnsCache?, Func{CurlExitCode, string}?, EngineTraceComponents?, Func{long}?)" /> once the
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
        TransferPerformer performer = new(TimeProvider.System, EasyErrorText);
        return new(
            SharingRunCookies((options, cookies) => CreateNetworkTransferDispatch(options, cookies, ExtensionResolverHooksOf(options), runConnections, tlsSessions, runLog, runDnsCache, performer)),
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
            transferPerformer: performer,
            curlEngine: new ManagedCurlEngine(TimeProvider.System, null, EasyErrorText));
    }

    /// <summary>
    /// Builds one option group's dispatch for the executable: its transports over the real network, the TCP and UDP
    /// connectors looking names up through <paramref name="resolverHooks" /> as the handlers do (ADR-1012).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="cookies">The run's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <param name="resolverHooks">The resolver hook the group's extension options activated, or <see cref="ResolverHooks.None" />.</param>
    /// <param name="runConnections">The run's connection cache.</param>
    /// <param name="tlsSessions">The run's <c>--ssl-sessions</c> cache.</param>
    /// <param name="runLog">The run's diagnostic log.</param>
    /// <param name="runDnsCache">The run's DNS cache.</param>
    /// <param name="performer">Takes the run's next <c>%{conn_id}</c> for each DoH connection.</param>
    /// <returns>The dispatch.</returns>
    private static TransferDispatch CreateNetworkTransferDispatch(CommandLineOptions options, CookieEngine? cookies, ResolverHooks resolverHooks, ConnectionCache runConnections, TlsSessionCache tlsSessions, IDiagnosticLog runLog, DnsCache runDnsCache, TransferPerformer performer)
    {
        ConnectionHooks connectionHooks = ExtensionConnectionHooksOf(options);
        CurlTransports transports = CreateNetworkTransports(options, resolverHooks, connectionHooks, runConnections, tlsSessions, runLog, runDnsCache, performer);
        return CreateTransferDispatch(transports, cookies, NegotiateOptionsMapping.Of(EasyOptionsFromCommandLine.AuthenticationOf(options)), ExtensionHandlerHooksOf(options, transports.PoolingConnector, new TcpConnectionListener()), connectionHooks, resolverHooks, ExtensionCertificateCheckHooksOf(options));
    }

    /// <summary>
    /// Builds one option group's transports over the real network: the TCP and UDP connectors look names up through
    /// <paramref name="resolverHooks" /> (ADR-1012), and QUIC opens its channels through the datagram connector
    /// <paramref name="connectionHooks" />' hook supplies, when it supplies one (BL-4318).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="resolverHooks">The resolver hook the group's extension options activated, or <see cref="ResolverHooks.None" />.</param>
    /// <param name="connectionHooks">The connection hook the group's extension options activated, or <see cref="ConnectionHooks.None" />.</param>
    /// <param name="runConnections">The run's connection cache.</param>
    /// <param name="tlsSessions">The run's <c>--ssl-sessions</c> cache.</param>
    /// <param name="runLog">The run's diagnostic log.</param>
    /// <param name="runDnsCache">The run's DNS cache.</param>
    /// <param name="performer">Takes the run's next <c>%{conn_id}</c> for each DoH connection.</param>
    /// <returns>The transports.</returns>
    internal static CurlTransports CreateNetworkTransports(CommandLineOptions options, ResolverHooks resolverHooks, ConnectionHooks connectionHooks, ConnectionCache runConnections, TlsSessionCache tlsSessions, IDiagnosticLog runLog, DnsCache runDnsCache, TransferPerformer performer) =>
        CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options), TimeProvider.System, runConnections, tlsSessions, runLog, runDnsCache, EasyErrorText, EasyOptionsFromCommandLine.TraceComponentsOf(options), resolverHooks.ResolverOver, takeDohConnectionId: performer.TakeConnectionId, extensionDatagramConnector: ExtensionDatagramConnectorOf(connectionHooks));

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
    /// for this process's platform. Tests pass <see langword="false" /> to reach <c>--http2</c> and <c>--ssl-sessions</c>
    /// on every platform (ADR-0397).
    /// </param>
    /// <param name="writesProgressMeter">
    /// Whether the runner writes curl's progress meter and the <c>-#</c> bar, as the executable's runner
    /// does; off by default, so tests that pin standard error see none. The conformance harness turns it on (BL-1445).
    /// </param>
    /// <param name="writeOutFileOpener">
    /// Opens the <c>-w</c> <c>%output{file}</c> targets, such as a <see cref="DiskWriteOutFileOpener" />; or
    /// <see langword="null" /> for the runner's <see cref="RefusingWriteOutFileOpener" />, which opens none (BL-1445).
    /// </param>
    /// <param name="parsesAsQuicBuild">
    /// Whether the runner reads the command line as a curl built with a QUIC library does, accepting <c>--http3</c> and
    /// <c>--http3-only</c>; off by default, as every reference build refuses them (ADR-0540). Tests and the conformance
    /// harness turn it on to reach the hand-built HTTP/3 (BL-2260, BL-2332).
    /// </param>
    /// <param name="parsesAsEchBuild">
    /// Whether the runner reads the command line as a curl built with ECH does, accepting <c>--ech</c>; off by default, as
    /// every reference build refuses it (ADR-0918). Tests and the conformance harness turn it on to reach the hand-built ECH.
    /// </param>
    /// <param name="extensionOptions">The extensions' long options the parser reads (ADR-1006); <see langword="null" /> for every registered extension's. Tests pass their own.</param>
    /// <param name="extensionTransferHookActivators">The extensions' transfer hook activators (ADR-1007); <see langword="null" /> for <see cref="ExtensionTransferHookCatalogue.Entries" />. Tests pass their own.</param>
    /// <param name="extensionConnectionHookActivators">The extensions' connection hook activators (ADR-1010); <see langword="null" /> for <see cref="ExtensionConnectionHookCatalogue.Entries" />. Tests pass their own.</param>
    /// <param name="extensionCertificateCheckHookActivators">The extensions' certificate check hook activators (ADR-1016); <see langword="null" /> for <see cref="ExtensionCertificateCheckHookCatalogue.Entries" />. Tests pass their own.</param>
    /// <param name="extensionSchemeHookActivators">The extensions' scheme hook activators (ADR-1048); <see langword="null" /> for <see cref="ExtensionSchemeHookCatalogue.Entries" />. Tests pass their own.</param>
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
        IWriteOutFileOpener? writeOutFileOpener = null,
        bool parsesAsQuicBuild = false,
        bool parsesAsEchBuild = false,
        IReadOnlyList<ExtensionOption>? extensionOptions = null,
        IReadOnlyList<Func<ExtensionOptionValues, ITransferHook?>>? extensionTransferHookActivators = null,
        IReadOnlyList<Func<ExtensionOptionValues, IConnectionHook?>>? extensionConnectionHookActivators = null,
        IReadOnlyList<Func<ExtensionOptionValues, ICertificateCheckHook?>>? extensionCertificateCheckHookActivators = null,
        IReadOnlyList<Func<ExtensionOptionValues, ISchemeHook?>>? extensionSchemeHookActivators = null)
    {
        LateBoundDiagnosticLog runLog = new();
        IReadOnlyList<Func<ExtensionOptionValues, ITransferHook?>> activators = extensionTransferHookActivators ?? ExtensionTransferHookCatalogue.Entries;
        IReadOnlyList<Func<ExtensionOptionValues, IConnectionHook?>> connectionActivators = extensionConnectionHookActivators ?? ExtensionConnectionHookCatalogue.Entries;
        return new(
            SharingRunCookies((options, cookies) => CreateTransferDispatch(GroupConnectorOf(options, connector, runConnections), datagramConnector, CreateTlsProvider(TlsClientOptionsMapping.Of(EasyOptionsFromCommandLine.TlsOf(options), options.UseAlpn, options.SslSessionsFile), TimeProvider.System), cookies, proxySelector, securityContexts, options, signingClock, runLog, activators, connectionActivators, extensionCertificateCheckHookActivators ?? ExtensionCertificateCheckHookCatalogue.Entries, extensionSchemeHookActivators ?? ExtensionSchemeHookCatalogue.Entries)),
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
            parsesAsQuicBuild: parsesAsQuicBuild,
            parsesAsEchBuild: parsesAsEchBuild,
            transferPerformer: new TransferPerformer(TimeProvider.System, EasyErrorText),
            extensionOptions: extensionOptions);
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
    /// for this process's platform (ADR-0397).
    /// </param>
    /// <param name="parsesAsQuicBuild">
    /// Whether the runner reads the command line as a curl built with a QUIC library does, accepting <c>--http3</c> and
    /// <c>--http3-only</c>; off by default, as every reference build refuses them (ADR-0540). Tests and the conformance
    /// harness turn it on to reach the hand-built HTTP/3 (BL-2260, BL-2332).
    /// </param>
    /// <param name="parsesAsEchBuild">
    /// Whether the runner reads the command line as a curl built with ECH does, accepting <c>--ech</c>; off by default, as
    /// every reference build refuses it (ADR-0918). Tests and the conformance harness turn it on to reach the hand-built ECH.
    /// </param>
    /// <param name="extendedAttributeWriter">
    /// Sets the <c>--xattr</c> attributes; <see langword="null" /> for a run that sets none. The conformance harness
    /// passes one that prints them as curl's debug build does under <c>CURL_FAKE_XATTR</c> (BL-2833).
    /// </param>
    /// <param name="honoursDebugWriteOutVariables">
    /// Whether <c>CURL_TIME</c>, <c>CURL_DEBUG_SIZE</c> and <c>CURL_VERSION</c> change what <c>-w</c> prints
    /// (<see cref="DebugWriteOutStandIn" />), as in curl's debug build; off by default, as in curl's release
    /// build, and turned on by the conformance harness (ADR-0732, BL-2909).
    /// </param>
    /// <param name="webSocketRandomSource">
    /// Where the WebSocket handler draws each <c>Sec-WebSocket-Key</c> and frame mask from; <see langword="null" />, the
    /// default, for <see cref="SystemWebSocketRandomSource" />. The conformance harness passes the debug build's forced
    /// <c>CURL_ENTROPY</c> sequence (ADR-0735, BL-2910).
    /// </param>
    /// <param name="fileSystem">
    /// Opens the <c>-T</c> files and the other files the runner reads and writes; <see langword="null" />, the default,
    /// for <see cref="PhysicalFileSystem" />. The conformance harness passes one that cuts an upload to the debug build's
    /// <c>CURL_UPLOAD_SIZE</c> (ADR-0735, BL-2910).
    /// </param>
    /// <param name="securityContexts">
    /// Makes the NTLM, Negotiate and Kerberos contexts every option group's handlers and CONNECT proxy run on;
    /// <see langword="null" />, the default, for <see cref="CreateDialingSecurityContextFactory" />. The conformance
    /// harness passes its stand-in for the debug build's stub GSS-API under <c>CURL_STUB_GSS_CREDS</c> (BL-2836).
    /// </param>
    /// <param name="featureMask">
    /// The features the run behaves as built without; <see cref="EngineFeatureMask.None" />, the default, hides none. Under
    /// <see cref="EngineFeatureMask.Proxy" /> an option group with <c>-x</c> writes <see cref="ProxySupportDisabledLine" />
    /// before each transfer and the transfer ends with exit 4, as a curl built without proxy support does (ADR-0746, BL-2984).
    /// </param>
    /// <param name="standardOutputIsTerminal">
    /// Whether the run treats standard output as a terminal; <see langword="false" />, the default. The conformance
    /// harness passes <see langword="true" /> for a case setting the debug build's <c>CURL_ISATTY</c> (BL-2908).
    /// </param>
    /// <param name="digestRandomBytes">
    /// Where Digest and DIGEST-MD5 draw their client nonce's bytes from; <see langword="null" />, the default, for
    /// <see cref="System.Security.Cryptography.RandomNumberGenerator" />. The conformance harness passes the debug build's
    /// forced <c>CURL_ENTROPY</c> sequence (ADR-0751, BL-2834).
    /// </param>
    /// <param name="extensionOptions">The extensions' long options the parser reads (ADR-1006); <see langword="null" /> for every registered extension's. Tests pass their own.</param>
    /// <param name="extensionResolverHookActivators">The extensions' resolver hook activators (ADR-1012); <see langword="null" /> for <see cref="ExtensionResolverHookCatalogue.Entries" />. Tests pass their own.</param>
    /// <param name="extensionConnectionHookActivators">The extensions' connection hook activators (ADR-1010); <see langword="null" /> for <see cref="ExtensionConnectionHookCatalogue.Entries" />. Tests pass their own.</param>
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
        bool? parsesAsWindowsBuild = null,
        bool parsesAsQuicBuild = false,
        IExtendedAttributeWriter? extendedAttributeWriter = null,
        bool honoursDebugWriteOutVariables = false,
        IWebSocketRandomSource? webSocketRandomSource = null,
        IFileSystem? fileSystem = null,
        ISecurityContextFactory? securityContexts = null,
        EngineFeatureMask featureMask = EngineFeatureMask.None,
        bool standardOutputIsTerminal = false,
        Func<int, byte[]>? digestRandomBytes = null,
        bool parsesAsEchBuild = false,
        IReadOnlyList<ExtensionOption>? extensionOptions = null,
        IReadOnlyList<Func<ExtensionOptionValues, IResolverHook?>>? extensionResolverHookActivators = null,
        IReadOnlyList<Func<ExtensionOptionValues, IConnectionHook?>>? extensionConnectionHookActivators = null)
    {
        ConnectionCache runConnections = new(TimeProvider.System);
        LateBoundDiagnosticLog runLog = new();
        Func<string, string?> environment = readEnvironmentVariable ?? (_ => null);
        string? dnsServer = honoursDnsServerVariable ? environment(DnsServerVariableName) : null;
        IReadOnlyList<Func<ExtensionOptionValues, IResolverHook?>> resolverActivators = extensionResolverHookActivators ?? ExtensionResolverHookCatalogue.Entries;
        IReadOnlyList<Func<ExtensionOptionValues, IConnectionHook?>> connectionActivators = ConnectionHookActivatorsOf(extensionConnectionHookActivators);
        return new(
            SharingRunCookies((options, cookies) => CreateDialingTransferDispatch(tcpDialer, ExtensionResolverHooksOf(options, resolverActivators).ResolverOver(DialingDohResolverOf(options, tcpDialer, DnsServerVariableResolverOf(EasyOptionsFromCommandLine.NetworkOf(options), dnsServer, dnsResolver, datagramConnector))), datagramConnector, cookies, options, runConnections, runLog, usesHandBuiltNtlm, environment, ftpListener, signingClock, webSocketRandomSource, securityContexts, featureMask, digestRandomBytes, connectionActivators)),
            RunFileSystemOf(fileSystem),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            terminalColumns: TerminalColumns.Resolve(environment(TerminalColumns.ColumnsVariableName), () => null),
            writesProgressMeter: writesProgressMeter,
            standardOutputIsTerminal: standardOutputIsTerminal,
            writeOutFileOpener: writeOutFileOpener,
            writeOutTimeDialect: WriteOutTimeDialectFor(OperatingSystem.IsWindows()),
            outputPaths: new PhysicalOutputPaths(),
            defaultConfigFileSearch: defaultConfigFileSearch,
            readEnvironmentVariable: readEnvironmentVariable,
            runConnectionCache: runConnections,
            lateBoundDiagnosticLog: runLog,
            hstsClock: HonouredHstsClockOf(honoursHstsTimeVariable, environment),
            hstsLearnsOverHttp: LearnsHstsOverHttp(honoursHstsHttpVariable, environment),
            altSvcUsesOverHttp: UsesAltSvcOverHttp(honoursAltSvcHttpVariable, environment),
            parsesAsWindowsBuild: parsesAsWindowsBuild,
            parsesAsQuicBuild: parsesAsQuicBuild,
            parsesAsEchBuild: parsesAsEchBuild,
            transferPerformer: new TransferPerformer(TimeProvider.System, EasyErrorText),
            extendedAttributeWriter: extendedAttributeWriter,
            debugWriteOut: DebugWriteOutStandIn.OfHonoured(honoursDebugWriteOutVariables, environment),
            extensionOptions: extensionOptions);
    }

    /// <summary>The connection hook activators the dialing runner activates: <paramref name="given" />, or <see cref="ExtensionConnectionHookCatalogue.Entries" /> when none were given.</summary>
    /// <param name="given">The activators a test gave, or <see langword="null" />.</param>
    /// <returns>The run's connection hook activators.</returns>
    private static IReadOnlyList<Func<ExtensionOptionValues, IConnectionHook?>> ConnectionHookActivatorsOf(IReadOnlyList<Func<ExtensionOptionValues, IConnectionHook?>>? given) =>
        given ?? ExtensionConnectionHookCatalogue.Entries;

    /// <summary>The HSTS clock a <c>CURL_TIME</c> fixes (<see cref="HstsClockOf" />) when the seam is on; <see langword="null" />, for the real clock, when it is off.</summary>
    /// <param name="honoursHstsTimeVariable">Whether the seam is on.</param>
    /// <param name="environment">Reads the run's environment.</param>
    /// <returns>The fixed clock, or <see langword="null" />.</returns>
    private static FixedWallClock? HonouredHstsClockOf(bool honoursHstsTimeVariable, Func<string, string?> environment) =>
        honoursHstsTimeVariable ? HstsClockOf(environment(HstsTimeVariableName)) : null;

    /// <summary>The file system the dialing runner opens its files through: <paramref name="fileSystem" />, or <see cref="PhysicalFileSystem" /> when none was given.</summary>
    /// <param name="fileSystem">The file system the caller gave, or <see langword="null" />.</param>
    /// <returns>The run's file system.</returns>
    private static IFileSystem RunFileSystemOf(IFileSystem? fileSystem) => fileSystem ?? new PhysicalFileSystem();

    /// <summary>
    /// The resolver of one option group of the dialing runner: with <c>--doh-url</c>, a DoH resolver whose POSTs dial
    /// through <paramref name="tcpDialer" /> and whose DoH server name resolves through <paramref name="dnsResolver" />,
    /// so the conformance harness's in-memory servers answer them (BL-2928); otherwise <paramref name="dnsResolver" />.
    /// </summary>
    /// <param name="options">The option group, whose DoH URL, DoH TLS options, IP version and trace components pick the resolver.</param>
    /// <param name="tcpDialer">Opens the DoH server connections.</param>
    /// <param name="dnsResolver">The resolver the runner was given.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver DialingDohResolverOf(CommandLineOptions options, ITcpDialer tcpDialer, IDnsResolver dnsResolver)
    {
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(options);
        return network.DohUrl is { } dohUrl
            ? CreateDohResolver(dohUrl, CreateDohConnector(EasyOptionsFromCommandLine.TraceComponentsOf(options), EasyOptionsFromCommandLine.TlsOf(options), tcpDialer, TimeProvider.System, dnsResolver), AddressFamilyOf(network), easyErrorText: EasyErrorText)
            : dnsResolver;
    }

    /// <summary>
    /// Builds one option group's dispatch for the dialing <see cref="CreateRunner(Stream, Stream, Stream, ITcpDialer, IDnsResolver, IDatagramConnector, bool, IWriteOutFileOpener?, bool, Func{string, string?}?, IConnectionListener?, DefaultConfigFileSearch?, TimeProvider?, bool, bool, bool, bool, bool?, bool, IExtendedAttributeWriter?, bool, IWebSocketRandomSource?, IFileSystem?, ISecurityContextFactory?, EngineFeatureMask, bool, Func{int, byte[]}?, bool, IReadOnlyList{ExtensionOption}?, IReadOnlyList{Func{ExtensionOptionValues, IResolverHook?}}?, IReadOnlyList{Func{ExtensionOptionValues, IConnectionHook?}}?)" />:
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
    /// <param name="readEnvironmentVariable">Reads the proxy environment variables and <c>SSLKEYLOGFILE</c>, which the target's and the HTTPS proxy's TLS options carry (BL-2716).</param>
    /// <param name="ftpListener">Listens for FTP's active-mode data connections, or <see langword="null" /> for a <see cref="TcpConnectionListener" />.</param>
    /// <param name="signingClock">The clock <c>--aws-sigv4</c> and the <c>--httpsig-*</c> options sign with; <see cref="TimeProvider.System" /> when <see langword="null" />.</param>
    /// <param name="webSocketRandomSource">Where the WebSocket handler draws its keys and masks from; <see cref="SystemWebSocketRandomSource" /> when <see langword="null" />.</param>
    /// <param name="givenSecurityContexts">Makes the group's NTLM, Negotiate and Kerberos contexts; <see cref="CreateDialingSecurityContextFactory" /> when <see langword="null" />.</param>
    /// <param name="featureMask">The features the run behaves as built without, which the dispatch carries.</param>
    /// <param name="digestRandomBytes">Where Digest and DIGEST-MD5 draw their client nonce's bytes from; <see cref="System.Security.Cryptography.RandomNumberGenerator" /> when <see langword="null" />.</param>
    /// <param name="connectionActivators">One per extension: its connection hook when its option was given, otherwise <see langword="null" />.</param>
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
        TimeProvider? signingClock,
        IWebSocketRandomSource? webSocketRandomSource,
        ISecurityContextFactory? givenSecurityContexts,
        EngineFeatureMask featureMask,
        Func<int, byte[]>? digestRandomBytes,
        IEnumerable<Func<ExtensionOptionValues, IConnectionHook?>> connectionActivators)
    {
        ConnectionHooks connectionHooks = ExtensionConnectionHooksOf(options, connectionActivators);
        CertificateCheckHooks certificateCheckHooks = ExtensionCertificateCheckHooksOf(options);
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(options);
        EasyTlsOptions tls = EasyOptionsFromCommandLine.TlsOf(options);
        EasyHttpOptions http = EasyOptionsFromCommandLine.HttpOf(options);
        EasyProxyOptions proxy = EasyOptionsFromCommandLine.ProxyOf(options);
        EasyAuthenticationOptions authentication = EasyOptionsFromCommandLine.AuthenticationOf(options);
        TlsClientOptions tlsClientOptions = TlsClientOptionsMapping.Of(tls, options.UseAlpn, options.SslSessionsFile, readEnvironmentVariable);
        ITlsProviderWithWarnings tlsProvider = CreateTlsProvider(tlsClientOptions, TimeProvider.System);
        ITlsProviderWithWarnings proxyTlsProvider = CreateTlsProvider(TlsClientOptionsMapping.ProxyOf(tls, options.UseAlpn, readEnvironmentVariable), TimeProvider.System);
        LateBoundSecurityContextFactory proxyContexts = new() { UsesHandBuiltNtlm = usesHandBuiltNtlm };
        HttpProxyTunnelOptions proxyTunnelOptions = CreateProxyTunnelOptions(http, proxy, authentication, proxyContexts, diagnosticLog);
        EngineTraceComponents traces = EasyOptionsFromCommandLine.TraceComponentsOf(options);
        EngineToolOptions tool = EasyOptionsFromCommandLine.ToolOptionsOf(options);
        QuicDialer quicDialer = CreateDatagramConnectorQuicDialer(traces, tlsClientOptions, connectionHooks.DatagramConnectorOver(datagramConnector));
        TcpConnector tcpConnector = CreateTcpConnector(tool, traces, network, http, proxy, authentication, dnsResolver, tcpDialer, tlsProvider, TimeProvider.System, proxyTunnelOptions, proxyTlsProvider, quicDialer, socks5SecurityContexts: proxyContexts, featureMask: featureMask);
        PoolingConnector poolingConnector = CreatePoolingConnector(tool, network, tls, http, proxy, tcpConnector, TimeProvider.System, runConnections, featureMask);
        ISecurityContextFactory securityContexts = givenSecurityContexts ?? CreateDialingSecurityContextFactory(poolingConnector, datagramConnector, diagnosticLog, usesHandBuiltNtlm);
        proxyContexts.Bind(securityContexts);
        return new(
            new ProtocolDispatcher(CreateProtocolHandlers(poolingConnector, datagramConnector, tlsProvider, dnsResolver, cookies?.HandlerStore, securityContexts, proxyAuthSchemes: proxyTunnelOptions.ProxyAuthSchemes, negotiateOptions: NegotiateOptionsMapping.Of(authentication), signingClock: signingClock, ftpDataConnector: poolingConnector.Over(tcpConnector.ForFtpDataConnections()), diagnosticLog: diagnosticLog, ftpListener: ftpListener, webSocketRandomSource: webSocketRandomSource, digestRandomBytes: digestRandomBytes, extensionHandlers: ExtensionProtocolHandlers.For(readEnvironmentVariable), extensionTransferHooks: ExtensionHandlerHooksOf(options, poolingConnector, ftpListener ?? new TcpConnectionListener()), extensionConnector: ExtensionConnectorOf(connectionHooks, tlsProvider), extensionDatagramConnector: ExtensionDatagramConnectorOf(connectionHooks), extensionCertificateConnector: ExtensionCertificateConnectorOf(certificateCheckHooks), extensionTlsProvider: ExtensionTlsProviderOf(certificateCheckHooks))),
            ProxyDisabledLinesOf(featureMask, proxy),
            cookies,
            new ProxySelector(readEnvironmentVariable),
            poolingConnector,
            tcpConnector.LoadResolveEntries)
        {
            FeatureMask = featureMask,
        };
    }

    /// <summary>
    /// What curl 8.22.0's <c>src/tool_operate.c</c> writes when setting <c>CURLOPT_PROXY</c> fails in a
    /// build without proxy support, before it returns <c>CURLE_NOT_BUILT_IN</c> as a synthetic error
    /// (upstream test375).
    /// </summary>
    internal const string ProxySupportDisabledLine = "curl: proxy support is disabled in this libcurl";

    /// <summary>
    /// The lines an option group writes before each transfer for the features <paramref name="featureMask" />
    /// hides: <see cref="ProxySupportDisabledLine" /> when it hides <see cref="EngineFeatureMask.Proxy" /> and
    /// the group sets a proxy, else none.
    /// </summary>
    /// <param name="featureMask">The features the run behaves as built without.</param>
    /// <param name="proxy">The group's proxy options.</param>
    /// <returns>The lines.</returns>
    private static string[] ProxyDisabledLinesOf(EngineFeatureMask featureMask, EasyProxyOptions proxy) =>
        featureMask.HasFlag(EngineFeatureMask.Proxy) && proxy.Proxy is not null ? [ProxySupportDisabledLine] : [];

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
        runConnections is null ? connector : CreatePoolingConnector(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), connector, TimeProvider.System, runConnections);

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
    /// The encoding the <c>-b name=value</c> strings reach curl's <c>argv</c> in, whose bytes go out as they are: the
    /// ANSI code page on Windows (<c>CredentialEncoding.ForPlatform</c>), and off Windows UTF-8 that gives each
    /// <see cref="InvalidUtf8ByteEscape" /> surrogate back as its byte, so <c>-b $'a=\xff'</c> sends <c>a=\xff</c>
    /// as curl 8.18.0 does on Linux (CGR-016) and <c>-b c=中</c> still sends its UTF-8 bytes (CGR-065).
    /// </summary>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <returns>The encoding for <see cref="CookieEngine.FromHttpOptions(EasyHttpOptions, CookieStore, Encoding?)" />.</returns>
    internal static Encoding CookieStringEncodingFor(bool runsOnWindows) =>
        runsOnWindows ? CredentialEncoding.ForPlatform(true) : InvalidUtf8ByteEscape.Encoding;

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
        Encoding cookieStringEncoding = CookieStringEncodingFor(OperatingSystem.IsWindows());
        return options => createTransferDispatch(options, CookieEngine.FromHttpOptions(EasyOptionsFromCommandLine.HttpOf(options), runCookies, cookieStringEncoding));
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
    /// <param name="extensionTransferHooks">Puts each handler through the transfer hooks the option group's extension options activated (<see cref="ExtensionTransferHooksOf(CommandLineOptions, IConnector, IConnectionListener)" />), or <see langword="null" /> for none.</param>
    /// <param name="extensionConnectionHooks">The connection hook the option group's extension options activated (<see cref="ExtensionConnectionHooksOf(CommandLineOptions)" />), or <see langword="null" /> for none.</param>
    /// <param name="extensionResolverHooks">The resolver hook the option group's extension options activated (<see cref="ExtensionResolverHooksOf(CommandLineOptions)" />), or <see langword="null" /> for none; it answers the handlers' look-ups, and the connectors' when <paramref name="transports" /> were built with its <see cref="ResolverHooks.ResolverOver" /> as their look-up wrapper.</param>
    /// <param name="extensionCertificateCheckHooks">The certificate checks the option group's extension options activated (<see cref="ExtensionCertificateCheckHooksOf(CommandLineOptions)" />), or <see langword="null" /> for none; they run on every chain curl accepted, and the first refusal fails the transfer with exit 60.</param>
    /// <returns>The dispatcher, the warning lines, the cookies, the proxy selector, the connection pool and the <c>--resolve</c> loader.</returns>
    internal static TransferDispatch CreateTransferDispatch(CurlTransports transports, CookieEngine? cookies = null, NegotiateOptions? negotiateOptions = null, Func<IProtocolHandler, IProtocolHandler>? extensionTransferHooks = null, ConnectionHooks? extensionConnectionHooks = null, ResolverHooks? extensionResolverHooks = null, CertificateCheckHooks? extensionCertificateCheckHooks = null) =>
        CreateTransferDispatch(transports, OperatingSystem.IsWindows(), cookies, negotiateOptions, extensionTransferHooks, extensionConnectionHooks, extensionResolverHooks, extensionCertificateCheckHooks);

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

    /// <summary>
    /// Gives the extension options one option group gave, as its extensions' transfer hooks read them
    /// (ADR-1006, ADR-1007): each option's value, or the empty string for a flag.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The group's extension option values, holding none when it gave none.</returns>
    internal static ExtensionOptionValues ExtensionOptionValuesOf(CommandLineOptions options) =>
        new(options.ExtensionOptionValues);

    /// <summary>
    /// Gives what the engine puts each of an option group's protocol handlers through (ADR-1007): the transfer hooks
    /// <see cref="ExtensionTransferHookCatalogue.Entries" /> activate for the group's extension options.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="connector">The group's connector, lent to the hooks to make their own requests (ADR-1039).</param>
    /// <param name="listener">The loopback listener lent to the hooks (ADR-1039).</param>
    /// <returns><see cref="TransferHooks.Around" /> of the activated hooks, or <see langword="null" /> when the group activated none.</returns>
    internal static Func<IProtocolHandler, IProtocolHandler>? ExtensionTransferHooksOf(CommandLineOptions options, IConnector connector, IConnectionListener listener) =>
        ExtensionTransferHooksOf(options, ExtensionTransferHookCatalogue.Entries, connector, listener);

    /// <summary>
    /// Gives what the engine puts each of an option group's protocol handlers through: the transfer hooks
    /// <paramref name="activators" /> activate for the group's extension options (ADR-1007).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <param name="connector">The group's connector, lent to the hooks to make their own requests (ADR-1039).</param>
    /// <param name="listener">The loopback listener lent to the hooks (ADR-1039).</param>
    /// <returns><see cref="TransferHooks.Around" /> of the activated hooks, or <see langword="null" /> when the group activated none, so the engine runs no hook.</returns>
    internal static Func<IProtocolHandler, IProtocolHandler>? ExtensionTransferHooksOf(CommandLineOptions options, IEnumerable<Func<ExtensionOptionValues, ITransferHook?>> activators, IConnector connector, IConnectionListener listener)
    {
        TransferHooks hooks = TransferHooks.ActivatedBy(ExtensionOptionValuesOf(options), activators, new TransferHookServices(connector, listener));
        return hooks.Hooks.Count == 0 ? null : hooks.Around;
    }

    /// <summary>
    /// Gives what the engine puts each of an option group's protocol handlers through: the scheme hook
    /// <see cref="ExtensionSchemeHookCatalogue.Entries" /> activate for the group's extension options (ADR-1048),
    /// inside the transfer hooks <see cref="ExtensionTransferHookCatalogue.Entries" /> activate (ADR-1007).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="connector">The group's connector, lent to the hooks (ADR-1039).</param>
    /// <param name="listener">The loopback listener lent to the hooks (ADR-1039).</param>
    /// <returns>The hooks around a handler, or <see langword="null" /> when the group activated none.</returns>
    /// <exception cref="ExtensionOptionConflictException">Two of the group's extension options each serve a curl scheme.</exception>
    internal static Func<IProtocolHandler, IProtocolHandler>? ExtensionHandlerHooksOf(CommandLineOptions options, IConnector connector, IConnectionListener listener) =>
        HandlerHooksOver(ExtensionTransferHooksOf(options, connector, listener), ExtensionSchemeHooksOf(options, ExtensionSchemeHookCatalogue.Entries, new TransferHookServices(connector, listener)));

    /// <summary>
    /// Gives what the engine puts each of an option group's protocol handlers through: the scheme hook
    /// <paramref name="schemeActivators" /> activate for the group's extension options (ADR-1048), inside the
    /// transfer hooks <paramref name="transferActivators" /> activate (ADR-1007).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="transferActivators">One per extension: its transfer hook when its option was given, otherwise <see langword="null" />.</param>
    /// <param name="schemeActivators">One per extension: its scheme hook when its option was given, otherwise <see langword="null" />.</param>
    /// <param name="connector">The group's connector, lent to the hooks (ADR-1039).</param>
    /// <param name="listener">The loopback listener lent to the hooks (ADR-1039).</param>
    /// <returns>The hooks around a handler, or <see langword="null" /> when the group activated none.</returns>
    /// <exception cref="ExtensionOptionConflictException">Two of the group's extension options each serve a curl scheme.</exception>
    internal static Func<IProtocolHandler, IProtocolHandler>? ExtensionHandlerHooksOf(CommandLineOptions options, IEnumerable<Func<ExtensionOptionValues, ITransferHook?>> transferActivators, IEnumerable<Func<ExtensionOptionValues, ISchemeHook?>> schemeActivators, IConnector connector, IConnectionListener listener) =>
        HandlerHooksOver(ExtensionTransferHooksOf(options, transferActivators, connector, listener), ExtensionSchemeHooksOf(options, schemeActivators, new TransferHookServices(connector, listener)));

    /// <summary>
    /// Gives the scheme hook <paramref name="activators" /> activate for an option group's extension options
    /// (ADR-1048), turning two hooks into the command-line error the runner reports with exit 2.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <param name="services">The group's connector and loopback listener, lent to the hook's handler.</param>
    /// <returns>The activated hook, or <see cref="SchemeHooks.None" />.</returns>
    /// <exception cref="ExtensionOptionConflictException">Two of the group's extension options each serve a curl scheme; the message names both.</exception>
    internal static SchemeHooks ExtensionSchemeHooksOf(CommandLineOptions options, IEnumerable<Func<ExtensionOptionValues, ISchemeHook?>> activators, TransferHookServices services)
    {
        try
        {
            return SchemeHooks.ActivatedBy(ExtensionOptionValuesOf(options), activators, services);
        }
        catch (ExtensionHookConflictException conflict)
        {
            throw OptionConflictOf(conflict);
        }
    }

    /// <summary>Puts a handler through <paramref name="schemeHooks" /> first and then <paramref name="transferHooks" />, so the transfer hooks see an extension's transfers too.</summary>
    /// <param name="transferHooks">The activated transfer hooks, or <see langword="null" /> for none.</param>
    /// <param name="schemeHooks">The activated scheme hook, or <see cref="SchemeHooks.None" />.</param>
    /// <returns>Both around a handler, or <paramref name="transferHooks" /> alone when no scheme hook was activated.</returns>
    private static Func<IProtocolHandler, IProtocolHandler>? HandlerHooksOver(Func<IProtocolHandler, IProtocolHandler>? transferHooks, SchemeHooks schemeHooks)
    {
        if (schemeHooks.Hook is null)
        {
            return transferHooks;
        }

        return transferHooks is null ? schemeHooks.Over : handler => transferHooks(schemeHooks.Over(handler));
    }

    /// <summary>
    /// Gives the connection hook <see cref="ExtensionConnectionHookCatalogue.Entries" /> activate for an option
    /// group's extension options (ADR-1010).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The activated hook, or <see cref="ConnectionHooks.None" />.</returns>
    /// <exception cref="ExtensionOptionConflictException">Two of the group's extension options each claim the connection.</exception>
    internal static ConnectionHooks ExtensionConnectionHooksOf(CommandLineOptions options) =>
        ExtensionConnectionHooksOf(options, ExtensionConnectionHookCatalogue.Entries);

    /// <summary>
    /// Gives the connection hook <paramref name="activators" /> activate for an option group's extension options
    /// (ADR-1010), turning two hooks into the command-line error the runner reports with exit 2.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <returns>The activated hook, or <see cref="ConnectionHooks.None" />.</returns>
    /// <exception cref="ExtensionOptionConflictException">Two of the group's extension options each claim the connection; the message names both.</exception>
    internal static ConnectionHooks ExtensionConnectionHooksOf(CommandLineOptions options, IEnumerable<Func<ExtensionOptionValues, IConnectionHook?>> activators)
    {
        try
        {
            return ConnectionHooks.ActivatedBy(ExtensionOptionValuesOf(options), activators);
        }
        catch (ExtensionHookConflictException conflict)
        {
            throw OptionConflictOf(conflict);
        }
    }

    /// <summary>
    /// Gives the resolver hook <see cref="ExtensionResolverHookCatalogue.Entries" /> activate for an option
    /// group's extension options (ADR-1012).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The activated hook, or <see cref="ResolverHooks.None" />.</returns>
    /// <exception cref="ExtensionOptionConflictException">Two of the group's extension options, or one and <c>--doh-url</c>, each claim the resolver.</exception>
    internal static ResolverHooks ExtensionResolverHooksOf(CommandLineOptions options) =>
        ExtensionResolverHooksOf(options, ExtensionResolverHookCatalogue.Entries);

    /// <summary>
    /// Gives the resolver hook <paramref name="activators" /> activate for an option group's extension options,
    /// checked against the group's <c>--doh-url</c> (ADR-1012).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <returns>The activated hook, or <see cref="ResolverHooks.None" />.</returns>
    /// <exception cref="ExtensionOptionConflictException">Two of the group's extension options, or one and <c>--doh-url</c>, each claim the resolver; the message names both.</exception>
    internal static ResolverHooks ExtensionResolverHooksOf(CommandLineOptions options, IEnumerable<Func<ExtensionOptionValues, IResolverHook?>> activators)
    {
        try
        {
            return ResolverHooks.ActivatedBy(ExtensionOptionValuesOf(options), activators, options.DohUrl is not null);
        }
        catch (ExtensionHookConflictException conflict)
        {
            throw OptionConflictOf(conflict);
        }
    }

    /// <summary>Gives the engine's <c>extensionResolver</c> for an option group's resolver hook.</summary>
    /// <param name="hooks">The group's resolver hook, or <see langword="null" /> for none.</param>
    /// <returns><see cref="ResolverHooks.ResolverOver" />, or <see langword="null" /> without a hook, so the engine keeps curl's resolver.</returns>
    internal static Func<IDnsResolver, IDnsResolver>? ExtensionResolverOf(ResolverHooks? hooks) =>
        hooks is { Hook: not null } ? hooks.ResolverOver : null;

    // An extension hook conflict as curl's command-line error.
    private static ExtensionOptionConflictException OptionConflictOf(ExtensionHookConflictException conflict) =>
        new(conflict.Message);

    /// <summary>Gives the engine's <c>extensionConnector</c> for an option group's connection hook.</summary>
    /// <param name="hooks">The group's connection hook, or <see langword="null" /> for none.</param>
    /// <param name="tlsProvider">The group's TLS provider, handed to the hook so one that supplies only the byte stream layers curl's TLS over it (ADR-1055).</param>
    /// <returns><see cref="ConnectionHooks.ConnectorOver(IConnector, ITlsProvider)" /> over <paramref name="tlsProvider" />, or <see langword="null" /> without a hook, so the engine keeps curl's connector.</returns>
    internal static Func<IConnector, IConnector>? ExtensionConnectorOf(ConnectionHooks? hooks, ITlsProvider tlsProvider) =>
        hooks is { Hook: not null } ? curlConnector => hooks.ConnectorOver(curlConnector, tlsProvider) : null;

    /// <summary>Gives the engine's <c>extensionDatagramConnector</c> for an option group's connection hook.</summary>
    /// <param name="hooks">The group's connection hook, or <see langword="null" /> for none.</param>
    /// <returns><see cref="ConnectionHooks.DatagramConnectorOver" />, or <see langword="null" /> without a hook, so the engine keeps curl's datagram connector.</returns>
    internal static Func<IDatagramConnector, IDatagramConnector>? ExtensionDatagramConnectorOf(ConnectionHooks? hooks) =>
        hooks is { Hook: not null } ? hooks.DatagramConnectorOver : null;
    /// <summary>
    /// Gives the certificate checks <see cref="ExtensionCertificateCheckHookCatalogue.Entries" /> activate for an
    /// option group's extension options (ADR-1016).
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <returns>The activated checks, or none.</returns>
    internal static CertificateCheckHooks ExtensionCertificateCheckHooksOf(CommandLineOptions options) =>
        ExtensionCertificateCheckHooksOf(options, ExtensionCertificateCheckHookCatalogue.Entries);

    /// <summary>
    /// Gives the certificate checks <paramref name="activators" /> activate for an option group's extension options
    /// (ADR-1016). Every activated check runs, so two of them are no conflict.
    /// </summary>
    /// <param name="options">The option group.</param>
    /// <param name="activators">One per extension: its check when its option was given, otherwise <see langword="null" />.</param>
    /// <returns>The activated checks, or none.</returns>
    internal static CertificateCheckHooks ExtensionCertificateCheckHooksOf(CommandLineOptions options, IEnumerable<Func<ExtensionOptionValues, ICertificateCheckHook?>> activators) =>
        CertificateCheckHooks.ActivatedBy(ExtensionOptionValuesOf(options), activators);

    /// <summary>Gives the engine's <c>extensionCertificateConnector</c> for an option group's certificate checks.</summary>
    /// <param name="hooks">The group's certificate checks, or <see langword="null" /> for none.</param>
    /// <returns><see cref="CertificateCheckHooks.ConnectorOver" />, or <see langword="null" /> without a check, so the engine keeps curl's connector.</returns>
    internal static Func<IConnector, IConnector>? ExtensionCertificateConnectorOf(CertificateCheckHooks? hooks) =>
        hooks is { Hooks.Count: > 0 } ? hooks.ConnectorOver : null;

    /// <summary>Gives the engine's <c>extensionTlsProvider</c> for an option group's certificate checks.</summary>
    /// <param name="hooks">The group's certificate checks, or <see langword="null" /> for none.</param>
    /// <returns><see cref="CertificateCheckHooks.TlsProviderOver" />, or <see langword="null" /> without a check, so the engine keeps curl's TLS provider.</returns>
    internal static Func<ITlsProvider, ITlsProvider>? ExtensionTlsProviderOf(CertificateCheckHooks? hooks) =>
        hooks is { Hooks.Count: > 0 } ? hooks.TlsProviderOver : null;

    // The tool's warning for an option curl's Schannel build ignores.
    private static string SchannelIgnoredOptionWarning(string option) =>
        $"Warning: ignoring {option}, not supported by libcurl with Schannel";

    /// <summary>
    /// Creates what one run transfers through as <see cref="CreateTransferDispatch(CurlTransports, CookieEngine?, NegotiateOptions?, Func{IProtocolHandler, IProtocolHandler}?, ConnectionHooks?, ResolverHooks?, CertificateCheckHooks?)" />
    /// does, warning as the named curl build does.
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <param name="matchesSchannelBuild">Whether the run warns as curl's Schannel build does.</param>
    /// <param name="cookies">The run's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <param name="negotiateOptions">The service names and delegation the HTTP handler's Negotiate answers with; <see cref="NegotiateOptions.Default" /> when <see langword="null" />.</param>
    /// <param name="extensionTransferHooks">Puts each handler through the transfer hooks the option group's extension options activated, or <see langword="null" /> for none.</param>
    /// <param name="extensionConnectionHooks">The connection hook the option group's extension options activated, or <see langword="null" /> for none.</param>
    /// <param name="extensionResolverHooks">The resolver hook the option group's extension options activated, or <see langword="null" /> for none.</param>
    /// <param name="extensionCertificateCheckHooks">The certificate checks the option group's extension options activated, or <see langword="null" /> for none.</param>
    /// <returns>The dispatcher, the warning lines, the cookies, the proxy selector, the connection pool and the <c>--resolve</c> loader.</returns>
    internal static TransferDispatch CreateTransferDispatch(CurlTransports transports, bool matchesSchannelBuild, CookieEngine? cookies = null, NegotiateOptions? negotiateOptions = null, Func<IProtocolHandler, IProtocolHandler>? extensionTransferHooks = null, ConnectionHooks? extensionConnectionHooks = null, ResolverHooks? extensionResolverHooks = null, CertificateCheckHooks? extensionCertificateCheckHooks = null)
    {
        Func<string, string?> readEnvironmentVariable = ProcessEnvironmentVariable;
        return new(
            new ProtocolDispatcher(CreateProtocolHandlers(transports.PoolingConnector, transports.UdpDatagramConnector, transports.TlsProvider, transports.DnsResolver, cookies?.HandlerStore, proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes, negotiateOptions: negotiateOptions, ftpDataConnector: FtpDataConnectorOf(transports), diagnosticLog: transports.DiagnosticLog, tracesFtp: transports.TracesFtp, tracesSmtp: transports.TracesSmtp, tracesWs: transports.TracesWs, tracesSsh: transports.TracesSsh, tracesHttp2: transports.TracesHttp2, tracesHttp3: transports.TracesHttp3, tracesRead: transports.TracesRead, extensionHandlers: ExtensionProtocolHandlers.For(readEnvironmentVariable), extensionTransferHooks: extensionTransferHooks, extensionConnector: ExtensionConnectorOf(extensionConnectionHooks, transports.TlsProvider), extensionDatagramConnector: ExtensionDatagramConnectorOf(extensionConnectionHooks), extensionResolver: ExtensionResolverOf(extensionResolverHooks), extensionCertificateConnector: ExtensionCertificateConnectorOf(extensionCertificateCheckHooks), extensionTlsProvider: ExtensionTlsProviderOf(extensionCertificateCheckHooks))),
            WarningLinesBeforeEachTransfer(transports, matchesSchannelBuild),
            cookies,
            new ProxySelector(readEnvironmentVariable),
            transports.PoolingConnector,
            transports.TcpConnector.LoadResolveEntries);
    }

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
    /// <param name="extensionTransferHookActivators">The extensions' transfer hook activators the group's extension options are checked against.</param>
    /// <param name="extensionConnectionHookActivators">The extensions' connection hook activators the group's extension options are checked against.</param>
    /// <param name="extensionCertificateCheckHookActivators">The extensions' certificate check hook activators the group's extension options are checked against.</param>
    /// <param name="extensionSchemeHookActivators">The extensions' scheme hook activators the group's extension options are checked against.</param>
    private static TransferDispatch CreateTransferDispatch(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ITlsProvider tlsProvider,
        CookieEngine? cookies,
        ProxySelector? proxySelector,
        ISecurityContextFactory? securityContexts,
        CommandLineOptions options,
        TimeProvider? signingClock,
        IDiagnosticLog diagnosticLog,
        IEnumerable<Func<ExtensionOptionValues, ITransferHook?>> extensionTransferHookActivators,
        IEnumerable<Func<ExtensionOptionValues, IConnectionHook?>> extensionConnectionHookActivators,
        IEnumerable<Func<ExtensionOptionValues, ICertificateCheckHook?>> extensionCertificateCheckHookActivators,
        IEnumerable<Func<ExtensionOptionValues, ISchemeHook?>> extensionSchemeHookActivators)
    {
        ConnectionHooks connectionHooks = ExtensionConnectionHooksOf(options, extensionConnectionHookActivators);
        CertificateCheckHooks certificateCheckHooks = ExtensionCertificateCheckHooksOf(options, extensionCertificateCheckHookActivators);
        return new(
            new ProtocolDispatcher(CreateProtocolHandlers(connector, datagramConnector, tlsProvider, new SystemDnsResolver(), cookies?.HandlerStore, securityContexts, options.ProxyAuthSchemes, NegotiateOptionsMapping.Of(EasyOptionsFromCommandLine.AuthenticationOf(options)), signingClock, diagnosticLog: diagnosticLog, extensionTransferHooks: ExtensionHandlerHooksOf(options, extensionTransferHookActivators, extensionSchemeHookActivators, connector, new TcpConnectionListener()), extensionConnector: ExtensionConnectorOf(connectionHooks, tlsProvider), extensionDatagramConnector: ExtensionDatagramConnectorOf(connectionHooks), extensionCertificateConnector: ExtensionCertificateConnectorOf(certificateCheckHooks), extensionTlsProvider: ExtensionTlsProviderOf(certificateCheckHooks))),
            [],
            cookies,
            proxySelector);
    }
}
