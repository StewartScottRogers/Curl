using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// One easy handle of a <see cref="ManagedCurlEngine" /> (ADR-0506, ADR-0541). <see cref="PerformAsync" />
/// runs one transfer from <see cref="Options" />: a dispatcher built from them, a context
/// <see cref="TransferContextFactory" /> builds, and the attempt <see cref="TransferPerformer.FollowWatchingSpeedAsync" />
/// performs through a <see cref="RedirectFollower" />. Body bytes go to the write callback and header
/// lines to the header callback, each discarded when unset; writing to standard output when no write
/// callback is set is <c>Libcurl</c>'s job, not the engine's. An upload (<see cref="Api.Options.Behaviour.EasyTransferOptions.Upload" />)
/// reads the read callback through <see cref="CallbackReadStream" />. Under <c>CURLOPT_VERBOSE</c> every
/// transfer event goes to the debug callback (<see cref="DebugCallbackTransferEvents" />), and without
/// <c>CURLOPT_NOPROGRESS</c> the progress goes to the xferinfo or progress callback
/// (<see cref="CallbackTransferProgress" />), whose abort ends the transfer with
/// <see cref="CurlCode.AbortedByCallback" /> (ADR-0543). Cookies, the HSTS cache, pooled
/// connections and the <c>%{xfer_id}</c> and <c>%{conn_id}</c> numbering are the engine's (<see cref="ManagedEngineSharedState" />),
/// shared with its other handles (ADR-0542), and a perform attached to a <see cref="ManagedShareHandle" /> through
/// <c>CURLOPT_SHARE</c> uses the share's caches for the kinds it shares (ADR-0606); while a transfer performs, <see cref="Info" /> holds its
/// <c>%{xfer_id}</c> from the start and its <c>%{conn_id}</c> once it connects (-1 before), for the debug
/// callback's <c>--trace-ids</c> prefix (ADR-0547). The certificate verify codes the transfer reports
/// reach <see cref="Info" /> through <see cref="TlsVerifyResultRecordingTransferEvents" /> (BL-2366). Around the attempt it does the runner's setup and cleanup:
/// the <c>-b</c> files, <c>--alt-svc</c> and <c>--hsts</c> files read before it and the <c>-c</c> jar,
/// alt-svc and HSTS files written after it, through <see cref="Host" /> (ADR-0544); <c>CURLOPT_HSTSREADFUNCTION</c> is
/// asked for entries before each perform and <c>CURLOPT_HSTSWRITEFUNCTION</c> given them at cleanup (ADR-0620). <see cref="Headers" /> keeps the
/// headers of the last perform's HTTP responses (ADR-0603), and <see cref="Connection" /> the connection a
/// <c>CURLOPT_CONNECT_ONLY</c> 1 perform left open (ADR-0608), and <see cref="WebSocket" /> the connection a
/// <c>CURLOPT_CONNECT_ONLY</c> 2 perform of a <c>ws</c> or <c>wss</c> URL upgraded (ADR-0616).
/// </summary>
/// <param name="createDispatcher">Builds a transfer's dispatcher from its options, the engine's shared state, the cookie store its HTTP handler keeps and the HTTP/2 push sink it offers server pushes to (<see cref="PushSinkOf" />).</param>
/// <param name="shared">What the engine's handles share.</param>
internal sealed partial class ManagedEasyTransfer(
    Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, IHttp2PushSink?, HandleRtspSession, WsPerformConnection, ProtocolDispatcher> createDispatcher,
    ManagedEngineSharedState shared) : IToolEasyTransfer
{
    /// <summary>libcurl's <c>CURLE_URL_MALFORMAT</c> text for a perform with no URL set.</summary>
    internal const string NoUrlSetText = "No URL set";

    /// <summary>The RTSP <c>CSeq</c> counter and session the handle keeps from perform to perform (BL-2704).</summary>
    private readonly HandleRtspSession rtspSession = new();


    /// <summary>
    /// Gets or sets every option of the handle (<c>curl_easy_setopt</c>). A <c>CURLOPT_SHARE</c> naming another
    /// <see cref="ManagedShareHandle" /> detaches the handle from the one before and attaches it to the new one at once,
    /// and each <c>CURLOPT_COOKIELIST</c> item added takes the attached share's cookie lock, as libcurl does (upstream test506).
    /// </summary>
    public EasyOptions Options
    {
        get => options;
        set
        {
            EasyOptions previous = options;
            options = value;
            optionShare = ManagedShareHandle.Reattach(optionShare, value.Behaviour.Share as ManagedShareHandle, this);
            TakeCookieListItems(previous.Http.CookieList.Count, value.Http);
        }
    }

    /// <summary>The handle's options, which <see cref="Options" /> gives.</summary>
    private EasyOptions options = new();

    /// <summary>The share handle <c>CURLOPT_SHARE</c> attached the handle to, or <see langword="null" /> for none.</summary>
    private ManagedShareHandle? optionShare;

    /// <summary>
    /// How many of the first <c>CURLOPT_COOKIELIST</c> items were applied to a share handle's cookies as they were set
    /// (<see cref="TakeCookieListItems" />), so a perform applies only the rest.
    /// </summary>
    private int cookieListItemsApplied;

    /// <summary>
    /// Gets what the last perform found out (<c>curl_easy_getinfo</c>). While the handle is attached to a share handle
    /// that shares cookies, <see cref="TransferInfo.CookieList" /> is the share's cookies, read under one cookie lock
    /// pair when the list is first read (<see cref="ShareCookieListLines" />), as curl 8.22.0's <c>CURLINFO_COOKIELIST</c>
    /// reads them (upstream test506).
    /// </summary>
    public TransferInfo Info
    {
        get => WithConnectOnlySocket(optionShare is { SharesCookies: true } share ? field with { CookieList = new ShareCookieListLines(share, this) } : field);
        private set;
    } = new();

    /// <summary>
    /// Gives <paramref name="info" /> the socket number of the connection a <c>CURLOPT_CONNECT_ONLY</c> 1 perform left
    /// open as <see cref="TransferInfo.ActiveSocket" /> and <see cref="TransferInfo.LastSocket" />, as libcurl answers its
    /// socket, which a caller passes to <c>curl_multi_wait</c> (<see cref="ConnectOnlyConnection.LiveSocket" />,
    /// upstream test677, BL-2633), and -1 (<c>CURL_SOCKET_BAD</c>) for both once it is closed or the options no longer
    /// ask for connect-only, as for every perform that is not connect-only; before the first perform <paramref name="info" />
    /// is unchanged.
    /// </summary>
    /// <param name="info">What the last perform found out.</param>
    /// <returns><paramref name="info" />, with the connect-only socket.</returns>
    private TransferInfo WithConnectOnlySocket(TransferInfo info) =>
        connectOnly is { LiveSocket.Handle: var socket }
            ? info with { ActiveSocket = socket, LastSocket = socket }
            : info;

    /// <summary>
    /// The events of <paramref name="events" /> the connect-only connection numbered <paramref name="socket" /> is
    /// ready for, for a <c>curl_multi_wait</c> given that socket (<see cref="ConnectOnlyConnection.ReadyEvents" />);
    /// <see langword="null" /> when <paramref name="socket" /> is not this handle's open connect-only connection.
    /// </summary>
    /// <param name="socket">The socket the caller waits on.</param>
    /// <param name="events">The events the caller waits for.</param>
    /// <returns>The events that happened, or <see langword="null" /> for another socket.</returns>
    internal CurlWaitEvents? ConnectOnlyReadyEvents(CurlSocket socket, CurlWaitEvents events) =>
        connectOnly is { } open && socket != CurlSocket.Bad && open.LiveSocket == socket ? open.ReadyEvents(events) : null;

    /// <summary>
    /// Takes the <c>CURLOPT_COOKIELIST</c> items added since <paramref name="previousCount" />: while the handle is
    /// attached to a share handle that shares cookies and every earlier item was applied, applies each to the share's
    /// cookies at once under its own cookie lock pair, as curl 8.22.0's <c>lib/setopt.c</c> does (upstream test506);
    /// else takes the share's cookie lock once for each and leaves them to the next perform.
    /// </summary>
    /// <param name="previousCount">How many items the options held before.</param>
    /// <param name="http">The handle's HTTP options now.</param>
    private void TakeCookieListItems(int previousCount, Api.Options.Http.EasyHttpOptions http)
    {
        if (optionShare is { SharesCookies: true } share && cookieListItemsApplied == previousCount)
        {
            share.ApplyCookieListItems(this, http.CookieList.Skip(previousCount), (cookies, item) => ApplyCookieListItemNow(http, cookies, item));
            cookieListItemsApplied = http.CookieList.Count;
            return;
        }

        optionShare?.LockCookies(this, http.CookieList.Count - previousCount);
        cookieListItemsApplied = Math.Min(cookieListItemsApplied, http.CookieList.Count);
    }

    /// <summary>
    /// Applies one <c>CURLOPT_COOKIELIST</c> item to a share handle's cookies as it is set, waiting for a <c>RELOAD</c>'s
    /// files or a <c>FLUSH</c>'s jar, as <c>curl_easy_setopt</c> is synchronous.
    /// </summary>
    /// <param name="http">The handle's HTTP options.</param>
    /// <param name="cookies">The share handle's cookies.</param>
    /// <param name="item">The item.</param>
    private void ApplyCookieListItemNow(Api.Options.Http.EasyHttpOptions http, Curl.Cookies.CookieStore cookies, string item)
    {
        EasyTransferHost host = Host;
        CookieEngine.OverSharedCookies(http, cookies)
            .ApplyCookieListItemAsync(item, host.FileSystem, host.StandardInput, host.JarPrintTarget, shared.TimeProvider.GetUtcNow(), NoTransferEvents.Instance)
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>The caches the share handle the running perform is attached to gives (ADR-0606), or <see langword="null" /> for none.</summary>
    private ManagedEngineSharedState? attachedShared;

    /// <summary>
    /// Gets the caches the running perform uses: those its share handle gives, else the engine's for the curl tool's
    /// transfers (<see cref="ToolSettings" />, ADR-0542), else the multi's connections, TLS sessions and resolved names with
    /// the handle's own cookies and HSTS while it performs in a multi handle, else the handle's own (BL-3204).
    /// </summary>
    private ManagedEngineSharedState PerformShared => (attachedShared ?? (ToolSettings is not null ? shared.EngineWide : MultiCachesOf() ?? shared)).WithSocketWait(SocketWait);

    /// <summary>
    /// Gets or sets what the handle's transfers wait for on their socket while a multi handle drives them by
    /// <c>curl_multi_socket_action</c>: each perform starts its clock (<see cref="TransferSocketWait.Start" />) and
    /// dials through a <see cref="SocketActionGatedTcpDialer" /> over it (ADR-0917, BL-3859); <see langword="null" />,
    /// the default, outside socket-action mode, where nothing changes.
    /// </summary>
    internal TransferSocketWait? SocketWait { get; set; }

    /// <summary>Starts the perform's clock on <see cref="SocketWait" />, when the handle has one.</summary>
    /// <param name="options">The perform's options.</param>
    private void StartSocketWait(EasyOptions options) => SocketWait?.Start(options);

    /// <summary>The caches the handle runs over in the multi handle it is in, or <see langword="null" /> outside one.</summary>
    /// <returns>The handle's state over the multi's caches, or <see langword="null" />.</returns>
    private ManagedEngineSharedState? MultiCachesOf() =>
        shared.MultiOwners.TryGetValue(this, out ManagedMultiTransfer? multi) && shared.MultiCaches.TryGetValue(multi, out ManagedEngineSharedState? caches)
            ? shared.InMulti(caches)
            : null;

    /// <summary>The share handle the running perform is attached to, or <see langword="null" /> for none.</summary>
    private ManagedShareHandle? performShare;

    /// <summary>The handle's pause state, which <see cref="Pause" /> sets and a pausing callback waits on (ADR-0573).</summary>
    private readonly EasyPauseGate pauseGate = new();

    /// <summary>The directions left paused, by <see cref="Pause" /> or by a callback that returned a pause.</summary>
    internal CurlPause PausedDirections => pauseGate.Directions;

    /// <summary>
    /// Gets or sets the curl tool's own settings for the handle's transfers - <c>-s</c>, <c>-S</c>,
    /// <c>--fail-with-body</c> and the rest that <see cref="TransferSettingsOf" /> can only default - for
    /// <c>Curl.Console</c> to set from its command line once the handle builds the runner's serial attempts from the options (BL-2394, ADR-0542); <see langword="null" />, as for every
    /// <c>Curl.Api</c> and <c>Libcurl</c> handle, to take them from <see cref="Options" />.
    /// </summary>
    public EngineTransferSettings? ToolSettings { get; set; }

    /// <summary>
    /// Gets or sets what the handle's transfers reach outside the engine for their setup and cleanup -
    /// the file system the <c>-b</c>, <c>-c</c>, <c>--alt-svc</c> and <c>--hsts</c> files go through,
    /// standard input for <c>-b -</c>, the <c>-c -</c> print target and the diagnostic log (ADR-0544) -
    /// for <c>Curl.Console</c> to set from its runner once the handle builds its serial attempts (BL-2394); <see cref="EasyTransferHost.Library" />,
    /// as for every <c>Curl.Api</c> and <c>Libcurl</c> handle, by default. <see cref="Duplicate" /> copies it
    /// and <see cref="Reset" /> leaves it, as it is the host's wiring, not an option.
    /// </summary>
    public EasyTransferHost Host { get; set; } = EasyTransferHost.Library;

    /// <summary>
    /// Gets or sets the run's <c>--trace-config</c> components, libcurl's <c>curl_global_trace</c> setting,
    /// which add the <c>[READ]</c>, <c>[WRITE]</c> and <c>[TIMER]</c> lines to the debug callback's events
    /// (<see cref="WithTraceComponentEvents" />), for <c>Curl.Console</c> to set from its command line (BL-2394,
    /// ADR-0545); <see cref="EngineTraceComponents.None" /> by default. <see cref="Duplicate" /> copies it and
    /// <see cref="Reset" /> leaves it, as it is a global setting, not an option.
    /// </summary>
    internal EngineTraceComponents TraceComponents { get; set; } = EngineTraceComponents.None;

    /// <summary>
    /// Gets or sets what gives each <c>CURLOPT_MIMEPOST</c> body its boundary: the engine's
    /// <see cref="ManagedEngineSharedState.CreateFormBoundary" /> when it has one (BL-2695), else <see cref="MultipartBoundary.CreateRandom" />,
    /// curl's random one; tests set a fixed one to compare bodies. <see cref="Duplicate" /> copies it and
    /// <see cref="Reset" /> leaves it, as it is not an option.
    /// </summary>
    internal Func<string> CreateFormBoundary { get; set; } = shared.CreateFormBoundary ?? MultipartBoundary.CreateRandom;

    /// <summary>
    /// Gets or sets the pieces of each transfer the curl tool's runner has already chosen before connecting -
    /// the proxy and looked-up credentials, the SSH options, the <c>--etag-compare</c> lines, the <c>-r</c> range,
    /// the <c>-C</c> offset, the <c>-F</c> body, the <c>-T</c> source and the alt-svc cache - for <c>Curl.Console</c>
    /// to set per transfer once its runner performs from the options (BL-2394, ADR-0554). While set, a perform
    /// from the options puts them on the context as they are and leaves the cookie, alt-svc and HSTS files to the
    /// runner, which reads and writes them itself; <see langword="null" />, as for every <c>Curl.Api</c> and
    /// <c>Libcurl</c> handle, to choose them from <see cref="Options" /> and do that file setup and cleanup here.
    /// <see cref="Duplicate" /> does not copy it and <see cref="Reset" /> clears it, as it belongs to one transfer.
    /// </summary>
    public EngineToolTransferInputs? ToolInputs { get; set; }

    /// <summary>
    /// Gets or sets where each transfer writes its body and header bytes as the curl tool's runner built them -
    /// its <c>--limit-rate</c>, <c>-N</c>, <c>--use-ascii</c> and standard-output or file chain, the <c>-D</c>
    /// stream, the <c>-J</c> header watcher and the <c>--styled-output</c> styles - for <c>Curl.Console</c> to set
    /// per transfer once its runner performs from the options (BL-2394, ADR-0558). While set, a perform from the
    /// options writes there in place of <see cref="EasyCallbackOptions.Write" /> and
    /// <see cref="EasyCallbackOptions.Header" />; <see langword="null" />, as for every <c>Curl.Api</c> and
    /// <c>Libcurl</c> handle, to write to those callbacks. <see cref="Duplicate" /> does not copy it and
    /// <see cref="Reset" /> clears it, as it belongs to one transfer.
    /// </summary>
    public EngineToolTransferOutputs? ToolOutputs { get; set; }

    /// <summary>
    /// Gets or sets the curl tool's runner's events for each transfer and the running transfer they keep - its
    /// <c>-v</c>, <c>--trace</c>, <c>--trace-ascii</c>, <c>--trace-ids</c> and <c>--trace-config</c> chain and its
    /// <c>%{conn_id}</c> recorder - for <c>Curl.Console</c> to set per transfer once its runner performs from the
    /// options (BL-2394, ADR-0561). While set, a perform from the options reports every event there alone, in place
    /// of <see cref="EasyCallbackOptions.Debug" /> and <see cref="TraceComponents" />' lines, runs on its
    /// <see cref="EngineToolTransferEvents.State" /> and takes the <c>%{conn_id}</c> the runner's recorder kept there,
    /// numbering none itself; <see langword="null" />, as for every <c>Curl.Api</c> and <c>Libcurl</c> handle, to
    /// report to the debug callback and number <c>%{conn_id}</c> through <see cref="EngineConnectionIdTransferEvents" />.
    /// <see cref="Duplicate" /> does not copy it and <see cref="Reset" /> clears it, as it belongs to one transfer.
    /// </summary>
    public EngineToolTransferEvents? ToolEvents { get; set; }

    /// <summary>
    /// Gets or sets the curl tool's runner's progress meter for each transfer and its <c>-Z</c> <c>--fail-early</c>
    /// abort token, for <c>Curl.Console</c> to set per transfer once its runner performs from the options (BL-2394,
    /// ADR-0567). While set, a perform from the options reports its progress there in place of
    /// <see cref="EasyCallbackOptions.TransferInfo" /> and <see cref="EasyCallbackOptions.Progress" />, stops on its
    /// token too, and starts fresh <c>-Y</c>/<c>-y</c> and <c>-m</c> watchdogs from
    /// <see cref="Api.Options.Behaviour.EasyTransferOptions" /> on the running transfer, writing to
    /// <see cref="Host" />'s diagnostic log, as the runner does for each attempt; <see langword="null" />, as for every
    /// <c>Curl.Api</c> and <c>Libcurl</c> handle, to report to those callbacks and start no watchdog.
    /// <see cref="Duplicate" /> does not copy it and <see cref="Reset" /> clears it, as it belongs to one transfer.
    /// </summary>
    public EngineToolTransferProgress? ToolProgress { get; set; }

    /// <summary>
    /// Gets or sets the curl tool's runner's follower and context factory for each transfer - its option group's
    /// <c>TransferDispatch</c> behind a <see cref="RedirectFollower" />, and its own <see cref="TransferContextFactory" />
    /// with standard input, the platform's credential encoding, the run's diagnostic log and HSTS store - for
    /// <c>Curl.Console</c> to set per transfer once its runner performs from the options (BL-2394, BL-2479, ADR-0614).
    /// While set, a perform from the options runs through that follower and builds its context with that factory, in
    /// place of the dispatcher the engine's factory builds and a factory with no standard input; <see langword="null" />,
    /// as for every <c>Curl.Api</c> and <c>Libcurl</c> handle, to build both itself. <see cref="Duplicate" /> does not
    /// copy it and <see cref="Reset" /> clears it, as it belongs to one transfer.
    /// </summary>
    public EngineToolTransferDispatch? ToolDispatch { get; set; }

    /// <summary>
    /// Gets the context the last perform from the options built, for the curl tool's <c>--retry</c> and
    /// <c>%{conn_id}</c> reuse, which read its parsed <see cref="TransferContext.Url" /> (BL-2479); <see langword="null" />
    /// before the first perform and after one that failed before building it (no URL set, one that does not parse, a
    /// connect-only perform or a form that could not be built).
    /// </summary>
    internal TransferContext? LastContext { get; private set; }

    /// <summary>
    /// Gets the whole result of the last perform, for the curl tool's <c>--retry</c>, failure lines and <c>-w</c>,
    /// which read more of it than <see cref="Info" /> carries (ADR-0554); <see langword="null" /> before the first
    /// perform, after one that failed before connecting (no URL set, or one that does not parse), and after one
    /// the progress callback aborted.
    /// </summary>
    public TransferResult? LastResult { get; private set; }

    /// <summary>The <c>%{conn_id}</c> of a transfer that failed before it could connect.</summary>
    internal const long NoConnectionId = -1;

    /// <summary>The headers the last perform from the options received over HTTP, for the header API (ADR-0603).</summary>
    private readonly TransferHeaderStore headers = new();

    /// <summary>
    /// The <c>WWW-Authenticate</c> values of the 401 that ended the last perform, which the next
    /// perform's first request answers when Digest is then the one scheme allowed, as libcurl
    /// 8.22.0 keeps its Digest state on the easy handle (BL-2747, ADR-0692); empty after any other
    /// outcome and after <see cref="Reset" />.
    /// </summary>
    private IReadOnlyList<string> keptDigestChallenges = [];

    /// <summary>
    /// The scheme the last perform's server picked (<see cref="TransferReport.AuthSchemePicked" />), which the next
    /// perform starts from, as libcurl 8.22.0 keeps <c>data->state.authhost.picked</c> on the easy handle and
    /// <c>Curl_pretransfer</c> resets only <c>want</c> (upstream test694, BL-2709); kept through a perform that ends
    /// with no result, and <see cref="HttpAuthSchemes.None" /> before any perform and after <see cref="Reset" />.
    /// </summary>
    private HttpAuthSchemes keptAuthSchemePicked;

    /// <summary>The alt-svc cache the handle's first perform opened, kept across performs and <see cref="Reset" /> and saved by <see cref="DisposeAsync" /> (BL-2671).</summary>
    private AltSvcTransferCache? handleAltSvc;

    /// <summary>The cookies of the last HTTP perform that had a <c>CURLOPT_COOKIEJAR</c>, which <see cref="DisposeAsync" /> writes, as <c>curl_easy_cleanup</c> does (CGR-258, BL-3787); <see langword="null" /> before one.</summary>
    private CookieEngine? handleCookieJar;

    /// <summary>
    /// The <c>host:port</c> of the last perform's URL (<see cref="OriginOf" />), to which alone
    /// <see cref="keptDigestChallenges" /> are answered, as curl 8.22.0 sends no kept Digest answer to another
    /// host (upstream test1985); <see langword="null" /> before any perform.
    /// </summary>
    private string? keptDigestOrigin;

    /// <summary>
    /// Whether the last perform ended with Digest still marked available (<see cref="TransferReport.DigestStaysAvailable" />),
    /// which the next perform to the same origin carries beside <see cref="keptDigestChallenges" />, as libcurl 8.22.0 keeps
    /// <c>authhost.avail</c> on the easy handle (upstream test2030, BL-2820); <see langword="false" /> after <see cref="Reset" />.
    /// </summary>
    private bool keptDigestStaysAvailable;

    /// <summary>Whether a perform of <paramref name="url" /> carries <see cref="keptDigestStaysAvailable" />: only to the origin it came from.</summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns><see langword="true" /> when Digest stays marked available for the perform.</returns>
    private bool KeptDigestStaysAvailableFor(CurlUrl url) => keptDigestStaysAvailable && OriginOf(url) == keptDigestOrigin;

    /// <summary>The <c>host:port</c> the kept Digest challenges are answered to.</summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The host and port.</returns>
    private static string OriginOf(CurlUrl url) => $"{url.Host}:{url.Port}";

    /// <summary>The engine clock's timestamp when the running perform started, which the live times on <see cref="Info" /> count from.</summary>
    private long performStartedTimestamp;

    /// <summary>
    /// The engine clock's timestamp when the running perform's handle took a connection (<see cref="ShowConnectionOnInfo" />),
    /// or <see langword="null" /> before it has one.
    /// </summary>
    private long? connectedTimestamp;

    /// <summary>
    /// The header API over the headers the last perform from <see cref="Options" /> received over <c>http</c>,
    /// <c>https</c>, <c>ws</c> or <c>wss</c>: every perform and <see cref="Reset" /> forget them, and a perform
    /// writing to the curl tool's <see cref="ToolOutputs" /> records none (ADR-0603).
    /// </summary>
    public ITransferHeaders Headers => headers;

    /// <summary>
    /// <c>curl_ws_recv</c>, <c>curl_ws_send</c> and <c>curl_ws_meta</c> on the connection the last perform with
    /// <c>CURLOPT_CONNECT_ONLY</c> 2 upgraded (<see cref="ManagedEasyWebSocket" />, ADR-0616); every later perform,
    /// and cleanup, closes it.
    /// </summary>
    public IEasyWebSocket WebSocket => WebSocketConnection;

    /// <summary>The connection a WebSocket connect-only perform upgraded, for <see cref="WebSocket" />.</summary>
    private ManagedEasyWebSocket? webSocket;

    private ManagedEasyWebSocket WebSocketConnection => webSocket ??= new ManagedEasyWebSocket(
        () => Options.Network.ConnectOnly != Api.Options.Network.ConnectOnly.Disabled,
        () => Options.Protocols.WebSocket.Options,
        WebSocketRandomSource);

    /// <summary>
    /// Gets or sets where the WebSocket API's frame masks and a WebSocket connect-only perform's
    /// <c>Sec-WebSocket-Key</c> come from: the engine's <see cref="ManagedEngineSharedState.WebSocketRandomSource" /> when it has
    /// one (BL-2688), else <see cref="SystemWebSocketRandomSource" />; tests fix it.
    /// <see cref="Duplicate" /> copies it and <see cref="Reset" /> leaves it, as it is not an option.
    /// </summary>
    internal IWebSocketRandomSource WebSocketRandomSource { get; set; } = shared.WebSocketRandomSource ?? new SystemWebSocketRandomSource();

    /// <summary>The connection a connect-only perform left open, for <see cref="Connection" />.</summary>
    private ConnectOnlyConnection? connectOnly;

    /// <summary>
    /// Gets or sets what builds the connector a <c>CURLOPT_CONNECT_ONLY</c> 1 perform dials through:
    /// <see cref="ManagedCurlEngine.CreateConnectOnlyConnector" /> by default; tests put a fake connector there.
    /// <see cref="Duplicate" /> copies it and <see cref="Reset" /> leaves it, as it is not an option.
    /// </summary>
    internal Func<EasyOptions, ManagedEngineSharedState, IConnector> CreateConnectOnlyConnector { get; set; } = ManagedCurlEngine.CreateConnectOnlyConnector;

    /// <summary>
    /// <c>curl_easy_recv</c> and <c>curl_easy_send</c> on the connection the last perform with
    /// <c>CURLOPT_CONNECT_ONLY</c> 1 left open (<see cref="ConnectOnlyConnection" />, ADR-0608); every later perform,
    /// and cleanup, closes it.
    /// </summary>
    public IConnectOnlyConnection Connection => ConnectOnly;

    private ConnectOnlyConnection ConnectOnly => connectOnly ??= new ConnectOnlyConnection(() => Options.Network.ConnectOnly != Api.Options.Network.ConnectOnly.Disabled);
}
