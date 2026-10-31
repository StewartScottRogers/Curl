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
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

// A perform's set-up and clean-up: tool or own inputs, alt-svc, cookies, state and events.
internal sealed partial class ManagedEasyTransfer
{
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
    /// <returns>The URL to transfer, and the failure that ends the perform before anything is sent, or <see langword="null" />.</returns>
    private async Task<(string TransferUrl, TransferResult? Failure)> SetUpUnlessToolDidAsync(
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
            return (urlText, null);
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
            handleCookieJar = cookies is not null && IsHttpUrl(settings.AppendUrlQuery(transferUrl)) ? cookies : handleCookieJar;
            await shared.Performer.WriteCookieJarAltSvcAndHstsFilesAsync(
                null, settings, options.Http, transferUrl, Host.JarPrintTarget, state, PerformShared.Hsts, Host.FileSystem, Host.RunsOnWindows, writesAltSvc: false).ConfigureAwait(false);
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
    /// <see cref="TryLookUpUrlAndNetrcCredentials" /> chose from the URL and the <c>.netrc</c> file (BL-2696, BL-2684); no <c>If-None-Match</c> lines, range or form body of the runner's; <c>CURLOPT_RESUME_FROM</c> when
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
            LookedUpCredentials: lookedUpCredentials,
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
    /// <returns>
    /// The URL to transfer: <paramref name="urlText" /> itself, or the <c>https</c> one HSTS switched it to; and
    /// <see langword="null" />, or exit 100 when a cookie file held a line longer than 4998 characters, which ends the
    /// perform before the alt-svc and HSTS files are read and before anything is sent, as libcurl 8.22.0's
    /// <c>Curl_pretransfer</c> fails with <c>Curl_cookie_loadfiles</c>' <c>CURLE_TOO_LARGE</c> (BL-2978).
    /// </returns>
    private async Task<(string TransferUrl, TransferResult? Failure)> SetUpAsync(
        EasyOptions options,
        EngineTransferSettings settings,
        CookieEngine? cookies,
        EngineTransferState state,
        string urlText,
        ITransferEvents events)
    {
        EasyTransferHost host = Host;
        await ApplyCookieListAsync(cookies, host, events).ConfigureAwait(false);
        if (await shared.Performer.LoadCookieFilesAsync(cookies, settings, urlText, host.FileSystem, host.StandardInput, events).ConfigureAwait(false) is { } tooLarge)
        {
            return (urlText, tooLarge);
        }

        await OpenOrKeepAltSvcCacheAsync(options, settings, state, urlText, host).ConfigureAwait(false);
        return (await TransferPerformer.SwitchToHttpsForHstsAsync(PerformShared.Hsts, settings, options.Network, options.Http, urlText, host.FileSystem, events).ConfigureAwait(false), null);
    }

    /// <summary>
    /// Gives the transfer the handle's alt-svc cache: the one an earlier perform opened (libcurl keeps the handle's cache
    /// across performs and <c>curl_easy_reset</c>, and saves it only at <c>curl_easy_cleanup</c>, BL-2671), else a new one
    /// opened from <c>CURLOPT_ALTSVC</c>, which the handle then keeps.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings.</param>
    /// <param name="state">The running transfer, which keeps the cache.</param>
    /// <param name="urlText">The URL set.</param>
    /// <param name="host">What the file system, the diagnostic log and <c>CURL_ALTSVC_HTTP</c> come from.</param>
    /// <returns>A task that completes once the transfer holds the cache.</returns>
    private async Task OpenOrKeepAltSvcCacheAsync(EasyOptions options, EngineTransferSettings settings, EngineTransferState state, string urlText, EasyTransferHost host)
    {
        if (handleAltSvc is null)
        {
            await shared.Performer.OpenAltSvcCacheAsync(state, settings, options.Network, options.Http, urlText, host.FileSystem, host.DiagnosticLog, host.AltSvcUsesOverHttp).ConfigureAwait(false);
            handleAltSvc = state.AltSvc;
            return;
        }

        state.AltSvc = handleAltSvc;
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
    /// verify codes, so the handler sees the runner's own events and nothing records here (BL-2394), except that under
    /// <c>-Y</c>/<c>-y</c> they pass through <see cref="AbortedHeadRecordingTransferEvents" /> alone, so a low-speed abort
    /// keeps its response code, HTTP version and sizes for <c>-w</c> as curl does (BL-3673); or a new state and the
    /// handle's own events (<see cref="EventsOf" />) kept by <see cref="TlsVerifyResultRecordingTransferEvents" /> for
    /// <c>CURLINFO_SSL_VERIFYRESULT</c> without them (ADR-0561).
    /// </summary>
    /// <param name="toolEvents">The runner's events and state, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <returns>The state and the events.</returns>
    private (EngineTransferState State, ITransferEvents Events, TlsVerifyResultRecordingTransferEvents? Recorder, NegotiateFailureRecordingTransferEvents? NegotiateFailure, AbortedHeadRecordingTransferEvents? AbortedHead) StateAndEventsOf(EngineToolTransferEvents? toolEvents, EasyOptions options)
    {
        if (toolEvents is not null)
        {
            AbortedHeadRecordingTransferEvents? toolAbortedHead = WatchesLowSpeed(options.Transfer) ? new(toolEvents.Events) : null;
            return (toolEvents.State, toolAbortedHead ?? toolEvents.Events, null, null, toolAbortedHead);
        }

        EngineTransferState state = new();
        AbortedHeadRecordingTransferEvents abortedHead = new(EventsOf(options, state));
        NegotiateFailureRecordingTransferEvents negotiateFailure = new(abortedHead);
        TlsVerifyResultRecordingTransferEvents recorder = new(negotiateFailure);
        return (state, recorder, recorder, negotiateFailure, abortedHead);
    }

    /// <summary>Tells whether <c>-Y</c> or <c>-y</c> is set, so a <c>-Y</c>/<c>-y</c> watchdog may end the transfer.</summary>
    /// <param name="transfer">The transfer's options.</param>
    /// <returns><see langword="true" /> when the low-speed limit or time is above zero.</returns>
    private static bool WatchesLowSpeed(EasyTransferOptions transfer) =>
        transfer.LowSpeedLimit > 0 || transfer.LowSpeedTime > TimeSpan.Zero;

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
    /// The transfer's events: the debug callback's under <c>CURLOPT_VERBOSE</c>, or libcurl's default lines on
    /// <c>CURLOPT_STDERR</c>, else <see cref="EasyTransferHost.StandardError" />, when none is set (BL-2726), else none, with the
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
                (ITransferEvents?)DebugCallbackTransferEvents.Of(options.Behaviour.Verbose, options.Callbacks.Debug, () => options.Behaviour.StandardError ?? Host.StandardError.Value) ?? NoTransferEvents.Instance,
                options,
                TraceComponents),
            state,
            shared.ConnectionIdsByPoolNumber,
            () => state.ConnectionId ??= shared.Performer.TakeConnectionId(),
            ShowConnectionOnInfo,
            ShowConnectionEndPointsOnInfo);

    /// <summary>
    /// Puts the end points of a connection the running perform opened on <see cref="Info" /> at once and counts it in
    /// <see cref="TransferInfo.NumConnects" />, as libcurl fills <c>CURLINFO_PRIMARY_IP</c>, <c>CURLINFO_LOCAL_IP</c> and
    /// their ports once it connects, so a transfer a callback aborts still answers them (CGR-128, BL-3657).
    /// </summary>
    /// <param name="opened">The connection opened.</param>
    private void ShowConnectionEndPointsOnInfo(ConnectionOpenedEvent opened) => Info = WithOpenedConnection(Info, opened);

    /// <summary>
    /// <paramref name="info" /> with <paramref name="opened" />'s end points (<see cref="WithConnectionEndPoints" />), its
    /// <see cref="ConnectionOpenedEvent.UnixSocketRemoteIp" /> as the primary IP when it went over a Unix socket.
    /// </summary>
    /// <param name="info">The info so far.</param>
    /// <param name="opened">The connection opened.</param>
    /// <returns>The info with the connection.</returns>
    internal static TransferInfo WithOpenedConnection(TransferInfo info, ConnectionOpenedEvent opened)
    {
        TransferInfo connected = WithConnectionEndPoints(info, opened.RemoteEndPoint, opened.LocalEndPoint);
        return opened.UnixSocketRemoteIp is { } unixSocketIp ? connected with { PrimaryIp = unixSocketIp } : connected;
    }

    /// <summary>
    /// Puts the connection the running perform took on <see cref="Info" /> at once (BL-2364) and notes when it was
    /// taken, the first time, for the live <see cref="TransferInfo.ConnectTime" /> (<see cref="ShowResponseHeadOnInfo" />).
    /// </summary>
    /// <param name="connectionId">The connection's <c>%{conn_id}</c>.</param>
    private void ShowConnectionOnInfo(long connectionId)
    {
        connectedTimestamp ??= shared.TimeProvider.GetTimestamp();
        Info = Info with { ConnectionId = connectionId };
    }

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
}
