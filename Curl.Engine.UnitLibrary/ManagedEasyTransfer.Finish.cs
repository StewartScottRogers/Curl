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

// Finishing a perform: connection ids, performed info and the result code.
internal sealed partial class ManagedEasyTransfer
{
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
    /// <param name="transferUrl">
    /// The URL the setup chose: the URL set, or the <c>https</c> one HSTS switched it to, which the effective URL falls back to
    /// when the report carries none, as a connect that failed after the switch leaves it (upstream test1922, BL-2676).
    /// </param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cookies">The transfer's cookies, or <see langword="null" /> when no cookie option is set.</param>
    /// <param name="connectionId">The transfer's <c>%{conn_id}</c>.</param>
    /// <param name="abortedText">The text of an aborted transfer (<see cref="AbortedTextOf" />).</param>
    /// <param name="toolState">
    /// The curl tool's runner's running transfer, whose answers <see cref="WithToolAnswers" /> lays on the info, or
    /// <see langword="null" /> for a handle's own.
    /// </param>
    /// <returns>The transfer's code.</returns>
    private CurlCode FinishPerformed(TransferResult? result, EasyOptions options, string transferUrl, long transferId, CookieEngine? cookies, long connectionId, string abortedText, EngineTransferState? toolState)
    {
        IReadOnlyList<string> cookieList = cookies?.ListLines ?? [];
        if (result is null)
        {
            return Finish(
                CurlCode.AbortedByCallback,
                abortedText,
                new TransferInfo { EffectiveUrl = transferUrl, TransferId = transferId, ConnectionId = connectionId, CookieList = cookieList });
        }

        return Finish(
            result.ExitCode.ToCurlCode(),
            result.ErrorMessage ?? string.Empty,
            PerformedInfoOf(result, options, transferUrl, transferId, cookieList, connectionId, toolState));
    }

    /// <summary>
    /// The <c>WWW-Authenticate</c> values of <paramref name="result" />'s last response when it is a 401, else none.
    /// </summary>
    /// <param name="result">The transfer's result, or <see langword="null" /> for none.</param>
    /// <returns>The challenges.</returns>
    internal static IReadOnlyList<string> ChallengesOfLast401(TransferResult? result) =>
        result is { Report: { ResponseCode: 401 } report }
            ? [.. report.ResponseHeaders.Where(header => string.Equals(header.Key, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase)).Select(header => header.Value.Trim())]
            : [];

    /// <summary>
    /// The info of a transfer that performed: what <see cref="TransferInfoFromReport.Of" /> fills from its report, the
    /// curl tool's runner's request URL standing in for the URL set when it gave one, with its IDs, cookie list and
    /// declared upload size, the <c>Referer</c> the last request was sent with else <c>CURLOPT_REFERER</c> (BL-3257), and the runner's answers (<see cref="WithToolAnswers" />) when it gave <paramref name="toolState" />.
    /// </summary>
    /// <param name="result">The attempt's result.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="transferUrl">The URL the setup chose (<see cref="FinishPerformed" />).</param>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="cookieList">The cookie engine's list.</param>
    /// <param name="connectionId">The transfer's <c>%{conn_id}</c>.</param>
    /// <param name="toolState">The runner's running transfer, or <see langword="null" /> for a handle's own.</param>
    /// <returns>The info.</returns>
    private TransferInfo PerformedInfoOf(TransferResult result, EasyOptions options, string transferUrl, long transferId, IReadOnlyList<string> cookieList, long connectionId, EngineTransferState? toolState)
    {
        string requestUrl = toolState?.RequestUrl ?? transferUrl;
        TransferInfo info = TransferInfoFromReport.Of(result, requestUrl, options.Transfer.FileTime, shared.TimeProvider, options.Tls.GatherCertificateInfo) with
        {
            TransferId = transferId,
            ConnectionId = connectionId,
            CookieList = cookieList,
            ContentLengthUpload = ContentLengthUploadOf(options),
        };
        info = info with { PrimaryPort = PrimaryPortOf(info, result), Referer = info.Referer ?? options.Http.Referer };
        return toolState is null
            ? info with { EffectiveUrl = UrlEffective.Normalize(info.EffectiveUrl, options.Network.PathAsIs) }
            : WithToolAnswers(info, result, options, toolState, requestUrl);
    }

    /// <summary>
    /// The <c>CURLINFO_PRIMARY_PORT</c> of a performed transfer: -1 for a failed one that reached no peer, as libcurl
    /// 8.22.0 answers after a refused connect or a failed name lookup (measured 2026-10-22, BL-3195 Notes), else the
    /// port <paramref name="info" /> took from the report.
    /// </summary>
    /// <param name="info">The info from the report.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The port.</returns>
    internal static long PrimaryPortOf(TransferInfo info, TransferResult result) =>
        info.PrimaryPort == 0 && result.ExitCode != CurlExitCode.Ok ? -1 : info.PrimaryPort;

    /// <summary>
    /// The info of a transfer the curl tool's runner performed, with the answers its <c>-w</c> output prints in place of
    /// the report's (BL-2495): the scheme of <paramref name="requestUrl" /> (<see cref="TransferInfoFromReport.RequestSchemeOf" />),
    /// and the verify results and early
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
            SslVerifyResult = toolState.SslVerifyResult,
            ProxySslVerifyResult = toolState.ProxySslVerifyResult,
            EarlyDataSent = toolState.TlsEarlyDataSent,
        };

    /// <summary>
    /// Performs the attempt with <see cref="TransferPerformer.FollowWatchingSpeedAsync" />, unless the
    /// progress callback aborts it: then, whether the cancel stopped the attempt or it finished first, the
    /// transfer is libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> (ADR-0543), as it is when the read callback
    /// returned <c>CURL_READFUNC_ABORT</c> (ADR-0573), whether the handler mapped the read's
    /// <see cref="IOException" /> to a result or let it escape (BL-2640).
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
        catch (IOException) when (AnyReadAborted(reads))
        {
            // A handler that does not map the read's failure itself (SMTP's after DATA) lets it escape; the
            // abort still ends the perform with CURLE_ABORTED_BY_CALLBACK (upstream test1507, BL-2640).
            return null;
        }

        return progress is { Aborted: true } || AnyReadAborted(reads) ? null : result;
    }

    /// <summary>Whether any of <paramref name="reads" /> returned <c>CURL_READFUNC_ABORT</c>.</summary>
    /// <param name="reads">The read callbacks' streams the transfer reads.</param>
    /// <returns><see langword="true" /> when one aborted.</returns>
    private static bool AnyReadAborted(IReadOnlyList<CallbackReadStream> reads) => reads.Any(read => read.Aborted);

    /// <summary>
    /// Puts the response code and <c>Content-Length</c> the header lines have given so far on <see cref="Info" /> while
    /// the transfer runs, before the line reaches the header callback, so a header callback reading
    /// <c>CURLINFO_RESPONSE_CODE</c> or <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c> sees them as libcurl's does once the
    /// head is parsed (upstream test1485, BL-2639). It also puts the times so far there, as libcurl's are live during the
    /// transfer (upstream test1541, curl issue #13125, BL-2648): the connect and pretransfer times from the perform's start
    /// to the connection taken (0 when the curl tool's runner numbers the connections, as the handle then sees none), the
    /// starttransfer time to the first head line (kept from then on) and the total time to now. The finished transfer's
    /// report replaces them all.
    /// </summary>
    private void ShowResponseHeadOnInfo()
    {
        TimeSpan sinceStart = shared.TimeProvider.GetElapsedTime(performStartedTimestamp);
        TimeSpan connect = connectedTimestamp is { } connected ? shared.TimeProvider.GetElapsedTime(performStartedTimestamp, connected) : TimeSpan.Zero;
        TransferInfo info = Info;
        Info = info with
        {
            ResponseCode = headers.ResponseCode,
            ContentLengthDownload = headers.ContentLength,
            ConnectTime = connect,
            PreTransferTime = connect,
            StartTransferTime = info.StartTransferTime > TimeSpan.Zero ? info.StartTransferTime : sinceStart,
            TotalTime = sinceStart,
        };
    }

    private CurlCode Finish(CurlCode code, string errorText, TransferInfo info)
    {
        ErrorText = errorText;
        Info = info;
        return code;
    }
}
