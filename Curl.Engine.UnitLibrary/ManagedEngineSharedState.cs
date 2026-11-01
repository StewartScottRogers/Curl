using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Curl.Cookies;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// The caches a transfer runs over - connections, TLS sessions, resolved names, cookies and HSTS - with what every
/// easy handle of one <see cref="ManagedCurlEngine" /> shares: the clock, the multi owners and the
/// <see cref="TransferPerformer" /> that numbers the transfers' <c>%{xfer_id}</c> and holds curl's easy error text.
/// The engine's own state (<see cref="EngineWide" />) holds the caches the curl tool's transfers share across one run
/// (ADR-0542); each library handle and each multi handle runs over caches of its own (<see cref="WithOwnCaches" />,
/// BL-3204), so two handles share none unless a <see cref="ManagedShareHandle" /> links them (ADR-0606).
/// None of it is static, so two engines in one process share nothing.
/// </summary>
internal sealed class ManagedEngineSharedState
{
    /// <summary>Creates an engine's state, every cache its own.</summary>
    /// <param name="timeProvider">The clock the caches, the HSTS cache and the transfers run on.</param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code (<c>CurlEasyErrorText.Of</c> from <c>Libcurl/Misc/ErrorText</c>, passed in by <c>Curl.Console</c>, in production).</param>
    /// <param name="readEnvironmentVariable">
    /// Reads the proxy environment variables every handle's own perform consults (<see cref="ProxySelector" />):
    /// <see cref="Environment.GetEnvironmentVariable(string)" />, the default, in production; a dictionary in tests.
    /// </param>
    /// <param name="dialing">What the transfers dial and resolve through in place of sockets (ADR-0658); <see langword="null" />, in production, for sockets.</param>
    /// <param name="signingClock">The clock <c>CURLOPT_AWS_SIGV4</c> and the HTTP signatures sign with (BL-2675); <see langword="null" />, in production, for <see cref="TimeProvider.System" />.</param>
    /// <param name="webSocketRandomSource">Where every handle's WebSocket key and frame masks come from (BL-2688); <see langword="null" />, in production, for <see cref="SystemWebSocketRandomSource" />.</param>
    /// <param name="ftpPwdStop">The stand-in for the debug build's <c>CURL_FTP_PWD_STOP</c> (BL-2636); <see langword="null" />, in production, for none.</param>
    /// <param name="createFormBoundary">What gives every handle's <c>CURLOPT_MIMEPOST</c> bodies their boundary (BL-2695); <see langword="null" />, in production, for curl's random one.</param>
    /// <param name="learnsHstsOverHttp">Whether the HSTS cache learns <c>Strict-Transport-Security</c> over plain <c>http</c>: the stand-in for the debug build's <c>CURL_HSTS_HTTP</c> (upstream test1922, BL-2676, ADR-0494); <see langword="false" />, in production.</param>
    internal ManagedEngineSharedState(TimeProvider timeProvider, Func<CurlExitCode, string> easyErrorText, Func<string, string?>? readEnvironmentVariable = null, EngineDialing? dialing = null, TimeProvider? signingClock = null, IWebSocketRandomSource? webSocketRandomSource = null, FirstTransferFtpPwdStop? ftpPwdStop = null, Func<string>? createFormBoundary = null, bool learnsHstsOverHttp = false)
    {
        CreateFormBoundary = createFormBoundary;
        Dialing = dialing;
        SigningClock = signingClock;
        WebSocketRandomSource = webSocketRandomSource;
        FtpPwdStop = ftpPwdStop;
        ReadEnvironmentVariable = readEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        ProxySelector = new ProxySelector(ReadEnvironmentVariable);
        TimeProvider = timeProvider;
        Connections = new ConnectionCache(timeProvider);
        TlsSessions = new TlsSessionCache(timeProvider);
        DnsCache = new DnsCache();
        Cookies = new CookieStore();
        this.learnsHstsOverHttp = learnsHstsOverHttp;
        Hsts = new HstsTransferPolicy(timeProvider, learnsOverHttp: learnsHstsOverHttp);
        EngineWide = this;
        MultiCaches = new ConditionalWeakTable<ManagedMultiTransfer, ManagedEngineSharedState>();
        OwnConnectionCaches = new ConditionalWeakTable<ConnectionCache, ConnectionCache>();
        ConnectionIdsByPoolNumber = new ConcurrentDictionary<long, long>();
        MultiOwners = new ConcurrentDictionary<Api.EngineContracts.IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance);
        SocketHandles = new CurlSocketHandles();
        Performer = new TransferPerformer(timeProvider, easyErrorText);
    }

    /// <summary>
    /// Creates the state one perform of a transfer attached to a share handle runs over (<see cref="ManagedShareHandle.Attach" />):
    /// <paramref name="engine" />'s clock, numbering, multi owners and performer, with the caches given.
    /// </summary>
    /// <param name="engine">The transfer's engine's state.</param>
    /// <param name="connections">The pooled connections.</param>
    /// <param name="tlsSessions">The TLS sessions.</param>
    /// <param name="dnsCache">The resolved names.</param>
    /// <param name="cookies">The cookies.</param>
    /// <param name="hsts">The HSTS cache.</param>
    /// <param name="performer">The numbering of the new state's transfers, or <see langword="null" /> for <paramref name="engine" />'s.</param>
    /// <remarks>It dials as <paramref name="engine" /> dials (<see cref="Dialing" />).</remarks>
    internal ManagedEngineSharedState(
        ManagedEngineSharedState engine,
        ConnectionCache connections,
        TlsSessionCache tlsSessions,
        DnsCache dnsCache,
        CookieStore cookies,
        HstsTransferPolicy hsts,
        TransferPerformer? performer = null)
    {
        TimeProvider = engine.TimeProvider;
        Connections = connections;
        TlsSessions = tlsSessions;
        DnsCache = dnsCache;
        Cookies = cookies;
        Hsts = hsts;
        learnsHstsOverHttp = engine.learnsHstsOverHttp;
        EngineWide = engine.EngineWide;
        MultiCaches = engine.MultiCaches;
        OwnConnectionCaches = engine.OwnConnectionCaches;
        ConnectionIdsByPoolNumber = engine.ConnectionIdsByPoolNumber;
        MultiOwners = engine.MultiOwners;
        SocketHandles = engine.SocketHandles;
        Performer = performer ?? engine.Performer;
        ProxySelector = engine.ProxySelector;
        ReadEnvironmentVariable = engine.ReadEnvironmentVariable;
        Dialing = engine.Dialing;
        SigningClock = engine.SigningClock;
        WebSocketRandomSource = engine.WebSocketRandomSource;
        FtpPwdStop = engine.FtpPwdStop;
        CreateFormBoundary = engine.CreateFormBoundary;
    }

    /// <summary>Whether a new HSTS cache learns <c>Strict-Transport-Security</c> over plain <c>http</c>, as the engine's does.</summary>
    private readonly bool learnsHstsOverHttp;

    /// <summary>
    /// Creates a state with the engine's clock, numbering, multi owners and performer and caches of its own, all empty:
    /// what a library easy handle (<c>curl_easy_init</c>, <c>curl_easy_duphandle</c>) or a multi handle runs over, as upstream
    /// gives each its own connection pool, TLS sessions, resolved names, cookies and HSTS (BL-3204). The resolved names are
    /// the handle's own, as libcurl 8.22.0's <c>dnscache_get</c> takes the share's or the handle's multi's, so a new handle
    /// looks a name up again and calls its <c>CURLOPT_RESOLVER_START_FUNCTION</c> (CGR-151, ADR-0851). The engine keeps the
    /// new pool, which its <c>DisposeAsync</c> closes (<see cref="OwnConnectionCaches" />).
    /// </summary>
    /// <returns>The new state.</returns>
    internal ManagedEngineSharedState WithOwnCaches()
    {
        ConnectionCache connections = new(TimeProvider);
        OwnConnectionCaches.Add(connections, connections);
        return new(this, connections, new TlsSessionCache(TimeProvider), new DnsCache(), new CookieStore(), new HstsTransferPolicy(TimeProvider, learnsOverHttp: learnsHstsOverHttp), Performer.StartingAtZero());
    }

    /// <summary>
    /// Creates the state a <c>curl_easy_duphandle</c> copy runs over: caches of its own (<see cref="WithOwnCaches" />) whose
    /// HSTS cache starts with this state's entries, as upstream's duphandle copies the HSTS cache (upstream test1922).
    /// </summary>
    /// <returns>The new state.</returns>
    internal ManagedEngineSharedState DuplicatedWithOwnCaches()
    {
        ManagedEngineSharedState duplicate = EngineWide.WithOwnCaches();
        // No expiry is later than long.MaxValue, so the text is never null (HstsCache.FormatFile).
        duplicate.Hsts.ReadFile(Hsts.FormatFile("\n", long.MaxValue)!);
        return duplicate;
    }

    /// <summary>Gets the pools of the engine's library easy handles and multi handles, which the engine closes when it is disposed (ADR-0789).</summary>
    internal ConditionalWeakTable<ConnectionCache, ConnectionCache> OwnConnectionCaches { get; }

    /// <summary>
    /// Creates the state a handle performing in a multi handle runs over: the multi's connections, TLS sessions and resolved
    /// names, as upstream's multi handle pools them, with this state's cookies and HSTS (BL-3204).
    /// </summary>
    /// <param name="multi">The multi handle's caches (<see cref="MultiCaches" />).</param>
    /// <returns>The new state.</returns>
    internal ManagedEngineSharedState InMulti(ManagedEngineSharedState multi) =>
        new(this, multi.Connections, multi.TlsSessions, multi.DnsCache, Cookies, Hsts);

    /// <summary>Gets the engine's own state, whose caches the curl tool's transfers share across one run (ADR-0542).</summary>
    internal ManagedEngineSharedState EngineWide { get; }

    /// <summary>Gets the caches of each of the engine's multi handles, which the handles performing in it pool (BL-3204).</summary>
    internal ConditionalWeakTable<ManagedMultiTransfer, ManagedEngineSharedState> MultiCaches { get; }

    /// <summary>
    /// Gets what gives every handle's <c>CURLOPT_MIMEPOST</c> bodies their boundary: the conformance harness's stand-in for
    /// the debug build's boundary under <c>CURL_ENTROPY</c> (upstream test1972, BL-2695); <see langword="null" />, in production,
    /// for curl's random one.
    /// </summary>
    internal Func<string>? CreateFormBoundary { get; }

    /// <summary>
    /// Gets where every handle's WebSocket key and frame masks come from, one sequence for the whole engine as the
    /// debug build's <c>CURL_ENTROPY</c> is one for the whole process: the conformance harness's stand-in for it
    /// (BL-2688, ADR-0678); <see langword="null" />, in production, for <see cref="SystemWebSocketRandomSource" />.
    /// </summary>
    internal IWebSocketRandomSource? WebSocketRandomSource { get; }

    /// <summary>
    /// Gets the conformance harness's stand-in for the debug build's <c>CURL_FTP_PWD_STOP</c>, which the engine's first perform
    /// claims so its FTP connect phase stops before <c>PWD</c> (BL-2636, ADR-0687); <see langword="null" />, in production, for none.
    /// </summary>
    internal FirstTransferFtpPwdStop? FtpPwdStop { get; }

    /// <summary>
    /// Gets the clock the engine's transfers sign <c>CURLOPT_AWS_SIGV4</c> requests and HTTP signatures with: the
    /// conformance harness's stand-in for the debug build's <c>CURL_FORCETIME</c> (BL-2675); <see langword="null" />, in
    /// production, for <see cref="TimeProvider.System" />.
    /// </summary>
    internal TimeProvider? SigningClock { get; }

    /// <summary>
    /// Gets what chooses the proxy of every handle's own perform from <c>CURLOPT_PROXY</c>, <c>CURLOPT_NOPROXY</c>
    /// and the proxy environment variables (<c>http_proxy</c>, <c>HTTPS_PROXY</c>, <c>ALL_PROXY</c>, <c>NO_PROXY</c>
    /// and the rest), as libcurl reads them for every handle (BL-2519, ADR-0608).
    /// </summary>
    internal ProxySelector ProxySelector { get; }

    /// <summary>
    /// Gets what reads an environment variable for every handle's own perform: the proxy variables
    /// (<see cref="ProxySelector" />) and the <c>NETRC</c> and <c>HOME</c> variables the <c>.netrc</c> lookup reads.
    /// </summary>
    internal Func<string, string?> ReadEnvironmentVariable { get; }

    /// <summary>
    /// Gets what the engine's transfers dial and resolve through in place of sockets: the conformance harness's in-memory
    /// servers (ADR-0658); <see langword="null" /> in production.
    /// </summary>
    internal EngineDialing? Dialing { get; }

    /// <summary>Gets the clock the engine's transfers run on.</summary>
    internal TimeProvider TimeProvider { get; }

    /// <summary>Gets the pooled connections the transfers running over this state connect through.</summary>
    internal ConnectionCache Connections { get; }

    /// <summary>Gets the TLS sessions the handshakes running over this state resume.</summary>
    internal TlsSessionCache TlsSessions { get; }

    /// <summary>Gets the resolved names the transfers running over this state look up first.</summary>
    internal DnsCache DnsCache { get; }

    /// <summary>Gets the cookies the transfers running over this state send and keep with the cookie engine on.</summary>
    internal CookieStore Cookies { get; }

    /// <summary>Gets the HSTS cache the transfers running over this state learn from and switch by.</summary>
    internal HstsTransferPolicy Hsts { get; }

    /// <summary>Gets the <c>%{conn_id}</c> of each connection a handle's transfer opened, by the pool's number for it, which a later transfer reusing it takes (<see cref="EngineConnectionIdTransferEvents" />).</summary>
    internal ConcurrentDictionary<long, long> ConnectionIdsByPoolNumber { get; }

    /// <summary>Gets the multi handle each transfer added to one of the engine's multi handles is in, so it is in one at a time (<see cref="ManagedMultiTransfer" />).</summary>
    internal ConcurrentDictionary<Api.EngineContracts.IEasyTransfer, ManagedMultiTransfer> MultiOwners { get; }

    /// <summary>Gets the opaque socket handles the engine gives its connections when no open-socket callback made one (<see cref="SocketCallbackConnector" />).</summary>
    internal CurlSocketHandles SocketHandles { get; }

    /// <summary>Gets the performer that numbers the engine's transfers and connections and holds curl's easy error text.</summary>
    internal TransferPerformer Performer { get; }

    /// <summary>
    /// Gets what the transfer running over this state waits for on its socket while a multi handle drives it by
    /// <c>curl_multi_socket_action</c>, which its dialer gates each dialed connection on
    /// (<see cref="SocketActionGatedTcpDialer" />, ADR-0917); <see langword="null" /> outside socket-action mode.
    /// </summary>
    internal TransferSocketWait? SocketWait { get; private init; }

    /// <summary>
    /// Gives this state with <paramref name="wait" /> as its <see cref="SocketWait" />, or this state itself when
    /// <paramref name="wait" /> is <see langword="null" />.
    /// </summary>
    /// <param name="wait">What the transfer waits for on its socket, or <see langword="null" />.</param>
    /// <returns>The state the transfer runs over.</returns>
    internal ManagedEngineSharedState WithSocketWait(TransferSocketWait? wait) =>
        wait is null ? this : new(this, Connections, TlsSessions, DnsCache, Cookies, Hsts) { SocketWait = wait };
}
