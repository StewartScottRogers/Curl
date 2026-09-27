using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Serves the <c>http</c> and <c>https</c> schemes over HTTP/1.1, or HTTP/1.0 for <c>-0</c>: connects through the
/// injected <see cref="IConnector" />, sends the request head, writes the response head to
/// <see cref="ITransferContext.HeaderOutput" /> and the body to
/// <see cref="ITransferContext.Output" />, and reports what it learned in a
/// <see cref="TransferReport" />.
/// </summary>
/// <param name="connector">
/// Opens the connection for each transfer. <c>https</c> asks it for TLS with
/// <see cref="ConnectTarget.UseTls" />; the handler does no TLS of its own (ADR-0005).
/// </param>
/// <param name="authenticator">
/// Gives the <c>Authorization</c> value each request is sent with, before any response and
/// in answer to a 401's <c>WWW-Authenticate</c> challenges.
/// </param>
/// <param name="cookieStore">
/// The cookies to send and store, or <see langword="null" /> when cookies are off.
/// </param>
/// <remarks>
/// <para>
/// The port is the URL's, or 80 for <c>http</c> and 443 for <c>https</c> when it names none.
/// A connect failure (exit 6, 7, 35, 60 and the like) is returned with the connector's exit
/// code and message unchanged. Every other failure while the response is read is returned
/// with the exit code and message curl 8.21.0 reports, and with a report of whatever was
/// learned before it; only cancellation leaves this handler as an exception.
/// </para>
/// <para>
/// A body in <see cref="HttpRequestOptions.Body" /> makes the request a POST unless <c>-X</c>
/// names another method, and is sent after the head as <see cref="HttpRequestFraming" />
/// decides: at once, or after up to one second's wait for <c>100 Continue</c>, and not at all
/// when a final status arrives during that wait (but see the 417 resend below). A body stream of known length that fails a
/// read ends the transfer with exit 26.
/// </para>
/// <para>
/// Every response head read, 1xx heads included, is written to the header output as one
/// write, exactly as received with continuation lines folded, before any body byte; a
/// chunked body's trailers follow the body. A header output that fails a write ends the
/// transfer with exit 23.
/// </para>
/// <para>
/// <see cref="ITransferContext.NoBody" /> (<c>-I</c>) sends HEAD unless <c>-X</c> names
/// another method, and reads and writes no body whatever the method. A final status of 400 or
/// above ends the transfer with exit 22 for <c>-f</c>, after the head is written and before
/// any body is read, and for <c>--fail-with-body</c> after the body is written, as curl
/// 8.21.0 does (BL-176 Notes).
/// </para>
/// <para>
/// A 3xx response's <c>Location</c> is resolved against the request URL into
/// <see cref="TransferReport.RedirectUrl" /> (<see cref="HttpRedirectLocation" />), with or
/// without <c>-L</c>. Under <see cref="HttpRequestOptions.FollowRedirects" /> the body of a
/// response that has one is read and discarded, counted in the download size as curl counts
/// it, while its head and trailers are still written to the header output (BL-179 Notes).
/// </para>
/// <para>
/// <see cref="HttpRequestOptions.Compressed" /> sends <c>Accept-Encoding: deflate, gzip, br</c>
/// (ADR-0020) and, unless <see cref="HttpRequestOptions.Raw" /> is set, decodes the body as
/// its Content-Encoding says (<see cref="HttpContentDecoder" />, BL-177 Notes).
/// </para>
/// <para>
/// <see cref="HttpRequestOptions.TransferEncoding" /> (<c>--tr-encoding</c>) sends
/// <c>TE: gzip</c> and <c>TE</c> in the <c>Connection</c> header, and decodes the body as its
/// Transfer-Encoding says, <c>--raw</c> or not, before any Content-Encoding; a body with a
/// Transfer-Encoding that is not chunked runs to close. A discarded body is framed the same way
/// but not decoded (<see cref="HttpTransferEncoding.Requested" />, BL-315 Notes).
/// </para>
/// <para>
/// <see cref="HttpVersionPreference.Http10" /> (<c>-0</c>) ends the request line in
/// <c>HTTP/1.0</c>, adds no <c>Expect: 100-continue</c>, and fails a body of unknown length
/// with exit 25 once connected, sending nothing. <see cref="HttpRequestOptions.Raw" /> writes a
/// chunked body undecoded, chunk lines included, until the server closes, and refuses no
/// transfer coding. <see cref="HttpRequestOptions.IgnoreContentLength" /> reads a body that is
/// not chunked until the server closes, whatever its Content-Length says. Either leaves a
/// connection whose body ran to close unused afterwards (<see cref="HttpResponseBodyFraming" />;
/// measured on curl 8.21.0, BL-180 Notes).
/// </para>
/// <para>
/// The first request carries the authenticator's answer to no challenge, placed after
/// <c>Host</c> (<c>Basic dTpw</c> for <c>-u u:p</c>). A 401 to a request that carried none
/// is answered once more when the authenticator answers its challenges and the body, if any,
/// is bytes that can be sent again: the 401's head and trailers are written, its body is read
/// and discarded, and the retry goes out on the same connection unless the 401 closes it
/// (<see cref="HttpConnectionPersistence" />), in which case on a new one. A 401 that is not
/// retried is the result, exit 0, or exit 22 under <c>-f</c>; the 401 a retry answers never
/// fails the transfer. Measured on curl 8.21.0 (BL-181 Notes, ADR-0034).
/// </para>
/// <para>
/// A 417 that arrives while the body waits for <c>100 Continue</c> is answered, unless
/// <c>-f</c> is set or the 417 closes the connection, by resending the request once on the
/// same connection without curl's own <c>Expect</c> line and without the wait: the 417's head
/// and trailers are written, its body is read and discarded, and the resend's response is the
/// result; an <c>-H</c> <c>Expect</c> line is sent again. Measured on curl 8.21.0 (BL-260 Notes).
/// A 417 that arrives once the wait ran out, while the body is being sent, stops the sending
/// at the piece under way, and is answered the same way but on a new connection, with the body
/// sent from its start; a stream that cannot seek goes on from the first byte not sent. Under
/// <c>-f</c>, or when the 417 closes the connection, sending stops just the same and the 417
/// is the result. Measured on curl 8.21.0 (BL-319 Notes).
/// </para>
/// <para>
/// With a cookie store, every request, an authentication retry included, asks it afresh for
/// the <c>Cookie</c> value (sent over TLS for <c>https</c>, at the time from
/// <see cref="ITransferContext.TimeProvider" />), and every final response head read, a 3xx
/// or a 401 included, hands its <c>Set-Cookie</c> values to it in received order, before the
/// head is written. Measured on curl 8.21.0 (BL-182 Notes).
/// </para>
/// <para>
/// With <see cref="HttpRequestOptions.ForwardProxy" /> an HTTP-kind proxy
/// (<see cref="ProxyKind.Http" />, <see cref="ProxyKind.Http10" /> or
/// <see cref="ProxyKind.Https" />), an <c>http</c> URL and no
/// <see cref="HttpRequestOptions.ProxyTunnel" />, the handler connects to the proxy itself (with
/// TLS for <see cref="ProxyKind.Https" />) and sends the request in absolute form, with the
/// authenticator's pre-emptive <c>Proxy-Authorization</c> for the proxy's credential and
/// <c>Proxy-Connection: Keep-Alive</c>. Any other proxy - an <c>https</c> URL, <c>-p</c>, or a
/// SOCKS kind - is handed to the connector as <see cref="ConnectTarget.Proxy" /> to tunnel
/// through, and the request goes in origin form over the connection it returns. Measured on
/// curl 8.21.0 (BL-183 Notes).
/// </para>
/// <para>
/// An <c>ftp</c> URL is served the same way when forwarded through such a proxy, as libcurl
/// hands <c>ftp</c> to its HTTP code when not tunnelling (ADR-0056, rule 3): a GET for the
/// absolute <c>ftp://</c> URI with the port always on <c>Host</c> (<c>Host: example.com:21</c>),
/// and the proxy's reply is the response. <c>ftp</c> is not among
/// <see cref="SupportedSchemes" />: <c>Curl.Console</c> routes an <c>ftp</c> URL here only
/// when it is forwarded (BL-344). Measured on curl 8.21.0 (BL-330 Notes).
/// </para>
/// <para>
/// <see cref="ITransferContext.MaxTime" /> limits the whole transfer, authentication retry
/// included, counted from <see cref="ITransferContext.OperationStarted" /> when a redirect
/// chain set it, and <see cref="ITransferContext.ConnectTimeout" /> (300 seconds when not given)
/// each connect (<see cref="HttpTransferDeadline" />). A limit that passes during a connect
/// ends it with exit 28 and <c>Connection timed out after N milliseconds</c>; <c>-m</c> passing
/// after it ends the transfer with exit 28 and <c>Operation timed out after N milliseconds with
/// M bytes received</c>, or <c>M out of T bytes</c> while a Content-Length body is read. A
/// connection that fails a write ends the transfer with exit 55. The connect message's N
/// counts from this call, the operation message's from the operation's start. Measured on
/// curl 8.21.0 (BL-174 and BL-299 Notes).
/// </para>
/// <para>
/// <see cref="ITransferContext.ResumeFrom" /> above zero resumes a <c>-T</c> upload from that
/// offset with a <c>Content-Range</c> (<see cref="HttpUploadResume" />, BL-332 Notes); for a
/// request without a body it sends <c>Range: bytes=N-</c>, and else
/// <see cref="ITransferContext.Range" /> sends its range, for a request without a body
/// (<see cref="HttpRangeHeader" />); <see cref="ITransferContext.TimeCondition" /> sends
/// <c>If-Modified-Since</c> or <c>If-Unmodified-Since</c>. Once the final head is written,
/// <see cref="HttpDownloadConditions" /> ends the transfer with exit 63 for a Content-Length over
/// <see cref="ITransferContext.MaxFileSize" /> and with exit 33 for a resume the response does not
/// honour, and delivers no body for a 416 to a resume or an unmet <c>-z</c> condition. A body
/// that grows past the limit ends the transfer with exit 63 after as many bytes as it allows.
/// A successful result carries the <c>Last-Modified</c> time (<see cref="HttpLastModified" />).
/// Measured on curl 8.21.0 (BL-178 Notes, ADR-0044).
/// </para>
/// <para>
/// <see cref="ITransferContext.Progress" /> is told the transfer started once the first
/// connection is established, so a connect failure is never reported as started; the response
/// body bytes the output accepts, with the Content-Length as the expected total, or none when
/// the body has none; and the request body bytes sent, with the body's length when known. A body
/// read and discarded (a 3xx under <c>-L</c>, or a response a retry answers) is not reported,
/// and a body sent again by a retry is not reported below the count already reported
/// (<see cref="HttpTransferProgress" />, ADR-0045, ADR-0065).
/// </para>
/// <para>
/// Every report carries <see cref="TransferTimings" /> on <see cref="ITransferContext.TimeProvider" />:
/// the start of this method, the connector's timings, the moment before the first request byte,
/// the moment the request was sent, the first response byte
/// (<see cref="HttpFirstByteTimingConnection" />) and the report's end. A failed connect reports
/// one timestamp, taken as it failed, for all but the start and the connect, as curl 8.21.0
/// does (measured, ADR-0075).
/// </para>
/// </remarks>
public sealed class HttpProtocolHandler(
    IConnector connector,
    IHttpAuthenticator authenticator,
    ICookieStore? cookieStore = null) : IProtocolHandler
{
    private static readonly string[] Schemes = ["http", "https"];

    private readonly IConnector connector = connector ?? throw new ArgumentNullException(nameof(connector));

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <summary>
    /// Gets the authenticator the handler was given.
    /// </summary>
    internal IHttpAuthenticator Authenticator { get; } =
        authenticator ?? throw new ArgumentNullException(nameof(authenticator));

    /// <summary>
    /// Gets the cookie store the handler was given, or <see langword="null" /> when cookies are off.
    /// </summary>
    internal ICookieStore? CookieStore { get; } = cookieStore;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long started = context.TimeProvider.GetTimestamp();
        HttpRequestOptions options = context.Http ?? new HttpRequestOptions();
        HttpRequestFraming framing = HttpRequestFraming.Of(options, [.. options.Headers.Select(HttpCustomHeader.Parse)], context.NoBody, context.Upload, context.ResumeFrom, context.Range);
        HttpAuthRequest authRequest = new(
            framing.Method,
            context.Url,
            options.RequestTarget ?? HttpUrlText.RequestTarget(context.Url),
            context.Credentials,
            options.BearerToken,
            options.AuthSchemes,
            IsProxy: false);
        ProxyEndpoint? forwardProxy = ForwardProxyOf(context.Url, options);
        using HttpTransferDeadline deadline = new(context);
        HttpRequestPlan plan = new(context, options, framing, authRequest, Authenticator.CreateAuthorization(authRequest, []))
        {
            Started = started,
            Deadline = deadline,
            Progress = new HttpTransferProgress(context.Progress),
            ForwardProxy = forwardProxy,
            ProxyAuthorization = forwardProxy is null ? null : ProxyAuthorizationFor(authRequest, forwardProxy),
        };
        return await ConnectAndExchangeAsync(plan, earlier: null).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the connect target for <paramref name="url" />: its host without IPv6
    /// brackets, its port, and TLS for <c>https</c>.
    /// </summary>
    private static ConnectTarget TargetOf(CurlUrl url) =>
        new(url.IdnHost, url.Port, url.Scheme == "https");

    /// <summary>
    /// Builds the connect target for <paramref name="plan" />: the forward proxy itself, with
    /// TLS for an HTTPS proxy; or else the URL's host, tunnelled through the transfer's proxy
    /// when it has one.
    /// </summary>
    private static ConnectTarget TargetOf(HttpRequestPlan plan) =>
        plan.ForwardProxy is { } proxy
            ? new ConnectTarget(proxy.Host, proxy.Port, proxy.Kind == ProxyKind.Https)
            : TargetOf(plan.Context.Url) with { Proxy = plan.Options.ForwardProxy };

    /// <summary>
    /// Gives the proxy the request is forwarded through in absolute form: an HTTP-kind proxy,
    /// for an <c>http</c> or <c>ftp</c> URL, without <c>-p</c>. Every other proxy is tunnelled through by
    /// the connector, and <see langword="null" /> is returned for it.
    /// </summary>
    private static ProxyEndpoint? ForwardProxyOf(CurlUrl url, HttpRequestOptions options) =>
        options.ForwardProxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https } proxy
            && !options.ProxyTunnel
            && !TargetOf(url).UseTls
            ? proxy
            : null;

    /// <summary>
    /// Asks the authenticator for the pre-emptive <c>Proxy-Authorization</c> value: Basic, curl's
    /// default proxy scheme, for the proxy's credential, with no bearer token.
    /// </summary>
    private string? ProxyAuthorizationFor(HttpAuthRequest originRequest, ProxyEndpoint proxy) =>
        Authenticator.CreateAuthorization(
            originRequest with { Credential = proxy.Credential, BearerToken = null, AllowedSchemes = HttpAuthSchemes.Basic, IsProxy = true },
            []);

    /// <summary>
    /// Opens a connection and sends <paramref name="plan" /> on it, then each retry a response
    /// asks for - an authentication retry, or a resend without <c>Expect</c> - on the same
    /// connection while it stays open, or on a new one when it closes.
    /// </summary>
    /// <param name="plan">The request to send.</param>
    /// <param name="earlier">
    /// The report of the exchange whose response asked for this retry, or
    /// <see langword="null" /> for the first.
    /// </param>
    private async ValueTask<TransferResult> ConnectAndExchangeAsync(HttpRequestPlan plan, TransferReport? earlier)
    {
        ConnectResult connect = await plan.Deadline.ConnectAsync(connector, TargetOf(plan)).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return TransferResult.Failure(connect.ExitCode, connect.ErrorMessage!) with { Report = FailedConnectReport(plan) };
        }

        plan.Progress.ReportTransferStarted();

        HttpAttemptOutcome outcome;
        await using (connection.ConfigureAwait(false))
        {
            outcome = await ExchangeAsync(plan, connect, connection, earlier, newConnection: true).ConfigureAwait(false);
            while (outcome.Retry is { } retry && outcome.KeepsAlive)
            {
                outcome = await ExchangeAsync(retry, connect, connection, outcome.Result.Report, newConnection: false).ConfigureAwait(false);
            }
        }

        return outcome.Retry is { } reconnect
            ? await ConnectAndExchangeAsync(reconnect, outcome.Result.Report).ConfigureAwait(false)
            : outcome.Result;
    }

    /// <summary>
    /// Reports a connect that failed: whether it went to a forward proxy, and the transfer's
    /// timings with <c>%{time_pretransfer}</c>, <c>%{time_posttransfer}</c> and
    /// <c>%{time_starttransfer}</c> taken when it failed, as curl 8.21.0 takes them after a
    /// refused connect or a failed resolve (measured, ADR-0075).
    /// </summary>
    private static TransferReport FailedConnectReport(HttpRequestPlan plan)
    {
        long failed = plan.Context.TimeProvider.GetTimestamp();
        return new TransferReport
        {
            UsedProxy = plan.Options.ForwardProxy is not null,
            Timings = new TransferTimings(plan.Started, null, failed, failed, failed, failed),
        };
    }

    /// <summary>
    /// Sends the request on <paramref name="connection" /> and reads the response into the
    /// transfer's outputs; or, when the response is one this handler retries (a 401 it
    /// answers, or a 417 to a request whose body waited for <c>100 Continue</c>), writes its
    /// head and trailers, reads and discards its body, and returns the retry's plan.
    /// </summary>
    private async ValueTask<HttpAttemptOutcome> ExchangeAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection connection,
        TransferReport? earlier,
        bool newConnection)
    {
        ITransferContext context = plan.Context;
        HttpRequestOptions options = plan.Options;
        HttpRequestFraming framing = plan.Framing;
        CancellationToken cancellationToken = plan.Deadline.Token;
        byte[] request = HttpRequestHeadFormatter.Format(
            context.Url,
            options,
            context.NoBody,
            plan.Authorization,
            CookieHeaderFor(context),
            plan.ForwardProxy is not null,
            plan.ProxyAuthorization,
            HttpRangeHeader.ValueFor(context, framing.Body is not null),
            context.TimeCondition,
            framing);
        HttpFirstByteTimingConnection timedConnection = new(connection, context.TimeProvider);
        IConnection responseConnection = framing.AwaitsContinue ? new HttpContinueWaitConnection(timedConnection) : timedConnection;
        HttpRequestBodyWriter upload = new(connection)
        {
            SharedHeadLength = framing.AwaitsContinue ? 0 : request.Length,
            IsUpload = framing.IsUpload,
            Progress = plan.Progress,
            ExpectationWatch = responseConnection as HttpContinueWaitConnection,
        };
        HttpExchange exchange = new(connect, connection, framing.Method, request.Length, upload, earlier, newConnection)
        {
            UsedProxy = options.ForwardProxy is not null,
            Started = plan.Started,
            TimeProvider = context.TimeProvider,
            ResponseConnection = timedConnection,
        };
        HttpResponseBodyReader body = new(responseConnection)
        {
            PassesTransferCoding = options.Raw,
            IgnoresContentLength = options.IgnoreContentLength,
            DecodesTransferCoding = options.TransferEncoding,
        };
        HttpRequestPlan? retry = null;
        HttpBodyDelivery delivery = HttpBodyDelivery.Deliver;
        try
        {
            ThrowIfRefused(framing);
            exchange.RequestReady = context.TimeProvider.GetTimestamp();
            await HttpConnectionSend.WriteAsync(connection, request, cancellationToken).ConfigureAwait(false);
            await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
            bool bodyLeftUnsent = await SendBodyAsync(context.TimeProvider, framing, responseConnection, upload, cancellationToken).ConfigureAwait(false);
            exchange.RequestSent = context.TimeProvider.GetTimestamp();
            exchange.Head = await new HttpResponseHeadReader(responseConnection).ReadAsync(cancellationToken).ConfigureAwait(false);
            exchange.RedirectUrl = HttpRedirectLocation.Find(context.Url, exchange.Head);
            StoreCookies(context, exchange.Head);
            await WriteHeadersAsync(context.HeaderOutput, exchange.Head.HeadBytes, cancellationToken).ConfigureAwait(false);
            retry = RetryOf(plan, exchange.Head, bodyLeftUnsent, upload);
            HttpFailMode fail = retry is null ? options.Fail : HttpFailMode.None;
            ThrowIfFailing(fail, HttpFailMode.Fail, exchange.Head);
            bool discardsBody = retry is not null || (options.FollowRedirects && exchange.RedirectUrl is not null);
            delivery = DeliveryOf(plan, exchange.Head, discardsBody);
            await ReadBodyAsync(plan, exchange.Head, body, delivery, discardsBody, cancellationToken).ConfigureAwait(false);
            ThrowIfFailing(fail, HttpFailMode.FailWithBody, exchange.Head);
        }
        catch (HttpTransferException failure)
        {
            TransferResult failed = TransferResult.Failure(failure.ExitCode, failure.Message, body.BytesWritten)
                with
            { Report = exchange.Report(body.BytesWritten) };
            return new HttpAttemptOutcome(failed, null, KeepsAlive: false);
        }
        catch (OperationCanceledException) when (plan.Deadline.EndedByLimit)
        {
            string message = HttpTransferMessages.OperationTimedOut(plan.Deadline.OperationElapsedMilliseconds, body.BytesWritten, body.ExpectedLength);
            TransferResult timedOut = TransferResult.Failure(CurlExitCode.OperationTimedOut, message, body.BytesWritten)
                with
            { Report = exchange.Report(body.BytesWritten) };
            return new HttpAttemptOutcome(timedOut, null, KeepsAlive: false);
        }

        TransferResult result = Succeeded(delivery, exchange.Head!, exchange.Report(body.BytesWritten));
        return new HttpAttemptOutcome(result, retry, KeepsAlive(plan, exchange.Head, upload));
    }

    /// <summary>
    /// Decides whether the connection can carry another request: not after a body a
    /// <c>417</c> cut short, which curl 8.21.0 shuts the connection on (measured, BL-319
    /// Notes), and else as <see cref="HttpConnectionPersistence" /> says.
    /// </summary>
    private static bool KeepsAlive(HttpRequestPlan plan, HttpResponseHead head, HttpRequestBodyWriter upload) =>
        !upload.CutShort
            && HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, plan.Options.Raw, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding);

    /// <summary>
    /// Fails a request before any byte of it is sent, as curl 8.21.0 does once connected: a
    /// <c>-T</c> upload whose <c>-C</c> offset cannot be resumed from (exit 18 or 26, measured,
    /// BL-332 Notes), and an HTTP/1.0 request whose body length is unknown (exit 25, measured,
    /// BL-180 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">
    /// <see cref="HttpRequestFraming.ResumeFailure" /> or
    /// <see cref="HttpRequestFraming.RefusesUnknownLength" /> is set.
    /// </exception>
    private static void ThrowIfRefused(HttpRequestFraming framing)
    {
        if (framing.ResumeFailure is { } resumeFailure)
        {
            throw resumeFailure;
        }

        if (framing.RefusesUnknownLength)
        {
            throw new HttpTransferException(CurlExitCode.UploadFailed, HttpTransferMessages.ChunkedUploadNeedsHttp11);
        }
    }

    /// <summary>
    /// Applies <c>--max-filesize</c> to the head's Content-Length, then decides what becomes of
    /// a body that is not being discarded (<see cref="HttpDownloadConditions" />).
    /// </summary>
    /// <exception cref="HttpTransferException">
    /// The Content-Length is over the limit (exit 63), or a resume was not honoured (exit 33).
    /// </exception>
    private static HttpBodyDelivery DeliveryOf(HttpRequestPlan plan, HttpResponseHead head, bool discardsBody)
    {
        HttpDownloadConditions.ThrowIfContentLengthExceeds(plan.Context.MaxFileSize, head);
        return discardsBody ? HttpBodyDelivery.Deliver : HttpDownloadConditions.Decide(plan.Context, plan.Framing.Body is not null, head);
    }

    /// <summary>
    /// Reads the body into the transfer's output, or into nothing when it is discarded, held to
    /// the <c>--max-filesize</c> limit unless discarded, then writes a chunked body's trailers;
    /// or reads nothing when <paramref name="delivery" /> says there is no body to deliver.
    /// </summary>
    private static async ValueTask ReadBodyAsync(
        HttpRequestPlan plan,
        HttpResponseHead head,
        HttpResponseBodyReader body,
        HttpBodyDelivery delivery,
        bool discardsBody,
        CancellationToken cancellationToken)
    {
        if (delivery != HttpBodyDelivery.Deliver)
        {
            return;
        }

        ITransferContext context = plan.Context;
        Stream bodyOutput = discardsBody ? Stream.Null : context.Output;
        body.MaximumBodySize = discardsBody ? null : HttpDownloadConditions.LimitOf(context.MaxFileSize);
        body.Progress = discardsBody ? HttpTransferProgress.Silent : plan.Progress;
        await body.CopyAsync(head, context.NoBody, bodyOutput, DecodesContent(plan.Options, discardsBody), plan.Options.TransferEncoding && !discardsBody, cancellationToken)
            .ConfigureAwait(false);
        await WriteHeadersAsync(context.HeaderOutput, body.TrailerBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a successful exchange's result, carrying the <c>Last-Modified</c> time for
    /// <c>-R</c>: an unmet <c>-z</c> condition is marked so and reported as a 304, as curl
    /// 8.21.0's <c>%{response_code}</c> does for a response whose <c>Last-Modified</c> fails it.
    /// </summary>
    private static TransferResult Succeeded(HttpBodyDelivery delivery, HttpResponseHead head, TransferReport report)
    {
        DateTimeOffset? lastModified = HttpLastModified.Find(head);
        return delivery == HttpBodyDelivery.TimeConditionUnmet
            ? TransferResult.TimeConditionNotMet(lastModified) with { Report = report with { ResponseCode = 304 } }
            : TransferResult.Success(report.DownloadSize, lastModified) with { Report = report };
    }

    /// <summary>
    /// Asks the cookie store for the <c>Cookie</c> value to send to the transfer's URL, or
    /// gives <see langword="null" /> when cookies are off.
    /// </summary>
    private string? CookieHeaderFor(ITransferContext context) =>
        CookieStore?.GetCookieHeader(context.Url, TargetOf(context.Url).UseTls, context.TimeProvider.GetUtcNow());

    /// <summary>
    /// Hands the <c>Set-Cookie</c> values of <paramref name="head" />, in received order, to
    /// the cookie store, when cookies are on and the head has any.
    /// </summary>
    private void StoreCookies(ITransferContext context, HttpResponseHead head)
    {
        string[] setCookies = ValuesOf(head, "Set-Cookie");
        if (CookieStore is { } store && setCookies.Length > 0)
        {
            store.StoreFromResponse(context.Url, setCookies, context.TimeProvider.GetUtcNow());
        }
    }

    /// <summary>
    /// Decides whether a response is answered with one more request, and which: the same
    /// request with the <c>Authorization</c> value <see cref="RetryAuthorization" /> gives, or
    /// else, for a 417 <see cref="RetriesWithoutExpect" /> accepts, the same request without
    /// <c>Expect</c> and with the body <paramref name="upload" /> rewinds; <see langword="null" />
    /// when the response is the result.
    /// </summary>
    private HttpRequestPlan? RetryOf(HttpRequestPlan plan, HttpResponseHead head, bool bodyLeftUnsent, HttpRequestBodyWriter upload)
    {
        if (RetryAuthorization(plan, head) is { } authorization)
        {
            return plan.WithAuthorization(authorization);
        }

        return RetriesWithoutExpect(plan, head, bodyLeftUnsent || upload.CutShort) ? plan.WithoutExpect(upload.Rewound(plan.Framing.Body!)) : null;
    }

    /// <summary>
    /// Decides whether <paramref name="head" /> is answered by resending the request without
    /// <c>Expect</c>, as curl 8.21.0 does (measured, BL-260 and BL-319 Notes): a 417 that
    /// arrived before the whole body was sent - while it waited for <c>100 Continue</c>, or
    /// while it was being sent once the wait ran out - on a connection the 417 leaves open, and
    /// not under <c>-f</c>, which fails on the 417 itself. The resent request waits for
    /// nothing, so it is never resent again.
    /// </summary>
    private static bool RetriesWithoutExpect(HttpRequestPlan plan, HttpResponseHead head, bool bodyStopped) =>
        bodyStopped
            && head.StatusLine.StatusCode == 417
            && plan.Options.Fail != HttpFailMode.Fail
            && HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, plan.Options.Raw, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding);

    /// <summary>
    /// Decides whether a response is answered with one more request, and with what
    /// <c>Authorization</c> value: only a 401, only when the request that drew it sent none
    /// (a credential sent up front and refused ends the transfer, as in curl 8.21.0), only
    /// when its body can be sent again, and only when the authenticator answers the
    /// response's <c>WWW-Authenticate</c> challenges.
    /// </summary>
    private string? RetryAuthorization(HttpRequestPlan plan, HttpResponseHead head)
    {
        if (!MayRetry(plan, head))
        {
            return null;
        }

        string[] challenges = ValuesOf(head, "WWW-Authenticate");
        return challenges.Length == 0 ? null : Authenticator.CreateAuthorization(plan.AuthRequest, challenges);
    }

    /// <summary>
    /// Decides whether <paramref name="head" /> may be answered with a retry at all: a 401 to
    /// a request that sent no <c>Authorization</c> and whose body is not a stream.
    /// </summary>
    private static bool MayRetry(HttpRequestPlan plan, HttpResponseHead head) =>
        plan.Authorization is null && head.StatusLine.StatusCode == 401 && plan.Framing.Body is not StreamBody;

    /// <summary>
    /// Gets the values of every <paramref name="name" /> header of <paramref name="head" />,
    /// in received order.
    /// </summary>
    private static string[] ValuesOf(HttpResponseHead head, string name) =>
        [.. head.Headers
            .Where(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)];

    /// <summary>
    /// Decides whether the body is decoded: for <c>--compressed</c> without <c>--raw</c>, and
    /// not for a body that is read and discarded - a 3xx body <c>-L</c> follows past, which
    /// curl 8.21.0 does not decode (measured, BL-177 Notes), or a 401 body an authentication
    /// retry follows.
    /// </summary>
    private static bool DecodesContent(HttpRequestOptions options, bool discardsBody) =>
        options.Compressed && !options.Raw && !discardsBody;

    /// <summary>
    /// Sends the request body, if there is one: at once, or once
    /// <paramref name="responseConnection" /> has waited for <c>100 Continue</c> and not been
    /// answered with a final status instead.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when a final status arrived during the wait and the body was left
    /// unsent; <see langword="false" /> when the body was sent or there is none.
    /// </returns>
    private static async ValueTask<bool> SendBodyAsync(
        TimeProvider timeProvider,
        HttpRequestFraming framing,
        IConnection responseConnection,
        HttpRequestBodyWriter upload,
        CancellationToken cancellationToken)
    {
        if (framing.Body is not { } requestBody)
        {
            return false;
        }

        if (responseConnection is HttpContinueWaitConnection waiting
            && !await waiting.WaitForContinueAsync(timeProvider, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        await upload.WriteAsync(requestBody, framing.IsChunked, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Ends the transfer with exit 22 when <paramref name="fail" /> is
    /// <paramref name="failAt" /> and the final status is 400 or above: before the body for
    /// <c>-f</c>, after it for <c>--fail-with-body</c>. The head is written first either way.
    /// </summary>
    /// <exception cref="HttpTransferException">The status fails the transfer (exit 22).</exception>
    private static void ThrowIfFailing(HttpFailMode fail, HttpFailMode failAt, HttpResponseHead head)
    {
        int statusCode = head.StatusLine.StatusCode;
        if (fail == failAt && statusCode >= 400)
        {
            throw new HttpTransferException(CurlExitCode.HttpReturnedError, HttpTransferMessages.RequestedUrlReturnedError(statusCode));
        }
    }

    /// <summary>
    /// Writes head or trailer bytes to the header output as one write, or nothing when there
    /// is no header output or no bytes.
    /// </summary>
    /// <exception cref="HttpTransferException">The header output failed the write (exit 23).</exception>
    private static async ValueTask WriteHeadersAsync(Stream? headerOutput, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (headerOutput is null || bytes.IsEmpty)
        {
            return;
        }

        try
        {
            await headerOutput.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (OutputWriteFailedException failure)
        {
            throw WriteFailed(bytes.Length, failure.BytesAccepted);
        }
        catch (IOException)
        {
            throw WriteFailed(bytes.Length, 0);
        }
    }

    private static HttpTransferException WriteFailed(int passed, int returned) =>
        new(CurlExitCode.WriteError, HttpTransferMessages.OutputWriteFailed(passed, returned));

    /// <summary>
    /// What one exchange has learned so far, turned into a <see cref="TransferReport" /> when
    /// the transfer ends, successfully or not.
    /// </summary>
    /// <param name="connect">The connect that opened the connection.</param>
    /// <param name="connection">The connection the exchange runs on.</param>
    /// <param name="method">The request's method.</param>
    /// <param name="headSize">The request head's size in bytes.</param>
    /// <param name="upload">The writer that sends the request body.</param>
    /// <param name="earlier">
    /// The report of the exchange a retry answers, whose request and header sizes and
    /// connections this one's report adds to, as curl 8.21.0's <c>%{size_request}</c>,
    /// <c>%{size_header}</c> and <c>%{num_connects}</c> do (BL-181 and BL-260 Notes);
    /// <see langword="null" /> for the first exchange.
    /// </param>
    /// <param name="newConnection">
    /// <see langword="true" /> when this exchange opened its connection, rather than reusing
    /// the one <paramref name="earlier" /> used.
    /// </param>
    private sealed class HttpExchange(
        ConnectResult connect,
        IConnection connection,
        string method,
        int headSize,
        HttpRequestBodyWriter upload,
        TransferReport? earlier,
        bool newConnection)
    {
        /// <summary>
        /// Gets or sets the final response head, <see langword="null" /> until it is read.
        /// </summary>
        internal HttpResponseHead? Head { get; set; }

        /// <summary>
        /// Gets a value indicating whether the transfer goes through a proxy, forwarded or
        /// tunnelled, the report's <see cref="TransferReport.UsedProxy" />.
        /// </summary>
        internal bool UsedProxy { get; init; }

        /// <summary>
        /// Gets or sets the final head's <c>Location</c> resolved against the request URL,
        /// <see langword="null" /> until the head is read or when it names none.
        /// </summary>
        internal string? RedirectUrl { get; set; }

        /// <summary>
        /// Gets the moment the transfer began, the report's <see cref="TransferTimings.Started" />.
        /// </summary>
        internal required long Started { get; init; }

        /// <summary>
        /// Gets the clock every timestamp of the exchange is taken on.
        /// </summary>
        internal required TimeProvider TimeProvider { get; init; }

        /// <summary>
        /// Gets the connection the response is read through, which records when its first
        /// byte arrived.
        /// </summary>
        internal required HttpFirstByteTimingConnection ResponseConnection { get; init; }

        /// <summary>
        /// Gets or sets the moment the first request byte was about to be sent,
        /// <see langword="null" /> until then.
        /// </summary>
        internal long? RequestReady { get; set; }

        /// <summary>
        /// Gets or sets the moment the last request byte was sent, <see langword="null" />
        /// until then.
        /// </summary>
        internal long? RequestSent { get; set; }

        /// <summary>
        /// Builds the report: what the connect and request told, their timings with the
        /// transfer's end taken now, and what the final head told once it was read. The request
        /// size counts the body bytes sent as well as the head, as curl 8.21.0's
        /// <c>%{size_request}</c> does (BL-175 Notes).
        /// </summary>
        /// <param name="downloadSize">The body bytes the output accepted.</param>
        /// <returns>The report.</returns>
        internal TransferReport Report(long downloadSize)
        {
            TransferReport report = new()
            {
                Method = method,
                RequestSize = (earlier?.RequestSize ?? 0) + headSize + upload.BytesWritten,
                UploadSize = upload.BytesWritten,
                DownloadSize = downloadSize,
                HeaderSize = earlier?.HeaderSize ?? 0,
                ConnectionCount = (earlier?.ConnectionCount ?? 0) + (newConnection ? 1 : 0),
                ProxyConnectResponseCode = connect.ProxyConnectResponseCode,
                UsedProxy = UsedProxy,
                LocalEndPoint = connect.LocalEndPoint,
                PeerCertificates = connect.PeerCertificates,
                RemoteEndPoint = connection.RemoteEndPoint as IPEndPoint,
                Timings = new TransferTimings(
                    Started,
                    connect.Timings,
                    RequestReady,
                    RequestSent,
                    ResponseConnection.FirstByteReceived,
                    TimeProvider.GetTimestamp()),
            };
            return Head is null ? report : WithHead(report, Head) with { RedirectUrl = RedirectUrl };
        }

        private static TransferReport WithHead(TransferReport report, HttpResponseHead head) =>
            report with
            {
                ResponseCode = head.StatusLine.StatusCode,
                HttpVersion = head.StatusLine.Version,
                ResponseHeaders = [.. head.Headers.Select(header => KeyValuePair.Create(header.Name, header.Value))],
                ContentType = head.Headers.LastOrDefault(IsContentType)?.Value,
                HeaderSize = report.HeaderSize + head.HeadBytes.Length,
            };

        private static bool IsContentType(HttpResponseHeader header) =>
            string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One request as the handler sends it: the transfer, its options and framing, what the
    /// authenticator is asked about, and the <c>Authorization</c> value it is sent with.
    /// </summary>
    /// <param name="Context">The transfer.</param>
    /// <param name="Options">The transfer's HTTP options, defaults when it has none.</param>
    /// <param name="Framing">The request's method and body framing.</param>
    /// <param name="AuthRequest">The request as the authenticator is asked about it.</param>
    /// <param name="Authorization">
    /// The <c>Authorization</c> value to send, or <see langword="null" /> to send none.
    /// </param>
    private sealed class HttpRequestPlan(
        ITransferContext Context,
        HttpRequestOptions Options,
        HttpRequestFraming Framing,
        HttpAuthRequest AuthRequest,
        string? Authorization)
    {
        /// <summary>Gets the transfer.</summary>
        public ITransferContext Context { get; } = Context;

        /// <summary>Gets the transfer's HTTP options, defaults when it has none.</summary>
        public HttpRequestOptions Options { get; } = Options;

        /// <summary>Gets the request's method and body framing.</summary>
        public HttpRequestFraming Framing { get; } = Framing;

        /// <summary>Gets the request as the authenticator is asked about it.</summary>
        public HttpAuthRequest AuthRequest { get; } = AuthRequest;

        /// <summary>
        /// Gets the <c>Authorization</c> value to send, or <see langword="null" /> to send none.
        /// </summary>
        public string? Authorization { get; } = Authorization;

        /// <summary>
        /// Gets the time limits the transfer runs under, whose token cancels every read and
        /// write of the exchange.
        /// </summary>
        public required HttpTransferDeadline Deadline { get; init; }

        /// <summary>
        /// Gets the moment the transfer began, shared by every request it sends: the origin
        /// of every <c>%{time_*}</c> (<see cref="TransferTimings.Started" />).
        /// </summary>
        public required long Started { get; init; }

        /// <summary>
        /// Gets where the transfer's progress is reported, shared by every request it sends.
        /// </summary>
        public required HttpTransferProgress Progress { get; init; }

        /// <summary>
        /// Gets the proxy the request is forwarded through in absolute form, or
        /// <see langword="null" /> when it goes to the origin, directly or through a tunnel.
        /// </summary>
        public ProxyEndpoint? ForwardProxy { get; init; }

        /// <summary>
        /// Gets the <c>Proxy-Authorization</c> value to send, or <see langword="null" /> to
        /// send none.
        /// </summary>
        public string? ProxyAuthorization { get; init; }

        /// <summary>
        /// Makes the same request sent with <paramref name="authorization" /> instead.
        /// </summary>
        /// <param name="authorization">The <c>Authorization</c> value the retry is sent with.</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithAuthorization(string authorization) => With(Framing, authorization);

        /// <summary>
        /// Makes the same request without curl's own <c>Expect</c> and without the wait for
        /// <c>100 Continue</c> (<see cref="HttpRequestFraming.WithoutExpect" />), sending
        /// <paramref name="body" />.
        /// </summary>
        /// <param name="body">The body to resend.</param>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan WithoutExpect(HttpRequestBody body) => With(Framing.WithoutExpect(body), Authorization);

        private HttpRequestPlan With(HttpRequestFraming framing, string? authorization) =>
            new(Context, Options, framing, AuthRequest, authorization)
            {
                Started = Started,
                Deadline = Deadline,
                Progress = Progress,
                ForwardProxy = ForwardProxy,
                ProxyAuthorization = ProxyAuthorization,
            };
    }

    /// <summary>
    /// How one exchange ended: its result, and whether a retry follows.
    /// </summary>
    /// <param name="Result">The result, with the report so far.</param>
    /// <param name="Retry">
    /// The request to send next, or <see langword="null" /> when the result is final.
    /// </param>
    /// <param name="KeepsAlive">
    /// <see langword="true" /> when the retry may be sent on the same connection.
    /// </param>
    private sealed class HttpAttemptOutcome(TransferResult Result, HttpRequestPlan? Retry, bool KeepsAlive)
    {
        /// <summary>Gets the result, with the report so far.</summary>
        public TransferResult Result { get; } = Result;

        /// <summary>
        /// Gets the request to send next, or <see langword="null" /> when the result is final.
        /// </summary>
        public HttpRequestPlan? Retry { get; } = Retry;

        /// <summary>
        /// Gets a value indicating whether the retry may be sent on the same connection.
        /// </summary>
        public bool KeepsAlive { get; } = KeepsAlive;
    }
}
