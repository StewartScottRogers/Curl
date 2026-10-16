using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

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
    private readonly Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, ProtocolDispatcher> createDispatcher;

    /// <summary>Creates the engine on the system clock and the real network, with no easy error text.</summary>
    public ManagedCurlEngine()
        : this(TimeProvider.System, null)
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
    /// Curl's easy error text for each exit code: <c>Curl.Console</c>'s <c>CurlEasyErrorText.Of</c>, which
    /// stays there (BL-2291); <see langword="null" /> for an empty text.
    /// </param>
    internal ManagedCurlEngine(
        TimeProvider timeProvider,
        Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, ProtocolDispatcher>? createDispatcher,
        Func<CurlExitCode, string>? easyErrorText = null)
    {
        shared = new ManagedEngineSharedState(timeProvider, easyErrorText ?? (_ => string.Empty));
        this.createDispatcher = createDispatcher ?? CreateDispatcher;
    }

    /// <summary>Gets what the engine's handles share.</summary>
    internal ManagedEngineSharedState Shared => shared;

    /// <inheritdoc />
    public CurlVersionInfo VersionInfo { get; } = new()
    {
        Protocols =
        [
            "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ldap", "ldaps",
            "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smb", "smbs", "smtp", "smtps", "telnet", "tftp",
            "ws", "wss",
        ],
    };

    /// <inheritdoc />
    public IEasyTransfer CreateEasy() => new ManagedEasyTransfer(createDispatcher, shared);

    /// <summary>
    /// <c>curl_multi_init</c>: a <see cref="ManagedMultiTransfer" /> whose transfers run over the
    /// engine's shared connections (ADR-0594).
    /// </summary>
    /// <returns>The new multi handle; the caller disposes it (<c>curl_multi_cleanup</c>).</returns>
    public IMultiTransfer CreateMulti() => new ManagedMultiTransfer(shared.MultiOwners, shared.TimeProvider, SocketWaitPoller.Instance, shared.Connections.CloseIdleAsync, shared.DnsCache.Clear);

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

    /// <summary>Closes every connection the engine's transfers kept open (<c>curl_global_cleanup</c>).</summary>
    /// <returns>A task that completes once they are closed.</returns>
    public ValueTask DisposeAsync() => shared.Connections.DisposeAsync();

    /// <summary>
    /// Builds a transfer's dispatcher as <c>Curl.Console</c>'s composition builds an option group's:
    /// <c>EngineComposition.CreateTransports</c>
    /// over the engine's caches, and the handlers over its pooling connector, the HTTP handler keeping
    /// cookies in <paramref name="cookieStore" />.
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <param name="shared">The engine's shared caches.</param>
    /// <param name="cookieStore">The cookies the HTTP handler sends and keeps, or <see langword="null" /> with the cookie engine off.</param>
    /// <returns>The dispatcher.</returns>
    internal static ProtocolDispatcher CreateDispatcher(EasyOptions options, ManagedEngineSharedState shared, ICookieStore? cookieStore)
    {
        CurlTransports transports = CreateTransports(options, shared);
        return new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            PrerequestConnector.Over(PoolOf(transports, transports.TcpConnector, options.Callbacks, shared), options.Callbacks.Prerequest),
            transports.UdpDatagramConnector,
            transports.TlsProvider,
            transports.DnsResolver,
            cookieStore,
            proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes,
            negotiateOptions: NegotiateOptionsMapping.Of(options.Authentication),
            ftpDataConnector: PoolOf(transports, transports.TcpConnector.ForFtpDataConnections(), options.Callbacks, shared)));
    }

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
            EngineComposition.CreateSecurityContextFactory(transports.TcpConnector, transports.UdpDatagramConnector),
            NegotiateOptionsMapping.Of(options.Authentication));
    }

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

    private static CurlTransports CreateTransports(EasyOptions options, ManagedEngineSharedState shared)
    {
        EngineToolOptions tool = new(Parallel: false, ParallelImmediate: false, SslSessionsFile: null, MaxTime: options.Transfer.Timeout > TimeSpan.Zero ? options.Transfer.Timeout : null);
        return EngineComposition.CreateTransports(tool, options.Network, options.Tls, options.Http, options.Proxy, options.Authentication, shared.TimeProvider, shared.Connections, shared.TlsSessions, diagnosticLog: null, shared.DnsCache, easyErrorText: null, traces: null, resolver => ResolverStartDnsResolver.Over(resolver, options.Callbacks.ResolverStart));
    }
}
