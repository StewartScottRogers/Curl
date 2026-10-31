using System.Diagnostics.CodeAnalysis;
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
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Ws;

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
    /// <c>CURLINFO_CONTENT_LENGTH_UPLOAD</c> once an <c>http</c> or <c>https</c> request has been sent, as libcurl 8.22.0
    /// sets it from the last request's body (CGR-127): 0 for a request with no body (a plain <c>GET</c>,
    /// <c>CURLOPT_NOBODY</c>, <c>CURLOPT_HTTPGET</c>, or a post a followed redirect turned into a <c>GET</c>), -1 for a
    /// body sent chunked under a <c>Transfer-Encoding: chunked</c> header, else <see cref="ContentLengthUploadOf(EasyOptions)" />.
    /// An upload, a form body, another scheme or a transfer that sent no request keeps that answer.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="report">The transfer's report, or <see langword="null" />.</param>
    /// <param name="requestUrl">The URL the transfer requested.</param>
    /// <returns>The size, or -1.</returns>
    internal static long ContentLengthUploadOf(EasyOptions options, TransferReport? report, string requestUrl) =>
        SentAnHttpRequest(report, requestUrl) && HasNoUploadOrForm(options)
            ? HttpRequestBodyLengthOf(options, report)
            : ContentLengthUploadOf(options);

    private static bool SentAnHttpRequest([NotNullWhen(true)] TransferReport? report, string requestUrl) =>
        report is { RequestSize: > 0 } && IsHttpUrl(requestUrl);

    private static bool IsHttpUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static bool HasNoUploadOrForm(EasyOptions options) =>
        !options.Transfer.Upload && options.Http.MimePost is null && options.Http.HttpPost is null;

    private static long HttpRequestBodyLengthOf(EasyOptions options, TransferReport report)
    {
        long declared = ContentLengthUploadOf(options);
        return SendsNoBody(options, declared) || RedirectDroppedTheBody(report) ? 0
            : options.Http.HttpHeaders.Any(IsChunkedTransferEncoding) ? -1
            : declared;
    }

    private static bool SendsNoBody(EasyOptions options, long declared) =>
        (declared < 0 && !options.Http.Post) || options.Transfer.NoBody || (options.Http.HttpGet && !options.Http.Post);

    private static bool RedirectDroppedTheBody(TransferReport report) =>
        report.RedirectCount > 0 && report.Method is "GET" or "HEAD";

    private static bool IsChunkedTransferEncoding(string header) =>
        header.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase) && header.Contains("chunked", StringComparison.OrdinalIgnoreCase);

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
                TransferInfoFromReport.WithSchemeOf(
                    Info with { EffectiveUrl = transferUrl, EffectiveMethod = EffectiveMethodOf(options), TransferId = transferId, ConnectionId = connectionId, CookieList = cookieList },
                    transferUrl));
        }

        CurlCode code = result.ExitCode.ToCurlCode();
        return Finish(
            code,
            ErrorTextOf(code, result.ErrorMessage),
            PerformedInfoOf(result, options, transferUrl, transferId, cookieList, connectionId, toolState));
    }

    /// <summary>
    /// The text a failed perform leaves as <see cref="ErrorText" />: the handler's message, except that for
    /// <see cref="CurlCode.TooLarge" /> and <see cref="CurlCode.WriteError" /> a message that is only the code's stock
    /// text is dropped, as libcurl 8.22.0 returns those codes (an oversized header, a failed gzip trailer write) with no
    /// <c>failf</c>, leaving <c>CURLOPT_ERRORBUFFER</c> empty (BL-3318).
    /// </summary>
    /// <param name="code">The perform's code.</param>
    /// <param name="message">The handler's message, or <see langword="null" />.</param>
    /// <returns>The error text.</returns>
    internal static string ErrorTextOf(CurlCode code, string? message) =>
        message is null
        || (code == CurlCode.TooLarge && message == "A value or data field grew larger than allowed")
        || (code == CurlCode.WriteError && message == "Failed writing received data to disk/application")
            ? string.Empty
            : message;

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
        TransferInfo info = TransferInfoFromReport.Of(result, requestUrl, options.Transfer.FileTime, shared.TimeProvider, options.Tls.GatherCertificateInfo, options.Transfer.Upload, options.Http.IgnoreContentLength) with
        {
            TransferId = transferId,
            ConnectionId = connectionId,
            CookieList = cookieList,
            ContentLengthUpload = ContentLengthUploadOf(options, result.Report, requestUrl),
        };
        info = info with { SizeDelivered = lastWriteCallbackOutput?.BytesDelivered ?? info.SizeDownload };
        info = WithoutHeadWhenHeaderCallbackRefusedIt(info, result);
        info = info with { PrimaryPort = PrimaryPortOf(info, result), Referer = info.Referer ?? options.Http.Referer, ResponseCode = ResponseCodeOf(info, result) };
        info = info with { NumConnects = NumConnectsOf(info, result, options, requestUrl), HttpVersion = HttpVersionOf(info.HttpVersion, result, headers.HttpVersion) };
        return toolState is null
            ? info with { EffectiveUrl = UrlEffective.WithoutPortOverride(UrlEffective.Normalize(info.EffectiveUrl, options.Network.PathAsIs), transferUrl, Options.Network) }
            : WithToolAnswers(info, result, options, toolState, requestUrl);
    }

    /// <summary>
    /// Whether the transfer failed with <c>CURLE_WRITE_ERROR</c> because the header callback refused the status line: libcurl
    /// counts and parses a head only once the callback has taken its lines, so getinfo then reports no head (CGR-252).
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns><see langword="true" /> when the callback took no byte.</returns>
    private bool HeaderCallbackTookNothing(TransferResult result) =>
        result.ExitCode.ToCurlCode() == CurlCode.WriteError && lastHeaderCallbackOutput is { BytesDelivered: 0 };

    /// <summary><paramref name="info" /> with the head dropped when <see cref="HeaderCallbackTookNothing" /> (CGR-252).</summary>
    /// <param name="info">The info from the report.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The info.</returns>
    private TransferInfo WithoutHeadWhenHeaderCallbackRefusedIt(TransferInfo info, TransferResult result) =>
        HeaderCallbackTookNothing(result) ? info with { HeaderSize = 0, ContentType = null, ContentLengthDownload = -1 } : info;

    /// <summary>
    /// The <c>CURLINFO_RESPONSE_CODE</c> of a performed transfer: the report's, else - for a transfer a watchdog cut off
    /// with no report, such as a multi transfer's low-speed timeout - the status the header recording already put on the
    /// handle's info, as libcurl 8.22.0 keeps the status it received (BL-3309).
    /// </summary>
    /// <param name="info">The info from the report.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The response code.</returns>
    private long ResponseCodeOf(TransferInfo info, TransferResult result) =>
        result.Report is null ? Info.ResponseCode : info.ResponseCode;

    /// <summary>
    /// The <c>CURLINFO_NUM_CONNECTS</c> of a performed transfer: the report's, else 1 where libcurl 8.22.0 counts a
    /// connection the report does not (BL-3316): a <c>tftp</c> transfer (its socket is the connection), a transfer under
    /// <c>CURLOPT_FRESH_CONNECT</c> or <c>CURLOPT_FORBID_REUSE</c> that succeeded (libcurl always opens a new connection
    /// for it; this engine does not yet, see BL-3316 Notes), and one a watchdog cut off after the response head came, as the
    /// low-speed timeout does, one that timed out with no report, whose attempted connection libcurl counts (BL-3302),
    /// and one whose proxy refused the tunnel - a <c>CONNECT</c> answered with a status, or a SOCKS handshake that
    /// failed with a <c>CURLPX_*</c> code - whose connection to the proxy libcurl counts (CGR-084, BL-3613).
    /// </summary>
    /// <param name="info">The info with the report's count.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="requestUrl">The URL transferred.</param>
    /// <returns>The number of connections.</returns>
    internal static long NumConnectsOf(TransferInfo info, TransferResult result, EasyOptions options, string requestUrl)
    {
        if (info.NumConnects > 0)
        {
            return info.NumConnects;
        }

        return ForcesNewConnection(options.Network, result) || CountsAFailedConnection(info, result) || requestUrl.StartsWith("tftp:", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static bool CountsAFailedConnection(TransferInfo info, TransferResult result) =>
        CutOffAfterHead(info, result) || TimedOutWithNoReport(result) || ProxyRefusedTheTunnel(info);

    private static bool ProxyRefusedTheTunnel(TransferInfo info) =>
        info.HttpConnectCode > 0 || info.ProxyError > 0;

    private static bool ForcesNewConnection(EasyNetworkOptions network, TransferResult result) =>
        (network.FreshConnect || network.ForbidReuse) && result.ExitCode == CurlExitCode.Ok;

    private static bool TimedOutWithNoReport(TransferResult result) =>
        result.Report is null && result.ExitCode == CurlExitCode.OperationTimedOut;

    private static bool CutOffAfterHead(TransferInfo info, TransferResult result) =>
        result.Report is null && result.ExitCode != CurlExitCode.Ok && info.ResponseCode > 0;

    /// <summary>
    /// <c>CURLINFO_HTTP_VERSION</c> of a performed transfer: the report's, else, when the transfer failed, the version of the
    /// last status line the handle's headers read, as libcurl keeps it after a low-speed abort or a refused redirect hop (BL-3338).
    /// </summary>
    /// <param name="reported">The version the report gave, 0 for none.</param>
    /// <param name="result">The attempt's result.</param>
    /// <param name="lastStatusLineVersion">The version of the last status line the headers read, 0 for none.</param>
    /// <returns>The <c>CURL_HTTP_VERSION_*</c> number.</returns>
    internal static long HttpVersionOf(long reported, TransferResult result, long lastStatusLineVersion) =>
        reported == 0 && result.ExitCode != CurlExitCode.Ok ? lastStatusLineVersion : reported;

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

    /// <summary>
    /// The method a perform that ended with no report answers as <c>CURLINFO_EFFECTIVE_METHOD</c>, from the options as
    /// curl 8.22.0's <c>getinfo_char</c> reads it: <c>CURLOPT_CUSTOMREQUEST</c>, else <c>HEAD</c> under
    /// <c>CURLOPT_NOBODY</c>, <c>PUT</c> under <c>CURLOPT_UPLOAD</c>, <c>POST</c> for a post (<see cref="PostsOf" />)
    /// and <c>GET</c> otherwise (CGR-128).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The method.</returns>
    internal static string EffectiveMethodOf(EasyOptions options) =>
        options.Transfer.CustomRequest
        ?? (options.Transfer.NoBody ? "HEAD"
            : options.Transfer.Upload ? "PUT"
            : PostsOf(options.Http) ? "POST"
            : "GET");

    /// <summary>
    /// Whether the options post: <c>CURLOPT_POST</c>, or post fields or a MIME or form body not overridden by
    /// <c>CURLOPT_HTTPGET</c> (<see cref="WithoutPostFieldsUnderHttpGet" />).
    /// </summary>
    /// <param name="http">The handle's HTTP options.</param>
    /// <returns><see langword="true" /> for a post.</returns>
    private static bool PostsOf(Api.Options.Http.EasyHttpOptions http) =>
        http.Post || (!http.HttpGet && (http.PostFields is not null || http.MimePost is not null || http.HttpPost is not null));

    /// <summary>
    /// <paramref name="info" /> with the end points of the connection a perform opened as <c>CURLINFO_PRIMARY_IP</c>,
    /// <c>PRIMARY_PORT</c>, <c>LOCAL_IP</c> and <c>LOCAL_PORT</c> (empty and 0 for one not known), and that connection
    /// counted in <c>CURLINFO_NUM_CONNECTS</c> (CGR-128).
    /// </summary>
    /// <param name="info">The info so far.</param>
    /// <param name="remote">The peer's end point, or <see langword="null" />.</param>
    /// <param name="local">The local end point, or <see langword="null" />.</param>
    /// <returns>The info with the connection.</returns>
    private static TransferInfo WithConnectionEndPoints(TransferInfo info, System.Net.IPEndPoint? remote, System.Net.IPEndPoint? local) =>
        info with
        {
            PrimaryIp = AddressOf(remote),
            PrimaryPort = PortOf(remote),
            LocalIp = AddressOf(local),
            LocalPort = PortOf(local),
            NumConnects = info.NumConnects + 1,
        };

    /// <summary>The address of <paramref name="endPoint" />, or empty for none.</summary>
    /// <param name="endPoint">The end point, or <see langword="null" />.</param>
    /// <returns>The address text.</returns>
    private static string AddressOf(System.Net.IPEndPoint? endPoint) => endPoint?.Address.ToString() ?? string.Empty;

    /// <summary>The port of <paramref name="endPoint" />, or 0 for none.</summary>
    /// <param name="endPoint">The end point, or <see langword="null" />.</param>
    /// <returns>The port.</returns>
    private static long PortOf(System.Net.IPEndPoint? endPoint) => endPoint?.Port ?? 0;

    private CurlCode Finish(CurlCode code, string errorText, TransferInfo info)
    {
        ErrorText = errorText;
        Info = info;
        return code;
    }
}
