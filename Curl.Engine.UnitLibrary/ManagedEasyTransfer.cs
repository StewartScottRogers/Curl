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
using Curl.Protocol.Http;
using Curl.Protocol.Ws;

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
internal sealed class ManagedEasyTransfer(
    Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, IHttp2PushSink?, ProtocolDispatcher> createDispatcher,
    ManagedEngineSharedState shared) : IToolEasyTransfer
{
    /// <summary>libcurl's <c>CURLE_URL_MALFORMAT</c> text for a perform with no URL set.</summary>
    internal const string NoUrlSetText = "No URL set";

    /// <summary>libcurl's <c>CURLE_URL_MALFORMAT</c> text for a URL it cannot parse.</summary>
    internal const string MalformedUrlText = "URL rejected: Malformed input to a URL function";

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

    /// <summary>Gets the caches the running perform uses: those its share handle gives, else the engine's.</summary>
    private ManagedEngineSharedState PerformShared => attachedShared ?? shared;

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
    /// Gets or sets what gives each <c>CURLOPT_MIMEPOST</c> body its boundary: <see cref="MultipartBoundary.CreateRandom" />,
    /// curl's random one, by default; tests set a fixed one to compare bodies. <see cref="Duplicate" /> copies it and
    /// <see cref="Reset" /> leaves it, as it is not an option.
    /// </summary>
    internal Func<string> CreateFormBoundary { get; set; } = MultipartBoundary.CreateRandom;

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

    /// <inheritdoc />
    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        headers.Clear();
        await ConnectOnly.CloseAsync().ConfigureAwait(false);
        await WebSocketConnection.CloseAsync().ConfigureAwait(false);
        if (PushedResponse is { } pushed)
        {
            PushedResponse = null;
            return await PerformPushedAsync(pushed, cancellationToken).ConfigureAwait(false);
        }

        return await PerformFromOptionsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the HTTP/2 server push this handle was duplicated for (<see cref="DuplicateForPush" />), which its next
    /// perform reads in place of sending a request; <see langword="null" /> for every other handle and after that perform.
    /// </summary>
    internal IHttpPushedResponse? PushedResponse { get; private set; }

    /// <summary>
    /// A duplicate of this handle bound to <paramref name="response" />, as curl 8.22.0's <c>push_promise</c> makes
    /// the pushed transfer with <c>curl_easy_duphandle</c> of the parent (BL-2469).
    /// </summary>
    /// <param name="response">The pushed response the duplicate's next perform reads.</param>
    /// <returns>The duplicate.</returns>
    internal ManagedEasyTransfer DuplicateForPush(IHttpPushedResponse response)
    {
        ManagedEasyTransfer pushed = (ManagedEasyTransfer)Duplicate();
        pushed.PushedResponse = response;
        return pushed;
    }

    /// <summary>
    /// The push sink the running perform's HTTP handler offers HTTP/2 server pushes to: an
    /// <see cref="EngineHttp2PushSink" /> while this handle is in a multi handle whose <c>CURLMOPT_PUSHFUNCTION</c>
    /// is set, else <see langword="null" />, so the handler keeps advertising <c>SETTINGS_ENABLE_PUSH</c> 0 (BL-2469).
    /// </summary>
    /// <returns>The sink, or <see langword="null" />.</returns>
    private EngineHttp2PushSink? PushSinkOf() =>
        shared.MultiOwners.TryGetValue(this, out ManagedMultiTransfer? multi) && multi.Options.Push is not null
            ? new EngineHttp2PushSink(this, multi)
            : null;

    /// <summary>
    /// Runs a pushed transfer: the pushed response's head goes to the header callback a line at a time and its body
    /// to the write callback (<see cref="PushedResponseCopier" />), nothing being sent; a failed stream answers
    /// <see cref="CurlCode.RecvError" /> and a short take by a callback <see cref="CurlCode.WriteError" />, each with
    /// its message as <see cref="ErrorText" />.
    /// </summary>
    /// <param name="pushed">The pushed response.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformPushedAsync(IHttpPushedResponse pushed, CancellationToken cancellationToken)
    {
        EngineToolTransferOutputs outputs = OwnOutputsOf(Options.Callbacks, pauseGate: null, headers);
        try
        {
            await PushedResponseCopier.CopyAsync(pushed.Content, outputs.HeaderOutput!, outputs.Output, cancellationToken).ConfigureAwait(false);
            ErrorText = string.Empty;
            return CurlCode.Ok;
        }
        catch (OutputWriteFailedException exception)
        {
            ErrorText = exception.Message;
            return CurlCode.WriteError;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ErrorText = exception.Message;
            return CurlCode.RecvError;
        }
    }

    /// <summary>
    /// Runs one transfer from <see cref="Options" />, the connections it opens the handle's own
    /// (<see cref="OwnTheConnectionsOpened" />), over the engine's caches, or, while
    /// <see cref="Api.Options.Behaviour.EasyBehaviourOptions.Share" /> names a <see cref="ManagedShareHandle" />,
    /// attached to it and over the caches it shares (ADR-0606); a share handle of another engine kind shares nothing.
    /// </summary>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformFromOptionsAsync(CancellationToken cancellationToken)
    {
        OwnTheConnectionsOpened();
        if (Options.Behaviour.Share is not ManagedShareHandle share)
        {
            return await PerformFromOptionsOverCachesAsync(cancellationToken).ConfigureAwait(false);
        }

        performShare = share;
        attachedShared = share.Attach(this, shared, Options.Http.CookieFiles.Count > 0, ShareHostKeyOf(Options));
        try
        {
            return await PerformFromOptionsOverCachesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            share.LockDnsAtDone(this);
            performShare = null;
            attachedShared = null;
        }
    }

    /// <summary>
    /// The <c>host:port</c> a perform from <paramref name="options" /> resolves, which a share handle sharing DNS
    /// looks up and adds under its lock (<see cref="ManagedShareHandle.Attach" />); <see langword="null" /> when no URL
    /// is set or it does not parse.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The name, or <see langword="null" />.</returns>
    internal static string? ShareHostKeyOf(EasyOptions options) =>
        options.Network.Url is { } urlText && CurlUrl.TryParse(urlText, options.Network.PathAsIs, out CurlUrl? url)
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{url.IdnHost}:{url.Port}")
            : null;

    /// <summary>
    /// The cookie store the transfer's HTTP handler keeps: <paramref name="cookies" />' own, under the share
    /// handle's cookie lock while the perform is attached to one (<see cref="ManagedShareHandle.CookieStoreFor" />).
    /// </summary>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <returns>The store, or <see langword="null" /> for none.</returns>
    private ICookieStore? HandlerCookieStoreOf(CookieEngine? cookies) =>
        performShare is { } share ? share.CookieStoreFor(cookies?.HandlerStore, this) : cookies?.HandlerStore;

    /// <summary>
    /// <paramref name="options" /> with no <c>CURLOPT_USERAGENT</c> set as an empty one, which sends no
    /// <c>User-Agent</c> line to the origin or a proxy's <c>CONNECT</c>, as libcurl 8.22.0 sends none without it; the
    /// HTTP handler's <c>curl/8.22.0</c> default is the curl tool's, which a perform with <see cref="ToolSettings" />
    /// keeps (BL-2559, ADR-0659).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options the perform runs with.</returns>
    internal static EasyOptions WithoutDefaultUserAgent(EasyOptions options) =>
        options.Http.UserAgent is null ? options with { Http = options.Http with { UserAgent = string.Empty } } : options;

    /// <summary>
    /// <paramref name="options" /> with <see cref="Curl.Api.Options.Http.EasyHttpOptions.PostFields" /> dropped when
    /// <see cref="Curl.Api.Options.Http.EasyHttpOptions.HttpGet" /> is set and <see cref="Curl.Api.Options.Http.EasyHttpOptions.Post" /> is not, so a
    /// <c>CURLOPT_HTTPGET</c> after <c>CURLOPT_POSTFIELDS</c> sends a <c>GET</c> with no body, as libcurl 8.22.0 switches
    /// only its method and keeps the fields for a later <c>CURLOPT_POST</c> (BL-2461, ADR-0565).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options the perform runs with.</returns>
    internal static EasyOptions WithoutPostFieldsUnderHttpGet(EasyOptions options) =>
        options.Http is { HttpGet: true, Post: false, PostFields: not null }
            ? options with { Http = options.Http with { PostFields = null } }
            : options;

    /// <summary>Runs one transfer from <see cref="Options" />, as <see cref="PerformAsync" /> describes, over <see cref="PerformShared" />.</summary>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformFromOptionsOverCachesAsync(CancellationToken cancellationToken)
    {
        EasyOptions options = UrlPortOverride.Apply(UrlHandleOverride.Apply(WithoutPostFieldsUnderHttpGet(ToolSettings is null ? WithoutDefaultUserAgent(Options) : Options)));
        LastResult = null;
        LastContext = null;
        if (options.Network.Url is not { } urlText)
        {
            return Finish(CurlCode.UrlMalformat, NoUrlSetText, new TransferInfo());
        }

        if (TransferUrlOf(urlText, options) is not { } url)
        {
            return Finish(CurlCode.UrlMalformat, MalformedUrlText, new TransferInfo { EffectiveUrl = urlText });
        }

        EngineTransferSettings settings = ToolSettings ?? TransferSettingsOf(options);
        long transferId = TransferIdOf(ToolEvents);
        Info = new TransferInfo { EffectiveUrl = urlText, TransferId = transferId, ConnectionId = NoConnectionId };
        performedHsts = PerformShared.Hsts;
        CurlCode loaded = LoadHstsFromCallback(options, performedHsts);
        if (loaded != CurlCode.Ok)
        {
            return Finish(loaded, string.Empty, Info);
        }

        return await PerformThroughProxyAsync(options, settings, url, urlText, transferId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Chooses the perform's proxy (<see cref="TrySelectProxy" />), then only connects through it
    /// (<see cref="PerformConnectOnlyAsync" />) or runs the transfer with it on the context; proxy text that cannot
    /// be used ends the perform with the selector's failure before anything is set up or dialled.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private Task<CurlCode> PerformThroughProxyAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        CancellationToken cancellationToken)
    {
        if (!TrySelectProxy(options, settings, url, out ProxyEndpoint? proxy, out TransferResult? refused))
        {
            return Task.FromResult(Finish(refused.ExitCode.ToCurlCode(), refused.ErrorMessage!, Info));
        }

        if (IsRawConnectOnly(options.Network.ConnectOnly, url))
        {
            return PerformConnectOnlyAsync(options, url, proxy, cancellationToken);
        }

        selectedProxy = proxy;
        if (!TryLookUpNetrcCredentials(options, url, out TransferResult? netrcFailure))
        {
            return Task.FromResult(Finish(netrcFailure.ExitCode.ToCurlCode(), netrcFailure.ErrorMessage!, Info));
        }

        return PerformResumingUploadAsync(options, settings, url, urlText, transferId, cancellationToken);
    }

    /// <summary>
    /// The perform's <c>%{xfer_id}</c>: the curl tool's runner's (its <c>--retry</c> attempt's, else its transfer's)
    /// when it gave <paramref name="toolEvents" />, so the handle answers its numbering (BL-2495); else the engine's next.
    /// </summary>
    /// <param name="toolEvents">The runner's events and state, or <see langword="null" /> for a handle's own.</param>
    /// <returns>The <c>%{xfer_id}</c>.</returns>
    private long TransferIdOf(EngineToolTransferEvents? toolEvents) =>
        toolEvents is { State: var state } ? state.RetryTransferId ?? state.TransferId : shared.Performer.TakeTransferId();

    /// <summary>
    /// Skips a read-callback upload's <c>CURLOPT_RESUME_FROM</c> offset (<see cref="CallbackUploadResume.SkipUnlessToolGave" />)
    /// and runs the transfer, or ends the perform with the skip's failure before anything is set up or sent (ADR-0641).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private Task<CurlCode> PerformResumingUploadAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        CancellationToken cancellationToken) =>
        CallbackUploadResume.SkipUnlessToolGave(ToolInputs, options) is { } refused
            ? Task.FromResult(Finish(refused.Code, refused.Text, Info))
            : PerformBuildingMimePostAsync(options, settings, url, urlText, transferId, cancellationToken);

    /// <summary>
    /// The URL the perform transfers: the one the curl tool's runner parsed and checked (<see cref="EngineToolTransferInputs.Url" />),
    /// else <paramref name="urlText" /> parsed under <c>CURLOPT_PATH_AS_IS</c> (BL-2394).
    /// </summary>
    /// <param name="urlText">The URL set.</param>
    /// <param name="options">The handle's options.</param>
    /// <returns>The URL, or <see langword="null" /> when the URL set does not parse.</returns>
    private CurlUrl? TransferUrlOf(string urlText, EasyOptions options) =>
        ToolInputs?.Url ?? (CurlUrl.TryParse(urlText, options.Network.PathAsIs, out CurlUrl? parsed) ? parsed : null);

    /// <summary>
    /// The HSTS cache the last perform from the options used - the engine's, or its share handle's - which
    /// <see cref="DisposeAsync" /> hands to <c>CURLOPT_HSTSWRITEFUNCTION</c>; <see langword="null" /> before one.
    /// </summary>
    private HstsTransferPolicy? performedHsts;

    /// <summary>
    /// <c>CURLOPT_HSTSREADFUNCTION</c> before the transfer, as libcurl's <c>Curl_pretransfer</c> calls it: the entries
    /// it hands over loaded into <paramref name="hsts" /> (<see cref="CallbackHstsCache.Load" />) while the handle's HSTS
    /// cache is enabled (<see cref="CallbackHstsCache.IsEnabled" />); nothing without the callback (ADR-0620).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="hsts">The HSTS cache the perform uses.</param>
    /// <returns><see cref="CurlCode.Ok" />, or the code that fails the perform.</returns>
    private static CurlCode LoadHstsFromCallback(EasyOptions options, HstsTransferPolicy hsts) =>
        options.Callbacks.HstsRead is { } read && CallbackHstsCache.IsEnabled(options.Http)
            ? CallbackHstsCache.Load(read, hsts)
            : CurlCode.Ok;

    /// <summary>
    /// <c>CURLOPT_HSTSWRITEFUNCTION</c> at cleanup, as libcurl's <c>Curl_close</c> calls it: every entry of the HSTS
    /// cache the last perform used (the engine's before any) handed to it (<see cref="CallbackHstsCache.Save" />) while
    /// the handle's HSTS cache is enabled; nothing without the callback (ADR-0620).
    /// </summary>
    private void SaveHstsToCallback()
    {
        EasyOptions options = Options;
        if (options.Callbacks.HstsWrite is { } write && CallbackHstsCache.IsEnabled(options.Http))
        {
            CallbackHstsCache.Save(write, performedHsts ?? shared.Hsts);
        }
    }

    /// <summary>The proxy <see cref="TrySelectProxy" /> chose for the running perform, which <see cref="OwnInputsOf" /> puts on the context.</summary>
    private ProxyEndpoint? selectedProxy;

    /// <summary>
    /// Chooses the perform's proxy: the curl tool's runner's (<see cref="EngineToolTransferInputs.Proxy" />) when it gave
    /// <see cref="ToolInputs" />; else <see cref="TransferProxySelection" />'s choice from <see cref="EasyOptions.Proxy" />
    /// and the proxy environment variables the engine's <see cref="ManagedEngineSharedState.ProxySelector" /> reads, as
    /// libcurl reads them for every handle, connect-only performs included (BL-2519, ADR-0608).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings, for the proxy credentials and the Unix socket.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="proxy">The proxy, or <see langword="null" /> to connect directly.</param>
    /// <param name="refused">The selector's failure for proxy text that cannot be used; <see langword="null" /> otherwise.</param>
    /// <returns><see langword="true" /> unless the perform must end with <paramref name="refused" />.</returns>
    private bool TrySelectProxy(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        out ProxyEndpoint? proxy,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out TransferResult? refused)
    {
        if (ToolInputs is { } toolInputs)
        {
            proxy = toolInputs.Proxy;
            refused = null;
            return true;
        }

        return TransferProxySelection.TrySelect(PerformShared.ProxySelector, options.Proxy, settings.OverUnixSocket, settings.ProxyCredentials, url, out proxy, out refused, Host.DiagnosticLog);
    }

    /// <summary>The credentials <see cref="TryLookUpNetrcCredentials" /> chose for the running perform, which <see cref="OwnInputsOf" /> puts on the context.</summary>
    private System.Net.NetworkCredential? netrcCredentials;

    /// <summary>
    /// Chooses the perform's credentials from the <c>.netrc</c> file under <c>CURLOPT_NETRC</c> with
    /// <see cref="TransferCredentialLookup" />, as libcurl's <c>override_login</c> does for every handle, proxied or
    /// not (upstream test2309, BL-2696): the <c>CURLOPT_NETRC_FILE</c> file, else the one <c>NETRC</c> names, else
    /// <c>.netrc</c> in <c>HOME</c>, read through <see cref="EasyTransferHost.ReadFile" />. Nothing is looked up with
    /// <see cref="ToolInputs" /> set, whose runner chose its own, or under <see cref="NetrcUsage.Ignored" />.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set, parsed, whose host picks the entry.</param>
    /// <param name="failure">The exit-26 failure of a required file missing or malformed, or the exit-3 one of URL credentials refused.</param>
    /// <returns><see langword="false" /> when the perform must end with <paramref name="failure" />.</returns>
    private bool TryLookUpNetrcCredentials(
        EasyOptions options,
        CurlUrl url,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out TransferResult? failure)
    {
        netrcCredentials = null;
        failure = null;
        if (ToolInputs is not null || options.Authentication.Netrc == NetrcUsage.Ignored)
        {
            return true;
        }

        TransferCredentialLookup lookup = new(Host.ReadFile, shared.ReadEnvironmentVariable, Host.RunsOnWindows, Host.DiagnosticLog);
        return lookup.TryLookUp(options.Authentication, url, out netrcCredentials, out failure);
    }

    /// <summary>
    /// Whether the perform only connects (<see cref="PerformConnectOnlyAsync" />): under <c>CURLOPT_CONNECT_ONLY</c> 1
    /// for a URL whose connect phase sends nothing (not <see cref="LogsInBeforeConnectOnly" />), and under 2 for a URL
    /// that is not <c>ws</c> or <c>wss</c>, as libcurl, which upgrades only those (ADR-0616).
    /// </summary>
    /// <param name="connectOnly">The handle's <c>CURLOPT_CONNECT_ONLY</c>.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <returns><see langword="true" /> when the perform connects and sends nothing.</returns>
    private static bool IsRawConnectOnly(Api.Options.Network.ConnectOnly connectOnly, CurlUrl url) => connectOnly switch
    {
        Api.Options.Network.ConnectOnly.Connect => !LogsInBeforeConnectOnly(url),
        Api.Options.Network.ConnectOnly.WebSocket => !IsWebSocket(url),
        _ => false,
    };

    /// <summary>
    /// Whether a <c>CURLOPT_CONNECT_ONLY</c> 1 perform of <paramref name="url" /> runs the protocol's connect phase
    /// (<see cref="ConnectOnlyLoginDispatcherOf" />): <c>ftp</c>, <c>ftps</c>, <c>imap</c> and <c>imaps</c>, whose
    /// handlers have a connect-only entry point, as libcurl logs in before such a perform ends (upstream tests 597
    /// and 660, BL-2623).
    /// </summary>
    /// <param name="url">The URL set, parsed.</param>
    /// <returns><see langword="true" /> for those schemes.</returns>
    private static bool LogsInBeforeConnectOnly(CurlUrl url) => url.Scheme.ToLowerInvariant() is "ftp" or "ftps" or "imap" or "imaps";

    /// <summary>Whether <paramref name="url" /> is a <c>ws</c> or <c>wss</c> URL.</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true" /> for either WebSocket scheme.</returns>
    private static bool IsWebSocket(CurlUrl url) => url.Scheme.ToLowerInvariant() is "ws" or "wss";

    /// <summary>
    /// <c>CURLOPT_CONNECT_ONLY</c> 1: connects to the URL's host and port through <see cref="CreateConnectOnlyConnector" />'s
    /// connector, with TLS for a TLS scheme, through the proxy <see cref="TrySelectProxy" /> chose (a tunnel through
    /// an HTTP or HTTPS proxy, the handshake of a SOCKS one, as libcurl tunnels for connect-only without
    /// <c>CURLOPT_HTTPPROXYTUNNEL</c>), sends nothing, and keeps the connection for <see cref="Connection" />; a
    /// failed connect ends the perform with the connector's code and text (ADR-0608).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="proxy">The proxy to connect through, or <see langword="null" /> to connect directly.</param>
    /// <param name="cancellationToken">Stops the connect.</param>
    /// <returns>The perform's code.</returns>
    private async Task<CurlCode> PerformConnectOnlyAsync(EasyOptions options, CurlUrl url, ProxyEndpoint? proxy, CancellationToken cancellationToken)
    {
        IConnector connector = CreateConnectOnlyConnector(options, PerformShared);
        ConnectTarget target = new(url.IdnHost, url.Port, ConnectOnlyConnection.UsesTls(url.Scheme)) { Proxy = proxy };
        ConnectResult connect = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } opened)
        {
            // ConnectResult.Failed takes a non-null message, so a result with no connection always has one.
            return Finish(connect.ExitCode.ToCurlCode(), connect.ErrorMessage!, Info);
        }

        await ConnectOnly.OpenAsync(opened).ConfigureAwait(false);
        return Finish(CurlCode.Ok, string.Empty, Info);
    }

    /// <summary>
    /// Builds the <c>CURLOPT_MIMEPOST</c> body (<see cref="MimePostFormUnlessToolGaveAsync" />) and runs the transfer
    /// with it, or ends the perform with the build's failure before anything is set up or sent.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformBuildingMimePostAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        CancellationToken cancellationToken)
    {
        EngineToolTransferInputs? toolInputs = ToolInputs;
        List<CallbackReadStream> mimeReads = [];
        MultipartFormBuildResult? form = await MimePostFormUnlessToolGaveAsync(toolInputs, options, url, mimeReads, cancellationToken).ConfigureAwait(false);
        return form is { IsBuilt: false }
            ? Finish(form.Failure.ExitCode.ToCurlCode(), form.Failure.ErrorMessage!, Info)
            : await PerformDisposingFormAsync(options, settings, url, urlText, transferId, toolInputs, form?.Body ?? ReadCallbackPostBodyOf(toolInputs, options, url, mimeReads), mimeReads, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The body of a <c>CURLOPT_POST</c> with no <c>CURLOPT_POSTFIELDS</c>, read through <c>CURLOPT_READFUNCTION</c>
    /// (<see cref="CallbackUploadOf" />) and sent as <c>application/x-www-form-urlencoded</c>, with
    /// <c>CURLOPT_POSTFIELDSIZE</c> as its <c>Content-Length</c> when not negative and chunked otherwise, as libcurl
    /// 8.22.0 posts (upstream test508, BL-2586); the stream joins <paramref name="reads" />, so its
    /// <c>CURL_READFUNC_ABORT</c> ends the perform. With no read callback and a <c>CURLOPT_POSTFIELDSIZE</c> of 0
    /// (<c>CURLOPT_POSTFIELDS</c> NULL), an empty body sent with <c>Content-Length: 0</c>, as libcurl 8.22.0 posts
    /// (upstream test515, BL-2592). None when the curl tool's runner gave <paramref name="toolInputs" />, for a
    /// <c>CURLOPT_UPLOAD</c>, or with no read callback and any other size.
    /// </summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set, whose scheme says whether the transfer can pause.</param>
    /// <param name="reads">Receives the body's stream.</param>
    /// <returns>The body, or <see langword="null" /> for none.</returns>
    private StreamBody? ReadCallbackPostBodyOf(EngineToolTransferInputs? toolInputs, EasyOptions options, CurlUrl url, ICollection<CallbackReadStream> reads)
    {
        if (toolInputs is not null || !PostsWithoutFields(options))
        {
            return null;
        }

        long size = options.Http.PostFieldSize;
        if (options.Callbacks.Read is not { } read)
        {
            return size == 0 ? new StreamBody(Stream.Null, 0, HttpRequestOptionsMapping.FormUrlEncoded) : null;
        }

        CallbackReadStream content = CallbackUploadOf(read, options.Callbacks, PauseGateFor(url));
        reads.Add(content);
        return new StreamBody(content, size < 0 ? null : size, HttpRequestOptionsMapping.FormUrlEncoded);
    }

    /// <summary>Whether the options post with <c>CURLOPT_POST</c>, no <c>CURLOPT_POSTFIELDS</c> and no <c>CURLOPT_UPLOAD</c>.</summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>Whether the post's body is the read callback's.</returns>
    private static bool PostsWithoutFields(EasyOptions options) =>
        options.Http.Post && options.Http.PostFields is null && !options.Transfer.Upload;

    /// <summary><see cref="PerformParsedAsync" />, then disposes <paramref name="formBody" />'s content.</summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="formBody">The <c>CURLOPT_MIMEPOST</c> body, or <see langword="null" /> for none.</param>
    /// <param name="mimeReads">The streams of the body's <c>curl_mime_data_cb</c> parts.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformDisposingFormAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        EngineToolTransferInputs? toolInputs,
        StreamBody? formBody,
        IReadOnlyList<CallbackReadStream> mimeReads,
        CancellationToken cancellationToken)
    {
        await using Stream? formContent = formBody?.Content;
        return await PerformParsedAsync(options, settings, url, urlText, transferId, toolInputs, formBody, mimeReads, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the transfer of a URL that parsed, its <c>CURLOPT_MIMEPOST</c> body built: the setup, the attempt and the
    /// cleanup <see cref="PerformAsync" /> describes.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="formBody">The <c>CURLOPT_MIMEPOST</c> body, or <see langword="null" /> for none.</param>
    /// <param name="mimeReads">The streams of the body's <c>curl_mime_data_cb</c> parts, whose abort ends the perform.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformParsedAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        string urlText,
        long transferId,
        EngineToolTransferInputs? toolInputs,
        StreamBody? formBody,
        IReadOnlyList<CallbackReadStream> mimeReads,
        CancellationToken cancellationToken)
    {
        CookieEngine? cookies = CookieEngine.FromHttpOptions(options.Http, PerformShared.Cookies);
        EngineToolTransferEvents? toolEvents = ToolEvents;
        (EngineTransferState state, ITransferEvents events, TlsVerifyResultRecordingTransferEvents? recorder) = StateAndEventsOf(toolEvents, options);
        string transferUrl = await SetUpUnlessToolDidAsync(toolInputs, options, settings, cookies, state, urlText, events).ConfigureAwait(false);
        EngineToolTransferDispatch? toolDispatch = ToolDispatch;
        RedirectFollower follower = FollowerOf(toolDispatch, options, cookies);
        EngineToolTransferInputs inputs = MimePostForm.WithBody(InputsOf(toolInputs, options, settings, url, state, PauseGateFor(url), selectedProxy), formBody, url.Scheme);
        IReadOnlyList<CallbackReadStream> callbackReads = inputs.Upload is CallbackReadStream upload ? [.. mimeReads, upload] : mimeReads;
        EngineToolTransferProgress? toolProgress = ToolProgress;
        using CancellationTokenSource abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, AbortTokenOf(toolProgress));
        CallbackTransferProgress? callbackProgress = CallbackProgressUnlessToolGave(toolProgress, options, abort);
        pauseGate.TickWhilePaused(CallbackTransferProgress.PausedTickOf(callbackProgress), shared.TimeProvider);
        TransferResult? result = await FollowUnlessAbortedAsync(
            follower,
            state,
            CreateContext(
                toolDispatch,
                options,
                settings,
                TransferUrlOf(transferUrl, urlText, url, options),
                ProgressStartingWatchdogsOf(toolProgress, callbackProgress, options, state),
                events,
                inputs,
                state,
                ContextTokenOf(toolProgress, cancellationToken, abort)),
            RedirectPolicyMapping.Of(settings, options.Http, options.Authentication),
            callbackProgress,
            callbackReads).ConfigureAwait(false);
        await CleanUpUnlessToolDoesAsync(toolInputs, options, settings, cookies, state, transferUrl).ConfigureAwait(false);
        LastResult = recorder is null ? result : recorder.ReportOn(result);
        long connectionId = toolEvents is null ? ConnectionIdOf(result, state) : ToolConnectionIdOf(result, state, toolEvents);
        return FinishPerformed(LastResult, options, transferId, cookies, connectionId, AbortedTextOf(callbackReads), toolEvents?.State);
    }

    /// <summary>
    /// The follower the transfer runs through: the curl tool's runner's from <paramref name="toolDispatch" />, or
    /// without it one over the dispatcher the engine's factory builds from the options, the caches and the cookie
    /// store the transfer's HTTP handler keeps (BL-2479); under <c>CURLOPT_CONNECT_ONLY</c> 2 (which reaches here only
    /// for a <c>ws</c> or <c>wss</c> URL) one over <see cref="WebSocketUpgradeDispatcherOf" /> alone, and under 1 (which
    /// reaches here only for a URL that <see cref="LogsInBeforeConnectOnly" />) one over <see cref="ConnectOnlyLoginDispatcherOf" />.
    /// </summary>
    /// <param name="toolDispatch">The runner's follower and context factory, or <see langword="null" /> for the handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <returns>The follower.</returns>
    private RedirectFollower FollowerOf(EngineToolTransferDispatch? toolDispatch, EasyOptions options, CookieEngine? cookies) =>
        toolDispatch?.Follower ?? new RedirectFollower(options.Network.ConnectOnly switch
        {
            Api.Options.Network.ConnectOnly.WebSocket => WebSocketUpgradeDispatcherOf(options),
            Api.Options.Network.ConnectOnly.Connect => ConnectOnlyLoginDispatcherOf(options),
            _ => createDispatcher(options, PerformShared, HandlerCookieStoreOf(cookies), PushSinkOf()),
        });

    /// <summary>
    /// The dispatcher of a <c>CURLOPT_CONNECT_ONLY</c> 1 perform of an <c>ftp</c>, <c>ftps</c>, <c>imap</c> or
    /// <c>imaps</c> URL: <see cref="ManagedCurlEngine.CreateConnectOnlyLoginHandlers" />' handlers over
    /// <see cref="CreateConnectOnlyConnector" />'s connector, which run the protocol's connect phase and give the open
    /// connection to <see cref="Connection" /> (BL-2623).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The dispatcher.</returns>
    private ProtocolDispatcher ConnectOnlyLoginDispatcherOf(EasyOptions options) =>
        new(ManagedCurlEngine.CreateConnectOnlyLoginHandlers(CreateConnectOnlyConnector(options, PerformShared), options, PerformShared, ConnectOnly));

    /// <summary>
    /// The dispatcher of a <c>CURLOPT_CONNECT_ONLY</c> 2 perform: a <see cref="WebSocketUpgradeOnlyHandler" /> whose
    /// <see cref="WsProtocolHandler" /> dials through <see cref="CreateConnectOnlyConnector" />'s connector, so the
    /// upgraded connection is the handle's own and never pooled, authenticates as the engine's HTTP handler does
    /// (<see cref="ManagedCurlEngine.CreateWebSocketAuthenticator" />) and gives the open connection to
    /// <see cref="WebSocket" /> (ADR-0616).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The dispatcher.</returns>
    private ProtocolDispatcher WebSocketUpgradeDispatcherOf(EasyOptions options)
    {
        WsProtocolHandler upgrader = new(
            CreateConnectOnlyConnector(options, PerformShared),
            ManagedCurlEngine.CreateWebSocketAuthenticator(options, PerformShared),
            WebSocketRandomSource);
        return new ProtocolDispatcher([new WebSocketUpgradeOnlyHandler(upgrader, WebSocketConnection)]);
    }

    /// <summary>
    /// The <c>CURLOPT_MIMEPOST</c> or <c>CURLOPT_HTTPPOST</c> body (<see cref="MimePostForm.BuildAsync" />), built in UTF-8 through
    /// <see cref="Host" />'s file system with <see cref="CreateFormBoundary" />'s boundary, unless the curl tool's runner
    /// gave <paramref name="toolInputs" />, whose <see cref="EngineToolTransferInputs.FormBody" /> it built itself.
    /// </summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set, whose scheme picks a form or a mail message.</param>
    /// <param name="mimeReads">Receives the stream of each <c>curl_mime_data_cb</c> part.</param>
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>The body or its failure, or <see langword="null" /> for none.</returns>
    private async Task<MultipartFormBuildResult?> MimePostFormUnlessToolGaveAsync(
        EngineToolTransferInputs? toolInputs,
        EasyOptions options,
        CurlUrl url,
        ICollection<CallbackReadStream> mimeReads,
        CancellationToken cancellationToken) =>
        toolInputs is null
            ? await MimePostForm.BuildAsync(new MultipartFormBodyBuilder(Host.FileSystem, Encoding.UTF8, CreateFormBoundary), options.Http, url.Scheme, options.Callbacks.Read, mimeReads, PauseGateFor(url), Host.StandardInput, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>The token the curl tool's runner aborts the transfer through, or none without <paramref name="toolProgress" />.</summary>
    /// <param name="toolProgress">The runner's progress and abort token, or <see langword="null" />.</param>
    /// <returns>The token.</returns>
    private static CancellationToken AbortTokenOf(EngineToolTransferProgress? toolProgress) =>
        toolProgress?.AbortToken ?? CancellationToken.None;

    /// <summary>
    /// The progress callback's sink (<see cref="CallbackTransferProgress.Of" />), unless the curl tool's runner gave
    /// <paramref name="toolProgress" />: then the callbacks are not called and nothing aborts by callback (ADR-0567).
    /// </summary>
    /// <param name="toolProgress">The runner's progress, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="abort">Cancelled when the callback aborts.</param>
    /// <returns>The sink, or <see langword="null" /> for none.</returns>
    private static CallbackTransferProgress? CallbackProgressUnlessToolGave(EngineToolTransferProgress? toolProgress, EasyOptions options, CancellationTokenSource abort) =>
        toolProgress is null ? CallbackTransferProgress.Of(options.Behaviour.NoProgress, options.Callbacks, abort) : null;

    /// <summary>
    /// The progress the context reports to: the runner's <paramref name="toolProgress" />, after starting the
    /// attempt's <c>-Y</c>/<c>-y</c> and <c>-m</c> watchdogs on <paramref name="state" /> as the runner does when it
    /// creates each attempt's context, so every perform, a <c>--retry</c> attempt's too, gets fresh ones; else
    /// <paramref name="callbackProgress" />, with no watchdog started (ADR-0567).
    /// </summary>
    /// <param name="toolProgress">The runner's progress, or <see langword="null" /> for a handle's own.</param>
    /// <param name="callbackProgress">The progress callback's sink, or <see langword="null" />.</param>
    /// <param name="options">The handle's options, whose transfer group holds <c>-Y</c>, <c>-y</c> and <c>-m</c>.</param>
    /// <param name="state">The running transfer, which keeps the watchdogs.</param>
    /// <returns>The progress, or <see langword="null" /> for none.</returns>
    private ITransferProgress? ProgressStartingWatchdogsOf(
        EngineToolTransferProgress? toolProgress,
        CallbackTransferProgress? callbackProgress,
        EasyOptions options,
        EngineTransferState state)
    {
        if (toolProgress is null)
        {
            return callbackProgress;
        }

        shared.Performer.StartLowSpeedWatchdog(state, options.Transfer, Host.DiagnosticLog);
        shared.Performer.StartMaxTimeWatchdog(state, options.Transfer, Host.DiagnosticLog);
        return toolProgress.Progress;
    }

    /// <summary>
    /// The setup <see cref="SetUpAsync" /> does, unless the curl tool's runner gave <paramref name="toolInputs" />,
    /// having done it itself: then the handle reads no file and transfers <paramref name="urlText" /> as it is
    /// (ADR-0554).
    /// </summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle that sets itself up.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <param name="state">The running transfer, which keeps the alt-svc cache.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="events">Where the setup's <c>-v</c> lines go.</param>
    /// <returns>The URL to transfer.</returns>
    private async Task<string> SetUpUnlessToolDidAsync(
        EngineToolTransferInputs? toolInputs,
        EasyOptions options,
        EngineTransferSettings settings,
        CookieEngine? cookies,
        EngineTransferState state,
        string urlText,
        ITransferEvents events)
    {
        if (toolInputs is not null)
        {
            state.AltSvc = toolInputs.AltSvc;
            return urlText;
        }

        return await SetUpAsync(options, settings, cookies, state, urlText, events).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <c>-c</c> jar, alt-svc and HSTS files after the attempt, unless the curl tool's runner gave
    /// <paramref name="toolInputs" />, as it writes them itself (ADR-0554).
    /// </summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" /> for a handle that cleans up itself.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <param name="state">The running transfer, which keeps the alt-svc cache.</param>
    /// <param name="transferUrl">The URL transferred.</param>
    /// <returns>A task that completes once the files are written.</returns>
    private async Task CleanUpUnlessToolDoesAsync(
        EngineToolTransferInputs? toolInputs,
        EasyOptions options,
        EngineTransferSettings settings,
        CookieEngine? cookies,
        EngineTransferState state,
        string transferUrl)
    {
        if (toolInputs is null)
        {
            await shared.Performer.WriteCookieJarAltSvcAndHstsFilesAsync(
                cookies, settings, options.Http, transferUrl, Host.JarPrintTarget, state, PerformShared.Hsts, Host.FileSystem, Host.RunsOnWindows).ConfigureAwait(false);
        }
    }

    /// <summary>The URL to transfer: <paramref name="url" /> when the setup kept the URL set, else the <c>https</c> one HSTS switched it to.</summary>
    /// <param name="transferUrl">The URL the setup chose.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="options">The handle's options, whose <c>CURLOPT_PATH_AS_IS</c> the switched URL is parsed under.</param>
    /// <returns>The parsed URL to transfer.</returns>
    private static CurlUrl TransferUrlOf(string transferUrl, string urlText, CurlUrl url, EasyOptions options) =>
        ReferenceEquals(transferUrl, urlText) ? url : CurlUrl.Parse(transferUrl, options.Network.PathAsIs);

    /// <summary>The pieces the context takes: the runner's <paramref name="toolInputs" />, or the handle's own (<see cref="OwnInputsOf" />) without them.</summary>
    /// <param name="toolInputs">The runner's chosen pieces, or <see langword="null" />.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings, whose passphrase an SSH key takes.</param>
    /// <param name="url">The URL to transfer, whose scheme says whether it takes SSH options.</param>
    /// <param name="state">The running transfer, which keeps the alt-svc cache.</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <param name="proxy">The proxy <see cref="TrySelectProxy" /> chose, or <see langword="null" /> to connect directly.</param>
    /// <returns>The pieces.</returns>
    private EngineToolTransferInputs InputsOf(
        EngineToolTransferInputs? toolInputs,
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        EngineTransferState state,
        EasyPauseGate? pauseGate,
        ProxyEndpoint? proxy) =>
        toolInputs ?? OwnInputsOf(options, settings, url, state, pauseGate, proxy);

    /// <summary>
    /// The pause state a callback's pause waits on: the handle's, but none for a <c>file</c> URL, which libcurl cannot
    /// pause (<c>PROTOPT_NONETWORK</c>), so a pause there fails as libcurl's does (ADR-0573).
    /// </summary>
    /// <param name="url">The URL set.</param>
    /// <returns>The pause state, or <see langword="null" />.</returns>
    private EasyPauseGate? PauseGateFor(CurlUrl url) =>
        string.Equals(url.Scheme, "file", StringComparison.OrdinalIgnoreCase) ? null : pauseGate;

    /// <summary>
    /// The text of a perform a callback aborted: libcurl's <c>operation aborted by callback</c> when a read callback (the
    /// upload's or a <c>curl_mime_data_cb</c> part's) returned <c>CURL_READFUNC_ABORT</c>, else the progress callback's
    /// <c>Callback aborted</c>.
    /// </summary>
    /// <param name="reads">The read callbacks' streams the transfer reads.</param>
    /// <returns>The text.</returns>
    private static string AbortedTextOf(IReadOnlyList<CallbackReadStream> reads) =>
        reads.Any(read => read.Aborted) ? CallbackReadStream.AbortedText : CallbackTransferProgress.CallbackAbortedText;

    /// <summary>
    /// The pieces a handle with no <see cref="ToolInputs" /> chooses from its options: the proxy <see cref="TrySelectProxy" />
    /// chose from <c>CURLOPT_PROXY</c> and the proxy environment (BL-2519); the credentials
    /// <see cref="TryLookUpNetrcCredentials" /> chose from the <c>.netrc</c> file (BL-2696); no <c>If-None-Match</c> lines, range or form body of the runner's; <c>CURLOPT_RESUME_FROM</c> when
    /// positive; the read callback under <c>CURLOPT_UPLOAD</c> (<see cref="UploadOf" />); the alt-svc cache its
    /// own setup opened; and for an <c>scp</c> or <c>sftp</c> URL the handle's SSH options with its host-key
    /// callbacks (<see cref="SshOptionsMapping.OfHandle" />, ADR-0623).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings, whose passphrase an SSH key takes.</param>
    /// <param name="url">The URL to transfer, whose scheme says whether it takes SSH options.</param>
    /// <param name="state">The running transfer, which keeps the alt-svc cache.</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <param name="proxy">The proxy to connect through, or <see langword="null" /> to connect directly.</param>
    /// <returns>The pieces.</returns>
    private EngineToolTransferInputs OwnInputsOf(EasyOptions options, EngineTransferSettings settings, CurlUrl url, EngineTransferState state, EasyPauseGate? pauseGate, ProxyEndpoint? proxy) =>
        new(
            Proxy: proxy,
            LookedUpCredentials: netrcCredentials,
            ResumeFrom: options.Transfer.ResumeFrom > 0 ? options.Transfer.ResumeFrom : null,
            Upload: UploadOf(options, url, pauseGate),
            AltSvc: state.AltSvc,
            Ssh: SshOptionsMapping.IsSshScheme(url.Scheme) ? SshOptionsMapping.OfHandle(options.Protocols.Ssh, options.Callbacks, settings.Passphrase) : null);

    /// <summary>
    /// The setup the curl tool's runner does around its serial attempt, before connecting
    /// (<c>CurlCommandRunner</c>, BL-2362): the <c>-b</c> files loaded into the engine's cookies, the
    /// <c>--alt-svc</c> file read into the transfer's own cache, and the <c>--hsts</c> file read, an
    /// <c>http</c> URL the HSTS cache knows switching to <c>https</c>.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <param name="state">The running transfer, which keeps the alt-svc cache.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="events">Where the setup's <c>-v</c> lines go.</param>
    /// <returns>The URL to transfer: <paramref name="urlText" /> itself, or the <c>https</c> one HSTS switched it to.</returns>
    private async Task<string> SetUpAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CookieEngine? cookies,
        EngineTransferState state,
        string urlText,
        ITransferEvents events)
    {
        EasyTransferHost host = Host;
        await ApplyCookieListAsync(cookies, host, events).ConfigureAwait(false);
        await shared.Performer.LoadCookieFilesAsync(cookies, settings, urlText, host.FileSystem, host.StandardInput, events).ConfigureAwait(false);
        await shared.Performer.OpenAltSvcCacheAsync(state, settings, options.Network, options.Http, urlText, host.FileSystem, host.DiagnosticLog, host.AltSvcUsesOverHttp).ConfigureAwait(false);
        return await TransferPerformer.SwitchToHttpsForHstsAsync(PerformShared.Hsts, settings, options.Network, options.Http, urlText, host.FileSystem, events).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies <c>CURLOPT_COOKIELIST</c> (<see cref="Api.Options.Http.EasyHttpOptions.CookieList" />) to the transfer's cookies before the
    /// cookie files load, as libcurl applies each item when it is set, before the perform loads the files (BL-2520).
    /// </summary>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option turns them on.</param>
    /// <param name="host">What the cookie files, the jar and <c>-</c> reach.</param>
    /// <param name="events">Where refused <c>Set-Cookie:</c> lines are reported.</param>
    /// <returns>A task that completes once every item is applied.</returns>
    private Task ApplyCookieListAsync(CookieEngine? cookies, EasyTransferHost host, ITransferEvents events) =>
        cookies?.ApplyCookieListAsync(cookieListItemsApplied, host.FileSystem, host.StandardInput, host.JarPrintTarget, shared.TimeProvider.GetUtcNow(), events) ?? Task.CompletedTask;

    /// <summary>
    /// The running transfer, the events it reports to and what records its verify codes: the runner's
    /// <paramref name="toolEvents" /> and their state as they are, whose recorders number <c>%{conn_id}</c> and keep the
    /// verify codes, so the handler sees the runner's own events and nothing records here (BL-2394); or a new state and
    /// the handle's own events (<see cref="EventsOf" />) kept by <see cref="TlsVerifyResultRecordingTransferEvents" />
    /// for <c>CURLINFO_SSL_VERIFYRESULT</c> without them (ADR-0561).
    /// </summary>
    /// <param name="toolEvents">The runner's events and state, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <returns>The state and the events.</returns>
    private (EngineTransferState State, ITransferEvents Events, TlsVerifyResultRecordingTransferEvents? Recorder) StateAndEventsOf(EngineToolTransferEvents? toolEvents, EasyOptions options)
    {
        if (toolEvents is not null)
        {
            return (toolEvents.State, toolEvents.Events, null);
        }

        EngineTransferState state = new();
        TlsVerifyResultRecordingTransferEvents recorder = new(EventsOf(options, state));
        return (state, recorder, recorder);
    }

    /// <summary>
    /// The token the context stops on: the runner's abort token as it is when the runner gave <paramref name="toolProgress" />
    /// and the perform's own token cannot be cancelled, so the handler sees the token the runner's own context would
    /// carry; else <paramref name="abort" />'s, which also stops when a callback aborts.
    /// </summary>
    /// <param name="toolProgress">The runner's progress and abort token, or <see langword="null" />.</param>
    /// <param name="cancellationToken">The perform's token.</param>
    /// <param name="abort">The perform's token linked with the runner's.</param>
    /// <returns>The token.</returns>
    private static CancellationToken ContextTokenOf(EngineToolTransferProgress? toolProgress, CancellationToken cancellationToken, CancellationTokenSource abort) =>
        toolProgress is not null && !cancellationToken.CanBeCanceled ? toolProgress.AbortToken : abort.Token;

    /// <summary>
    /// The transfer's events: the debug callback's under <c>CURLOPT_VERBOSE</c>, else none, with the
    /// <see cref="TraceComponents" />' lines added (<see cref="WithTraceComponentEvents" />), kept by
    /// <see cref="EngineConnectionIdTransferEvents" />, which gives the transfer its <c>%{conn_id}</c>
    /// when it opens a connection and the opener's when it reuses one, and puts it on <see cref="Info" />
    /// at once, so the debug callback can read it as curl's tool reads <c>CURLINFO_CONN_ID</c> for
    /// <c>--trace-ids</c> (BL-2364, ADR-0547).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="state">The running transfer, which keeps the <c>%{conn_id}</c>.</param>
    /// <returns>The events.</returns>
    private EngineConnectionIdTransferEvents EventsOf(EasyOptions options, EngineTransferState state) =>
        new(
            WithTraceComponentEvents(
                (ITransferEvents?)DebugCallbackTransferEvents.Of(options.Behaviour.Verbose, options.Callbacks.Debug) ?? NoTransferEvents.Instance,
                options,
                TraceComponents),
            state,
            shared.ConnectionIdsByPoolNumber,
            () => state.ConnectionId ??= shared.Performer.TakeConnectionId(),
            connectionId => Info = Info with { ConnectionId = connectionId });

    /// <summary>
    /// Makes the connections the running perform opens in the engine's cache the handle's own, which
    /// <see cref="DisposeAsync" /> closes, when the perform runs outside any multi handle: libcurl's
    /// <c>curl_easy_perform</c> keeps them in the handle's own connection cache, which <c>curl_easy_cleanup</c> closes,
    /// so an FTP control connection sends <c>QUIT</c> then (upstream test661, BL-2629). A multi handle's connections are
    /// its own, and a share handle sharing connections keeps them in its own cache. The owner is set in the caller's
    /// asynchronous flow only (<see cref="Curl.Networking.ConnectionCache.OpeningOwner" />). The curl tool's transfers (<see cref="ToolSettings" />
    /// set) own none, as its run's connections outlive each transfer's handle.
    /// </summary>
    private void OwnTheConnectionsOpened() =>
        shared.Connections.OpeningOwner = ToolSettings is null && !shared.MultiOwners.ContainsKey(this) ? this : null;

    /// <summary>
    /// Wraps <paramref name="events" /> in the decorators the curl tool's runner adds under the same
    /// components (<c>CurlCommandRunner.WithTraceLineEvents</c>), in its order: under <c>timer</c> the response
    /// wait's <c>[TIMER]</c> lines through <see cref="ResponseWaitTimerTraceEvents" />, under <c>read</c>
    /// <see cref="ClientReaderResetTraceEvents" />, and outermost, under <c>write</c>,
    /// <see cref="ClientWriterTraceEvents" />. The engine has no multi of its own, so the <c>[TIMER]</c> lines are
    /// the ones the runner writes with the multi not traced, and the <c>[MULTI]</c> lines stay the tool's (ADR-0545).
    /// </summary>
    /// <param name="events">The transfer's own events.</param>
    /// <param name="options">The handle's options, whose <c>CURLOPT_TIMEOUT</c> and <c>CURLOPT_CONNECTTIMEOUT</c> are the timers.</param>
    /// <param name="traces">The trace components.</param>
    /// <returns>The events the transfer reports to.</returns>
    internal static ITransferEvents WithTraceComponentEvents(ITransferEvents events, EasyOptions options, EngineTraceComponents traces)
    {
        IReadOnlyList<string> waitLines = EngineComposition.TracesTimer(traces)
            ? TransferTimers.Of(options.Network, options.Transfer).WaitLines(tracesExpiry: false)
            : [];
        ITransferEvents timer = waitLines.Count > 0 ? new ResponseWaitTimerTraceEvents(events, waitLines) : events;
        ITransferEvents readers = EngineComposition.TracesRead(traces) ? new ClientReaderResetTraceEvents(timer) : timer;
        return EngineComposition.TracesWrite(traces) ? new ClientWriterTraceEvents(readers) : readers;
    }

    /// <summary>
    /// The finished transfer's <c>%{conn_id}</c>, as the runner gives it: -1 when it failed before
    /// connecting (an unsupported scheme or a malformed URL), else the connection's it opened or
    /// reused, or else the next connection number.
    /// </summary>
    /// <param name="result">The attempt's result, or <see langword="null" /> when the progress callback aborted it.</param>
    /// <param name="state">The running transfer.</param>
    /// <returns>The <c>%{conn_id}</c>.</returns>
    private long ConnectionIdOf(TransferResult? result, EngineTransferState state) =>
        FailedBeforeConnecting(result)
            ? NoConnectionId
            : state.ConnectionId ?? shared.Performer.TakeConnectionId();

    /// <summary>
    /// The finished transfer's <c>%{conn_id}</c> under the curl tool's runner's <paramref name="toolEvents" />, as its
    /// <c>-w</c> output prints it (BL-2495): -1 when it failed before connecting; a <c>--retry</c> attempt that reused
    /// the retried attempt's connection keeps that attempt's (<see cref="TransferPerformer.KeepRetriedConnectionIdWhenReused" />);
    /// else the connection's the runner's recorder kept, or else the runner's next, kept on the state so the runner
    /// reads the same number and takes no other.
    /// </summary>
    /// <param name="result">The attempt's result, or <see langword="null" /> when a callback aborted it.</param>
    /// <param name="state">The running transfer.</param>
    /// <param name="toolEvents">The runner's events, whose numbering gives a transfer that took none its <c>%{conn_id}</c>.</param>
    /// <returns>The <c>%{conn_id}</c>.</returns>
    private long ToolConnectionIdOf(TransferResult? result, EngineTransferState state, EngineToolTransferEvents toolEvents) =>
        FailedBeforeConnecting(result) ? NoConnectionId : ToolConnectionIdAfterConnectingOf(result, state, toolEvents);

    /// <summary>
    /// <see cref="ToolConnectionIdOf" /> for a transfer that got as far as connecting: the retried attempt's when the
    /// attempt reused its connection, else the one the state holds, else the runner's next, kept on the state.
    /// </summary>
    /// <param name="result">The attempt's result, or <see langword="null" /> when a callback aborted it.</param>
    /// <param name="state">The running transfer.</param>
    /// <param name="toolEvents">The runner's events, whose numbering gives a transfer that took none its <c>%{conn_id}</c>.</param>
    /// <returns>The <c>%{conn_id}</c>.</returns>
    private long ToolConnectionIdAfterConnectingOf(TransferResult? result, EngineTransferState state, EngineToolTransferEvents toolEvents)
    {
        if (result is not null)
        {
            TransferPerformer.KeepRetriedConnectionIdWhenReused(state, LastContext!, result);
        }

        return state.ConnectionId ??= toolEvents.TakeConnectionId();
    }

    /// <summary>Whether the transfer failed before connecting: an unsupported scheme or a malformed URL.</summary>
    /// <param name="result">The attempt's result, or <see langword="null" /> when a callback aborted it.</param>
    /// <returns><see langword="true" /> when it took no connection.</returns>
    private static bool FailedBeforeConnecting(TransferResult? result) =>
        result is { ExitCode: CurlExitCode.UnsupportedProtocol or CurlExitCode.UrlMalformat };

    /// <summary>
    /// <c>CURLINFO_CONTENT_LENGTH_UPLOAD</c> of a performed transfer: the upload's declared size -
    /// <c>CURLOPT_INFILESIZE</c> under <c>CURLOPT_UPLOAD</c>,
    /// <c>CURLOPT_POSTFIELDSIZE</c> or else the length of <c>CURLOPT_POSTFIELDS</c> for a post, and -1,
    /// unknown, for anything else.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The declared size, or -1.</returns>
    internal static long ContentLengthUploadOf(EasyOptions options) =>
        options.Transfer.Upload ? options.Transfer.InFileSize
        : options.Http.PostFields is { } fields && options.Http.PostFieldSize < 0 ? fields.Length
        : options.Http.PostFields is not null || options.Http.Post ? options.Http.PostFieldSize
        : -1;

    /// <summary>
    /// Records a performed transfer's outcome: the result's code, message and <see cref="Info" />, or
    /// <see cref="CurlCode.AbortedByCallback" /> with <paramref name="abortedText" /> when the progress or read
    /// callback aborted it. <see cref="TransferInfo.CookieList" /> is the cookie engine's list either way.
    /// </summary>
    /// <param name="result">The attempt's result, or <see langword="null" /> when a callback aborted it.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <param name="connectionId">The transfer's <c>%{conn_id}</c>.</param>
    /// <param name="abortedText">The text of an aborted transfer (<see cref="AbortedTextOf" />).</param>
    /// <param name="toolState">
    /// The curl tool's runner's running transfer, whose answers <see cref="WithToolAnswers" /> lays on the info, or
    /// <see langword="null" /> for a handle's own.
    /// </param>
    /// <returns>The transfer's code.</returns>
    private CurlCode FinishPerformed(TransferResult? result, EasyOptions options, long transferId, CookieEngine? cookies, long connectionId, string abortedText, EngineTransferState? toolState)
    {
        string urlText = options.Network.Url!;
        IReadOnlyList<string> cookieList = cookies?.ListLines ?? [];
        if (result is null)
        {
            return Finish(
                CurlCode.AbortedByCallback,
                abortedText,
                new TransferInfo { EffectiveUrl = urlText, TransferId = transferId, ConnectionId = connectionId, CookieList = cookieList });
        }

        return Finish(
            result.ExitCode.ToCurlCode(),
            result.ErrorMessage ?? string.Empty,
            PerformedInfoOf(result, options, transferId, cookieList, connectionId, toolState));
    }

    /// <summary>
    /// The info of a transfer that performed: what <see cref="TransferInfoFromReport.Of" /> fills from its report, the
    /// curl tool's runner's request URL standing in for the URL set when it gave one, with its IDs, cookie list and
    /// declared upload size, and the runner's answers (<see cref="WithToolAnswers" />) when it gave <paramref name="toolState" />.
    /// </summary>
    /// <param name="result">The attempt's result.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cookieList">The cookie engine's list.</param>
    /// <param name="connectionId">The transfer's <c>%{conn_id}</c>.</param>
    /// <param name="toolState">The runner's running transfer, or <see langword="null" /> for a handle's own.</param>
    /// <returns>The info.</returns>
    private TransferInfo PerformedInfoOf(TransferResult result, EasyOptions options, long transferId, IReadOnlyList<string> cookieList, long connectionId, EngineTransferState? toolState)
    {
        string requestUrl = toolState?.RequestUrl ?? options.Network.Url!;
        TransferInfo info = TransferInfoFromReport.Of(result, requestUrl, options.Transfer.FileTime, shared.TimeProvider, options.Tls.GatherCertificateInfo) with
        {
            TransferId = transferId,
            ConnectionId = connectionId,
            CookieList = cookieList,
            ContentLengthUpload = ContentLengthUploadOf(options),
        };
        return toolState is null ? info : WithToolAnswers(info, result, options, toolState, requestUrl);
    }

    /// <summary>
    /// The info of a transfer the curl tool's runner performed, with the answers its <c>-w</c> output prints in place of
    /// the report's (BL-2495): the scheme of <paramref name="requestUrl" /> (<see cref="TransferInfoFromReport.RequestSchemeOf" />),
    /// the <c>Referer</c> the last request was sent with else <c>CURLOPT_REFERER</c>, and the verify results and early
    /// data the runner's recorder kept on <paramref name="toolState" />.
    /// </summary>
    /// <param name="info">The info from the report.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="toolState">The runner's running transfer.</param>
    /// <param name="requestUrl">The URL the runner transfers.</param>
    /// <returns>The info.</returns>
    private static TransferInfo WithToolAnswers(TransferInfo info, TransferResult result, EasyOptions options, EngineTransferState toolState, string requestUrl) =>
        info with
        {
            Scheme = TransferInfoFromReport.RequestSchemeOf(requestUrl, result),
            Referer = info.Referer ?? options.Http.Referer,
            SslVerifyResult = toolState.SslVerifyResult,
            ProxySslVerifyResult = toolState.ProxySslVerifyResult,
            EarlyDataSent = toolState.TlsEarlyDataSent,
        };

    /// <summary>
    /// Performs the attempt with <see cref="TransferPerformer.FollowWatchingSpeedAsync" />, unless the
    /// progress callback aborts it: then, whether the cancel stopped the attempt or it finished first, the
    /// transfer is libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> (ADR-0543), as it is when the read callback
    /// returned <c>CURL_READFUNC_ABORT</c> (ADR-0573).
    /// </summary>
    /// <param name="follower">Performs the attempt, following redirects.</param>
    /// <param name="state">The running transfer, which keeps the attempt's watchdogs.</param>
    /// <param name="context">The attempt's context.</param>
    /// <param name="redirectPolicy">The redirect policy.</param>
    /// <param name="progress">The progress callback's sink, or <see langword="null" /> for none.</param>
    /// <param name="reads">The read callbacks' streams the transfer reads: the upload's and the <c>curl_mime_data_cb</c> parts'.</param>
    /// <returns>The attempt's result, or <see langword="null" /> when a callback aborted it.</returns>
    private static async Task<TransferResult?> FollowUnlessAbortedAsync(
        RedirectFollower follower,
        EngineTransferState state,
        TransferContext context,
        RedirectPolicy redirectPolicy,
        CallbackTransferProgress? progress,
        IReadOnlyList<CallbackReadStream> reads)
    {
        TransferResult result;
        try
        {
            result = await TransferPerformer.FollowWatchingSpeedAsync(state, follower, context, redirectPolicy).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (progress is { Aborted: true })
        {
            return null;
        }

        return progress is { Aborted: true } || reads.Any(read => read.Aborted) ? null : result;
    }

    /// <inheritdoc />
    public IEasyTransfer Duplicate() => new ManagedEasyTransfer(createDispatcher, shared) { cookieListItemsApplied = cookieListItemsApplied, Options = Options, ToolSettings = ToolSettings, Host = Host, TraceComponents = TraceComponents, CreateFormBoundary = CreateFormBoundary, CreateConnectOnlyConnector = CreateConnectOnlyConnector, WebSocketRandomSource = WebSocketRandomSource };

    /// <inheritdoc />
    public void Reset()
    {
        headers.Clear();
        Options = new EasyOptions();
        ToolSettings = null;
        ToolInputs = null;
        ToolOutputs = null;
        ToolEvents = null;
        ToolProgress = null;
        ToolDispatch = null;
    }

    /// <summary>
    /// <c>curl_easy_pause</c>: records the directions to leave paused, and a transfer waiting on a callback's
    /// pause in a direction now clear hands the callback its data again (ADR-0573). The call always succeeds.
    /// </summary>
    /// <param name="directions">The directions to leave paused.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public CurlCode Pause(CurlPause directions)
    {
        pauseGate.Set(directions);
        return CurlCode.Ok;
    }

    /// <summary>
    /// <c>curl_easy_upkeep</c>: the engine's pooled connections need no keep-alive frames of their own
    /// (no HTTP/2 PING is sent on idle connections yet), so there is nothing to do.
    /// </summary>
    /// <param name="cancellationToken">Stops the upkeep.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => ValueTask.FromResult(CurlCode.Ok);

    /// <summary>
    /// <c>curl_easy_cleanup</c>: hands the HSTS cache to <c>CURLOPT_HSTSWRITEFUNCTION</c> (<see cref="SaveHstsToCallback" />),
    /// takes the attached share handle's cookie lock for libcurl's cookie flush and detaches from it, then closes the
    /// connection a connect-only perform left open or upgraded, if any, and the handle's own idle pooled connections
    /// (<see cref="OwnTheConnectionsOpened" />), as libcurl closes the handle's connection cache, FTP's sending <c>QUIT</c>.
    /// </summary>
    /// <returns>A task that completes once they are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        SaveHstsToCallback();
        optionShare?.LockCookies(this, 1);
        optionShare = ManagedShareHandle.Reattach(optionShare, named: null, this);
        await (connectOnly?.CloseAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        await (webSocket?.CloseAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        await shared.Connections.CloseIdleOwnedByAsync(this).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the transfer's context with <paramref name="toolDispatch" />'s factory, or without one a
    /// factory with no standard input over the engine's clock, the caches' HSTS store and <see cref="Host" />'s
    /// diagnostic log, and keeps it as <see cref="LastContext" />.
    /// </summary>
    /// <param name="toolDispatch">The runner's follower and context factory, or <see langword="null" /> for the handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="progress">Where the progress goes, or <see langword="null" />.</param>
    /// <param name="events">Where the events go.</param>
    /// <param name="inputs">The pieces chosen before connecting.</param>
    /// <param name="state">The running transfer, which keeps the watchdogs.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The context.</returns>
    private TransferContext CreateContext(
        EngineToolTransferDispatch? toolDispatch,
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        ITransferProgress? progress,
        ITransferEvents events,
        EngineToolTransferInputs inputs,
        EngineTransferState state,
        CancellationToken cancellationToken)
    {
        EngineToolTransferOutputs outputs = ToolOutputs ?? OwnOutputsOf(options.Callbacks, PauseGateFor(url), RecordsHeaders(url) ? headers : null);
        TransferContextFactory factory = toolDispatch?.ContextFactory ?? new TransferContextFactory(Stream.Null, shared.TimeProvider) { HstsStore = PerformShared.Hsts, DiagnosticLog = Host.DiagnosticLog };
        return LastContext = factory.Create(
            settings,
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            url,
            outputs.Output,
            range: inputs.Range,
            resumeFrom: inputs.ResumeFrom,
            headerOutput: outputs.HeaderOutput,
            formBody: inputs.FormBody,
            upload: inputs.Upload,
            proxy: inputs.Proxy,
            watchHeaderOutput: outputs.WatchHeaderOutput,
            progress: progress,
            events: events,
            lowSpeedWatchdog: state.AttemptLowSpeedWatchdog,
            maxTimeWatchdog: state.AttemptMaxTimeWatchdog,
            lookedUpCredentials: inputs.LookedUpCredentials,
            ifNoneMatchHeaders: inputs.IfNoneMatchHeaders,
            altSvc: inputs.AltSvc,
            bodyHeaderStyles: outputs.BodyHeaderStyles,
            abortToken: cancellationToken,
            ssh: inputs.Ssh);
    }

    /// <summary>
    /// The outputs a handle with no <see cref="ToolOutputs" /> writes to: the body to the write callback and the
    /// header lines to the header callback through <see cref="CallbackWriteStream" />, the body discarded and the
    /// header lines dropped when the callback is unset; with <paramref name="headerStore" />, every header line is
    /// recorded there first (<see cref="HeaderRecordingStream" />).
    /// </summary>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="pauseGate">The pause state a callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <param name="headerStore">Where the header lines are recorded, or <see langword="null" /> to record none.</param>
    /// <returns>The outputs.</returns>
    private static EngineToolTransferOutputs OwnOutputsOf(EasyCallbackOptions callbacks, EasyPauseGate? pauseGate, TransferHeaderStore? headerStore)
    {
        Stream? headerCallback = callbacks.Header is { } header ? CallbackWriteStream.Of(header, pauseGate) : null;
        return new(
            callbacks.Write is { } write ? CallbackWriteStream.Of(write, pauseGate) : Stream.Null,
            HeaderOutput: headerStore is null ? headerCallback : new HeaderRecordingStream(headerStore, headerCallback));
    }

    /// <summary>
    /// Whether a transfer of <paramref name="url" /> records its headers for the header API: only HTTP's and
    /// WebSocket's, as libcurl keeps only HTTP headers; other schemes' header lines (FTP's server replies and the
    /// like) are not <c>name: value</c> headers (ADR-0603).
    /// </summary>
    /// <param name="url">The URL transferred.</param>
    /// <returns>Whether its headers are recorded.</returns>
    private static bool RecordsHeaders(CurlUrl url) => url.Scheme.ToLowerInvariant() is "http" or "https" or "ws" or "wss";

    /// <summary>
    /// The upload source of a <see cref="Api.Options.Behaviour.EasyTransferOptions.Upload" /> transfer: the read callback, or
    /// nothing to read when none is set, as the engine has no standard input of its own; the host hands
    /// standard input or a <c>-T</c> file over as a read callback (ADR-0542). With a seek callback too, the
    /// upload is a <see cref="SeekingCallbackReadStream" />, which a resend rewinds through it (BL-2417).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL to transfer: an <c>http</c> or <c>https</c> one sends <c>CURLOPT_INFILESIZE</c> bytes (<see cref="DeclaredUploadLengthOf" />).</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The upload stream, or <see langword="null" /> when the transfer uploads nothing.</returns>
    private Stream? UploadOf(EasyOptions options, CurlUrl url, EasyPauseGate? pauseGate) =>
        options.Transfer.Upload
            ? options.Callbacks.Read is { } read ? CallbackUploadOf(read, options.Callbacks, pauseGate, WebSocketConnection.UploadFrames, DeclaredUploadLengthOf(options, url)) : Stream.Null
            : null;

    /// <summary>
    /// The length a read-callback upload is held to (BL-2645): <c>CURLOPT_INFILESIZE</c> when it is known and the
    /// URL is <c>http</c> or <c>https</c>, so the upload stops there and goes with a <c>Content-Length</c>, as
    /// curl 8.22.0's <c>cr_in_read</c> stops at <c>total_len</c>; else <see langword="null" />, the upload ending when
    /// the callback returns 0 (ADR-0676).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <returns>The declared length, or <see langword="null" />.</returns>
    internal static long? DeclaredUploadLengthOf(EasyOptions options, CurlUrl url) =>
        options.Transfer.InFileSize >= 0 && (IsScheme(url, "http") || IsScheme(url, "https")) ? options.Transfer.InFileSize : null;

    /// <summary>Whether <paramref name="url" />'s scheme is <paramref name="scheme" />, in any case.</summary>
    /// <param name="url">The URL.</param>
    /// <param name="scheme">The scheme.</param>
    /// <returns><see langword="true" /> when it is.</returns>
    private static bool IsScheme(CurlUrl url, string scheme) => string.Equals(url.Scheme, scheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The read callback's upload stream: one the seek callback rewinds when one is set, else one the ioctl callback rewinds (BL-2607), carrying the trailer
    /// callback a chunked HTTP upload asks for its trailers (BL-2475).
    /// </summary>
    /// <param name="read">The read callback.</param>
    /// <param name="callbacks">The handle's callbacks, whose seek and trailer callbacks the stream takes.</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" />.</param>
    /// <param name="uploadFrames">The frames a WebSocket upload goes out in (<see cref="ManagedEasyWebSocket.UploadFrames" />), or <see langword="null" /> for a POST body.</param>
    /// <param name="declaredLength">The length the upload is held to (<see cref="CallbackReadStream.DeclaredLength" />), or <see langword="null" />.</param>
    /// <returns>The upload stream.</returns>
    private static CallbackReadStream CallbackUploadOf(ReadCallback read, EasyCallbackOptions callbacks, EasyPauseGate? pauseGate, WsUploadFrames? uploadFrames = null, long? declaredLength = null) =>
        (callbacks.Seek, callbacks.Ioctl) switch
        {
            ({ } seek, _) => new SeekingCallbackReadStream(read, seek, pauseGate) { Trailer = callbacks.Trailer, UploadFrames = uploadFrames, DeclaredLength = declaredLength },
            (null, { } ioctl) => new IoctlRewoundCallbackReadStream(read, ioctl, pauseGate) { Trailer = callbacks.Trailer, UploadFrames = uploadFrames, DeclaredLength = declaredLength },
            _ => new CallbackReadStream(read, pauseGate) { Trailer = callbacks.Trailer, UploadFrames = uploadFrames, DeclaredLength = declaredLength },
        };

    /// <summary>
    /// The curl tool's own settings for a transfer driven through the easy interface with no
    /// <see cref="ToolSettings" />: what the easy groups say (<c>CURLOPT_SSL_VERIFYPEER</c>,
    /// <c>CURLOPT_KEYPASSWD</c>, <c>CURLOPT_SSLENGINE</c>, <c>CURLOPT_PROTOCOLS_STR</c>,
    /// <c>CURLOPT_REDIR_PROTOCOLS_STR</c>, <c>CURLOPT_DIRLISTONLY</c>, <c>CURLOPT_PROXYHEADER</c> (none under <c>CURLHEADER_UNIFIED</c>, whose request carries <c>CURLOPT_HTTPHEADER</c> alone),
    /// <c>CURLOPT_HTTPPROXYTUNNEL</c>, <c>CURLOPT_UNIX_SOCKET_PATH</c>), and libcurl's defaults for the
    /// rest, which are the tool's alone (ADR-0541, ADR-0542). <c>CURLOPT_FAILONERROR</c> needs nothing here:
    /// <see cref="HttpRequestOptionsMapping" /> reads it from the behaviour group.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The settings.</returns>
    internal static EngineTransferSettings TransferSettingsOf(EasyOptions options) =>
        new(
            Silent: true,
            ShowError: false,
            FailMode: HttpFailMode.None,
            CreateFileMode: null,
            Insecure: !options.Tls.Origin.VerifyPeer,
            Passphrase: options.Tls.Origin.KeyPassword,
            Engine: options.Tls.Engine,
            EchModeIsMalformed: false,
            PostData: null,
            ProxyCredentials: ProxyCredentialsOf(options.Authentication),
            AllowedProtocols: ProtocolSetOf(options.Network.Protocols),
            AllowedRedirectProtocols: ProtocolSetOf(options.Network.RedirectProtocols),
            SslLevel: TransportSecurityLevel.None,
            FtpSslControlOnly: false,
            SslRequired: false,
            ListOnly: options.Transfer.DirListOnly,
            UploadFlags: [],
            ProxyHeaders: options.Proxy.HeaderOption == HeaderOption.Unified ? [] : options.Proxy.ProxyHeaders,
            ProxyTunnel: options.Proxy.HttpProxyTunnel,
            OverUnixSocket: options.Network.UnixSocketPath is not null,
            HappyEyeballsTimeout: HttpRequestOptionsMapping.HappyEyeballsTimeoutOf(null),
            AppendUrlQuery: url => url);

    /// <summary>
    /// The proxy credentials <c>CURLOPT_PROXYUSERPWD</c>, <c>CURLOPT_PROXYUSERNAME</c> and <c>CURLOPT_PROXYPASSWORD</c>
    /// give, as the curl tool's <c>-U</c> gives them: <c>CURLOPT_PROXYUSERPWD</c> split at its first colon (no colon is
    /// an empty password), the user name and password options each winning over their half of it. They replace the
    /// proxy URL's own user information, as in libcurl (upstream test1588, BL-2660, ADR-0677).
    /// </summary>
    /// <param name="authentication">The handle's authentication options.</param>
    /// <returns>The credentials, or <see langword="null" /> when none of the three is set.</returns>
    internal static System.Net.NetworkCredential? ProxyCredentialsOf(EasyAuthenticationOptions authentication) =>
        authentication is { ProxyUserPassword: null, ProxyUserName: null, ProxyPassword: null } ? null : ProxyCredentialOf(authentication);

    private static System.Net.NetworkCredential ProxyCredentialOf(EasyAuthenticationOptions authentication)
    {
        (string user, string password) = SplitAtFirstColon(authentication.ProxyUserPassword ?? string.Empty);
        return new System.Net.NetworkCredential(authentication.ProxyUserName ?? user, authentication.ProxyPassword ?? password);
    }

    private static (string User, string Password) SplitAtFirstColon(string userPassword)
    {
        int colon = userPassword.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (userPassword, string.Empty) : (userPassword[..colon], userPassword[(colon + 1)..]);
    }

    /// <summary>
    /// The schemes a <c>CURLOPT_PROTOCOLS_STR</c> or <c>CURLOPT_REDIR_PROTOCOLS_STR</c> list names, lower
    /// cased with blanks around each name dropped.
    /// </summary>
    /// <param name="protocols">The comma-separated list, or <see langword="null" /> when not set.</param>
    /// <returns>The schemes, or <see langword="null" /> for libcurl's default.</returns>
    internal static IReadOnlySet<string>? ProtocolSetOf(string? protocols) =>
        protocols?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(protocol => protocol.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private CurlCode Finish(CurlCode code, string errorText, TransferInfo info)
    {
        ErrorText = errorText;
        Info = info;
        return code;
    }
}
