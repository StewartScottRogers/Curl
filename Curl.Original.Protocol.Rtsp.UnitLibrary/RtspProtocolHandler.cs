using System.Globalization;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Serves the <c>rtsp</c> scheme as the curl 8.21.0 tool does: connects, sends one
/// <c>OPTIONS * RTSP/1.0</c> request with <c>CSeq: 1</c>, writes the reply head to the header
/// output, reads and discards the body, and fails a reply whose <c>CSeq</c> does not match with
/// exit 85 and one whose <c>Session</c> contradicts the transfer's session ID with exit 86
/// (ADR-0169). A libcurl <c>DESCRIBE</c> or <c>GET_PARAMETER</c> writes its reply body to the
/// body output instead, as curl 8.22.0's <c>lib/rtsp.c</c> does (BL-2789).
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port (554 when the URL names none),
/// tunnelled through <see cref="ITransferContext.Proxy" /> when one is set. No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="authenticator">
/// Builds the pre-emptive <c>Authorization</c> value for <c>-u</c>, <c>--basic</c> and
/// <c>--oauth2-bearer</c>, with no challenges; and, when a request sent without one gets a
/// <c>401</c> with <c>WWW-Authenticate</c> challenges, the value it is sent again with once, with
/// the next <c>CSeq</c>, as <c>--anyauth</c> or <c>CURLAUTH_ANY</c> does (BL-2818).
/// </param>
/// <param name="interleaveSink">
/// Takes each interleaved <c>$</c> RTP frame read before a reply or during a <c>RECEIVE</c>, as
/// libcurl's <c>CURLOPT_INTERLEAVEFUNCTION</c> does; or <see langword="null" />, the curl tool's
/// case, to skip frames before a reply unwritten and write a <c>RECEIVE</c>'s frames to the body
/// output (BL-2477, ADR-0612).
/// </param>
/// <param name="requestSettings">
/// The libcurl perform's RTSP request settings, made into the request as curl 8.22.0's
/// <c>lib/rtsp.c</c> makes it; or <see langword="null" />, the curl tool's case, for one
/// <c>OPTIONS *</c> (BL-2751, ADR-0691).
/// </param>
/// <param name="sessionState">
/// The <c>CSeq</c> counter and session ID a libcurl easy handle keeps from perform to perform; or
/// <see langword="null" /> for a fresh one per transfer, the curl tool's case.
/// </param>
/// <remarks>
/// Measured against a loopback listener (ADR-0169, BL-591). <c>-X</c>, <c>--request-target</c>,
/// <c>-d</c>, <c>-b</c> and <c>--compressed</c> change nothing; the reply body never reaches
/// the output, even with <c>-i</c>. A <c>-H</c> header naming <c>CSeq</c> fails with 85,
/// <c>CSeq cannot be set as a custom header.</c>, after connecting and before anything is sent;
/// one naming <c>Session</c> fails the same way with 43,
/// <c>Session ID cannot be set as a custom header.</c> (BL-592). The first <c>Session</c> ID a
/// reply gives is kept for the transfer and sent on its later requests; a <c>Session</c>
/// header naming another ID fails with 86 as soon as it is read (<see cref="RtspSessionState" />),
/// ahead of the <c>-f</c> and <c>CSeq</c> checks.
/// Once the head has ended: under <c>-f</c> a status of 400 or more fails with 22,
/// <c>The requested URL returned error: &lt;code&gt;</c>, ahead of any <c>CSeq</c> check; a
/// status below 100 fails with 1, <c>Unsupported response code in HTTP response</c>. A reply
/// <c>CSeq</c> other than the one sent, or none, fails with 85,
/// <c>The CSeq of this request 1 did not match the response &lt;received&gt;</c>, also when the
/// server closed before the head ended. A connect failure is returned as the connector
/// reported it. Under <c>--max-filesize</c> above 0, a <c>Content-Length</c> over the limit
/// fails with 63, <c>Maximum file size exceeded</c>, after any <c>-f</c> refusal and before the
/// head's blank line is reported or any body byte read, and the connection is closed (BL-1292).
/// For <c>-v</c> and <c>--trace</c> (BL-593) the request is reported as one header event and
/// <c>Request completely sent off</c>, the reply head one line at a time, the body as received
/// data, each failure's message, and then what became of the connection. The connection is
/// pooled under the <c>rtsp</c> scheme and handed back after a success, a <c>CSeq</c> mismatch
/// or a refused <c>-H Session</c> header without a reply body; a transfer that starts on a
/// reused connection sends <c>CSeq: 0</c> (ADR-0169 decision 5).
/// The request, each reply header's name, the reply's status and session and the transfer's
/// end are written to <see cref="ITransferContext.DiagnosticLog" /> under the <c>rtsp</c>
/// component (<see cref="RtspTransferLog" />).
/// </remarks>
public sealed class RtspProtocolHandler(
    IConnector connector,
    IHttpAuthenticator authenticator,
    IRtspInterleaveSink? interleaveSink = null,
    RtspRequestSettings? requestSettings = null,
    RtspSessionState? sessionState = null) : IProtocolHandler
{
    /// <summary>The port an <c>rtsp</c> URL without one connects to.</summary>
    internal const int DefaultPort = 554;

    /// <summary>The <c>CSeq</c> of a transfer's first request.</summary>
    internal const long FirstSequenceNumber = 1;

    /// <summary>
    /// The <c>CSeq</c> of the first request of a transfer that starts on a reused connection,
    /// as curl 8.21.0 sends it (ADR-0169 row 20).
    /// </summary>
    internal const long ReusedFirstSequenceNumber = 0;

    /// <summary>The user name or password held a byte below 0x20 (exit 43), curl 8.22.0's text (BL-2179).</summary>
    private const string CredentialsHaveControlCode = "control code detected in credentials";

    private const int LowestStatusCode = 100;

    private const int LowestErrorStatusCode = 400;

    private const int UnauthorizedStatusCode = 401;

    private const string UnsupportedResponseCode = "Unsupported response code in HTTP response";

    /// <summary>
    /// The one scheme this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names it.
    /// </summary>
    private static readonly string[] Schemes = ["rtsp"];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly IHttpAuthenticator authenticator =
        authenticator ?? throw new ArgumentNullException(nameof(authenticator));

    private RtspRequestPlan plan = RtspRequestPlan.For(requestSettings);

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long startTimestamp = context.TimeProvider.GetTimestamp();
        TransferResult result = await ConnectAndExchangeAsync(context).ConfigureAwait(false);
        new RtspTransferLog(context.DiagnosticLog).Ended(result, context.TimeProvider.GetElapsedTime(startTimestamp));
        return result;
    }

    /// <summary>
    /// The exit 43 curl 8.22.0 refuses <paramref name="credentials" /> with before it connects when the
    /// user name or password holds a byte below 0x20, or <see langword="null" /> when neither does (BL-2179).
    /// </summary>
    private static TransferResult? RefuseControlBytes(System.Net.NetworkCredential? credentials) =>
        credentials is not null && (credentials.UserName.Any(character => character < ' ') || credentials.Password.Any(character => character < ' '))
            ? TransferResult.Failure(CurlExitCode.BadFunctionArgument, CredentialsHaveControlCode)
            : null;

    // Connects and makes the transfer's one request; ExecuteAsync logs how it ended.
    private async ValueTask<TransferResult> ConnectAndExchangeAsync(ITransferContext context)
    {
        if (RefuseControlBytes(context.Credentials) is { } credentialsRefused)
        {
            return credentialsRefused;
        }

        CurlUrl url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            PoolScheme = Schemes[0],
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            RtspSessionState session = sessionState ?? new RtspSessionState();
            session.StartTransfer(requestSettings, connect.IsReused);
            plan = await plan.WithUploadAsync(context.Upload, context.CancellationToken).ConfigureAwait(false);
            TransferResult result = await RequestAsync(connection, context, session).ConfigureAwait(false);
            ReportConnectionEnd(context.Events, target, connect, result, session.ClosesConnection);
            return result;
        }
    }

    /// <summary>
    /// Hands the connection back to the pool and reports it left intact when curl 8.21.0 keeps
    /// it; otherwise reports it shut down or closed, as measured (BL-593, BL-1403).
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, ConnectTarget target, ConnectResult connect, TransferResult result, bool closesConnection)
    {
        if (!closesConnection && RtspVerboseLines.LeavesIntact(result))
        {
            connect.Connection!.MarkReusable();
            events.ReportInfo(RtspVerboseLines.LeftIntact(connect.ConnectionNumber, target.Host, target.Port));
            return;
        }

        events.ReportInfo(RtspVerboseLines.Dropped(result, connect.ConnectionNumber));
    }

    /// <summary>
    /// Makes the perform's request: a <c>RECEIVE</c>, after which the connection the server closed
    /// is not kept, or any other kind's request and reply.
    /// </summary>
    private async Task<TransferResult> RequestAsync(IConnection connection, ITransferContext context, RtspSessionState session)
    {
        if (plan.Kind != RtspRequestKind.Receive)
        {
            return await ExchangeAsync(connection, context, session).ConfigureAwait(false);
        }

        session.CloseConnection();
        return await ReceiveAsync(connection, context, session).ConfigureAwait(false);
    }

    /// <summary>Reports <paramref name="message" /> as a <c>-v</c> line and returns it as the transfer's failure.</summary>
    private static TransferResult Fail(ITransferEvents events, CurlExitCode exitCode, string message, long bytesTransferred = 0)
    {
        events.ReportInfo(message);
        return TransferResult.Failure(exitCode, message, bytesTransferred);
    }

    /// <summary>
    /// Makes one <c>OPTIONS</c> request of a transfer on <paramref name="connection" />: sent
    /// with the transfer's next <c>CSeq</c> and, once a reply has given one, its session ID; the
    /// reply read and checked in curl's order.
    /// </summary>
    /// <remarks>
    /// The curl tool makes one request per transfer (ADR-0169), so <see cref="ExecuteAsync" />
    /// calls this once. It takes the session state so a transfer that makes several requests,
    /// as a libcurl caller can, carries its <c>CSeq</c> and session ID from one to the next.
    /// </remarks>
    /// <param name="connection">The transfer's connection.</param>
    /// <param name="context">The transfer.</param>
    /// <param name="session">The transfer's <c>CSeq</c> counter and session ID.</param>
    /// <returns>The request's outcome.</returns>
    internal async Task<TransferResult> ExchangeAsync(IConnection connection, ITransferContext context, RtspSessionState session)
    {
        try
        {
            RtspExchange first = await SendAndReadAsync(connection, context, session, null).ConfigureAwait(false);
            return first.AnswerAuthorization is { } answer
                ? (await SendAndReadAsync(connection, context, session, answer).ConfigureAwait(false)).Result
                : first.Result;
        }
        catch (RtspTransferException failure)
        {
            return Fail(context.Events, failure.ExitCode, failure.Message);
        }
    }

    /// <summary>
    /// Makes an RTSP <c>RECEIVE</c> on <paramref name="connection" /> as curl 8.22.0 does: sends
    /// nothing, checks no <c>CSeq</c>, and hands each interleaved frame of one read (and of the reads
    /// that finish a frame it cut) to the injected sink, or writes it to the body output when none is
    /// injected (BL-2477, BL-2787).
    /// </summary>
    /// <param name="connection">The transfer's connection.</param>
    /// <param name="context">The transfer.</param>
    /// <param name="session">The handle's session state, whose replies' <c>Transport</c> headers set up the channels.</param>
    /// <returns>Success with the frame bytes taken, or the read (56) or sink (23) failure.</returns>
    internal async Task<TransferResult> ReceiveAsync(IConnection connection, ITransferContext context, RtspSessionState session)
    {
        var frames = new RtspInterleavedFrameFilter(interleaveSink ?? new RtspStreamInterleaveSink(context.Output), session.IsChannelSetUp);
        try
        {
            byte[] carried = session.CarriedBytes;
            session.CarriedBytes = [];
            await RtspReplyReader.ReceiveFramesAsync(connection, frames, carried, context.CancellationToken).ConfigureAwait(false);
            return TransferResult.Success(frames.BytesHanded);
        }
        catch (RtspTransferException failure)
        {
            return Fail(context.Events, failure.ExitCode, failure.Message, frames.BytesHanded);
        }
    }

    private static string SequenceMismatch(long sent, long received) =>
        string.Create(CultureInfo.InvariantCulture, $"The CSeq of this request {sent} did not match the response {received}");

    /// <summary>
    /// Sends <paramref name="request" />. A failed send is reported as curl 8.21.0's
    /// <c>rtsp_do</c> reports it: the send failure's message, then <c>Failed sending RTSP
    /// request</c> as a <c>-v</c> line only, the message staying the transfer's (BL-1230).
    /// </summary>
    /// <returns>The exit 55 failure, or <see langword="null" /> when the request was sent.</returns>
    private static async ValueTask<TransferResult?> SendAsync(IConnection connection, byte[] request, ITransferEvents events, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException exception)
        {
            RtspTransferException failure = RtspIoFailures.SendFailed(exception);
            TransferResult result = Fail(events, failure.ExitCode, failure.Message);
            events.ReportInfo(RtspVerboseLines.RequestSendFailed);
            return result;
        }
    }

    /// <summary>
    /// Refuses a <c>-H</c> header naming <c>CSeq</c> (85) or <c>Session</c> (43), in that order,
    /// as curl 8.21.0 does after connecting and before sending anything.
    /// </summary>
    /// <remarks>
    /// Ahead of both, as <c>lib/rtsp.c</c>'s <c>rtsp_setup_request</c> runs first, a <c>SETUP</c>
    /// with no <c>Transport</c> is refused with 43 (BL-2751).
    /// </remarks>
    /// <returns>The failure, or <see langword="null" /> when the request is not refused.</returns>
    private TransferResult? RequestRefusal(HttpRequestOptions options, ITransferEvents events)
    {
        if (plan.LacksTransport(RtspRequestFormatter.NamesTransport(options)))
        {
            return Fail(events, CurlExitCode.BadFunctionArgument, RtspRequestPlan.SetupWithoutTransport);
        }

        if (RtspRequestFormatter.NamesCSeq(options))
        {
            return Fail(events, CurlExitCode.RtspCseqError, RtspRequestFormatter.CustomCSeqRefused);
        }

        return RtspRequestFormatter.NamesSession(options)
            ? Fail(events, CurlExitCode.BadFunctionArgument, RtspRequestFormatter.CustomSessionRefused)
            : null;
    }

    /// <summary>
    /// Sends the request and reads its reply, checking the <c>Session</c>, the status and the
    /// <c>CSeq</c> in curl's order.
    /// </summary>
    /// <param name="connection">The transfer's connection.</param>
    /// <param name="context">The transfer.</param>
    /// <param name="session">The transfer's <c>CSeq</c> counter and session ID.</param>
    /// <param name="answer">
    /// The <c>Authorization</c> value answering the previous reply's <c>401</c>, or
    /// <see langword="null" /> for the first request, which sends only a pre-emptive one.
    /// </param>
    private async Task<RtspExchange> SendAndReadAsync(IConnection connection, ITransferContext context, RtspSessionState session, string? answer)
    {
        HttpRequestOptions options = context.Http ?? new HttpRequestOptions();
        ITransferEvents events = context.Events;
        if (RequestRefusal(options, events) is { } refused)
        {
            return new(refused, null);
        }

        string? authorization = answer ?? Authorization(context, options, []);
        byte[] requestHead = FormatHead(context, options, session, authorization);
        events.ReportRequestHeader(requestHead);
        byte[] request = [.. requestHead, .. plan.SentBody];
        if (await SendAsync(connection, request, events, context.CancellationToken).ConfigureAwait(false) is { } sendFailure)
        {
            return new(sendFailure, null);
        }

        return await ReadReplyAsync(connection, context, session, options, request.Length, authorization).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the reply to the request just sent, checking the <c>Session</c>, the status and the
    /// <c>CSeq</c> in curl's order, and names the value to send it again with after a <c>401</c>.
    /// </summary>
    /// <param name="connection">The transfer's connection.</param>
    /// <param name="context">The transfer.</param>
    /// <param name="session">The transfer's <c>CSeq</c> counter and session ID.</param>
    /// <param name="options">The transfer's request options.</param>
    /// <param name="requestSize">How many bytes the request took, for <c>%{size_request}</c>.</param>
    /// <param name="authorization">The <c>Authorization</c> value the request was sent with, or <see langword="null" />.</param>
    private async Task<RtspExchange> ReadReplyAsync(IConnection connection, ITransferContext context, RtspSessionState session, HttpRequestOptions options, int requestSize, string? authorization)
    {
        ITransferEvents events = context.Events;
        long sequenceNumber = session.NextSequenceNumber;
        session.TakeSequenceNumber();
        events.ReportInfo(RtspVerboseLines.RequestSent);
        var log = new RtspTransferLog(context.DiagnosticLog);
        log.RequestSent(plan.MethodName, sequenceNumber);
        RtspReplyHead head = await RtspReplyReader.ReadHeadAsync(connection, session, context.HeaderOutput, events, context.CancellationToken, log, context.MaxFileSize, Frames(session)).ConfigureAwait(false);
        log.ReplyRead(head, session.SessionId);
        TransferReport report = new()
        {
            ResponseCode = head.StatusCode,
            HttpVersion = System.Net.HttpVersion.Version11,
            PseudoHeaders = head.HeaderLines,
            Method = plan.MethodName,
            HeaderSize = head.Length,
            RequestSize = requestSize,
            RtspSessionId = session.SessionId,
            RtspClientSequenceNumber = session.NextSequenceNumber,
            RtspSequenceNumberReceived = head.SequenceNumber,
        };
        if (StatusFailure(head, options, context.MaxFileSize, events) is { } statusFailure)
        {
            return new(statusFailure with { Report = report }, null);
        }

        long bodyRead = await ReadBodyAsync(connection, context, head).ConfigureAwait(false);
        await TakeBytesAfterReplyAsync(head, session, context.CancellationToken).ConfigureAwait(false);
        context.Progress.ReportTransferDone();
        TransferResult result = head.SequenceNumber == sequenceNumber
            ? TransferResult.Success(bodyRead)
            : Fail(events, CurlExitCode.RtspCseqError, SequenceMismatch(sequenceNumber, head.SequenceNumber), bodyRead);
        result = result with { Report = report with { DownloadSize = bodyRead } };
        return new(result, result.IsSuccess && authorization is null ? AnswerChallenges(context, options, head) : null);
    }

    /// <summary>
    /// The <c>Authorization</c> value a <c>401</c> to a request sent without one is answered with,
    /// as curl 8.22.0's <c>Curl_http_auth_act</c> sends the request again under <c>CURLAUTH_ANY</c>
    /// (upstream test3100, BL-2818).
    /// </summary>
    /// <returns>
    /// The value, or <see langword="null" /> when the reply is no <c>401</c>, offers no challenge, or
    /// none the authenticator can answer with a header.
    /// </returns>
    private string? AnswerChallenges(ITransferContext context, HttpRequestOptions options, RtspReplyHead head) =>
        head.StatusCode == UnauthorizedStatusCode && head.Challenges.Count > 0
            && Authorization(context, options, head.Challenges) is { Length: > 0 } answer
            ? answer
            : null;

    /// <summary>
    /// Reads the reply body unless the plan leaves it unread, writing it to the body output when the
    /// plan writes it and discarding it otherwise (BL-2789).
    /// </summary>
    /// <returns>How many body bytes were read.</returns>
    private async ValueTask<long> ReadBodyAsync(IConnection connection, ITransferContext context, RtspReplyHead head) =>
        plan.SkipsReplyBody
            ? 0
            : await RtspReplyReader.ReadBodyAsync(connection, head.Remaining, head.ContentLength, plan.WritesReplyBody ? context.Output : null, context.Progress, context.Events, context.CancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Hands the frames that came in the same read after the reply's body to the interleave sink, as curl 8.22.0's
    /// <c>rtsp_filter_rtp</c> does for the bytes after a response, and keeps the rest - a frame cut off, or the start of
    /// a reply - on <paramref name="session" /> for the connection's next request (BL-2787).
    /// </summary>
    private async ValueTask TakeBytesAfterReplyAsync(RtspReplyHead head, RtspSessionState session, CancellationToken cancellationToken)
    {
        int bodyInHead = plan.SkipsReplyBody ? 0 : (int)Math.Clamp(head.ContentLength, 0, head.Remaining.Length);
        ReadOnlyMemory<byte> after = head.Remaining.AsMemory(bodyInHead);
        RtspInterleavedFrameFilter? frames = Frames(session);
        int taken = frames is null ? 0 : await frames.ConsumeAsync(after, stopAtReply: true, cancellationToken).ConfigureAwait(false);
        session.CarriedBytes = after[taken..].ToArray();
    }

    /// <summary>
    /// Fails a completed head curl refuses: a status of 400 or more under <c>-f</c> with 22, else
    /// a <c>Content-Length</c> over a <c>--max-filesize</c> limit above 0 with 63 before any of the
    /// body is read (BL-1292), else a status below 100 with 1. Reports the head's blank line to
    /// <c>-v</c> as curl 8.21.0 does: after the first two messages, before the last.
    /// </summary>
    /// <returns>The failure, or <see langword="null" /> when the head is accepted.</returns>
    private static TransferResult? StatusFailure(RtspReplyHead head, HttpRequestOptions options, long? maxFileSize, ITransferEvents events)
    {
        if (head.EndLine is not { } endLine)
        {
            return null;
        }

        TransferResult? refused = options.Fail != HttpFailMode.None && head.StatusCode >= LowestErrorStatusCode
            ? Fail(
                events,
                CurlExitCode.HttpReturnedError,
                string.Create(CultureInfo.InvariantCulture, $"The requested URL returned error: {head.StatusCode}"))
            : SizeFailure(head.ContentLength, maxFileSize, events);
        events.ReportResponseHeader(endLine);
        if (refused is not null)
        {
            return refused;
        }

        return head.StatusCode < LowestStatusCode
            ? Fail(events, CurlExitCode.UnsupportedProtocol, UnsupportedResponseCode)
            : null;
    }

    /// <summary>Fails a known <c>Content-Length</c> over a <c>--max-filesize</c> limit above 0 with 63.</summary>
    /// <returns>The failure, or <see langword="null" /> when the length is within the limit or there is none.</returns>
    private static TransferResult? SizeFailure(long contentLength, long? maxFileSize, ITransferEvents events) =>
        maxFileSize > 0 && contentLength > maxFileSize
            ? Fail(events, CurlExitCode.FilesizeExceeded, RtspReplyHeadParser.MaxFileSizeExceeded)
            : null;

    /// <summary>The filter that hands frames before a reply to the injected sink, or <see langword="null" /> to skip them.</summary>
    private RtspInterleavedFrameFilter? Frames(RtspSessionState session) =>
        interleaveSink is null ? null : new RtspInterleavedFrameFilter(interleaveSink, session.IsChannelSetUp);

    /// <summary>Writes the request head with the session's next <c>CSeq</c>, its session ID and <paramref name="authorization" />.</summary>
    private byte[] FormatHead(ITransferContext context, HttpRequestOptions options, RtspSessionState session, string? authorization) =>
        RtspRequestFormatter.Format(
            new RtspMethod(plan.MethodName),
            plan.Target,
            session.NextSequenceNumber,
            session.SessionId,
            plan.SendsRange ? RtspRequestFormatter.RangeValue(context.ResumeFrom, context.RangeText) : null,
            options,
            authorization,
            plan,
            context.TimeCondition);

    private string? Authorization(ITransferContext context, HttpRequestOptions options, IReadOnlyList<string> challenges) =>
        authenticator.CreateAuthorization(
            new HttpAuthRequest(
                plan.MethodName,
                context.Url,
                plan.Target,
                context.Credentials,
                options.BearerToken,
                options.AuthSchemes,
                IsProxy: false),
            challenges);

    /// <summary>One request's outcome, and the <c>Authorization</c> value to send it again with after a <c>401</c>.</summary>
    /// <param name="Result">The request's outcome.</param>
    /// <param name="AnswerAuthorization">The value to send the request again with, or <see langword="null" /> not to.</param>
    private sealed record RtspExchange(TransferResult Result, string? AnswerAuthorization);
}
