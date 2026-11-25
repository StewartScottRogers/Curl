using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that creates the readers, the writer and the settings of one exchange.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Gives the connection an exchange reads its response from: one that waits for <c>100 Continue</c>
    /// when the request expects it; over HTTP/1.x with a stream body, one that can read the status line
    /// while a read of the body stays pending (<see cref="HttpEarlyResponseConnection" />, BL-2729); and
    /// else <paramref name="timedConnection" /> itself.
    /// </summary>
    private IConnection ResponseConnectionOf(HttpRequestPlan plan, IHttpStreamConnection? requestStream, IConnection timedConnection) =>
        (plan.Framing.AwaitsContinue, plan.Framing.Body is StreamBody && requestStream is null) switch
        {
            (true, _) => new HttpContinueWaitConnection(timedConnection) { ContinueWait = plan.Options.ContinueWait, WaitBegins = ContinueWaitBegins, WaitEnds = ContinueWaitEnds },
            (false, true) => new HttpEarlyResponseConnection(timedConnection),
            _ => timedConnection,
        };

    /// <summary>
    /// Builds the request body writer of an exchange: holding the request head back when it
    /// awaits <c>100 Continue</c>, otherwise sharing it with the body's first write.
    /// </summary>
    private HttpRequestBodyWriter CreateUpload(
        HttpRequestPlan plan,
        IConnection connection,
        IConnection responseConnection,
        IHttpStreamConnection? requestStream,
        byte[] request)
    {
        HttpRequestFraming framing = plan.Framing;
        return new HttpRequestBodyWriter(connection)
        {
            SharedHeadLength = framing.AwaitsContinue ? 0 : request.Length,
            HeldHead = request,
            IsUpload = framing.IsUpload,
            IsReadFunctionBody = framing.Body is StreamBody { ContentType: var bodyType } && !bodyType.StartsWith("multipart/", StringComparison.Ordinal),
            Progress = plan.Progress,
            Events = plan.Context.Events,
            EarlyResponseWatch = responseConnection as HttpContinueWaitConnection,
            EarlyResponseRead = responseConnection as HttpEarlyResponseConnection,
            KeepsSendingOnError = plan.Options.KeepSendingOnError,
            SendBytesPerSecond = plan.Options.SendBytesPerSecond,
            TimeProvider = plan.Context.TimeProvider,
            TracesReaders = TracesReadersOf(requestStream),
        };
    }

    /// <summary>
    /// Builds the record of an exchange that <see cref="HttpExchange.Report" /> turns into the
    /// transfer's report.
    /// </summary>
    private static HttpExchange CreateExchange(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection connection,
        HttpFirstByteTimingConnection timedConnection,
        int headSize,
        HttpRequestBodyWriter upload,
        TransferReport? earlier,
        bool newConnection) =>
        new(connect, connection, plan.Framing.Method, headSize, upload, earlier, newConnection)
        {
            UsedProxy = plan.Options.ForwardProxy is not null,
            Started = plan.Started,
            TimeProvider = plan.Context.TimeProvider,
            ResponseConnection = timedConnection,
            RedirectCount = plan.RedirectsFollowed - plan.Options.RedirectsFollowed,
            AuthSchemePicked = AuthSchemePickedBy(plan),
            ProxyAuthSchemePicked = plan.ProxyAuthorizationAnswersChallenge ? SchemeOfAuthorization(plan.ProxyAuthorization) : HttpAuthSchemes.None,
            DigestAuthorizationSent = DigestAuthorizationSentBy(plan),
            HeldDigestNonce = plan.HoldsDigestNonce,
            SentNtlmAuthorization = SchemeOfAuthorization(plan.Authorization) == HttpAuthSchemes.Ntlm,
        };

    /// <summary>
    /// Builds the response body reader of an exchange from the transfer's options.
    /// </summary>
    private static HttpResponseBodyReader CreateBodyReader(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection responseConnection,
        HttpExchangeLog exchangeLog,
        TransferReport? earlier)
    {
        HttpRequestOptions options = plan.Options;
        return new HttpResponseBodyReader(responseConnection)
        {
            PassesTransferCoding = options.PassesTransferCoding,
            IgnoresContentLength = options.IgnoreContentLength,
            LimitsFileSize = HttpDownloadConditions.LimitOf(plan.Context.MaxFileSize) is not null,
            DecodesTransferCoding = options.TransferEncoding,
            Log = exchangeLog,
            HeadersStoredBefore = HeadersStoredBefore(earlier, options, connect),
        };
    }

    /// <summary>
    /// Builds the response head reader of an exchange, wired to report auth problems and the
    /// upload rewind, to store cookies, Alt-Svc and HSTS from the headers it reads, and to
    /// echo the response line on the stream it came from.
    /// </summary>
    private HttpResponseHeadReader CreateHeadReader(
        HttpRequestPlan plan,
        IConnection responseConnection,
        IConnection connection,
        IHttpStreamConnection? requestStream,
        HttpResponseBodyReader body,
        HttpRequestBodyWriter upload)
    {
        ITransferContext context = plan.Context;
        HttpRequestOptions options = plan.Options;
        int cookiesStored = 0;
        HttpAuthProblemLines originProblems = new();
        HttpAuthProblemLines proxyProblems = new();
        return new HttpResponseHeadReader(responseConnection)
        {
            Events = context.Events,
            HeadersStoredBefore = body.HeadersStoredBefore,
            LineReporting = line => (requestStream as Http3StreamConnection)?.EchoResponseLineBefore(line),
            LineReported = line => EchoResponseLineAfter(requestStream, connection, line),
            StatusLineReported = statusLine => ReportUploadRewind(plan, statusLine, upload),
            HeaderReceived = (statusLine, header) =>
            {
                ReportAuthProblemLines(plan, statusLine, header, originProblems, proxyProblems);
                ReportNtlmAuthRestart(plan, statusLine, header);
                cookiesStored = StoreCookie(context, options, header, cookiesStored);
                StoreAltSvc(context, options.AltSvcStore, statusLine.Version, header);
                StoreHsts(context, options.HstsStore, header);
            },
            FindRefusal = head => body.FindHeadRefusal(head, context.NoBody, DecodesContent(options)),
            IsHttp2OrHttp3 = requestStream is not null,
            AcceptsHttp09 = options.AllowHttp09Reply,
            Http200Aliases = options.Http200Aliases,
            IsHeadRequest = context.NoBody,
            IgnoresContentLength = options.IgnoreContentLength,
            IsThroughHttpProxy = IsThroughHttpProxy(options),
            IsSwitchedToHttp2 = () => IsSwitchedToHttp2(connection),
            HttpVersionSeenBefore = () => Http1VersionSeen.Of(connection) is not null,
            AsksH2cUpgrade = connection is HttpH2cUpgradeConnection,
            DefersFrom = (statusLine, header) => plan.Framing.Body is not StreamBody && IsAuthChallenge(plan, statusLine, header),
        };
    }

    /// <summary>
    /// Gives how many response headers the transfer stored before an exchange: those of the
    /// exchange a retry answers, through its <paramref name="earlier" /> report, or else those
    /// of the hops before this one (<see cref="HttpRequestOptions.ResponseHeadersStored" />),
    /// with the header lines of the CONNECT reply that opened the connection's tunnel,
    /// since curl 8.21.0 counts them all toward one limit of 5000 (measured, BL-1448 and BL-1609 Notes).
    /// </summary>
    private static int HeadersStoredBefore(TransferReport? earlier, HttpRequestOptions options, ConnectResult connect) =>
        earlier?.ResponseHeadersStored ?? (options.ResponseHeadersStored + connect.ConnectReplyHeadersStored);

    /// <summary>
    /// Gives how <c>-f</c> or <c>--fail-with-body</c> applies to a response: as asked, unless
    /// the handler answers it with <paramref name="retry" />, which no failing mode stops, or
    /// it is a 416 to a resumed GET, which curl 8.21.0 takes as a file already downloaded
    /// (<see cref="HttpDownloadConditions.IsResumeAlreadyComplete" />).
    /// </summary>
    private static HttpFailMode FailModeOf(HttpRequestPlan plan, HttpRequestPlan? retry, HttpResponseHead head) =>
        retry is null && !HttpDownloadConditions.IsResumeAlreadyComplete(plan.Context, plan.Framing.Body is not null, head)
            ? plan.Options.Fail
            : HttpFailMode.None;

    /// <summary>
    /// Tells whether the response's body is read and discarded rather than delivered: when the
    /// handler answers it with <paramref name="retry" />, or follows its redirect - a 3xx with a
    /// target; a <c>Location</c> on any other status is reported but not followed (BL-2212).
    /// </summary>
    private static bool DiscardsBody(HttpRequestOptions options, HttpRequestPlan? retry, HttpResponseHead head, string? redirectUrl) =>
        retry is not null || (options.FollowRedirects && redirectUrl is not null && head.StatusLine.StatusCode is >= 300 and < 400);
}
