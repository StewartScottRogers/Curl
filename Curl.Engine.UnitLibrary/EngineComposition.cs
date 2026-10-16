using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Authentication;
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
using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Engine;

/// <summary>
/// Builds the engine's services with plain constructor calls - protocol handlers, transports, DNS
/// resolvers, TLS providers, security-context factories and authenticators - for the composition
/// root to wire into a runner. There is no container and no reflection, so native ahead-of-time
/// publishing sees every type that is used (ADR-0506).
/// </summary>
internal static class EngineComposition
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
    /// (ADR-0169); <c>smb</c> and <c>smbs</c> over <paramref name="connector" />, speaking curl's SMBv1 on every
    /// platform (ADR-0200); <c>scp</c> and <c>sftp</c> over <paramref name="connector" />, reading the known-hosts
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
        IConnectionListener? ftpListener = null)
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector recordingConnector = new(connector, recorder);
        EndPointRecordingDatagramConnector recordingDatagramConnector = new(NumberedDatagramsOf(connector, datagramConnector), recorder);
        Lazy<ISecurityContextFactory> contexts = new(() => securityContexts ?? CreateSecurityContextFactory(connector, datagramConnector, diagnosticLog));
        Lazy<RankedHttpAuthenticator> httpAuthenticator = new(() => CreateHttpAuthenticator(contexts.Value, negotiateOptions, diagnosticLog));
        SecurityDelegation saslDelegation = (negotiateOptions ?? NegotiateOptions.Default).Delegation;
        LazyProtocolHandler http = new(["http", "https"], () => new HttpProtocolHandler(recordingConnector, new HttpSignatureHttpAuthenticator(new AwsSigV4HttpAuthenticator(httpAuthenticator.Value, new AwsSigV4Signer(signingClock ?? TimeProvider.System, CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()), diagnosticLog)), signingClock ?? TimeProvider.System), cookieStore, proxyAuthSchemes) { TracesHttp2Frames = tracesHttp2, TracesHttp3Streams = tracesHttp3, TracesClientReaders = tracesRead });

        IProtocolHandler[] handlers =
        [
            new LazyProtocolHandler(["file"], () => new FileProtocolHandler(new PhysicalFileSystem(), ConnectionNumbersOf(connector))),
            new LazyProtocolHandler(["dict"], () => new DictProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["gopher", "gophers"], () => new GopherProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["telnet"], () => new TelnetProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["tftp"], () => new TftpProtocolHandler(recordingDatagramConnector, recordingConnector, CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()))),
            new LazyProtocolHandler(["mqtt", "mqtts"], () => new MqttProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["imap", "imaps"], () => new ImapProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts.Value, saslDelegation, diagnosticLog))),
            new LazyProtocolHandler(["pop3", "pop3s"], () => new Pop3ProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts.Value, saslDelegation, diagnosticLog))),
            new LazyProtocolHandler(["smtp", "smtps"], () => new SmtpProtocolHandler(recordingConnector, tlsProvider, CreateSaslAuthenticator(contexts.Value, saslDelegation, diagnosticLog)) { TracesStateMachine = tracesSmtp }),
            new LazyProtocolHandler(["ldap", "ldaps"], () => new LdapProtocolHandler(recordingConnector, LdapDialectFor(OperatingSystem.IsWindows()))),
            new LazyProtocolHandler(["ws", "wss"], () => new WsProtocolHandler(recordingConnector, httpAuthenticator.Value, new SystemWebSocketRandomSource()) { TracesFrames = tracesWs }),
            new LazyProtocolHandler(["rtsp"], () => new RtspProtocolHandler(recordingConnector, httpAuthenticator.Value)),
            new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector)),
            new LazyProtocolHandler(["scp", "sftp"], () => new SshProtocolHandler(
                recordingConnector,
                new PhysicalFileSystem(),
                SshAlgorithmPreferencesFor(OperatingSystem.IsWindows()),
                CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())) { TracesStateMachine = tracesSsh }),
            http,
            new LazyProtocolHandler(["ftp", "ftps"], () => new RoutingFtpProtocolHandler(
                http,
                CreateFtpProtocolHandler(recordingConnector, new EndPointRecordingConnector(ftpDataConnector ?? connector, recorder), tlsProvider, dnsResolver, tracesFtp, ftpListener))),
        ];

        return [.. handlers.Select(handler => new EndPointReportingProtocolHandler(handler, recorder))];
    }

    /// <summary>
    /// Gives the LDAP library the platform's curl is built with: WinLDAP on Windows, OpenLDAP elsewhere.
    /// </summary>
    /// <param name="runsOnWindows">Whether the run is on Windows.</param>
    /// <returns>The dialect.</returns>
    internal static LdapDialect LdapDialectFor(bool runsOnWindows) =>
        runsOnWindows ? LdapDialect.WinLdap : LdapDialect.OpenLdap;

    /// <summary>
    /// Gives the SSH algorithms the platform's curl offers: the Schannel build's on Windows, the
    /// OpenSSL build's elsewhere.
    /// </summary>
    /// <param name="runsOnWindows">Whether the run is on Windows.</param>
    /// <returns>The algorithm preferences.</returns>
    internal static SshAlgorithmPreferences SshAlgorithmPreferencesFor(bool runsOnWindows) =>
        runsOnWindows ? SshAlgorithmPreferences.WindowsReference : SshAlgorithmPreferences.OpenSslReference;

    /// <summary>
    /// Creates the FTP handler for <c>ftp</c> and <c>ftps</c> (ADR-0102): the control connection
    /// through <paramref name="connector" />, passive data
    /// connections through <paramref name="dataConnector" />, active ones (<c>-P</c>) on a
    /// <see cref="TcpConnectionListener" />, a <c>-P</c> interface name looked up with
    /// <see cref="SystemNetworkInterfaceLookup" /> (ADR-0110) and a <c>-P</c> host name
    /// resolved with <paramref name="dnsResolver" /> (ADR-0108), and TLS from
    /// <paramref name="tlsProvider" />.
    /// </summary>
    /// <param name="connector">Supplies the control connection.</param>
    /// <param name="dataConnector">Supplies the passive data connection.</param>
    /// <param name="tlsProvider">Upgrades a connection after an accepted <c>AUTH</c> or <c>PROT P</c>.</param>
    /// <param name="dnsResolver">Resolves a host name given to <c>-P</c>.</param>
    /// <param name="tracesFtp">Whether it writes the <c>--trace-config ftp</c> lines (<see cref="TracesFtp" />, BL-1162).</param>
    /// <param name="listener">Listens for active-mode data connections; a <see cref="TcpConnectionListener" /> when not given.</param>
    /// <returns>The handler.</returns>
    internal static FtpProtocolHandler CreateFtpProtocolHandler(IConnector connector, IConnector dataConnector, ITlsProvider tlsProvider, IDnsResolver dnsResolver, bool tracesFtp = false, IConnectionListener? listener = null) =>
        new(connector, dataConnector, listener ?? new TcpConnectionListener(), tlsProvider, dnsResolver, new SystemNetworkInterfaceLookup()) { TracesStateMachine = tracesFtp };

    /// <summary>
    /// The connector FTP's passive data connections go through: the option group's pooling
    /// connector over its TCP connector's <see cref="TcpConnector.ForFtpDataConnections" />, so
    /// they share the pool's numbering and the connector's DNS cache and <c>--resolve</c>, but
    /// take only a matching <c>--connect-to</c> host, keeping the reply's port, as curl 8.21.0 does
    /// (upstream test713, BL-1976), and only <c>-m</c> or the system's own connect timeout ends
    /// one, as curl 8.21.0 does not hold it to <c>--connect-timeout</c> (measured, ADR-0286, BL-797).
    /// </summary>
    /// <param name="transports">The option group's connectors.</param>
    /// <returns>The connector.</returns>
    internal static IConnector FtpDataConnectorOf(CurlTransports transports) =>
        transports.PoolingConnector.Over(transports.TcpConnector.ForFtpDataConnections());

    /// <summary>
    /// Creates the HTTP authenticator: a <see cref="RankedHttpAuthenticator" /> that answers the
    /// scheme curl 8.21.0 picks among those <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c> and <c>--anyauth</c>
    /// allow, with a <see cref="BasicAndBearerAuthenticator" /> for Basic, Bearer
    /// (<c>--oauth2-bearer</c>) and the first request, and a <see cref="DigestAuthenticator" />
    /// drawing each client nonce from the matched build's <see cref="DigestClientNonce.ForBuild" />, both encoding
    /// credentials in the platform's encoding (<see cref="CredentialEncoding.ForPlatform" />),
    /// and a <see cref="NegotiateHttpAuthenticator" /> and an <see cref="NtlmHttpAuthenticator" /> over <paramref name="securityContexts" />
    /// for <c>--negotiate</c> and <c>--ntlm</c> (ADR-0176, ADR-0181), a refused NTLM Type 2 message failing the transfer
    /// on Windows, as curl's SSPI build fails it, Negotiate naming the service and delegating as
    /// <paramref name="negotiateOptions" /> says (ADR-0188).
    /// </summary>
    /// <param name="securityContexts">Makes Negotiate's and NTLM's contexts: <see cref="CreateSecurityContextFactory" />'s in production.</param>
    /// <param name="negotiateOptions">The service names and delegation level; <see cref="NegotiateOptions.Default" /> when <see langword="null" />.</param>
    /// <returns>The authenticator.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    internal static RankedHttpAuthenticator CreateHttpAuthenticator(ISecurityContextFactory securityContexts, NegotiateOptions? negotiateOptions = null, IDiagnosticLog? diagnosticLog = null)
    {
        Encoding credentialEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows());
        bool digestMatchesSspiBuild = MatchesSspiBuild(securityContexts);

        return new RankedHttpAuthenticator(
            new BasicAndBearerAuthenticator(credentialEncoding),
            new DigestAuthenticator(credentialEncoding, DigestClientNonce.ForBuild(digestMatchesSspiBuild), diagnosticLog, matchesSspiBuild: digestMatchesSspiBuild),
            new NegotiateHttpAuthenticator(securityContexts, negotiateOptions, diagnosticLog: diagnosticLog),
            new NtlmHttpAuthenticator(securityContexts, matchesSspiBuild: OperatingSystem.IsWindows(), diagnosticLog),
            diagnosticLog);
    }

    /// <summary>
    /// Whether Digest sends its header in the Schannel build's SSPI form: on Windows, unless the contexts
    /// are the hand-built ones that curl's non-SSPI build is matched with (the upstream conformance runner's).
    /// </summary>
    /// <param name="securityContexts">The contexts the authenticator is made over.</param>
    /// <returns><see langword="true" /> for the SSPI form.</returns>
    private static bool MatchesSspiBuild(ISecurityContextFactory securityContexts) =>
        OperatingSystem.IsWindows() && securityContexts is not HandBuiltNtlmSecurityContextFactory and not LateBoundSecurityContextFactory { UsesHandBuiltNtlm: true };

    /// <summary>
    /// Creates ADR-0142's router for NTLM, Negotiate and Kerberos contexts: SSPI on Windows,
    /// elsewhere the system GSS-API with the hand-built SPNEGO and Kerberos behind it, whose KDC
    /// exchanges go through a <see cref="KerberosKdcSocketTransport" /> over
    /// <paramref name="datagramConnector" /> and <paramref name="connector" /> (waiting
    /// <see cref="KdcReplyTimeout" /> for a UDP reply), whose <c>https://</c> KDCs are reached through a
    /// <see cref="KerberosKdcProxyHttpsTransport" /> over <paramref name="connector" /> with TLS, and whose SRV lookups go through a
    /// <see cref="DnsServerResolver" /> asking the system's DNS servers (ADR-0176).
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <returns>The router.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    internal static RoutingSecurityContextFactory CreateSecurityContextFactory(IConnector connector, IDatagramConnector datagramConnector, IDiagnosticLog? diagnosticLog = null) =>
        new(
            OperatingSystem.IsWindows(),
            new SystemSecurityContextFactory(),
            CreateHandBuiltSecurityContextFactory(connector, datagramConnector, diagnosticLog),
            diagnosticLog);

    /// <summary>
    /// Creates the contexts the dialing runner (<c>Curl.Console</c>'s <c>CurlComposition.CreateRunner</c>)
    /// uses: <see cref="CreateSecurityContextFactory(IConnector, IDatagramConnector, IDiagnosticLog?)" />'s
    /// router, with NTLM sent to the hand-built context on every platform when
    /// <paramref name="usesHandBuiltNtlm" /> is set (<see cref="HandBuiltNtlmSecurityContextFactory" />, BL-1858).
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <param name="diagnosticLog">The run's diagnostic log.</param>
    /// <param name="usesHandBuiltNtlm">Whether NTLM is answered by the hand-built context, as curl's non-SSPI build answers it.</param>
    /// <returns>The factory.</returns>
    internal static ISecurityContextFactory CreateDialingSecurityContextFactory(IConnector connector, IDatagramConnector datagramConnector, IDiagnosticLog? diagnosticLog, bool usesHandBuiltNtlm) =>
        usesHandBuiltNtlm
            ? new HandBuiltNtlmSecurityContextFactory(CreateSecurityContextFactory(connector, datagramConnector, diagnosticLog), CreateHandBuiltSecurityContextFactory(connector, datagramConnector, diagnosticLog))
            : CreateSecurityContextFactory(connector, datagramConnector, diagnosticLog);

    /// <summary>
    /// Creates ADR-0142's hand-built factory: curl's own NTLM, and SPNEGO and Kerberos over the
    /// hand-built Kerberos whose KDC exchanges are described on
    /// <see cref="CreateSecurityContextFactory(IConnector, IDatagramConnector, IDiagnosticLog?)" />.
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <param name="diagnosticLog">The run's diagnostic log.</param>
    /// <returns>The factory.</returns>
    private static HandBuiltSecurityContextFactory CreateHandBuiltSecurityContextFactory(IConnector connector, IDatagramConnector datagramConnector, IDiagnosticLog? diagnosticLog)
    {
        KerberosKdcSocketTransport kdcTransport = new(datagramConnector, connector, KdcReplyTimeout, TimeProvider.System);
        DnsServerResolver srvResolver = new(new DnsServerResolverOptions(null, null, null, null), TimeProvider.System);
        HandBuiltKerberosSources sources = new(
            new KerberosDiskFileReader(),
            new KerberosDiskFileWriter(),
            HandBuiltKerberosSources.ReadProcessEnvironmentVariable,
            ProcessUserId.Read,
            new KerberosDnsSrvLookup(srvResolver.ResolveServiceAsync),
            kdcTransport,
            new KerberosKdcProxyHttpsTransport(connector, KdcProxyExchangeTimeout, TimeProvider.System),
            TimeProvider.System);
        return new HandBuiltSecurityContextFactory(sources.CreateTicketSource(), TimeProvider.System, new SystemKerberosRandomSource(), new SystemNtlmRandomSource(), diagnosticLog);
    }

    /// <summary>
    /// How long a UDP request to a KDC waits for its reply before the next KDC is tried: MIT's
    /// first per-KDC wait (<c>krb5_sendto_kdc</c>), one second.
    /// </summary>
    internal static TimeSpan KdcReplyTimeout { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long one HTTPS exchange with an MS-KKDCP proxy, connection and TLS included, may take
    /// before the next KDC is tried (BL-882): ten seconds, as MIT's <c>krb5_sendto_kdc</c> gives
    /// one KDC's stream connection.
    /// </summary>
    internal static TimeSpan KdcProxyExchangeTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates the SASL authenticator the SMTP, POP3 and IMAP handlers share (ADR-0121): a
    /// <see cref="SaslAuthenticator" /> encoding credentials in the platform's encoding
    /// (<see cref="CredentialEncoding.ForPlatform" />), as the HTTP authenticator does, and answering
    /// GSSAPI and NTLM on the contexts <paramref name="securityContexts" /> makes (ADR-0184, BL-852),
    /// asking GSSAPI's for the <paramref name="gssapiDelegation" /> level (BL-874).
    /// </summary>
    /// <param name="securityContexts">Makes GSSAPI's and NTLM's contexts: <see cref="CreateSecurityContextFactory" />'s in production.</param>
    /// <param name="gssapiDelegation">The <c>--delegation</c> level; <see cref="SecurityDelegation.None" /> when not given.</param>
    /// <returns>The authenticator.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    internal static ISaslAuthenticator CreateSaslAuthenticator(ISecurityContextFactory securityContexts, SecurityDelegation gssapiDelegation = SecurityDelegation.None, IDiagnosticLog? diagnosticLog = null) =>
        new SaslAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()), securityContexts, diagnosticLog) { GssapiDelegation = gssapiDelegation };

    /// <summary>
    /// Creates the network transports for one run: a <see cref="TcpConnector" /> over the
    /// resolver <see cref="CreateDnsResolver" /> picks, a <see cref="TcpDialer" /> that sets <c>TCP_NODELAY</c> and
    /// <c>SO_KEEPALIVE</c> unless <c>--no-tcp-nodelay</c> or <c>--no-keepalive</c> says not to, and the
    /// TLS provider <see cref="CreateTlsProvider" /> routes to, and a <see cref="UdpDatagramConnector" />. Both
    /// connectors and the TLS provider share the one resolver and <see cref="TimeProvider.System" />. TLS uses
    /// the <see cref="TlsClientOptions" /> mapped from <paramref name="tls" /> by
    /// <see cref="TlsClientOptionsMapping.Of" /> from <paramref name="tls" />, one set shared by every URL on
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
    /// Creates the run's resolver: the <see cref="CreateDohResolver" /> one when <c>--doh-url</c> is
    /// given, since curl asks the DoH server whichever resolver it was built with (BL-642); otherwise
    /// the hand-built <see cref="DnsServerResolver" /> when any of
    /// <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> and <c>--dns-ipv6-addr</c>
    /// is given, as curl's c-ares build resolves then (ADR-0170, BL-694), asking only the
    /// <c>-4</c> or <c>-6</c> family's records; otherwise the <see cref="SystemDnsResolver" />.
    /// </summary>
    /// <param name="network">The option group's network options, whose DNS settings and IP version pick the resolver.</param>
    /// <param name="tls">The option group's TLS options, whose DoH settings the DoH connector's handshakes apply.</param>
    /// <param name="timeProvider">The clock the hand-built resolver and the DoH connections time on.</param>
    /// <param name="tcpDialer">Opens the plaintext TCP connections to the DoH server.</param>
    /// <param name="dohTrace">
    /// Receives the DoH resolver's <c>--trace-config doh</c> lines (BL-1102); <see langword="null" />
    /// for none.
    /// </param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code, which the DoH resolver's <c>--trace-config doh</c> lines carry (BL-1102): <c>Curl.Console</c>'s <c>CurlEasyErrorText.Of</c> in production; <see langword="null" /> for an empty text.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the DoH connector's DNS filter lines; <see langword="null" /> for none.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver CreateDnsResolver(EasyNetworkOptions network, EasyTlsOptions tls, TimeProvider timeProvider, ITcpDialer tcpDialer, ITransferEvents? dohTrace = null, Func<CurlExitCode, string>? easyErrorText = null, EngineTraceComponents? traces = null)
    {
        if (network.DohUrl is { } dohUrl)
        {
            return CreateDohResolver(dohUrl, CreateDohConnector(traces ?? EngineTraceComponents.None, tls, tcpDialer, timeProvider), AddressFamilyOf(network), dohTrace, easyErrorText);
        }

        DnsServerResolverOptions resolverOptions = new(
            network.DnsServers,
            network.DnsInterface,
            network.DnsLocalIPv4,
            network.DnsLocalIPv6,
            AddressFamilyOf(network));
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
    /// <param name="addressFamily">
    /// The <c>-4</c> or <c>-6</c> family (<see cref="AddressFamilyOf" />), whose query alone is sent (BL-939).
    /// </param>
    /// <param name="dohTrace">
    /// Receives the <c>--trace-config doh</c> lines, with <paramref name="easyErrorText" />'s texts
    /// (BL-1102), and each DoH sub-transfer's own lines through a <see cref="DohSubTransferEvents" />
    /// (BL-1180); <see langword="null" /> for none.
    /// </param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code, which the DoH resolver's <c>--trace-config doh</c> lines carry (BL-1102): <c>Curl.Console</c>'s <c>CurlEasyErrorText.Of</c> in production; <see langword="null" /> for an empty text.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver CreateDohResolver(string dohUrl, IConnector connector, AddressFamily addressFamily, ITransferEvents? dohTrace = null, Func<CurlExitCode, string>? easyErrorText = null) =>
        DohUrlOf(dohUrl) is { } url
            ? new DohDnsResolver(connector, url, dohTrace ?? NoTransferEvents.Instance, easyErrorText ?? NoEasyErrorText)
            {
                AddressFamily = addressFamily,
                SubTransferEvents = dohTrace is null ? NoTransferEvents.Instance : new DohSubTransferEvents(dohTrace, PlatformTlsBackend.ForProcess),
            }
            : new UnusableDohUrlResolver();

    /// <summary>The easy error text of a run given none: empty for every exit code.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>An empty text.</returns>
    private static string NoEasyErrorText(CurlExitCode exitCode) => string.Empty;

    /// <summary>
    /// Whether <c>--trace-config</c> turned on curl 8.21.0's <c>[DNS]</c> lines: <c>dns</c>, <c>doh</c>,
    /// <c>network</c> (measured, BL-1103 Notes) and <c>all</c> each turn on the DNS filter's lines and the DoH resolver's alike (measured,
    /// BL-1102 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesDns(EngineTraceComponents traces) =>
        traces.Components.Contains("dns") || traces.Components.Contains("doh") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SETUP]</c> lines are written: <c>setup</c> or <c>all</c> is among the
    /// trace components, which <c>-vv</c> and up put there too (measured, BL-1103 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSetup(EngineTraceComponents traces) =>
        traces.Components.Contains("setup") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HAPROXY]</c> lines are written: <c>haproxy</c>, <c>proxy</c> or
    /// <c>all</c> is among the trace components, which <c>-vvvv</c> puts there too; <c>network</c>
    /// does not turn them on (measured, BL-1160 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHaproxy(EngineTraceComponents traces) =>
        traces.Components.Contains("haproxy") || traces.Components.Contains("proxy") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SOCKS]</c> lines are written: <c>socks</c>, <c>proxy</c> or a
    /// <c>--trace-config all</c> is among the trace components; neither <c>network</c> nor the
    /// <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1191 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSocks(EngineTraceComponents traces) =>
        traces.Components.Contains("socks")
        || traces.Components.Contains("proxy")
        || (traces.Components.Contains("all") && !traces.VerbosityComponents.Contains("all"));

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTP-PROXY]</c> lines are written: <c>http-proxy</c>, <c>proxy</c> or
    /// a <c>--trace-config all</c> is among the trace components; neither <c>network</c> nor the
    /// <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1193 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttpProxy(EngineTraceComponents traces) => TracesProxyComponent(traces, "http-proxy");

    /// <summary>
    /// Whether curl 8.21.0's <c>[H1-PROXY]</c> lines are written: <c>h1-proxy</c>, <c>proxy</c> or a
    /// <c>--trace-config all</c> is among the trace components; neither <c>network</c> nor the
    /// <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1193 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesH1Proxy(EngineTraceComponents traces) => TracesProxyComponent(traces, "h1-proxy");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SSL]</c> lines are written around an origin's TLS handshake:
    /// <c>ssl</c>, <c>network</c> or <c>all</c> is among the trace components, which <c>-vvvv</c> puts
    /// there too; <c>proxy</c> and <c>tls</c> do not (measured, BL-1287 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSsl(EngineTraceComponents traces) =>
        traces.Components.Contains("ssl") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SSL-PROXY]</c> lines are written around an HTTPS proxy's TLS handshake:
    /// <c>proxy</c> or a <c>--trace-config all</c> is among the trace components; neither <c>ssl</c>,
    /// <c>network</c> nor the <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1287 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSslProxy(EngineTraceComponents traces) =>
        traces.Components.Contains("proxy")
        || (traces.Components.Contains("all") && !traces.VerbosityComponents.Contains("all"));

    private static bool TracesProxyComponent(EngineTraceComponents traces, string component) =>
        traces.Components.Contains(component)
        || traces.Components.Contains("proxy")
        || (traces.Components.Contains("all") && !traces.VerbosityComponents.Contains("all"));

    /// <summary>
    /// Whether curl 8.21.0's <c>[HAPPY-EYEBALLS]</c> lines are written: <c>happy-eyeballs</c>,
    /// <c>network</c> or <c>all</c> is among the trace components, which <c>-vvvv</c> puts there too
    /// (measured, BL-1161 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHappyEyeballs(EngineTraceComponents traces) =>
        traces.Components.Contains("happy-eyeballs") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[TCP]</c> lines are written: <c>tcp</c>, <c>network</c> or <c>all</c> is
    /// among the trace components, which <c>-vvvv</c> puts there too (measured, BL-1161 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesTcp(EngineTraceComponents traces) =>
        traces.Components.Contains("tcp") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[TIMER]</c> lines are written: <c>timer</c>, <c>network</c> or <c>all</c>
    /// is among the trace components, which <c>-vvvv</c> puts there too (measured, BL-1186 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesTimer(EngineTraceComponents traces) =>
        traces.Components.Contains("timer") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTPS-CONNECT]</c> lines are written: <c>https-connect</c> or <c>all</c>
    /// is among the trace components, which <c>-vvvv</c> puts there too; neither <c>network</c> nor
    /// <c>proxy</c> turns them on (measured, BL-1192 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttpsConnect(EngineTraceComponents traces) =>
        traces.Components.Contains("https-connect") || traces.Components.Contains("all");

    /// <summary>
    /// The HTTP version curl 8.21.0's <c>[HTTPS-CONNECT]</c> filter names for its first attempt:
    /// <c>h1</c> under <c>--http1.0</c> and <c>--http1.1</c>, <c>h3</c> under <c>--http3</c> and
    /// <c>--http3-only</c>, else <c>h2</c> (measured for none and <c>--http1.1</c>, BL-1192 Notes).
    /// </summary>
    /// <param name="version">The option group's HTTP version, <see cref="HttpVersion.None" /> when no version option was given.</param>
    /// <returns>The version's name.</returns>
    internal static string HttpsConnectFirstAttemptVersionOf(HttpVersion version) =>
        version switch
        {
            HttpVersion.Http10 or HttpVersion.Http11 => "h1",
            HttpVersion.Http3 or HttpVersion.Http3Only => "h3",
            _ => "h2",
        };

    /// <summary>
    /// Whether curl 8.21.0's <c>[READ]</c> lines are written: <c>read</c> or <c>all</c> is among the
    /// trace components, which <c>-vvv</c> and up put there too; <c>network</c> does not turn them on
    /// (measured, BL-1103 and BL-1159 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesRead(EngineTraceComponents traces) =>
        traces.Components.Contains("read") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[MULTI]</c> transfer engine lines are written: <c>multi</c>, <c>network</c>
    /// or <c>all</c> is among the trace components, which <c>-vvvv</c> puts there too (measured, BL-1103
    /// and BL-1188 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesMulti(EngineTraceComponents traces) =>
        traces.Components.Contains("multi") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[WRITE]</c> client writer lines are written: <c>write</c> or <c>all</c> is
    /// among the trace components, which <c>-vvv</c> and up put there too; <c>network</c> does not turn them
    /// on (measured, BL-1103 and BL-1187 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesWrite(EngineTraceComponents traces) =>
        traces.Components.Contains("write") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[FTP]</c> lines are written: <c>ftp</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (measured,
    /// BL-1162 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesFtp(EngineTraceComponents traces) =>
        traces.Components.Contains("ftp") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SMTP]</c> lines are written: <c>smtp</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (BL-1163).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSmtp(EngineTraceComponents traces) =>
        traces.Components.Contains("smtp") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[WS]</c> lines are written: <c>ws</c>, <c>protocol</c> or <c>all</c>
    /// is among the trace components, which <c>-vv</c> and up put there too (BL-1164).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesWs(EngineTraceComponents traces) =>
        traces.Components.Contains("ws") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SSH]</c> lines are written: <c>ssh</c>, <c>protocol</c> or <c>all</c>
    /// is among the trace components, which <c>-vv</c> and up put there too (BL-1166).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSsh(EngineTraceComponents traces) =>
        traces.Components.Contains("ssh") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTP/2]</c> frame lines are written: <c>http/2</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (BL-1167).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttp2(EngineTraceComponents traces) =>
        traces.Components.Contains("http/2") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTP/3]</c> stream lines are written: <c>http/3</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (BL-1168).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttp3(EngineTraceComponents traces) =>
        traces.Components.Contains("http/3") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

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
    /// <see cref="CreateTlsProvider" /> routes the options <see cref="TlsClientOptionsMapping.DohOf" />
    /// maps. No <c>--resolve</c>, <c>--connect-to</c>, proxy or <c>-4</c>/<c>-6</c> applies to it.
    /// Under <see cref="TracesDns" /> it writes the DNS filter's lines for each DoH sub-transfer too,
    /// as curl 8.21.0 does (BL-1180).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the DNS filter lines.</param>
    /// <param name="tls">The option group's TLS options.</param>
    /// <param name="tcpDialer">Opens each plaintext connection.</param>
    /// <param name="timeProvider">The clock the connector and its TLS provider time on.</param>
    /// <returns>The connector.</returns>
    internal static TcpConnector CreateDohConnector(EngineTraceComponents traces, EasyTlsOptions tls, ITcpDialer tcpDialer, TimeProvider timeProvider) =>
        new(
            new SystemDnsResolver(),
            tcpDialer,
            CreateTlsProvider(TlsClientOptionsMapping.DohOf(tls), timeProvider),
            timeProvider)
        {
            TracesDnsFilter = TracesDns(traces),
        };

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
    /// <param name="easyErrorText">Curl's easy error text for each exit code, which the DoH resolver's <c>--trace-config doh</c> lines carry (BL-1102): <c>Curl.Console</c>'s <c>CurlEasyErrorText.Of</c> in production; <see langword="null" /> for an empty text.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components; <see langword="null" /> for none.</param>
    internal static CurlTransports CreateTransports(EngineToolOptions tool, EasyNetworkOptions network, EasyTlsOptions tls, EasyHttpOptions http, EasyProxyOptions proxy, EasyAuthenticationOptions authentication, TimeProvider timeProvider, ConnectionCache? runConnections = null, TlsSessionCache? tlsSessions = null, IDiagnosticLog? diagnosticLog = null, DnsCache? runDnsCache = null, Func<CurlExitCode, string>? easyErrorText = null, EngineTraceComponents? traces = null) =>
        CreateTransports(tool, network, tls, http, proxy, authentication, timeProvider, runConnections, tlsSessions, diagnosticLog, runDnsCache, easyErrorText, traces, static resolver => resolver);

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
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports(EngineToolOptions tool, EasyNetworkOptions network, EasyTlsOptions tls, EasyHttpOptions http, EasyProxyOptions proxy, EasyAuthenticationOptions authentication, TimeProvider timeProvider, ConnectionCache? runConnections, TlsSessionCache? tlsSessions, IDiagnosticLog? diagnosticLog, DnsCache? runDnsCache, Func<CurlExitCode, string>? easyErrorText, EngineTraceComponents? traces, Func<IDnsResolver, IDnsResolver> wrapLookUps)
    {
        traces ??= EngineTraceComponents.None;
        TcpDialer tcpDialer = new(TcpSocketOptionsOf(network));
        FlowScopedTransferEvents? resolverEvents = TracesDns(traces) ? new() : null;
        IDnsResolver dnsResolver = CreateDnsResolver(network, tls, timeProvider, tcpDialer, resolverEvents, easyErrorText, traces);
        IDnsResolver lookUps = wrapLookUps(dnsResolver);
        TlsClientOptions tlsClientOptions = TlsClientOptionsMapping.Of(tls, http.SslEnableAlpn, tool.SslSessionsFile);
        ITlsProviderWithWarnings tlsProvider = CreateTlsProvider(tlsClientOptions, timeProvider, tlsSessions, dnsResolver as IEchConfigListLookup);
        TlsClientOptions proxyTlsClientOptions = TlsClientOptionsMapping.ProxyOf(tls, http.SslEnableAlpn);
        ITlsProviderWithWarnings proxyTlsProvider = CreateTlsProvider(proxyTlsClientOptions, timeProvider);
        LateBoundSecurityContextFactory proxyContexts = new();
        HttpProxyTunnelOptions proxyTunnelOptions = CreateProxyTunnelOptions(http, proxy, authentication, proxyContexts, diagnosticLog);
        QuicDialer quicDialer = new(tlsClientOptions, timeProvider) { WritesHttp3ConnectionLines = TracesHttp3(traces) };
        TcpConnector tcpConnector = CreateTcpConnector(tool, traces, network, http, proxy, authentication, lookUps, tcpDialer, tlsProvider, timeProvider, proxyTunnelOptions, proxyTlsProvider, quicDialer, proxyContexts, runDnsCache, resolverEvents);
        UdpDatagramConnector udpDatagramConnector = CreateUdpDatagramConnector(network, lookUps, timeProvider, diagnosticLog);
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
    /// Creates an option group's pooling connector over <paramref name="connector" />, waiting for
    /// multiplexing as <see cref="WaitsForMultiplexing" /> says: over the run's
    /// <paramref name="runConnections" /> with the group's <see cref="OptionGroupConnectionSettings" />,
    /// so a later group reuses an earlier group's connection when their settings are equal, as curl
    /// 8.21.0 does (ADR-0285, BL-754); or, without one, over a pool of its own.
    /// </summary>
    /// <param name="tool">The curl tool's own settings, whose <c>-Z</c> and <c>--ssl-sessions</c> the pool's settings hold.</param>
    /// <param name="network">The option group's network options, whose <c>--connect-to</c> entries, binding, socket and IP version the pool's settings hold.</param>
    /// <param name="tls">The option group's TLS options, which the pool's settings hold.</param>
    /// <param name="http">The option group's HTTP options, whose HTTP version the pool's settings hold.</param>
    /// <param name="proxy">The option group's proxy options, whose pre-proxy and HAProxy line the pool's settings hold.</param>
    /// <param name="connector">Opens a connection when the pool has none to reuse.</param>
    /// <param name="timeProvider">The clock a pool of its own measures idle time on.</param>
    /// <param name="runConnections">The run's connection cache, or <see langword="null" />.</param>
    /// <returns>The pooling connector.</returns>
    internal static PoolingConnector CreatePoolingConnector(
        EngineToolOptions tool,
        EasyNetworkOptions network,
        EasyTlsOptions tls,
        EasyHttpOptions http,
        EasyProxyOptions proxy,
        IConnector connector,
        TimeProvider timeProvider,
        ConnectionCache? runConnections)
    {
        bool waitsForMultiplexing = WaitsForMultiplexing(tool);
        return runConnections is null
            ? new PoolingConnector(connector, timeProvider) { WaitsForMultiplexing = waitsForMultiplexing }
            : new PoolingConnector(connector, runConnections, OptionGroupConnectionSettings.Of(tool, network, tls, http, proxy), new ConnectToMappings(network.ConnectTo)) { WaitsForMultiplexing = waitsForMultiplexing };
    }

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
        FlowScopedTransferEvents? resolverEvents = null) =>
        new(
            dnsResolver,
            tcpDialer,
            tlsProvider,
            timeProvider,
            proxyTunnelOptions,
            ResolveOverrides.Parse(network.Resolve),
            new ConnectToMappings(network.ConnectTo),
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
        };

    /// <summary>
    /// The SOCKS proxy the connector reaches an HTTP or HTTPS proxy through: the <c>--preproxy</c>
    /// value, with no scheme as SOCKS4 as curl 8.21.0 reads it (BL-614), or <see langword="null" />
    /// when it is absent, empty, does not parse or is not a SOCKS proxy. The last three fail each
    /// transfer in <see cref="TransferProxySelection" /> before anything connects.
    /// </summary>
    /// <param name="proxy">The option group's proxy options.</param>
    /// <returns>The pre-proxy, or <see langword="null" />.</returns>
    internal static ProxyEndpoint? PreProxyOf(EasyProxyOptions proxy) =>
        !string.IsNullOrEmpty(proxy.PreProxy)
        && ProxyUrlParser.TryParse(proxy.PreProxy, ProxyKind.Socks4, out ProxyEndpoint? preProxy, out _)
        && preProxy.Kind is not (ProxyKind.Http or ProxyKind.Https)
            ? preProxy
            : null;

    /// <summary>
    /// The HAProxy PROXY protocol v1 line the TCP connector sends first on each connection:
    /// with <c>--haproxy-protocol</c>, or with <c>--haproxy-clientip</c> alone, which turns it on
    /// even after <c>--no-haproxy-protocol</c> as curl 8.21.0's libcurl does (measured, BL-616
    /// Notes); <see langword="null" /> for neither.
    /// </summary>
    /// <param name="proxy">The option group's proxy options.</param>
    /// <returns>The header, or <see langword="null" />.</returns>
    internal static HaproxyProtocolHeader? HaproxyProtocolOf(EasyProxyOptions proxy) =>
        proxy.HaProxyProtocol || proxy.HaProxyClientIp is not null
            ? new HaproxyProtocolHeader(proxy.HaProxyClientIp)
            : null;

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

    /// <summary>
    /// Maps the command line to what the CONNECT request through an HTTP proxy carries: the
    /// <c>-A</c> value as its <c>User-Agent</c>, no <c>User-Agent</c> header for <c>-A ""</c>,
    /// <c>curl/8.21.0</c> without <c>-A</c>; the proxy credential encoded as the server
    /// credential is (<see cref="CredentialEncoding.ForPlatform" />, ADR-0022); and the
    /// <c>--proxy-header</c> values verbatim, never the <c>-H</c> ones (ADR-0077); the
    /// <c>-A</c> and <c>--proxy-header</c> text encoded in that same platform encoding (ADR-0067);
    /// and the proxy authenticated with the scheme the <c>--proxy-*</c> auth switches pick
    /// (<see cref="EasyAuthenticationOptions.ProxyAuthentication" />, <see cref="HttpRequestOptionsMapping.ProxyAuthSchemesOf" />), answered by the same
    /// <see cref="CreateHttpAuthenticator" /> the origin uses: Basic up front, Digest and
    /// <c>--proxy-anyauth</c> after a <c>407</c> (ADR-0186), NTLM's Type 1 and Type 3 and
    /// Negotiate's tokens on the same connection (ADR-0270). Its Negotiate and NTLM contexts come
    /// from <paramref name="securityContexts" />, with the <c>--proxy-service-name</c> and
    /// <c>--delegation</c> of <see cref="NegotiateOptionsMapping.Of" />; and an HTTP/2
    /// tunnel through an <c>https://</c> proxy when <c>--proxy-http2</c> asked for one (ADR-0408), which
    /// <paramref name="proxy" /> holds as <see cref="ProxyType.Https2" /> (ADR-0515).
    /// </summary>
    /// <param name="http">The option group's HTTP options, for <c>-A</c>.</param>
    /// <param name="proxy">The option group's proxy options, for <c>--proxy-header</c> and <c>--proxy-http2</c>.</param>
    /// <param name="authentication">The option group's authentication options, for the proxy's auth methods, <c>--proxy-service-name</c> and <c>--delegation</c>.</param>
    /// <param name="securityContexts">Makes the proxy's NTLM and Negotiate contexts: <see cref="CreateSecurityContextFactory" />'s router in production.</param>
    /// <returns>The tunnel's options.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    internal static HttpProxyTunnelOptions CreateProxyTunnelOptions(EasyHttpOptions http, EasyProxyOptions proxy, EasyAuthenticationOptions authentication, ISecurityContextFactory securityContexts, IDiagnosticLog? diagnosticLog = null) =>
        new(
            http.UserAgent switch
            {
                null => HttpProxyTunnelOptions.Default.UserAgent,
                "" => null,
                string userAgent => userAgent,
            },
            CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()))
        {
            ProxyHeaders = proxy.ProxyHeaders,
            CommandLineTextEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()),
            ProxyAuthSchemes = HttpRequestOptionsMapping.ProxyAuthSchemesOf(authentication),
            ProxyAuthenticator = CreateHttpAuthenticator(securityContexts, NegotiateOptionsMapping.Of(authentication), diagnosticLog),
            ProxyHttp2 = proxy.ProxyType == ProxyType.Https2,
        };

    /// <summary>
    /// Whether each <c>--alt-svc</c> cache learns and looks up over plain <c>http</c>: only behind the
    /// harness seam, and then when <c>CURL_ALTSVC_HTTP</c> is set at all, as curl's debug build tests it
    /// with <c>getenv</c> (ADR-0497, BL-2263).
    /// </summary>
    /// <param name="honoursAltSvcHttpVariable">Whether the seam is on.</param>
    /// <param name="environment">Reads the run's environment.</param>
    /// <returns>Whether the caches serve <c>http</c> origins.</returns>
    internal static bool UsesAltSvcOverHttp(bool honoursAltSvcHttpVariable, Func<string, string?> environment) =>
        honoursAltSvcHttpVariable && environment(AltSvcHttpVariableName) is not null;

    /// <summary>The variable whose presence makes curl's debug build use Alt-Svc over plain <c>http</c>.</summary>
    internal const string AltSvcHttpVariableName = "CURL_ALTSVC_HTTP";

    /// <summary>
    /// Whether the run's HSTS cache learns over plain <c>http</c>: only behind the harness seam, and
    /// then when <c>CURL_HSTS_HTTP</c> is set at all, as curl's debug build tests it with <c>getenv</c>
    /// (ADR-0494, BL-2250).
    /// </summary>
    /// <param name="honoursHstsHttpVariable">Whether the seam is on.</param>
    /// <param name="environment">Reads the run's environment.</param>
    /// <returns>Whether the cache learns over <c>http</c>.</returns>
    internal static bool LearnsHstsOverHttp(bool honoursHstsHttpVariable, Func<string, string?> environment) =>
        honoursHstsHttpVariable && environment(HstsHttpVariableName) is not null;

    /// <summary>The variable curl's debug build reads its HSTS clock from, in whole seconds since the epoch.</summary>
    internal const string HstsTimeVariableName = "CURL_TIME";

    /// <summary>The variable whose presence makes curl's debug build learn HSTS over plain <c>http</c>.</summary>
    internal const string HstsHttpVariableName = "CURL_HSTS_HTTP";

    /// <summary>
    /// The HSTS clock a <c>CURL_TIME</c> value fixes: a <see cref="FixedWallClock" /> at that many
    /// seconds after the epoch, or <see langword="null" /> (the run's own clock) when the value is
    /// not set or is not a whole number of seconds curl's dates can hold (ADR-0493, BL-2234).
    /// </summary>
    /// <param name="value">The variable's value, or <see langword="null" /> when not set.</param>
    /// <returns>The clock, or <see langword="null" />.</returns>
    internal static FixedWallClock? HstsClockOf(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) && seconds <= MaximumHstsTimeSeconds
            ? new FixedWallClock(DateTimeOffset.FromUnixTimeSeconds(seconds))
            : null;

    /// <summary>The last second <see cref="DateTimeOffset" /> holds: 9999-12-31T23:59:59Z.</summary>
    private const long MaximumHstsTimeSeconds = 253402300799;

    /// <summary>The variable curl's debug build reads the DNS server to ask from (<c>override-dns</c>).</summary>
    internal const string DnsServerVariableName = "CURL_DNS_SERVER";

    /// <summary>
    /// The resolver an option group of the dialing runner uses: with a non-empty
    /// <paramref name="dnsServer" />, a <see cref="DnsServerResolver" /> asking that server
    /// (<c>ip:port</c>, parsed as <c>--dns-servers</c> is) for the <c>-4</c> or <c>-6</c> family's
    /// records through <paramref name="datagramConnector" />, as curl's debug build honours
    /// <c>CURL_DNS_SERVER</c> (ADR-0492, BL-2248); otherwise <paramref name="dnsResolver" />.
    /// </summary>
    /// <param name="network">The option group's network options, whose IP version the queries ask for.</param>
    /// <param name="dnsServer">The <c>CURL_DNS_SERVER</c> value, or <see langword="null" /> when not honoured or not set.</param>
    /// <param name="dnsResolver">The resolver the runner was given.</param>
    /// <param name="datagramConnector">Carries the queries.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver DnsServerVariableResolverOf(EasyNetworkOptions network, string? dnsServer, IDnsResolver dnsResolver, IDatagramConnector datagramConnector) =>
        string.IsNullOrEmpty(dnsServer)
            ? dnsResolver
            : new DnsServerResolver(new DnsServerResolverOptions(dnsServer, null, null, null, AddressFamilyOf(network)), TimeProvider.System, datagramConnector);

    /// <summary>
    /// The QUIC dialer of an option group of the dialing runner: its <c>--http3</c> and
    /// <c>--http3-only</c> datagrams go through <paramref name="datagramConnector" />
    /// (<see cref="DatagramConnectorUdpChannelOpener" />), not a real UDP socket, so an in-process
    /// HTTP/3 server answers them (BL-2260). The executable's <see cref="CreateTransports(EngineToolOptions, EasyNetworkOptions, EasyTlsOptions, EasyHttpOptions, EasyProxyOptions, EasyAuthenticationOptions, TimeProvider, ConnectionCache?, TlsSessionCache?, IDiagnosticLog?, DnsCache?, Func{CurlExitCode, string}?, EngineTraceComponents?)" />
    /// keeps the socket-opening dialer.
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the HTTP/3 connection lines.</param>
    /// <param name="tlsClientOptions">The origin's TLS options.</param>
    /// <param name="datagramConnector">Opens the QUIC channels.</param>
    /// <returns>The dialer.</returns>
    internal static QuicDialer CreateDatagramConnectorQuicDialer(EngineTraceComponents traces, TlsClientOptions tlsClientOptions, IDatagramConnector datagramConnector) =>
        new(new DatagramConnectorUdpChannelOpener(datagramConnector), tlsClientOptions, TimeProvider.System) { WritesHttp3ConnectionLines = TracesHttp3(traces) };

    /// <summary>
    /// The datagram connector TFTP opens its channel through: over a
    /// <see cref="PoolingConnector" />, one that numbers each open in that pool's sequence, as
    /// curl 8.21.0 numbers a TFTP transfer's connection with the connections before it
    /// (BL-969); over any other connector, <paramref name="datagramConnector" /> itself.
    /// </summary>
    /// <param name="connector">The connector the TCP protocols connect through.</param>
    /// <param name="datagramConnector">Opens the channels.</param>
    /// <returns>The datagram connector.</returns>
    internal static IDatagramConnector NumberedDatagramsOf(IConnector connector, IDatagramConnector datagramConnector) =>
        connector is PoolingConnector pool ? pool.NumberingDatagrams(datagramConnector) : datagramConnector;

    /// <summary>
    /// The count the <c>file://</c> handler numbers its transfers in: over a
    /// <see cref="PoolingConnector" />, that pool's, as curl 8.21.0 numbers a <c>file://</c>
    /// transfer with every connection the run opens (BL-977); over any other connector, a count
    /// of its own from <c>0</c>.
    /// </summary>
    /// <param name="connector">The connector the TCP protocols connect through.</param>
    /// <returns>The count.</returns>
    internal static IConnectionNumbers ConnectionNumbersOf(IConnector connector) =>
        connector is PoolingConnector pool ? pool.ConnectionNumbers : new ConnectionNumberSequence();
}
