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
/// shared with its other handles (ADR-0542). Around the attempt it does the runner's setup and cleanup:
/// the <c>-b</c> files, <c>--alt-svc</c> and <c>--hsts</c> files read before it and the <c>-c</c> jar,
/// alt-svc and HSTS files written after it, through <see cref="Host" /> (ADR-0544). The header, WebSocket and connect-only
/// APIs are phase 3 work and throw <see cref="NotSupportedException" />.
/// </summary>
/// <param name="createDispatcher">Builds a transfer's dispatcher from its options, the engine's shared state and the cookie store its HTTP handler keeps.</param>
/// <param name="shared">What the engine's handles share.</param>
internal sealed class ManagedEasyTransfer(
    Func<EasyOptions, ManagedEngineSharedState, ICookieStore?, ProtocolDispatcher> createDispatcher,
    ManagedEngineSharedState shared) : IEasyTransfer
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

    /// <summary>The directions <see cref="Pause" /> last left paused.</summary>
    internal CurlPause PausedDirections { get; private set; }

    /// <summary>
    /// Gets or sets the curl tool's own settings for the handle's transfers - <c>-s</c>, <c>-S</c>,
    /// <c>--fail-with-body</c> and the rest that <see cref="TransferSettingsOf" /> can only default - for
    /// <c>Curl.Console</c> to set from its command line once its runner performs through the engine (BL-2318, ADR-0542); <see langword="null" />, as for every
    /// <c>Curl.Api</c> and <c>Libcurl</c> handle, to take them from <see cref="Options" />.
    /// </summary>
    internal EngineTransferSettings? ToolSettings { get; set; }

    /// <summary>
    /// Gets or sets what the handle's transfers reach outside the engine for their setup and cleanup -
    /// the file system the <c>-b</c>, <c>-c</c>, <c>--alt-svc</c> and <c>--hsts</c> files go through,
    /// standard input for <c>-b -</c>, the <c>-c -</c> print target and the diagnostic log (ADR-0544) -
    /// for <c>Curl.Console</c> to set from its runner (BL-2318); <see cref="EasyTransferHost.Library" />,
    /// as for every <c>Curl.Api</c> and <c>Libcurl</c> handle, by default. <see cref="Duplicate" /> copies it
    /// and <see cref="Reset" /> leaves it, as it is the host's wiring, not an option.
    /// </summary>
    internal EasyTransferHost Host { get; set; } = EasyTransferHost.Library;

    /// <summary>
    /// Gets or sets the run's <c>--trace-config</c> components, libcurl's <c>curl_global_trace</c> setting,
    /// which add the <c>[READ]</c>, <c>[WRITE]</c> and <c>[TIMER]</c> lines to the debug callback's events
    /// (<see cref="WithTraceComponentEvents" />), for <c>Curl.Console</c> to set from its command line (BL-2318,
    /// ADR-0545); <see cref="EngineTraceComponents.None" /> by default. <see cref="Duplicate" /> copies it and
    /// <see cref="Reset" /> leaves it, as it is a global setting, not an option.
    /// </summary>
    internal EngineTraceComponents TraceComponents { get; set; } = EngineTraceComponents.None;

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
    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        EasyOptions options = Options;
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
        CookieEngine? cookies = CookieEngine.FromHttpOptions(options.Http, shared.Cookies);
        EngineTransferState state = new();
        ITransferEvents events = EventsOf(options, state);
        string transferUrl = await SetUpAsync(options, settings, cookies, state, urlText, events).ConfigureAwait(false);
        RedirectFollower follower = new(createDispatcher(options, shared, cookies?.HandlerStore));
        using CancellationTokenSource abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CallbackTransferProgress? progress = CallbackTransferProgress.Of(options.Behaviour.NoProgress, options.Callbacks, abort);
        TransferResult? result = await FollowUnlessAbortedAsync(
            follower,
            state,
            CreateContext(options, settings, ReferenceEquals(transferUrl, urlText) ? url : CurlUrl.Parse(transferUrl, options.Network.PathAsIs), progress, events, state.AltSvc, abort.Token),
            RedirectPolicyMapping.Of(settings, options.Http, options.Authentication),
            progress).ConfigureAwait(false);
        await shared.Performer.WriteCookieJarAltSvcAndHstsFilesAsync(
            cookies, settings, options.Http, transferUrl, Host.JarPrintTarget, state, shared.Hsts, Host.FileSystem, Host.RunsOnWindows).ConfigureAwait(false);
        return FinishPerformed(result, options, transferId, cookies, ConnectionIdOf(result, state));
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
    /// The transfer's events: the debug callback's under <c>CURLOPT_VERBOSE</c>, else none, with the
    /// <see cref="TraceComponents" />' lines added (<see cref="WithTraceComponentEvents" />), kept by
    /// <see cref="EngineConnectionIdTransferEvents" />, which gives the transfer its <c>%{conn_id}</c>
    /// when it opens a connection and the opener's when it reuses one.
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
            () => state.ConnectionId ??= shared.Performer.TakeConnectionId());

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
    /// <see cref="CurlCode.AbortedByCallback" /> with libcurl's <c>Callback aborted</c> when the progress
    /// callback aborted it. <see cref="TransferInfo.CookieList" /> is the cookie engine's list either way.
    /// </summary>
    /// <param name="result">The attempt's result, or <see langword="null" /> when the progress callback aborted it.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <param name="connectionId">The transfer's <c>%{conn_id}</c>.</param>
    /// <returns>The transfer's code.</returns>
    private CurlCode FinishPerformed(TransferResult? result, EasyOptions options, long transferId, CookieEngine? cookies, long connectionId)
    {
        string urlText = options.Network.Url!;
        IReadOnlyList<string> cookieList = cookies?.ListLines ?? [];
        if (result is null)
        {
            return Finish(
                CurlCode.AbortedByCallback,
                CallbackTransferProgress.CallbackAbortedText,
                new TransferInfo { EffectiveUrl = urlText, TransferId = transferId, ConnectionId = connectionId, CookieList = cookieList });
        }

        return Finish(
            result.ExitCode.ToCurlCode(),
            result.ErrorMessage ?? string.Empty,
            TransferInfoFromReport.Of(result, urlText, options.Transfer.FileTime, shared.TimeProvider) with
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
    /// transfer is libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> (ADR-0543).
    /// </summary>
    /// <param name="follower">Performs the attempt, following redirects.</param>
    /// <param name="state">The running transfer, which keeps the attempt's watchdogs.</param>
    /// <param name="context">The attempt's context.</param>
    /// <param name="redirectPolicy">The redirect policy.</param>
    /// <param name="progress">The progress callback's sink, or <see langword="null" /> for none.</param>
    /// <returns>The attempt's result, or <see langword="null" /> when the progress callback aborted it.</returns>
    private static async Task<TransferResult?> FollowUnlessAbortedAsync(
        RedirectFollower follower,
        EngineTransferState state,
        TransferContext context,
        RedirectPolicy redirectPolicy,
        CallbackTransferProgress? progress)
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

        return progress is { Aborted: true } ? null : result;
    }

    /// <inheritdoc />
    public IEasyTransfer Duplicate() => new ManagedEasyTransfer(createDispatcher, shared) { Options = Options, ToolSettings = ToolSettings, Host = Host, TraceComponents = TraceComponents };

    /// <inheritdoc />
    public void Reset()
    {
        Options = new EasyOptions();
        ToolSettings = null;
    }

    /// <summary>
    /// <c>curl_easy_pause</c>: records the directions to leave paused. A handle outside a transfer has
    /// nothing to pause yet, so the call always succeeds, as libcurl's does on an idle handle.
    /// </summary>
    /// <param name="directions">The directions to leave paused.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public CurlCode Pause(CurlPause directions)
    {
        PausedDirections = directions;
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
        CallbackTransferProgress? progress,
        ITransferEvents events,
        AltSvcTransferCache? altSvc,
        CancellationToken cancellationToken) =>
        new TransferContextFactory(Stream.Null, shared.TimeProvider) { HstsStore = shared.Hsts, DiagnosticLog = Host.DiagnosticLog }.Create(
            settings,
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            url,
            options.Callbacks.Write is { } write ? CallbackWriteStream.Of(write) : Stream.Null,
            range: null,
            resumeFrom: options.Transfer.ResumeFrom > 0 ? options.Transfer.ResumeFrom : null,
            headerOutput: options.Callbacks.Header is { } header ? CallbackWriteStream.Of(header) : null,
            upload: UploadOf(options),
            progress: progress,
            events: events,
            altSvc: altSvc,
            abortToken: cancellationToken);

    /// <summary>
    /// The upload source of a <see cref="Api.Options.Behaviour.EasyTransferOptions.Upload" /> transfer: the read callback, or
    /// nothing to read when none is set, as the engine has no standard input of its own; the host hands
    /// standard input or a <c>-T</c> file over as a read callback (ADR-0542).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The upload stream, or <see langword="null" /> when the transfer uploads nothing.</returns>
    private static Stream? UploadOf(EasyOptions options) =>
        options.Transfer.Upload
            ? options.Callbacks.Read is { } read ? new CallbackReadStream(read) : Stream.Null
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
