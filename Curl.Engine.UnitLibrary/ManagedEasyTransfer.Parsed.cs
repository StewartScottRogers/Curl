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

// The parsed perform: kept handle state, redirect follower, dispatchers and progress watchdogs.
internal sealed partial class ManagedEasyTransfer
{
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
        (EngineTransferState state, ITransferEvents events, TlsVerifyResultRecordingTransferEvents? recorder, NegotiateFailureRecordingTransferEvents? negotiateFailure, AbortedHeadRecordingTransferEvents? abortedHead) = StateAndEventsOf(toolEvents, options);
        (string transferUrl, TransferResult? setUpFailure) = await SetUpUnlessToolDidAsync(toolInputs, options, settings, cookies, state, urlText, events).ConfigureAwait(false);
        if (setUpFailure is not null)
        {
            return Finish(setUpFailure.ExitCode.ToCurlCode(), setUpFailure.ErrorMessage!, Info);
        }

        EngineToolTransferDispatch? toolDispatch = ToolDispatch;
        RedirectFollower follower = FollowerOf(toolDispatch, options, cookies);
        EngineToolTransferInputs inputs = MimePostForm.WithBody(InputsOf(toolInputs, options, settings, url, state, PauseGateFor(url), selectedProxy), formBody, url.Scheme);
        IReadOnlyList<CallbackReadStream> callbackReads = inputs.Upload is CallbackReadStream upload ? [.. mimeReads, upload] : mimeReads;
        EngineToolTransferProgress? toolProgress = ToolProgress;
        using CancellationTokenSource abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, AbortTokenOf(toolProgress));
        CallbackTransferProgress? callbackProgress = CallbackProgressUnlessToolGave(toolProgress, options, abort, () => options.Behaviour.StandardError ?? Host.StandardError.Value, shared.TimeProvider);
        pauseGate.TickWhilePaused(CallbackTransferProgress.PausedTickOf(callbackProgress), shared.TimeProvider);
        TransferResult? result = await FollowFinishingMeterAsync(
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
        KeepHandleStateOf(recorder, negotiateFailure, abortedHead is null ? result : abortedHead.ReportOn(result), url);
        long connectionId = toolEvents is null ? ConnectionIdOf(result, state) : ToolConnectionIdOf(result, state, toolEvents);
        return FinishPerformed(LastResult, options, transferUrl, transferId, cookies, connectionId, AbortedTextOf(callbackReads), toolEvents?.State);
    }

    /// <summary>
    /// Performs the attempt as <see cref="FollowUnlessAbortedAsync" /> does, then ends the built-in progress meter
    /// <paramref name="progress" /> drew (<see cref="CallbackTransferProgress.FinishMeter" />), even when the attempt
    /// throws, so the meter's clock always stops (BL-4057, ADR-0993).
    /// </summary>
    /// <param name="follower">Performs the attempt, following redirects.</param>
    /// <param name="state">The running transfer, which keeps the attempt's watchdogs.</param>
    /// <param name="context">The attempt's context.</param>
    /// <param name="redirectPolicy">The redirect policy.</param>
    /// <param name="progress">The progress callback's sink, or <see langword="null" /> for none.</param>
    /// <param name="reads">The read callbacks' streams the transfer reads.</param>
    /// <returns>The attempt's result, or <see langword="null" /> when a callback aborted it.</returns>
    private static async Task<TransferResult?> FollowFinishingMeterAsync(
        RedirectFollower follower,
        EngineTransferState state,
        TransferContext context,
        RedirectPolicy redirectPolicy,
        CallbackTransferProgress? progress,
        IReadOnlyList<CallbackReadStream> reads)
    {
        TransferResult? result = null;
        try
        {
            result = await FollowUnlessAbortedAsync(follower, state, context, redirectPolicy, progress, reads).ConfigureAwait(false);
            return result;
        }
        finally
        {
            progress?.FinishMeter(result?.IsSuccess == true);
        }
    }

    /// <summary>
    /// Keeps what the handle carries from a finished perform to the next: <see cref="LastResult" /> (with the
    /// verify results <paramref name="recorder" /> kept, when there is one), the Digest challenges of a final 401 and
    /// the origin they came from, and the scheme the server picked (<see cref="keptAuthSchemePicked" />).
    /// </summary>
    /// <param name="recorder">The verify-result recorder, or <see langword="null" /> when the runner's events report.</param>
    /// <param name="negotiateFailure">The recorder of the first SSPI failure line, or <see langword="null" /> when the runner's events report.</param>
    /// <param name="result">The perform's result, or <see langword="null" /> after an abort.</param>
    /// <param name="url">The URL performed.</param>
    private void KeepHandleStateOf(TlsVerifyResultRecordingTransferEvents? recorder, NegotiateFailureRecordingTransferEvents? negotiateFailure, TransferResult? result, CurlUrl url)
    {
        LastResult = recorder is null ? result : negotiateFailure!.ReportOn(recorder.ReportOn(result));
        keptDigestChallenges = ChallengesOfLast401(result);
        keptDigestStaysAvailable = result?.Report?.DigestStaysAvailable ?? false;
        keptDigestOrigin = OriginOf(url);
        keptAuthSchemePicked = AuthSchemeKeptAfter(result, keptAuthSchemePicked);
    }

    /// <summary>The scheme the handle keeps after a perform: its report's picked scheme, else the one kept before.</summary>
    /// <param name="result">The perform's result, or <see langword="null" /> after an abort.</param>
    /// <param name="kept">The scheme kept before the perform.</param>
    /// <returns>The scheme to keep.</returns>
    internal static HttpAuthSchemes AuthSchemeKeptAfter(TransferResult? result, HttpAuthSchemes kept) =>
        result?.Report?.AuthSchemePicked ?? kept;

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
            _ => createDispatcher(options, PerformShared, HandlerCookieStoreOf(cookies), PushSinkOf(), rtspSession, WebSocketConnection.PerformConnection),
        }, selectHopCredentials: hopCredentials);

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
            ? await MimePostForm.BuildAsync(new MultipartFormBodyBuilder(Host.FileSystem, Encoding.UTF8, CreateFormBoundary) { SevenBitRefusalWaitsForSend = true, MissingFileWaitsForSend = true }, options.Http, url.Scheme, options.Callbacks.Read, mimeReads, PauseGateFor(url), Host.StandardInput, cancellationToken).ConfigureAwait(false)
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
    /// <param name="standardError">The handle's standard error, for the built-in meter.</param>
    /// <param name="timeProvider">The clock the built-in meter draws on.</param>
    /// <returns>The sink, or <see langword="null" /> for none.</returns>
    private static CallbackTransferProgress? CallbackProgressUnlessToolGave(EngineToolTransferProgress? toolProgress, EasyOptions options, CancellationTokenSource abort, Func<Stream> standardError, TimeProvider timeProvider) =>
        toolProgress is null ? CallbackTransferProgress.Of(options.Behaviour.NoProgress, options.Callbacks, abort, standardError, timeProvider) : null;

    /// <summary>
    /// The progress the context reports to, after starting the attempt's <c>-Y</c>/<c>-y</c> watchdog on
    /// <paramref name="state" /> for every handle, so a library handle's <c>CURLOPT_LOW_SPEED_LIMIT</c> and
    /// <c>CURLOPT_LOW_SPEED_TIME</c> end a too-slow transfer with <c>CURLE_OPERATION_TIMEDOUT</c> as libcurl 8.22.0
    /// does (BL-3196): the runner's <paramref name="toolProgress" />, after starting the <c>-m</c> watchdog too as the
    /// runner does when it creates each attempt's context, so every perform, a <c>--retry</c> attempt's too, gets
    /// fresh ones; else <paramref name="callbackProgress" /> (ADR-0567).
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
        shared.Performer.StartLowSpeedWatchdog(state, options.Transfer, Host.DiagnosticLog);
        if (toolProgress is null)
        {
            return callbackProgress;
        }

        shared.Performer.StartMaxTimeWatchdog(state, options.Transfer, Host.DiagnosticLog);
        return toolProgress.Progress;
    }
}
