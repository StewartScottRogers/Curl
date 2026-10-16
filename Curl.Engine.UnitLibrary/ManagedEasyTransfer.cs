using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Core;
using Curl.Protocol.Abstractions;

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
/// shared with its other handles (ADR-0542); while a transfer performs, <see cref="Info" /> holds its
/// <c>%{xfer_id}</c> from the start and its <c>%{conn_id}</c> once it connects (-1 before), for the debug
/// callback's <c>--trace-ids</c> prefix (ADR-0547). The certificate verify codes the transfer reports
/// reach <see cref="Info" /> through <see cref="TlsVerifyResultRecordingTransferEvents" /> (BL-2366). Around the attempt it does the runner's setup and cleanup:
/// the <c>-b</c> files, <c>--alt-svc</c> and <c>--hsts</c> files read before it and the <c>-c</c> jar,
/// alt-svc and HSTS files written after it, through <see cref="Host" /> (ADR-0544). The header, WebSocket and connect-only
/// APIs are phase 3 work and throw <see cref="NotSupportedException" />.
/// </summary>
/// <param name="createDispatcher">Builds a transfer's dispatcher from its options, the engine's shared state and the cookie store its HTTP handler keeps.</param>
/// <param name="shared">What the engine's handles share.</param>
internal sealed class ManagedEasyTransfer(
    Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, ProtocolDispatcher> createDispatcher,
    ManagedEngineSharedState shared) : IToolAttemptTransfer
{
    /// <summary>libcurl's <c>CURLE_URL_MALFORMAT</c> text for a perform with no URL set.</summary>
    internal const string NoUrlSetText = "No URL set";

    /// <summary>libcurl's <c>CURLE_URL_MALFORMAT</c> text for a URL it cannot parse.</summary>
    internal const string MalformedUrlText = "URL rejected: Malformed input to a URL function";

    /// <inheritdoc />
    public EasyOptions Options { get; set; } = new();

    /// <inheritdoc />
    public TransferInfo Info { get; private set; } = new();

    /// <inheritdoc />
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>The handle's pause state, which <see cref="Pause" /> sets and a pausing callback waits on (ADR-0569).</summary>
    private readonly EasyPauseGate pauseGate = new();

    /// <summary>The directions left paused, by <see cref="Pause" /> or by a callback that returned a pause.</summary>
    internal CurlPause PausedDirections => pauseGate.Directions;

    /// <summary>
    /// Gets or sets the curl tool's own settings for the handle's transfers - <c>-s</c>, <c>-S</c>,
    /// <c>--fail-with-body</c> and the rest that <see cref="TransferSettingsOf" /> can only default - for
    /// <c>Curl.Console</c> to set from its command line once the handle builds the runner's serial attempts from the options (BL-2394, ADR-0542); <see langword="null" />, as for every
    /// <c>Curl.Api</c> and <c>Libcurl</c> handle, to take them from <see cref="Options" />.
    /// </summary>
    internal EngineTransferSettings? ToolSettings { get; set; }

    /// <summary>
    /// Gets or sets what the handle's transfers reach outside the engine for their setup and cleanup -
    /// the file system the <c>-b</c>, <c>-c</c>, <c>--alt-svc</c> and <c>--hsts</c> files go through,
    /// standard input for <c>-b -</c>, the <c>-c -</c> print target and the diagnostic log (ADR-0544) -
    /// for <c>Curl.Console</c> to set from its runner once the handle builds its serial attempts (BL-2394); <see cref="EasyTransferHost.Library" />,
    /// as for every <c>Curl.Api</c> and <c>Libcurl</c> handle, by default. <see cref="Duplicate" /> copies it
    /// and <see cref="Reset" /> leaves it, as it is the host's wiring, not an option.
    /// </summary>
    internal EasyTransferHost Host { get; set; } = EasyTransferHost.Library;

    /// <summary>
    /// Gets or sets the run's <c>--trace-config</c> components, libcurl's <c>curl_global_trace</c> setting,
    /// which add the <c>[READ]</c>, <c>[WRITE]</c> and <c>[TIMER]</c> lines to the debug callback's events
    /// (<see cref="WithTraceComponentEvents" />), for <c>Curl.Console</c> to set from its command line (BL-2394,
    /// ADR-0545); <see cref="EngineTraceComponents.None" /> by default. <see cref="Duplicate" /> copies it and
    /// <see cref="Reset" /> leaves it, as it is a global setting, not an option.
    /// </summary>
    internal EngineTraceComponents TraceComponents { get; set; } = EngineTraceComponents.None;

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
    internal EngineToolTransferInputs? ToolInputs { get; set; }

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
    internal EngineToolTransferOutputs? ToolOutputs { get; set; }

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
    internal EngineToolTransferEvents? ToolEvents { get; set; }

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
    internal EngineToolTransferProgress? ToolProgress { get; set; }

    /// <summary>
    /// Gets the whole result of the last perform, for the curl tool's <c>--retry</c>, failure lines and <c>-w</c>,
    /// which read more of it than <see cref="Info" /> carries (ADR-0554); <see langword="null" /> before the first
    /// perform, after one that failed before connecting (no URL set, or one that does not parse), and after one
    /// the progress callback aborted.
    /// </summary>
    internal TransferResult? LastResult { get; private set; }

    /// <inheritdoc />
    public ToolAttempt? ToolAttempt { get; set; }

    /// <inheritdoc />
    public TransferResult? ToolAttemptResult { get; private set; }

    /// <summary>The <c>%{conn_id}</c> of a transfer that failed before it could connect.</summary>
    internal const long NoConnectionId = -1;

    /// <summary>Not supported yet: the header API is phase 3's headers work (ADR-0506).</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ITransferHeaders Headers => throw new NotSupportedException("The header API is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: the WebSocket API is phase 3's WebSocket work (ADR-0506).</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IEasyWebSocket WebSocket => throw new NotSupportedException("The WebSocket API is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: <c>curl_easy_recv</c> and <c>curl_easy_send</c> are phase 3's connect-only work (ADR-0506).</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IConnectOnlyConnection Connection => throw new NotSupportedException("The connect-only API is phase 3 work (ADR-0506).");

    /// <inheritdoc />
    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) =>
        ToolAttempt is { } attempt
            ? await PerformToolAttemptAsync(attempt).ConfigureAwait(false)
            : await PerformFromOptionsAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Runs one transfer from <see cref="Options" />, as <see cref="PerformAsync" /> describes.</summary>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code.</returns>
    private async Task<CurlCode> PerformFromOptionsAsync(CancellationToken cancellationToken)
    {
        EasyOptions options = Options;
        LastResult = null;
        if (options.Network.Url is not { } urlText)
        {
            return Finish(CurlCode.UrlMalformat, NoUrlSetText, new TransferInfo());
        }

        if (!CurlUrl.TryParse(urlText, options.Network.PathAsIs, out CurlUrl? url))
        {
            return Finish(CurlCode.UrlMalformat, MalformedUrlText, new TransferInfo { EffectiveUrl = urlText });
        }

        EngineTransferSettings settings = ToolSettings ?? TransferSettingsOf(options);
        long transferId = shared.Performer.TakeTransferId();
        Info = new TransferInfo { EffectiveUrl = urlText, TransferId = transferId, ConnectionId = NoConnectionId };
        CookieEngine? cookies = CookieEngine.FromHttpOptions(options.Http, shared.Cookies);
        (EngineTransferState state, TlsVerifyResultRecordingTransferEvents events) = StateAndEventsOf(ToolEvents, options);
        EngineToolTransferInputs? toolInputs = ToolInputs;
        string transferUrl = await SetUpUnlessToolDidAsync(toolInputs, options, settings, cookies, state, urlText, events).ConfigureAwait(false);
        RedirectFollower follower = new(createDispatcher(options, shared, cookies?.HandlerStore));
        EngineToolTransferInputs inputs = InputsOf(toolInputs, options, state, PauseGateFor(url));
        CallbackReadStream? callbackUpload = inputs.Upload as CallbackReadStream;
        EngineToolTransferProgress? toolProgress = ToolProgress;
        using CancellationTokenSource abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, AbortTokenOf(toolProgress));
        CallbackTransferProgress? callbackProgress = CallbackProgressUnlessToolGave(toolProgress, options, abort);
        TransferResult? result = await FollowUnlessAbortedAsync(
            follower,
            state,
            CreateContext(
                options,
                settings,
                TransferUrlOf(transferUrl, urlText, url, options),
                ProgressStartingWatchdogsOf(toolProgress, callbackProgress, options, state),
                events,
                inputs,
                state,
                abort.Token),
            RedirectPolicyMapping.Of(settings, options.Http, options.Authentication),
            callbackProgress,
            callbackUpload).ConfigureAwait(false);
        await CleanUpUnlessToolDoesAsync(toolInputs, options, settings, cookies, state, transferUrl).ConfigureAwait(false);
        LastResult = events.ReportOn(result);
        return FinishPerformed(LastResult, options, transferId, cookies, ConnectionIdOf(result, state), AbortedTextOf(callbackUpload));
    }

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
                cookies, settings, options.Http, transferUrl, Host.JarPrintTarget, state, shared.Hsts, Host.FileSystem, Host.RunsOnWindows).ConfigureAwait(false);
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
    /// <param name="state">The running transfer, which keeps the alt-svc cache.</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The pieces.</returns>
    private static EngineToolTransferInputs InputsOf(EngineToolTransferInputs? toolInputs, EasyOptions options, EngineTransferState state, EasyPauseGate? pauseGate) =>
        toolInputs ?? OwnInputsOf(options, state, pauseGate);

    /// <summary>
    /// The pause state a callback's pause waits on: the handle's, but none for a <c>file</c> URL, which libcurl cannot
    /// pause (<c>PROTOPT_NONETWORK</c>), so a pause there fails as libcurl's does (ADR-0569).
    /// </summary>
    /// <param name="url">The URL set.</param>
    /// <returns>The pause state, or <see langword="null" />.</returns>
    private EasyPauseGate? PauseGateFor(CurlUrl url) =>
        string.Equals(url.Scheme, "file", StringComparison.OrdinalIgnoreCase) ? null : pauseGate;

    /// <summary>
    /// The text of a perform a callback aborted: libcurl's <c>operation aborted by callback</c> when the read callback
    /// returned <c>CURL_READFUNC_ABORT</c>, else the progress callback's <c>Callback aborted</c>.
    /// </summary>
    /// <param name="upload">The read callback's stream, or <see langword="null" /> when the transfer reads none.</param>
    /// <returns>The text.</returns>
    private static string AbortedTextOf(CallbackReadStream? upload) =>
        upload is { Aborted: true } ? CallbackReadStream.AbortedText : CallbackTransferProgress.CallbackAbortedText;

    /// <summary>
    /// The pieces a handle with no <see cref="ToolInputs" /> chooses from its options: no proxy, credentials, SSH
    /// options, <c>If-None-Match</c> lines, range or form body of the runner's; <c>CURLOPT_RESUME_FROM</c> when
    /// positive; the read callback under <c>CURLOPT_UPLOAD</c> (<see cref="UploadOf" />); and the alt-svc cache its
    /// own setup opened.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="state">The running transfer, which keeps the alt-svc cache.</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The pieces.</returns>
    private static EngineToolTransferInputs OwnInputsOf(EasyOptions options, EngineTransferState state, EasyPauseGate? pauseGate) =>
        new(
            ResumeFrom: options.Transfer.ResumeFrom > 0 ? options.Transfer.ResumeFrom : null,
            Upload: UploadOf(options, pauseGate),
            AltSvc: state.AltSvc);

    /// <summary>
    /// Performs an attempt the curl tool's runner set up (ADR-0552) under its watchdogs
    /// (<see cref="TransferPerformer.FollowWatchingSpeedAsync" />), keeps its whole result as
    /// <see cref="ToolAttemptResult" />, and records its code, message and <see cref="Info" />, the request URL
    /// being <see cref="Options" />' and the <c>%{conn_id}</c> the running transfer's (-1 before it connects).
    /// </summary>
    /// <param name="attempt">The attempt; cleared from <see cref="ToolAttempt" /> before it runs.</param>
    /// <returns>The attempt's code.</returns>
    private async Task<CurlCode> PerformToolAttemptAsync(ToolAttempt attempt)
    {
        ToolAttempt = null;
        EasyOptions options = Options;
        TransferResult result = await TransferPerformer.FollowWatchingSpeedAsync(attempt.State, attempt.Follower, attempt.Context, attempt.RedirectPolicy).ConfigureAwait(false);
        ToolAttemptResult = result;
        LastResult = result;
        return Finish(
            result.ExitCode.ToCurlCode(),
            result.ErrorMessage ?? string.Empty,
            TransferInfoFromReport.Of(result, options.Network.Url ?? string.Empty, options.Transfer.FileTime, shared.TimeProvider, options.Tls.GatherCertificateInfo) with
            {
                ConnectionId = attempt.State.ConnectionId ?? NoConnectionId,
            });
    }

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
        await shared.Performer.LoadCookieFilesAsync(cookies, settings, urlText, host.FileSystem, host.StandardInput, events).ConfigureAwait(false);
        await shared.Performer.OpenAltSvcCacheAsync(state, settings, options.Network, options.Http, urlText, host.FileSystem, host.DiagnosticLog, host.AltSvcUsesOverHttp).ConfigureAwait(false);
        return await TransferPerformer.SwitchToHttpsForHstsAsync(shared.Hsts, settings, options.Network, options.Http, urlText, host.FileSystem, events).ConfigureAwait(false);
    }

    /// <summary>
    /// The running transfer and the events it reports to, kept by <see cref="TlsVerifyResultRecordingTransferEvents" />
    /// for <c>CURLINFO_SSL_VERIFYRESULT</c>: the runner's <paramref name="toolEvents" /> and their state as they are,
    /// whose recorder numbers <c>%{conn_id}</c>, or a new state and the handle's own events (<see cref="EventsOf" />)
    /// without them (ADR-0561).
    /// </summary>
    /// <param name="toolEvents">The runner's events and state, or <see langword="null" /> for a handle's own.</param>
    /// <param name="options">The handle's options.</param>
    /// <returns>The state and the events.</returns>
    private (EngineTransferState State, TlsVerifyResultRecordingTransferEvents Events) StateAndEventsOf(EngineToolTransferEvents? toolEvents, EasyOptions options)
    {
        if (toolEvents is not null)
        {
            return (toolEvents.State, new TlsVerifyResultRecordingTransferEvents(toolEvents.Events));
        }

        EngineTransferState state = new();
        return (state, new TlsVerifyResultRecordingTransferEvents(EventsOf(options, state)));
    }

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
        result is { ExitCode: CurlExitCode.UnsupportedProtocol or CurlExitCode.UrlMalformat }
            ? NoConnectionId
            : state.ConnectionId ?? shared.Performer.TakeConnectionId();

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
    /// <returns>The transfer's code.</returns>
    private CurlCode FinishPerformed(TransferResult? result, EasyOptions options, long transferId, CookieEngine? cookies, long connectionId, string abortedText)
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
            TransferInfoFromReport.Of(result, urlText, options.Transfer.FileTime, shared.TimeProvider, options.Tls.GatherCertificateInfo) with
            {
                TransferId = transferId,
                ConnectionId = connectionId,
                CookieList = cookieList,
                ContentLengthUpload = ContentLengthUploadOf(options),
            });
    }

    /// <summary>
    /// Performs the attempt with <see cref="TransferPerformer.FollowWatchingSpeedAsync" />, unless the
    /// progress callback aborts it: then, whether the cancel stopped the attempt or it finished first, the
    /// transfer is libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> (ADR-0543), as it is when the read callback
    /// returned <c>CURL_READFUNC_ABORT</c> (ADR-0569).
    /// </summary>
    /// <param name="follower">Performs the attempt, following redirects.</param>
    /// <param name="state">The running transfer, which keeps the attempt's watchdogs.</param>
    /// <param name="context">The attempt's context.</param>
    /// <param name="redirectPolicy">The redirect policy.</param>
    /// <param name="progress">The progress callback's sink, or <see langword="null" /> for none.</param>
    /// <param name="upload">The read callback's stream, or <see langword="null" /> when the transfer reads none.</param>
    /// <returns>The attempt's result, or <see langword="null" /> when a callback aborted it.</returns>
    private static async Task<TransferResult?> FollowUnlessAbortedAsync(
        RedirectFollower follower,
        EngineTransferState state,
        TransferContext context,
        RedirectPolicy redirectPolicy,
        CallbackTransferProgress? progress,
        CallbackReadStream? upload)
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

        return progress is { Aborted: true } || upload is { Aborted: true } ? null : result;
    }

    /// <inheritdoc />
    public IEasyTransfer Duplicate() => new ManagedEasyTransfer(createDispatcher, shared) { Options = Options, ToolSettings = ToolSettings, Host = Host, TraceComponents = TraceComponents };

    /// <inheritdoc />
    public void Reset()
    {
        Options = new EasyOptions();
        ToolSettings = null;
        ToolInputs = null;
        ToolOutputs = null;
        ToolEvents = null;
        ToolProgress = null;
    }

    /// <summary>
    /// <c>curl_easy_pause</c>: records the directions to leave paused, and a transfer waiting on a callback's
    /// pause in a direction now clear hands the callback its data again (ADR-0569). The call always succeeds.
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

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private TransferContext CreateContext(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        ITransferProgress? progress,
        ITransferEvents events,
        EngineToolTransferInputs inputs,
        EngineTransferState state,
        CancellationToken cancellationToken)
    {
        EngineToolTransferOutputs outputs = ToolOutputs ?? OwnOutputsOf(options.Callbacks, PauseGateFor(url));
        return new TransferContextFactory(Stream.Null, shared.TimeProvider) { HstsStore = shared.Hsts, DiagnosticLog = Host.DiagnosticLog }.Create(
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
    /// header lines dropped when the callback is unset.
    /// </summary>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="pauseGate">The pause state a callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The outputs.</returns>
    private static EngineToolTransferOutputs OwnOutputsOf(EasyCallbackOptions callbacks, EasyPauseGate? pauseGate) =>
        new(
            callbacks.Write is { } write ? CallbackWriteStream.Of(write, pauseGate) : Stream.Null,
            HeaderOutput: callbacks.Header is { } header ? CallbackWriteStream.Of(header, pauseGate) : null);

    /// <summary>
    /// The upload source of a <see cref="Api.Options.Behaviour.EasyTransferOptions.Upload" /> transfer: the read callback, or
    /// nothing to read when none is set, as the engine has no standard input of its own; the host hands
    /// standard input or a <c>-T</c> file over as a read callback (ADR-0542).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="pauseGate">The pause state a read callback's pause waits on, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The upload stream, or <see langword="null" /> when the transfer uploads nothing.</returns>
    private static Stream? UploadOf(EasyOptions options, EasyPauseGate? pauseGate) =>
        options.Transfer.Upload
            ? options.Callbacks.Read is { } read ? new CallbackReadStream(read, pauseGate) : Stream.Null
            : null;

    /// <summary>
    /// The curl tool's own settings for a transfer driven through the easy interface with no
    /// <see cref="ToolSettings" />: what the easy groups say (<c>CURLOPT_SSL_VERIFYPEER</c>,
    /// <c>CURLOPT_KEYPASSWD</c>, <c>CURLOPT_SSLENGINE</c>, <c>CURLOPT_PROTOCOLS_STR</c>,
    /// <c>CURLOPT_REDIR_PROTOCOLS_STR</c>, <c>CURLOPT_DIRLISTONLY</c>, <c>CURLOPT_PROXYHEADER</c>,
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
            ProxyCredentials: null,
            AllowedProtocols: ProtocolSetOf(options.Network.Protocols),
            AllowedRedirectProtocols: ProtocolSetOf(options.Network.RedirectProtocols),
            SslLevel: TransportSecurityLevel.None,
            FtpSslControlOnly: false,
            SslRequired: false,
            ListOnly: options.Transfer.DirListOnly,
            UploadFlags: [],
            ProxyHeaders: options.Proxy.ProxyHeaders,
            ProxyTunnel: options.Proxy.HttpProxyTunnel,
            OverUnixSocket: options.Network.UnixSocketPath is not null,
            HappyEyeballsTimeout: HttpRequestOptionsMapping.HappyEyeballsTimeoutOf(null),
            AppendUrlQuery: url => url);

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
