using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Core;
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
/// <see cref="ManagedCurlUrlHandle" />; the MIME and form handles are phase 3 work (ADR-0506) and throw
/// <see cref="NotSupportedException" /> here.
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

    /// <summary>Not supported yet: the form API is phase 3's MIME API work (ADR-0506).</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ICurlFormBuilder CreateForm() => throw new NotSupportedException("The form API is phase 3 work (ADR-0506).");

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
            transports.PoolingConnector,
            transports.UdpDatagramConnector,
            transports.TlsProvider,
            transports.DnsResolver,
            cookieStore,
            proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes,
            negotiateOptions: NegotiateOptionsMapping.Of(options.Authentication),
            ftpDataConnector: EngineComposition.FtpDataConnectorOf(transports)));
    }

    /// <summary>
    /// Builds the connector a <c>CURLOPT_CONNECT_ONLY</c> 1 perform dials through: the <c>TcpConnector</c> of
    /// <see cref="CreateDispatcher" />'s transports, not its pool, so the connection is the handle's own (ADR-0608).
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <param name="shared">The engine's shared caches.</param>
    /// <returns>The connector.</returns>
    internal static IConnector CreateConnectOnlyConnector(EasyOptions options, ManagedEngineSharedState shared) =>
        CreateTransports(options, shared).TcpConnector;

    private static CurlTransports CreateTransports(EasyOptions options, ManagedEngineSharedState shared)
    {
        EngineToolOptions tool = new(Parallel: false, ParallelImmediate: false, SslSessionsFile: null, MaxTime: options.Transfer.Timeout > TimeSpan.Zero ? options.Transfer.Timeout : null);
        return EngineComposition.CreateTransports(tool, options.Network, options.Tls, options.Http, options.Proxy, options.Authentication, shared.TimeProvider, shared.Connections, shared.TlsSessions, runDnsCache: shared.DnsCache);
    }
}
