using System.Globalization;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Serves the <c>rtsp</c> scheme as the curl 8.21.0 tool does: connects, sends one
/// <c>OPTIONS * RTSP/1.0</c> request with <c>CSeq: 1</c>, writes the reply head to the header
/// output, reads and discards the body, and fails a reply whose <c>CSeq</c> does not match with
/// exit 85 and one whose <c>Session</c> contradicts the transfer's session ID with exit 86
/// (ADR-0169).
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port (554 when the URL names none),
/// tunnelled through <see cref="ITransferContext.Proxy" /> when one is set. No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="authenticator">
/// Builds the pre-emptive <c>Authorization</c> value for <c>-u</c>, <c>--basic</c> and
/// <c>--oauth2-bearer</c>, with no challenges.
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
/// reported it.
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
public sealed class RtspProtocolHandler(IConnector connector, IHttpAuthenticator authenticator) : IProtocolHandler
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

    /// <summary>The request target of an <c>OPTIONS</c> request.</summary>
    private const string OptionsTarget = "*";

    private const int LowestStatusCode = 100;

    private const int LowestErrorStatusCode = 400;

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

    // Connects and makes the transfer's one request; ExecuteAsync logs how it ended.
    private async ValueTask<TransferResult> ConnectAndExchangeAsync(ITransferContext context)
    {
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
            var session = new RtspSessionState(connect.IsReused ? ReusedFirstSequenceNumber : FirstSequenceNumber);
            TransferResult result = await ExchangeAsync(connection, context, session).ConfigureAwait(false);
            ReportConnectionEnd(context.Events, target, connect, result);
            return result;
        }
    }

    /// <summary>
    /// Hands the connection back to the pool and reports it left intact when curl 8.21.0 keeps
    /// it; otherwise reports it shut down or closed, as measured (BL-593).
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, ConnectTarget target, ConnectResult connect, TransferResult result)
    {
        if (RtspVerboseLines.LeavesIntact(result))
        {
            connect.Connection!.MarkReusable();
            events.ReportInfo(RtspVerboseLines.LeftIntact(connect.ConnectionNumber, target.Host, target.Port));
            return;
        }

        events.ReportInfo(RtspVerboseLines.Dropped(result, connect.ConnectionNumber));
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
            return await SendAndReadAsync(connection, context, session).ConfigureAwait(false);
        }
        catch (RtspTransferException failure)
        {
            return Fail(context.Events, failure.ExitCode, failure.Message);
        }
    }

    private static string SequenceMismatch(long sent, long received) =>
        string.Create(CultureInfo.InvariantCulture, $"The CSeq of this request {sent} did not match the response {received}");

    private static async ValueTask SendAsync(IConnection connection, byte[] request, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw RtspIoFailures.SendFailed(exception);
        }
    }

    /// <summary>
    /// Refuses a <c>-H</c> header naming <c>CSeq</c> (85) or <c>Session</c> (43), in that order,
    /// as curl 8.21.0 does after connecting and before sending anything.
    /// </summary>
    /// <returns>The failure, or <see langword="null" /> when no <c>-H</c> header is refused.</returns>
    private static TransferResult? CustomHeaderFailure(HttpRequestOptions options, ITransferEvents events)
    {
        if (RtspRequestFormatter.NamesCSeq(options))
        {
            return Fail(events, CurlExitCode.RtspCseqError, RtspRequestFormatter.CustomCSeqRefused);
        }

        return RtspRequestFormatter.NamesSession(options)
            ? Fail(events, CurlExitCode.BadFunctionArgument, RtspRequestFormatter.CustomSessionRefused)
            : null;
    }

    /// <summary>
    /// Sends the <c>OPTIONS</c> request and reads its reply, checking the <c>Session</c>, the
    /// status and the <c>CSeq</c> in curl's order.
    /// </summary>
    private async Task<TransferResult> SendAndReadAsync(IConnection connection, ITransferContext context, RtspSessionState session)
    {
        HttpRequestOptions options = context.Http ?? new HttpRequestOptions();
        ITransferEvents events = context.Events;
        if (CustomHeaderFailure(options, events) is { } refused)
        {
            return refused;
        }

        long sequenceNumber = session.TakeSequenceNumber();
        byte[] request = RtspRequestFormatter.Format(
            RtspMethod.Options,
            OptionsTarget,
            sequenceNumber,
            session.SessionId,
            options,
            Authorization(context, options));
        events.ReportRequestHeader(request);
        await SendAsync(connection, request, context.CancellationToken).ConfigureAwait(false);
        events.ReportInfo(RtspVerboseLines.RequestSent);
        var log = new RtspTransferLog(context.DiagnosticLog);
        log.RequestSent(RtspMethod.Options.Name, sequenceNumber);
        RtspReplyHead head = await RtspReplyReader.ReadHeadAsync(connection, session, context.HeaderOutput, events, context.CancellationToken, log).ConfigureAwait(false);
        log.ReplyRead(head, session.SessionId);
        TransferReport report = new()
        {
            ResponseCode = head.StatusCode,
            Method = RtspMethod.Options.Name,
            HeaderSize = head.Length,
            RequestSize = request.Length,
        };
        if (StatusFailure(head, options, events) is { } statusFailure)
        {
            return statusFailure with { Report = report };
        }

        long bodyRead = await RtspReplyReader.DiscardBodyAsync(connection, head.Remaining, head.ContentLength, context.Progress, events, context.CancellationToken).ConfigureAwait(false);
        context.Progress.ReportTransferDone();
        TransferResult result = head.SequenceNumber == sequenceNumber
            ? TransferResult.Success(bodyRead)
            : Fail(events, CurlExitCode.RtspCseqError, SequenceMismatch(sequenceNumber, head.SequenceNumber), bodyRead);
        return result with { Report = report with { DownloadSize = bodyRead } };
    }

    /// <summary>
    /// Fails a completed head whose status curl refuses: 400 or more under <c>-f</c> with 22,
    /// below 100 with 1. Reports the head's blank line to <c>-v</c> as curl 8.21.0 does: after
    /// the <c>-f</c> message, before the other one.
    /// </summary>
    /// <returns>The failure, or <see langword="null" /> when the status is accepted.</returns>
    private static TransferResult? StatusFailure(RtspReplyHead head, HttpRequestOptions options, ITransferEvents events)
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
            : null;
        events.ReportResponseHeader(endLine);
        if (refused is not null)
        {
            return refused;
        }

        return head.StatusCode < LowestStatusCode
            ? Fail(events, CurlExitCode.UnsupportedProtocol, UnsupportedResponseCode)
            : null;
    }

    private string? Authorization(ITransferContext context, HttpRequestOptions options) =>
        authenticator.CreateAuthorization(
            new HttpAuthRequest(
                RtspMethod.Options.Name,
                context.Url,
                OptionsTarget,
                context.Credentials,
                options.BearerToken,
                options.AuthSchemes,
                IsProxy: false),
            []);
}
