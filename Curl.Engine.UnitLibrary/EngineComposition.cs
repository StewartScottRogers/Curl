using Curl.Api.Options.Authentication;
using Curl.Authentication;
using Curl.Core.FileSystem;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Dict;
using Curl.Original.Protocol.File;
using Curl.Original.Protocol.Ftp;
using Curl.Original.Protocol.Gopher;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Imap;
using Curl.Original.Protocol.Ldap;
using Curl.Original.Protocol.Mqtt;
using Curl.Original.Protocol.Pop3;
using Curl.Original.Protocol.Rtsp;
using Curl.Original.Protocol.Smtp;
using Curl.Original.Protocol.Ssh;
using Curl.Original.Protocol.Telnet;
using Curl.Original.Protocol.Tftp;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// Builds the engine's services with plain constructor calls - protocol handlers, transports, DNS
/// resolvers, TLS providers, security-context factories and authenticators - for the composition
/// root to wire into a runner. There is no container and no reflection, so native ahead-of-time
/// publishing sees every type that is used (ADR-0506).
/// </summary>
internal static partial class EngineComposition
{
    /// <summary>
    /// Creates the protocol handlers the executable registers: <c>file</c> over the real
    /// disk; <c>dict</c>, <c>gopher</c> and <c>gophers</c>, <c>telnet</c>, <c>mqtt</c>
    /// and <c>mqtts</c>, <c>imap</c> and <c>imaps</c>, <c>pop3</c> and <c>pop3s</c>, <c>smtp</c> and <c>smtps</c> (the mail schemes authenticating with
    /// <see cref="CreateSaslAuthenticator" />'s authenticator and upgrading with
    /// <paramref name="tlsProvider" /> after <c>STARTTLS</c> or <c>STLS</c>), <c>ldap</c> and <c>ldaps</c>
    /// answering as WinLDAP's build on Windows and as the OpenLDAP build elsewhere (ADR-0166), and <c>http</c> and <c>https</c> over <paramref name="connector" />,
    /// the last two answering authentication with <see cref="CreateHttpAuthenticator" />'s
    /// authenticator, answering a forward proxy with <paramref name="proxyAuthSchemes" /> (ADR-0239),
    /// and keeping cookies in <paramref name="cookieStore" />; <c>ws</c> and <c>wss</c>
    /// over <paramref name="connector" />, sending a pre-emptive <c>Authorization</c> from
    /// <see cref="CreateHttpAuthenticator" />'s authenticator and drawing each
    /// <c>Sec-WebSocket-Key</c> and frame mask from <see cref="SystemWebSocketRandomSource" />
    /// (ADR-0128); <c>rtsp</c> over <paramref name="connector" />, sending one <c>OPTIONS *</c>
    /// request per transfer with a pre-emptive <c>Authorization</c> from the same authenticator
    /// (ADR-0169); <c>smb</c> and <c>smbs</c> refused as disabled, as curl 8.22.0's reference builds leave them out
    /// (ADR-0835); <c>scp</c> and <c>sftp</c> over <paramref name="connector" />, reading the known-hosts
    /// and key files from the disk and offering the Windows curl's libssh2 algorithms on Windows and the
    /// OpenSSL build's elsewhere (ADR-0122); and <c>tftp</c> over
    /// <paramref name="datagramConnector" />, sending its MASQUE request through an HTTP or HTTPS
    /// proxy over <paramref name="connector" /> with the proxy credential in the platform's
    /// encoding (ADR-0056, rule 4); and <c>ftp</c> and <c>ftps</c>, which
    /// <see cref="RoutingFtpProtocolHandler" /> hands to the HTTP handler when an <c>ftp</c>
    /// transfer is forwarded through an HTTP proxy without <c>-p</c> (ADR-0056, rule 3) and
    /// otherwise to <see cref="CreateFtpProtocolHandler" />'s handler (ADR-0323, ADR-0102).
    /// Each scheme is claimed by exactly one handler. Every handler connects through an
    /// <see cref="EndPointRecordingConnector" /> and an <see cref="EndPointRecordingDatagramConnector" />
    /// sharing one <see cref="ConnectionEndPointRecorder" />, and is wrapped in an
    /// <see cref="EndPointReportingProtocolHandler" />, so every scheme's report carries the end
    /// points of the first connection its transfer opened (ADR-0119).
    /// </summary>
    /// <param name="connector">Connects the TCP protocols, with TLS for <c>gophers</c>, <c>imaps</c>, <c>mqtts</c>, <c>pop3s</c>, <c>smtps</c>, <c>ldaps</c>, <c>smbs</c>, <c>wss</c> and <c>ftps</c>.</param>
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
    /// The scheme the <c>--proxy-*</c> auth switches pick (<see cref="EasyAuthenticationOptions.ProxyAuthentication" />, <see cref="HttpRequestOptionsMapping.ProxyAuthSchemesOf" />),
    /// which the HTTP handler answers a forward proxy with; Basic, curl's default, when not given.
    /// </param>
    /// <param name="negotiateOptions">
    /// The <c>--service-name</c>, <c>--proxy-service-name</c> and <c>--delegation</c> the HTTP
    /// handler's Negotiate answers with (<see cref="NegotiateOptionsMapping.Of" />), whose
    /// <c>--delegation</c> the mail handlers' SASL GSSAPI asks for too (BL-874);
    /// <see cref="NegotiateOptions.Default" /> when not given.
    /// </param>
    /// <param name="signingClock">
    /// The clock <c>--aws-sigv4</c> and the <c>--httpsig-*</c> options sign with (<see cref="AwsSigV4HttpAuthenticator" />
    /// and <see cref="HttpSignatureHttpAuthenticator" />, which
    /// the HTTP handler alone is given); <see cref="TimeProvider.System" /> when not given.
    /// </param>
    /// <param name="ftpDataConnector">
    /// Opens FTP's passive data connections, recorded like <paramref name="connector" />'s:
    /// <see cref="FtpDataConnectorOf" />'s in production, which does not hold them to
    /// <c>--connect-timeout</c> (BL-797); <paramref name="connector" /> when not given.
    /// </param>
    /// <returns>Every registered handler, each built on its first transfer (<see cref="LazyProtocolHandler" />, BL-1715).</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    /// <param name="tracesFtp">Whether the FTP handler writes the <c>--trace-config ftp</c> lines (<see cref="TracesFtp" />, BL-1162).</param>
    /// <param name="tracesSmtp">Whether the SMTP handler writes the <c>--trace-config smtp</c> lines (<see cref="TracesSmtp" />, BL-1163).</param>
    /// <param name="tracesWs">Whether the WebSocket handler writes the <c>--trace-config ws</c> lines (<see cref="TracesWs" />, BL-1164).</param>
    /// <param name="tracesSsh">Whether the SSH handler writes the <c>--trace-config ssh</c> lines (<see cref="TracesSsh" />, BL-1166).</param>
    /// <param name="tracesHttp2">Whether the HTTP handler writes the <c>--trace-config http/2</c> lines (<see cref="TracesHttp2" />, BL-1167).</param>
    /// <param name="tracesHttp3">Whether the HTTP handler writes the <c>--trace-config http/3</c> lines (<see cref="TracesHttp3" />, BL-1168).</param>
    /// <param name="tracesRead">Whether the HTTP handler writes an HTTP/1.x request body's <c>--trace-config read</c> lines (<see cref="TracesRead" />, BL-1189).</param>
    /// <param name="ftpListener">Listens for FTP's active-mode (<c>-P</c>) data connections; a <see cref="TcpConnectionListener" /> when not given.</param>
    /// <param name="ftpWildcardHooks">The FTP handler's wildcard hooks (<see cref="CallbackWildcardHooks.Of" />, ADR-0619); <see langword="null" /> fetches a wildcard URL as a plain path.</param>
    /// <param name="rtspInterleaveSink">Takes the RTSP handler's interleaved frames (<see cref="CallbackInterleaveSink.Of" />, ADR-0619); <see langword="null" /> writes them to the body output.</param>
    /// <param name="rtspRequestSettings">Gives a libcurl perform's RTSP request settings when the RTSP handler is first built (BL-2704); <see langword="null" /> for the curl tool's <c>OPTIONS *</c>.</param>
    /// <param name="rtspSessionState">The <c>CSeq</c> counter and session ID a libcurl handle keeps across performs (BL-2704); <see langword="null" /> for a fresh one per transfer.</param>
    /// <param name="webSocketRawMode">Whether the WebSocket handler writes the received stream unframed (<see cref="WsProtocolHandler.RawMode" />, libcurl's <c>CURLWS_RAW_MODE</c>, BL-2494).</param>
    /// <param name="webSocketPerformConnection">Where the WebSocket handler shows the connection its frames flow on, for <c>curl_ws_send</c> from a write callback (<see cref="WsProtocolHandler.PerformConnection" />, BL-2710); <see langword="null" /> shows it nowhere.</param>
    /// <param name="http2PushSink">Where the HTTP handler offers HTTP/2 server pushes (BL-2469); <see langword="null" /> keeps <c>SETTINGS_ENABLE_PUSH</c> 0.</param>
    /// <param name="webSocketRandomSource">Where the WebSocket handler draws each <c>Sec-WebSocket-Key</c> and frame mask from (BL-2688); <see langword="null" /> for <see cref="SystemWebSocketRandomSource" />.</param>
    /// <param name="ftpStopsBeforeEntryPath">Whether the FTP handler's new control connections stop before <c>PWD</c> (<see cref="FtpProtocolHandler.StopsBeforeEntryPath" />, BL-2636).</param>
    /// <param name="socketWait">The wait of a transfer driven by <c>curl_multi_socket_action</c>, which holds the HTTP handler's expect-100 deadline (<see cref="ContinueWaitBeginsOf" />, BL-3864); <see langword="null" /> outside socket-action mode.</param>
    /// <param name="digestRandomBytes">Where Digest and DIGEST-MD5 draw their client nonce's bytes from (ADR-0751, BL-2834); <see langword="null" /> for <see cref="System.Security.Cryptography.RandomNumberGenerator" />.</param>
    /// <param name="extensionHandlers">Builds the handlers a host's extensions add beyond upstream curl's, over the same transports, put after curl's (ADR-0999); <see langword="null" /> for curl's alone. The engine never references an extension.</param>
    /// <param name="extensionTransferHooks">Puts each handler's transfers through the hooks of the extensions a host's options activated (ADR-1007); <see langword="null" /> runs no hook. The engine never references an extension.</param>
    /// <param name="extensionConnector">Gives the connector of the extension a host's options activated in place of <paramref name="connector" /> (ADR-1010), as <c>--unix-socket</c> replaces curl's; <see langword="null" /> keeps <paramref name="connector" />.</param>
    /// <param name="extensionDatagramConnector">Gives that extension's datagram connector in place of <paramref name="datagramConnector" /> (ADR-1010); <see langword="null" /> keeps <paramref name="datagramConnector" />.</param>
    /// <param name="extensionResolver">Gives the resolver of the extension a host's options activated in place of <paramref name="dnsResolver" /> (ADR-1012), as <c>--doh-url</c> replaces curl's; <see langword="null" /> keeps <paramref name="dnsResolver" />.</param>
    /// <param name="extensionCertificateConnector">Puts the connector, after <paramref name="extensionConnector" />, through the certificate checks of the extensions a hosts options activated (ADR-1016), which can only refuse a chain curl accepted; <see langword="null" /> checks nothing more.</param>
    /// <param name="extensionTlsProvider">Puts <paramref name="tlsProvider" /> through those checks for in-place upgrades (ADR-1016); <see langword="null" /> keeps <paramref name="tlsProvider" />.</param>
    internal static IReadOnlyList<IProtocolHandler> CreateProtocolHandlers(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ITlsProvider tlsProvider,
        IDnsResolver dnsResolver,
        ICookieStore? cookieStore = null,
        ISecurityContextFactory? securityContexts = null,
        HttpAuthSchemes proxyAuthSchemes = HttpAuthSchemes.Basic,
        NegotiateOptions? negotiateOptions = null,
        TimeProvider? signingClock = null,
        IConnector? ftpDataConnector = null,
        IDiagnosticLog? diagnosticLog = null,
        bool tracesFtp = false,
        bool tracesSmtp = false,
        bool tracesWs = false,
        bool tracesSsh = false,
        bool tracesHttp2 = false,
        bool tracesHttp3 = false,
        bool tracesRead = false,
        IConnectionListener? ftpListener = null,
        IFtpWildcardHooks? ftpWildcardHooks = null,
        IRtspInterleaveSink? rtspInterleaveSink = null,
        Func<RtspRequestSettings?>? rtspRequestSettings = null,
        RtspSessionState? rtspSessionState = null,
        bool webSocketRawMode = false,
        WsPerformConnection? webSocketPerformConnection = null,
        IHttp2PushSink? http2PushSink = null,
        IWebSocketRandomSource? webSocketRandomSource = null,
        bool ftpStopsBeforeEntryPath = false,
        Func<int, byte[]>? digestRandomBytes = null,
        TransferSocketWait? socketWait = null,
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? extensionHandlers = null,
        Func<IProtocolHandler, IProtocolHandler>? extensionTransferHooks = null,
        Func<IConnector, IConnector>? extensionConnector = null,
        Func<IDatagramConnector, IDatagramConnector>? extensionDatagramConnector = null,
        Func<IDnsResolver, IDnsResolver>? extensionResolver = null,
        Func<IConnector, IConnector>? extensionCertificateConnector = null,
        Func<ITlsProvider, ITlsProvider>? extensionTlsProvider = null)
    {
        dnsResolver = ThroughExtension(dnsResolver, extensionResolver);
        connector = ThroughExtension(ThroughExtension(connector, extensionConnector), extensionCertificateConnector);
        tlsProvider = ThroughExtension(tlsProvider, extensionTlsProvider);
        datagramConnector = ThroughExtension(datagramConnector, extensionDatagramConnector);
        Func<RtspRequestSettings?> rtspSettingsOf = rtspRequestSettings ?? (() => null);
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector recordingConnector = new(connector, recorder);
        EndPointRecordingDatagramConnector recordingDatagramConnector = new(NumberedDatagramsOf(connector, datagramConnector), recorder);
        Lazy<ISecurityContextFactory> contexts = new(() => securityContexts ?? CreateSecurityContextFactory(connector, datagramConnector, diagnosticLog));
        Lazy<RankedHttpAuthenticator> httpAuthenticator = new(() => CreateHttpAuthenticator(contexts.Value, negotiateOptions, diagnosticLog, digestRandomBytes));
        SecurityDelegation saslDelegation = (negotiateOptions ?? NegotiateOptions.Default).Delegation;
        LazyProtocolHandler http = new(["http", "https"], () => new HttpProtocolHandler(recordingConnector, new HttpSignatureHttpAuthenticator(new AwsSigV4HttpAuthenticator(httpAuthenticator.Value, new AwsSigV4Signer(signingClock ?? TimeProvider.System, CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()), diagnosticLog)), signingClock ?? TimeProvider.System), cookieStore, proxyAuthSchemes, http2PushSink) { TracesHttp2Frames = tracesHttp2, TracesHttp3Streams = tracesHttp3, TracesClientReaders = tracesRead, ContinueWaitBegins = ContinueWaitBeginsOf(socketWait), ContinueWaitEnds = ContinueWaitEndsOf(socketWait) });

        IProtocolHandler[] handlers =
        [
            new LazyProtocolHandler(["file"], () => new FileProtocolHandler(new PhysicalFileSystem(), ConnectionNumbersOf(connector))),
            new LazyProtocolHandler(["dict"], () => new DictProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["gopher", "gophers"], () => new GopherProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["telnet"], () => new TelnetProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["tftp"], () => new TftpProtocolHandler(recordingDatagramConnector, recordingConnector, CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()))),
            new LazyProtocolHandler(["mqtt", "mqtts"], () => new MqttProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["imap", "imaps"], () => new ImapProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts.Value, saslDelegation, diagnosticLog, digestRandomBytes))),
            new LazyProtocolHandler(["pop3", "pop3s"], () => new Pop3ProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts.Value, saslDelegation, diagnosticLog, digestRandomBytes))),
            new LazyProtocolHandler(["smtp", "smtps"], () => new SmtpProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts.Value, saslDelegation, diagnosticLog, digestRandomBytes)) { TracesStateMachine = tracesSmtp }),
            new LazyProtocolHandler(["ldap", "ldaps"], () => new LdapProtocolHandler(recordingConnector, LdapDialectFor(OperatingSystem.IsWindows()), new NegotiateLogonTokenSource(), tlsProvider)),
            new LazyProtocolHandler(["ws", "wss"], () => new WsProtocolHandler(recordingConnector, httpAuthenticator.Value, webSocketRandomSource ?? new SystemWebSocketRandomSource()) { TracesFrames = tracesWs, RawMode = webSocketRawMode, PerformConnection = webSocketPerformConnection }),
            new LazyProtocolHandler(["rtsp"], () => new RtspProtocolHandler(recordingConnector, httpAuthenticator.Value, rtspInterleaveSink, rtspSettingsOf(), rtspSessionState)),
            new LazyProtocolHandler(["smb", "smbs"], () => new DisabledProtocolHandler(["smb", "smbs"])),
            new LazyProtocolHandler(["scp", "sftp"], () => new SshProtocolHandler(
                recordingConnector,
                new PhysicalFileSystem(),
                SshAlgorithmPreferencesFor(OperatingSystem.IsWindows()),
                CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())) { TracesStateMachine = tracesSsh }),
            http,
            new LazyProtocolHandler(["ftp", "ftps"], () => new RoutingFtpProtocolHandler(
                http,
                CreateFtpProtocolHandler(recordingConnector, new EndPointRecordingConnector(ftpDataConnector ?? connector, recorder), tlsProvider, dnsResolver, tracesFtp, ftpListener, ftpWildcardHooks, ftpStopsBeforeEntryPath))),
            .. ExtensionHandlersOver(new ProtocolHandlerServices(recordingConnector, recordingDatagramConnector, tlsProvider, dnsResolver), extensionHandlers),
        ];

        return [.. handlers.Select(handler => new EndPointReportingProtocolHandler(ThroughTransferHooks(handler, extensionTransferHooks), recorder))];
    }
}
