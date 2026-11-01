using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Api.Options.Protocols;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Imap;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// The managed <see cref="ICurlEngine" /> (ADR-0506, ADR-0541): each easy handle's transfer runs on
/// the protocol handlers and transports <see cref="EngineComposition" /> builds from that handle's
/// <see cref="EasyOptions" />. Every cache - connections, TLS sessions, DNS, cookies, HSTS - and the
/// <c>%{xfer_id}</c> numbering belong to the engine object (<see cref="ManagedEngineSharedState" />,
/// ADR-0542), shared by its handles as the curl tool's transfers share them across a run, and none is
/// static, so two engines in one process share nothing but what a <see cref="ManagedShareHandle" /> shares
/// (ADR-0606). The multi handle is <see cref="ManagedMultiTransfer" /> and the URL handle
/// <see cref="ManagedCurlUrlHandle" />, the MIME handle <see cref="ManagedMimeBuilder" /> and the form
/// <see cref="ManagedFormBuilder" />.
/// </summary>
public sealed class ManagedCurlEngine : ICurlEngine
{
    private readonly ManagedEngineSharedState shared;
    private readonly EasyTransferHost handleHost;
    private readonly Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, IHttp2PushSink?, HandleRtspSession, WsPerformConnection, ProtocolDispatcher> createDispatcher;

    /// <summary>Creates the engine on the system clock and the real network, with no easy error text.</summary>
    public ManagedCurlEngine()
        : this(TimeProvider.System, null)
    {
    }

    /// <summary>
    /// Creates the engine on the system clock and the real network, with no easy error text, its transfers reaching the
    /// handlers <paramref name="extensionHandlers" /> builds after curl's own (ADR-0999): the factory <c>Curl.Api</c> and
    /// <c>Curl.Mcp</c> pass in from <c>Curl.Extension.Composition</c>. The engine never references an extension.
    /// </summary>
    /// <param name="extensionHandlers">Builds the extensions' handlers over the transfer's transports; <see langword="null" /> for curl's alone.</param>
    public ManagedCurlEngine(Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? extensionHandlers)
        : this(TimeProvider.System, null, extensionHandlers: extensionHandlers)
    {
    }

    /// <summary>
    /// Creates the engine on <paramref name="timeProvider" />, building each transfer's dispatcher
    /// with <paramref name="createDispatcher" />, or <see cref="CreateDispatcher" />'s production one.
    /// </summary>
    /// <param name="timeProvider">The clock the engine's caches and transfers run on.</param>
    /// <param name="createDispatcher">
    /// Builds a transfer's dispatcher from its options, the engine's shared state and the cookie store
    /// its HTTP handler keeps (<see langword="null" /> with the cookie engine off); <see langword="null" />
    /// for the production one.
    /// </param>
    /// <param name="easyErrorText">
    /// Curl's easy error text for each exit code: <c>CurlEasyErrorText.Of</c> (in <c>Libcurl/Misc/ErrorText</c>), which
    /// <c>Curl.Console</c> passes in (BL-2291, BL-2450); <see langword="null" /> for an empty text.
    /// </param>
    /// <param name="readEnvironmentVariable">
    /// Reads the proxy environment variables each handle's own perform consults, as libcurl reads them for every
    /// handle (BL-2519); <see langword="null" /> for <see cref="Environment.GetEnvironmentVariable(string)" />. When given and
    /// it sets <c>CURL_HSTS_HTTP</c>, the engine's HSTS cache learns over plain <c>http</c> too, the harness's stand-in for
    /// the debug build's (upstream test1922, BL-2676, ADR-0494); the process's environment never turns that on.
    /// </param>
    /// <param name="dialing">
    /// What the production dispatcher's transports dial, resolve and open datagram channels through in place of
    /// sockets: the conformance harness's in-memory servers, as <c>Curl.Console</c>'s dialing runner takes them
    /// (ADR-0658); <see langword="null" /> for the real network.
    /// </param>
    /// <param name="signingClock">
    /// The clock the production dispatcher's HTTP handler signs <c>CURLOPT_AWS_SIGV4</c> requests and HTTP signatures with:
    /// the conformance harness's epoch, standing in for the debug build's <c>CURL_FORCETIME</c> (BL-2675, ADR-0671);
    /// <see langword="null" /> for <see cref="TimeProvider.System" />.
    /// </param>
    /// <param name="webSocketRandomSource">
    /// Where every handle's WebSocket key and frame masks come from: the conformance harness's stand-in for the debug
    /// build's <c>CURL_ENTROPY</c> (BL-2688, ADR-0678); <see langword="null" /> for <see cref="SystemWebSocketRandomSource" />.
    /// </param>
    /// <param name="standardInput">
    /// The standard input every handle's <see cref="EasyTransferHost.StandardInput" /> reads, as libcurl reads the process's
    /// <c>stdin</c> for a <c>curl_formadd</c> file named <c>-</c> and a cookie file named <c>-</c>: the conformance harness's
    /// case <c>&lt;stdin&gt;</c> (upstream test650, BL-2625); <see langword="null" /> for <see cref="EasyTransferHost.Library" />'s
    /// empty one.
    /// </param>
    /// <param name="stopsFirstFtpTransferBeforePwd">
    /// Whether the engine's first perform stops its FTP connect phase after the login, before <c>PWD</c>: the conformance
    /// harness's stand-in for the debug build's <c>CURL_FTP_PWD_STOP</c> (upstream test753, BL-2636, ADR-0687);
    /// <see langword="false" /> sends <c>PWD</c> as a release build does.
    /// </param>
    /// <param name="createFormBoundary">
    /// What gives every handle's <c>CURLOPT_MIMEPOST</c> bodies their boundary: the conformance harness's stand-in for the debug
    /// build's boundary under <c>CURL_ENTROPY</c> (upstream test1972, BL-2695); <see langword="null" /> for curl's random one.
    /// </param>
    /// <param name="standardError">
    /// Where every handle's <c>CURLOPT_VERBOSE</c> lines go when it sets neither <c>CURLOPT_DEBUGFUNCTION</c> nor
    /// <c>CURLOPT_STDERR</c> (<see cref="EasyTransferHost.StandardError" />), as libcurl writes them to the process's
    /// <c>stderr</c>: the conformance harness's case stderr (upstream tests 1506 and 1510, BL-2726); <see langword="null" />
    /// for the process's standard error.
    /// </param>
    /// <param name="extensionHandlers">
    /// Builds the handlers a host's extensions add after curl's own in the production dispatcher (ADR-0999);
    /// <see langword="null" /> for curl's alone. Ignored when <paramref name="createDispatcher" /> is given.
    /// </param>
    internal ManagedCurlEngine(
        TimeProvider timeProvider,
        Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, ProtocolDispatcher>? createDispatcher,
        Func<CurlExitCode, string>? easyErrorText = null,
        Func<string, string?>? readEnvironmentVariable = null,
        EngineDialing? dialing = null,
        TimeProvider? signingClock = null,
        IWebSocketRandomSource? webSocketRandomSource = null,
        Stream? standardInput = null,
        bool stopsFirstFtpTransferBeforePwd = false,
        Func<string>? createFormBoundary = null,
        Stream? standardError = null,
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? extensionHandlers = null)
    {
        shared = new ManagedEngineSharedState(timeProvider, easyErrorText ?? (_ => string.Empty), readEnvironmentVariable, dialing, signingClock, webSocketRandomSource, FirstTransferFtpPwdStop.When(stopsFirstFtpTransferBeforePwd), createFormBoundary, readEnvironmentVariable is not null && EngineComposition.LearnsHstsOverHttp(honoursHstsHttpVariable: true, readEnvironmentVariable));
        handleHost = HandleHostOf(standardInput, readEnvironmentVariable, standardError);
        this.createDispatcher = createDispatcher is null
            ? (options, state, cookies, pushSink, rtspSession, webSocketPerformConnection) => CreateDispatcher(options, state, cookies, pushSink, rtspSession, webSocketPerformConnection, extensionHandlers)
            : (options, state, cookies, _, _, _) => createDispatcher(options, state, cookies);
    }

    /// <summary>
    /// The host every handle starts with: <see cref="EasyTransferHost.Library" /> itself, or a copy that reads
    /// <paramref name="standardInput" /> and, when the engine's environment sets <c>CURL_ALTSVC_HTTP</c> (the harness's stand-in for
    /// the debug build's test of it, ADR-0497), serves alt-svc over plain <c>http</c> (upstream test1908, BL-2671).
    /// </summary>
    /// <param name="standardInput">What the handles read for <c>-</c>, or <see langword="null" /> for the library host's.</param>
    /// <param name="readEnvironmentVariable">The engine's environment reader, or <see langword="null" /> for the process's, which the stand-in ignores.</param>
    /// <param name="standardError">Where the handles' default verbose lines go, or <see langword="null" /> for the library host's.</param>
    /// <returns>The host.</returns>
    private static EasyTransferHost HandleHostOf(Stream? standardInput, Func<string, string?>? readEnvironmentVariable, Stream? standardError)
    {
        bool altSvcUsesOverHttp = readEnvironmentVariable is not null && EngineComposition.UsesAltSvcOverHttp(honoursAltSvcHttpVariable: true, readEnvironmentVariable);
        if (standardInput is null && standardError is null && !altSvcUsesOverHttp)
        {
            return EasyTransferHost.Library;
        }

        return EasyTransferHost.Library with
        {
            StandardInput = standardInput ?? EasyTransferHost.Library.StandardInput,
            StandardError = StandardErrorOf(standardError),
            AltSvcUsesOverHttp = altSvcUsesOverHttp,
        };
    }

    /// <summary>The handles' standard error: <paramref name="standardError" />, else <see cref="EasyTransferHost.Library" />'s.</summary>
    /// <param name="standardError">The engine's standard error, or <see langword="null" />.</param>
    /// <returns>The stream, opened when first used.</returns>
    private static Lazy<Stream> StandardErrorOf(Stream? standardError) =>
        standardError is null ? EasyTransferHost.Library.StandardError : new Lazy<Stream>(() => standardError);

    /// <summary>Gets what the engine's handles share.</summary>
    internal ManagedEngineSharedState Shared => shared;

    /// <inheritdoc />
    /// <remarks>The build <c>curl -V</c> describes on the running platform (<see cref="ManagedCurlVersion" />, BL-2465).</remarks>
    public CurlVersionInfo VersionInfo { get; } = ManagedCurlVersion.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    /// <inheritdoc />
    /// <remarks>The handle runs over caches of its own (<see cref="ManagedEngineSharedState.WithOwnCaches" />, BL-3204), as upstream's easy handle does.</remarks>
    public IEasyTransfer CreateEasy() => new ManagedEasyTransfer(createDispatcher, shared.WithOwnCaches()) { Host = handleHost };

    /// <summary>
    /// <c>curl_multi_init</c>: a <see cref="ManagedMultiTransfer" /> whose transfers pool connections, TLS sessions and
    /// resolved names in caches of the multi's own (ADR-0594, BL-3204), as upstream's multi handle does.
    /// </summary>
    /// <returns>The new multi handle; the caller disposes it (<c>curl_multi_cleanup</c>).</returns>
    public IMultiTransfer CreateMulti()
    {
        ManagedEngineSharedState caches = shared.WithOwnCaches();
        ManagedMultiTransfer multi = new(shared.MultiOwners, shared.TimeProvider, SocketWaitPoller.Instance, caches.Connections.CloseIdleAsync, caches.Connections.CloseIdleBeyondAsync, caches.DnsCache.Clear)
        {
            CloseIdleConnectionsToMakeRoom = caches.Connections.CloseIdleToMakeRoomAsync,
        };
        shared.MultiCaches.Add(multi, caches);
        return multi;
    }

    /// <summary><c>curl_share_init</c>: a <see cref="ManagedShareHandle" /> sharing nothing yet, on the engine's clock (ADR-0606).</summary>
    /// <returns>The new share handle; the caller frees it with <see cref="IShareHandle.Cleanup" />.</returns>
    public IShareHandle CreateShare() => new ManagedShareHandle(shared.TimeProvider);

    /// <summary><c>curl_url</c>: a new URL handle holding no part (ADR-0601).</summary>
    /// <returns>A <see cref="ManagedCurlUrlHandle" />; the caller disposes it.</returns>
    public ICurlUrlHandle CreateUrl() => new ManagedCurlUrlHandle();

    /// <summary><c>curl_mime_init</c>: a new MIME handle holding no part (BL-2439).</summary>
    /// <returns>A <see cref="ManagedMimeBuilder" />, whose body <c>CURLOPT_MIMEPOST</c> sends.</returns>
    public IMimeBuilder CreateMime() => new ManagedMimeBuilder();

    /// <summary><c>curl_formadd</c> on a null <c>httppost</c>: a new form holding no field (BL-2440).</summary>
    /// <returns>A <see cref="ManagedFormBuilder" /> whose <c>curl_formget</c> reads files from disk under curl's random boundary.</returns>
    public ICurlFormBuilder CreateForm() => new ManagedFormBuilder(new PhysicalFileSystem(), MultipartBoundary.CreateRandom);

    /// <summary>
    /// Closes every connection the engine's transfers kept open (<c>curl_global_cleanup</c>): those of each library easy
    /// handle's and multi handle's own pool, an FTP control connection with <c>QUIT</c>, then the engine-wide pool's (ADR-0789).
    /// </summary>
    /// <returns>A task that completes once they are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (KeyValuePair<Networking.ConnectionCache, Networking.ConnectionCache> own in shared.OwnConnectionCaches)
        {
            await own.Key.DisposeAsync().ConfigureAwait(false);
        }

        await shared.Connections.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a transfer's dispatcher as <c>Curl.Console</c>'s composition builds an option group's:
    /// <c>EngineComposition.CreateTransports</c>
    /// over the engine's caches, and the handlers over its pooling connector, the HTTP handler keeping
    /// cookies in <paramref name="cookieStore" />. FTP's active-mode listener runs the handle's socket callbacks
    /// (<see cref="SocketCallbackConnectionListener" />, BL-2588).
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <param name="shared">The engine's shared caches.</param>
    /// <param name="cookieStore">The cookies the HTTP handler sends and keeps, or <see langword="null" /> with the cookie engine off.</param>
    /// <param name="pushSink">Where the HTTP handler offers HTTP/2 server pushes (<see cref="EngineHttp2PushSink" />, BL-2469), or <see langword="null" /> to refuse them.</param>
    /// <param name="rtspSession">The handle's RTSP state, whose settings and session the RTSP handler sends (BL-2704), or <see langword="null" /> for the curl tool's <c>OPTIONS *</c>.</param>
    /// <param name="webSocketPerformConnection">The handle's WebSocket perform connection, which <c>curl_ws_send</c> from a write callback writes to (BL-2710), or <see langword="null" /> for none.</param>
    /// <param name="extensionHandlers">Builds the handlers a host's extensions add after curl's own (ADR-0999), or <see langword="null" /> for curl's alone.</param>
    /// <returns>The dispatcher.</returns>
    internal static ProtocolDispatcher CreateDispatcher(EasyOptions options, ManagedEngineSharedState shared, ICookieStore? cookieStore, IHttp2PushSink? pushSink = null, HandleRtspSession? rtspSession = null, WsPerformConnection? webSocketPerformConnection = null, Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? extensionHandlers = null)
    {
        CurlTransports transports = CreateTransports(options, shared);
        IConnector connector = PrerequestConnector.Over(PoolOf(transports, transports.TcpConnector, options.Callbacks, shared), options.Callbacks.Prerequest);
        return new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            connector,
            transports.UdpDatagramConnector,
            transports.TlsProvider,
            transports.DnsResolver,
            cookieStore,
            securityContexts: HandBuiltNtlmContextsOf(connector, transports.UdpDatagramConnector, shared),
            proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes,
            negotiateOptions: NegotiateOptionsMapping.Of(options.Authentication),
            signingClock: shared.SigningClock,
            ftpDataConnector: PoolOf(transports, transports.TcpConnector.ForFtpDataConnections(), options.Callbacks, shared),
            ftpWildcardHooks: CallbackWildcardHooks.Of(options),
            rtspInterleaveSink: CallbackInterleaveSink.Of(options.Callbacks.Interleave),
            rtspRequestSettings: rtspSession is null ? null : () => rtspSession.SettingsFor(options),
            rtspSessionState: rtspSession?.State,
            ftpListener: SocketCallbackConnectionListener.Over(shared.Dialing?.FtpListener ?? new Curl.Networking.TcpConnectionListener(), options.Callbacks, shared),
            webSocketRawMode: IsWebSocketRawMode(options),
            webSocketPerformConnection: webSocketPerformConnection,
            http2PushSink: pushSink,
            webSocketRandomSource: shared.WebSocketRandomSource,
            ftpStopsBeforeEntryPath: shared.FtpPwdStop?.TryClaim() == true,
            socketWait: shared.SocketWait,
            extensionHandlers: extensionHandlers));
    }

    /// <summary>
    /// Gives whether <c>CURLOPT_WS_OPTIONS</c> holds <c>CURLWS_RAW_MODE</c>, under which a WebSocket transfer that
    /// is not connect-only hands the write callback the received stream unframed (BL-2494).
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <returns><see langword="true" /> when <see cref="WebSocketOptionBits.RawMode" /> is set.</returns>
    internal static bool IsWebSocketRawMode(EasyOptions options) =>
        (options.Protocols.WebSocket.Options & WebSocketOptionBits.RawMode) != 0;

    /// <summary>
    /// Builds the connector a <c>CURLOPT_CONNECT_ONLY</c> 1 perform dials through: the <c>TcpConnector</c> of
    /// <see cref="CreateDispatcher" />'s transports, not its pool, so the connection is the handle's own (ADR-0608),
    /// under the handle's socket callbacks (ADR-0610) and its prerequest callback (ADR-0611).
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <param name="shared">The engine's shared caches.</param>
    /// <returns>The connector.</returns>
    internal static IConnector CreateConnectOnlyConnector(EasyOptions options, ManagedEngineSharedState shared) =>
        PrerequestConnector.Over(SocketCallbackConnector.Over(CreateTransports(options, shared).TcpConnector, options.Callbacks, shared), options.Callbacks.Prerequest);

    /// <summary>
    /// Builds the handlers of a <c>CURLOPT_CONNECT_ONLY</c> 1 perform of a scheme whose connect phase sends commands
    /// (BL-2623): an <see cref="FtpConnectOnlyHandler" /> and an <see cref="ImapConnectOnlyHandler" /> whose protocol
    /// handlers dial through <paramref name="connector" /> (<see cref="CreateConnectOnlyConnector" />'s, so the connection
    /// is the handle's own and never pooled), with <see cref="CreateDispatcher" />'s transports' TLS provider and DNS
    /// resolver, IMAP's SASL answering as the engine's IMAP handler does, each giving the open connection to
    /// <paramref name="connectOnly" />.
    /// </summary>
    /// <param name="connector">Dials the control connection.</param>
    /// <param name="options">The transfer's options.</param>
    /// <param name="shared">The engine's shared caches.</param>
    /// <param name="connectOnly">Keeps the connection the connect phase leaves open.</param>
    /// <returns>The handlers.</returns>
    internal static IProtocolHandler[] CreateConnectOnlyLoginHandlers(IConnector connector, EasyOptions options, ManagedEngineSharedState shared, ConnectOnlyConnection connectOnly)
    {
        CurlTransports transports = CreateTransports(options, shared);
        ISecurityContextFactory contexts = EngineComposition.CreateDialingSecurityContextFactory(transports.TcpConnector, transports.UdpDatagramConnector, diagnosticLog: null, UsesHandBuiltNtlm(shared));
        return
        [
            new FtpConnectOnlyHandler(EngineComposition.CreateFtpProtocolHandler(connector, connector, transports.TlsProvider, transports.DnsResolver), connectOnly),
            new ImapConnectOnlyHandler(new ImapProtocolHandler(connector, transports.TlsProvider, EngineComposition.CreateSaslAuthenticator(contexts, NegotiateOptionsMapping.Of(options.Authentication).Delegation)), connectOnly),
        ];
    }

    /// <summary>
    /// Builds the authenticator a <c>CURLOPT_CONNECT_ONLY</c> 2 perform's WebSocket upgrade answers a challenge with, as
    /// <see cref="EngineComposition.CreateProtocolHandlers" /> builds the WebSocket handler's: the ranked schemes over the
    /// security contexts of <see cref="CreateDispatcher" />'s transports, Negotiate as the handle's options say (ADR-0616).
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <param name="shared">The engine's shared caches.</param>
    /// <returns>The authenticator.</returns>
    internal static IHttpAuthenticator CreateWebSocketAuthenticator(EasyOptions options, ManagedEngineSharedState shared)
    {
        CurlTransports transports = CreateTransports(options, shared);
        return EngineComposition.CreateHttpAuthenticator(
            EngineComposition.CreateDialingSecurityContextFactory(transports.TcpConnector, transports.UdpDatagramConnector, diagnosticLog: null, UsesHandBuiltNtlm(shared)),
            NegotiateOptionsMapping.Of(options.Authentication));
    }

    /// <summary>
    /// The security contexts a transfer's handlers answer NTLM, Negotiate and Kerberos with when the engine's
    /// <see cref="EngineDialing.UsesHandBuiltNtlm" /> is set: <see cref="EngineComposition.CreateDialingSecurityContextFactory" />'s,
    /// NTLM going to the hand-built context on every platform, as the dialing runner's do (BL-2635); else
    /// <see langword="null" />, the handlers' own router.
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <param name="shared">The engine's shared state.</param>
    /// <returns>The factory, or <see langword="null" />.</returns>
    internal static ISecurityContextFactory? HandBuiltNtlmContextsOf(IConnector connector, IDatagramConnector datagramConnector, ManagedEngineSharedState shared) =>
        UsesHandBuiltNtlm(shared) ? EngineComposition.CreateDialingSecurityContextFactory(connector, datagramConnector, diagnosticLog: null, usesHandBuiltNtlm: true) : null;

    private static bool UsesHandBuiltNtlm(ManagedEngineSharedState shared) => shared.Dialing is { UsesHandBuiltNtlm: true };

    /// <summary>
    /// The pool a transfer's TCP handlers connect through: the transports' own over their <c>TcpConnector</c>, as
    /// <c>Curl.Console</c>'s composition builds it, or, when the handle sets a socket callback, one over the same cache
    /// that opens connections through a <see cref="SocketCallbackConnector" /> over <paramref name="opener" />, so the
    /// callbacks run once per connection opened and the close-socket callback when the pool closes it (ADR-0610).
    /// </summary>
    /// <param name="transports">The transfer's transports.</param>
    /// <param name="opener">Opens the pool's new connections: the <c>TcpConnector</c>, or its FTP data connector.</param>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="shared">The engine's shared state.</param>
    /// <returns>The pool.</returns>
    internal static IConnector PoolOf(CurlTransports transports, IConnector opener, EasyCallbackOptions callbacks, ManagedEngineSharedState shared)
    {
        IConnector socketCallbacks = SocketCallbackConnector.Over(opener, callbacks, shared);
        return ReferenceEquals(socketCallbacks, transports.TcpConnector) ? transports.PoolingConnector : transports.PoolingConnector.Over(socketCallbacks);
    }

    /// <summary>
    /// Creates a library handle's transports over the engine's caches. <c>CURLOPT_PIPEWAIT</c> stands where the
    /// curl tool's <c>-Z</c> without <c>--parallel-immediate</c> does, so the pool waits for a connection being
    /// opened to the same origin to show it multiplexes rather than opening another, as libcurl's does (BL-3351).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="shared">The engine's shared state.</param>
    /// <returns>The transports.</returns>
    internal static CurlTransports CreateTransports(EasyOptions options, ManagedEngineSharedState shared)
    {
        EngineToolOptions tool = new(Parallel: options.Http.PipeWait, ParallelImmediate: false, SslSessionsFile: null, MaxTime: options.Transfer.Timeout > TimeSpan.Zero ? options.Transfer.Timeout : null, TracesTransfer: options.Behaviour.Verbose);
        return EngineComposition.CreateTransports(tool, options.Network, options.Tls, options.Http, options.Proxy, options.Authentication, shared.TimeProvider, shared.Connections, shared.TlsSessions, diagnosticLog: null, shared.DnsCache, easyErrorText: null, traces: null, resolver => ResolverStartDnsResolver.Over(resolver, options.Callbacks.ResolverStart), shared.Dialing, shared.SocketWait);
    }
}
