using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Serves the <c>http</c> and <c>https</c> schemes over HTTP/1.1, HTTP/1.0 for <c>-0</c>, HTTP/2 on a
/// connection that speaks it (<see cref="Http2StreamConnection" />, ADR-0159), or HTTP/3 over QUIC
/// for <c>--http3</c> and <c>--http3-only</c> (<see cref="Http3StreamConnection" />, ADR-0172): connects through the
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
/// <param name="proxyAuthSchemes">
/// The schemes a forward proxy may be answered with: the <c>--proxy-*</c> auth switches' pick,
/// or <see cref="HttpAuthSchemes.Basic" />, curl's default.
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
/// Through a forward proxy a 407 is answered the same way, apart from the 401: once, when the
/// request that drew it sent no <c>Proxy-Authorization</c> and the authenticator answers its
/// <c>Proxy-Authenticate</c> challenges for the proxy's credential with the scheme set the
/// handler was given (the <c>--proxy-*</c> auth switches' pick). Each retry keeps the other
/// header as it was, so a 407 and a 401 can each be answered in the same transfer, in either
/// order. Measured on curl 8.21.0 (BL-603 Notes, ADR-0187).
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
/// That resend keeps an <c>-H</c> <c>Expect: 100-continue</c> line's wait, so it can draw
/// another 417 and another resend. Every resend counts as a followed redirect in
/// <see cref="TransferReport.RedirectCount" />, with or without <c>-L</c>, and one that would
/// pass <see cref="HttpRequestOptions.MaxRedirects" /> is not sent: the 417's head is written
/// and the transfer ends with exit 47, <c>Maximum (N) redirects followed</c>. Measured on curl
/// 8.21.0 (BL-396 Notes).
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
/// offset with a <c>Content-Range</c> (<see cref="HttpUploadResume" />, BL-332 Notes), and
/// <see cref="ITransferContext.ResumeUploadFromUnknownOffset" /> sends the whole upload with
/// <c>Content-Range: bytes 0-(L-1)/L</c> (BL-351 Notes); for a
/// request without a body it sends <c>Range: bytes=N-</c>, and else
/// <see cref="ITransferContext.RangeText" /> sends the <c>-r</c> text as typed, for a request without a body
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
    ICookieStore? cookieStore = null,
    HttpAuthSchemes proxyAuthSchemes = HttpAuthSchemes.Basic) : IProtocolHandler
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

    /// <summary>
    /// Gets the schemes a forward proxy may be answered with: <see cref="HttpAuthSchemes.Basic" />,
    /// curl's default, unless the handler was given the <c>--proxy-*</c> auth switches' pick.
    /// </summary>
    internal HttpAuthSchemes ProxyAuthSchemes { get; } = proxyAuthSchemes;

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
        HttpRequestFraming framing = HttpRequestFraming.Of(options, HttpRequestHeadFormatter.CustomHeadersOf(options.Headers, options), context.NoBody, context.Upload, context.ResumeFrom, context.RangeText, context.ResumeUploadFromUnknownOffset);
        HttpAuthRequest authRequest = new(
            framing.Method,
            context.Url,
            options.RequestTarget ?? HttpUrlText.RequestTarget(context.Url),
            context.Credentials,
            options.BearerToken,
            options.AuthSchemes,
            IsProxy: false);
        ProxyEndpoint? forwardProxy = ForwardProxyOf(context.Url, options);
        HttpAuthRequest? proxyAuthRequest = forwardProxy is null ? null : ProxyAuthRequestOf(authRequest, forwardProxy);
        using HttpTransferDeadline deadline = new(context);
        string? authorization = await Authenticator.CreateAuthorizationAsync(authRequest, [], context.CancellationToken).ConfigureAwait(false);
        HttpRequestPlan plan = new(context, options, framing, authRequest, authorization)
        {
            Started = started,
            Deadline = deadline,
            Progress = new HttpTransferProgress(context.Progress),
            ForwardProxy = forwardProxy,
            ProxyAuthRequest = proxyAuthRequest,
            ProxyAuthorization = proxyAuthRequest is null ? null : Authenticator.CreateAuthorization(proxyAuthRequest, []),
            RedirectsFollowed = options.RedirectsFollowed,
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
    /// Builds the connect target for <paramref name="plan" />: the forward proxy itself, marked
    /// <see cref="ConnectTarget.IsForwardProxy" />, with TLS for an HTTPS proxy; or else the URL's host, tunnelled through the transfer's proxy
    /// when it has one. Either is pooled under <c>https</c> for an <c>https</c> URL and
    /// <c>http</c> otherwise, so a forward proxy connection serves every origin behind it, and
    /// carries the transfer's events for the connector to report through (ADR-0050).
    /// </summary>
    private static ConnectTarget TargetOf(HttpRequestPlan plan)
    {
        ConnectTarget urlTarget = TargetOf(plan.Context.Url);
        ConnectTarget target = plan.ForwardProxy is { } proxy
            ? new ConnectTarget(proxy.Host, proxy.Port, proxy.Kind == ProxyKind.Https) { IsForwardProxy = true }
            : urlTarget with { Proxy = plan.Options.ForwardProxy };
        return target with { PoolScheme = urlTarget.UseTls ? "https" : "http", Events = plan.Context.Events };
    }

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
    /// Makes the request the authenticator is asked about for the forward proxy: the origin's
    /// method, URL and request target (Digest's <c>uri</c> is the origin form, as curl 8.21.0
    /// sends it), the proxy's credential, no bearer token, and <see cref="ProxyAuthSchemes" />.
    /// Answered with no challenges it gives the pre-emptive <c>Proxy-Authorization</c>, which
    /// only Basic sends.
    /// </summary>
    private HttpAuthRequest ProxyAuthRequestOf(HttpAuthRequest originRequest, ProxyEndpoint proxy) =>
        originRequest with { Credential = proxy.Credential, BearerToken = null, AllowedSchemes = ProxyAuthSchemes, IsProxy = true };

    /// <summary>
    /// Opens a connection, or takes a pooled one, and sends <paramref name="plan" /> on it, then
    /// each retry a response asks for - an authentication retry, or a resend without
    /// <c>Expect</c> - on the same connection while it stays open, or on a new one when it
    /// closes. A pooled connection that dies before its response begins is closed and the
    /// request sent again once on a fresh one, as curl 8.21.0 does (BL-336 Notes).
    /// </summary>
    /// <remarks>
    /// The connection is marked reusable when the last response on it persists, and the
    /// end-of-transfer <c>-v</c> line is reported once it is disposed (ADR-0050), after the
    /// final exchange reports the transfer done (ADR-0111). A connect
    /// that fails reports <c>closing connection #N</c> after the connector's own lines, as
    /// curl 8.21.0 does for a failed resolve, a refused dial and a connect timeout (ADR-0105).
    /// </remarks>
    /// <param name="plan">The request to send.</param>
    /// <param name="earlier">
    /// The report of the exchange whose response asked for this retry, or
    /// <see langword="null" /> for the first.
    /// </param>
    private async ValueTask<TransferResult> ConnectAndExchangeAsync(HttpRequestPlan plan, TransferReport? earlier)
    {
        ConnectTarget target = TargetOf(plan);
        if (plan.Options.Version == HttpVersionPreference.Http3Only && plan.Context.Url.Scheme != "https")
        {
            return Http3NeedsHttps(plan);
        }

        ConnectResult connect = await ConnectAsync(plan, target).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.Closing(connect.ConnectionNumber));
            return TransferResult.Failure(connect.ExitCode, connect.ErrorMessage!) with
            {
                Report = FailedConnectReport(plan, connect),
                IsConnectionRefused = connect.IsConnectionRefused,
            };
        }

        plan.Progress.ReportTransferStarted();

        HttpAttemptOutcome outcome;
        await using (connection.ConfigureAwait(false))
        {
            outcome = await ExchangeOnConnectionAsync(plan, connect, connection, earlier).ConfigureAwait(false);
        }

        if (outcome.Retry is null)
        {
            plan.Progress.ReportTransferDone();
        }

        ReportConnectionEnd(plan.Context, target, connect, outcome);
        return outcome.Retry is { } reconnect
            ? await ConnectAndExchangeAsync(reconnect, outcome.Result.Report).ConfigureAwait(false)
            : outcome.Result;
    }

    /// <summary>
    /// Fails <c>--http3-only</c> with a URL that is not <c>https://</c> before connecting: exit 3,
    /// <c>HTTP/3 requested for non-HTTPS URL</c>, reported with <c>closing connection #-1</c>, as
    /// curl.se's ngtcp2 build does (measured, ADR-0144).
    /// </summary>
    private static TransferResult Http3NeedsHttps(HttpRequestPlan plan)
    {
        plan.Context.Events.ReportInfo(HttpTransferMessages.Http3NeedsHttps);
        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.Closing(-1));
        return TransferResult.Failure(CurlExitCode.UrlMalformat, HttpTransferMessages.Http3NeedsHttps) with
        {
            Report = FailedConnectReport(plan, ConnectResult.Failed(CurlExitCode.UrlMalformat, HttpTransferMessages.Http3NeedsHttps)),
        };
    }

    /// <summary>
    /// Opens the transfer's connection (ADR-0144 section 4, ADR-0172): over QUIC for
    /// <c>--http3-only</c>, whose failure is the transfer's; over QUIC and then, when that fails,
    /// over TCP for <c>--http3</c>, failing with the QUIC attempt's exit code and message when
    /// both fail; and over TCP for every other version, for an <c>http://</c> URL and through a
    /// proxy. A QUIC connection is handed on as an <see cref="Http3Session" />.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectAsync(HttpRequestPlan plan, ConnectTarget target)
    {
        if (!TriesQuic(plan, target))
        {
            return await plan.Deadline.ConnectAsync(connector, target).ConfigureAwait(false);
        }

        MultiplexedConnectResult quic = await plan.Deadline.ConnectMultiplexedAsync(connector, target).ConfigureAwait(false);
        if (quic.Connection is { } quicConnection)
        {
            return ConnectResult.Connected(
                new Http3Session(quicConnection),
                quic.Timings,
                quicConnection.LocalEndPoint as IPEndPoint,
                applicationProtocol: quicConnection.ApplicationProtocol);
        }

        if (plan.Options.Version == HttpVersionPreference.Http3Only)
        {
            return ConnectResult.Failed(quic.ExitCode, quic.ErrorMessage!);
        }

        ConnectResult tcp = await plan.Deadline.ConnectAsync(connector, target).ConfigureAwait(false);
        return tcp.Connection is null ? ConnectResult.Failed(quic.ExitCode, quic.ErrorMessage!, tcp.Timings, tcp.ConnectionNumber) : tcp;
    }

    /// <summary>
    /// Decides whether the transfer tries QUIC: <c>--http3</c> or <c>--http3-only</c> with an
    /// <c>https://</c> URL and no proxy.
    /// </summary>
    private static bool TriesQuic(HttpRequestPlan plan, ConnectTarget target) =>
        plan.Options.Version is HttpVersionPreference.Http3 or HttpVersionPreference.Http3Only
            && target.UseTls
            && plan.Options.ForwardProxy is null;

    /// <summary>
    /// Reports a connect that failed: whether it went to a forward proxy, and the transfer's
    /// timings with <c>%{time_pretransfer}</c>, <c>%{time_posttransfer}</c> and
    /// <c>%{time_starttransfer}</c> taken when it failed, as curl 8.21.0 takes them after a
    /// refused connect or a failed resolve (measured, ADR-0075), and whatever connect timings
    /// the connector recorded before it failed, so a refused dial still reports
    /// <c>%{time_namelookup}</c> (measured, ADR-0091).
    /// </summary>
    private static TransferReport FailedConnectReport(HttpRequestPlan plan, ConnectResult connect)
    {
        long failed = plan.Context.TimeProvider.GetTimestamp();
        return new TransferReport
        {
            UsedProxy = plan.Options.ForwardProxy is not null,
            Timings = new TransferTimings(plan.Started, connect.Timings, failed, failed, failed, failed),
        };
    }

    /// <summary>
    /// Sends <paramref name="plan" /> on <paramref name="connection" />, then each retry that
    /// may go on the same connection; then marks the connection reusable when the last
    /// response is reported left intact - it persists, or it is an HTTP/1.0 keep-alive body the
    /// server closed, which the pool then finds dead as curl does (ADR-0112) - or reports that a
    /// pooled connection that died is being given up. A connection that speaks HTTP/2 or HTTP/3
    /// (<see cref="StreamSessionOf" />) carries each request on a stream of its own and is never
    /// marked reusable, since the pool would hand it on without its session (BL-658 Notes).
    /// </summary>
    private async ValueTask<HttpAttemptOutcome> ExchangeOnConnectionAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection connection,
        TransferReport? earlier)
    {
        if (HttpTimeConditionLimit.Refuses(plan.Context.TimeCondition, OperatingSystem.IsWindows()))
        {
            connection.MarkReusable();
            return TimeValueRefused(plan, connect);
        }

        IHttpStreamSession? streams = StreamSessionOf(plan, connect, connection);
        HttpAttemptOutcome outcome = await ExchangeWithRetriesAsync(plan, connect, connection, earlier, streams).ConfigureAwait(false);
        if (outcome.ReportsLeftIntact && streams is null)
        {
            connection.MarkReusable();
        }
        else if (outcome.DiedBeforeResponse)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.ConnectionDiedRetrying);
        }

        return outcome;
    }

    /// <summary>
    /// Sends <paramref name="plan" />, framed for HTTP/2 or HTTP/3 when <paramref name="streams" /> is set,
    /// then each retry the response asks for while the connection stays open.
    /// </summary>
    private async ValueTask<HttpAttemptOutcome> ExchangeWithRetriesAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection connection,
        TransferReport? earlier,
        IHttpStreamSession? streams)
    {
        HttpRequestPlan first = streams is null ? plan : plan.ForHttp2OrHttp3();
        HttpAttemptOutcome outcome = await ExchangeAsync(first, connect, connection, earlier, newConnection: !connect.IsReused, streams).ConfigureAwait(false);
        while (outcome.Retry is { } retry && outcome.KeepsAlive)
        {
            outcome = await ExchangeAsync(retry, connect, connection, outcome.Result.Report, newConnection: false, streams).ConfigureAwait(false);
        }

        return outcome;
    }

    /// <summary>
    /// Decides whether the connection speaks HTTP/2 (ADR-0141): with
    /// <see cref="HttpVersionPreference.Http2PriorKnowledge" />, from the first byte, and else
    /// when the TLS handshake agreed <c>h2</c> with ALPN.
    /// </summary>
    private static bool SpeaksHttp2(HttpRequestPlan plan, ConnectResult connect) =>
        plan.Options.Version == HttpVersionPreference.Http2PriorKnowledge || connect.ApplicationProtocol == "h2";

    /// <summary>
    /// Gives the session that carries each request on a stream of its own over the connection:
    /// the HTTP/3 session a QUIC connect made, a new HTTP/2 session for a connection that speaks
    /// HTTP/2 (<see cref="SpeaksHttp2" />), or <see langword="null" /> for HTTP/1.x.
    /// </summary>
    private static IHttpStreamSession? StreamSessionOf(HttpRequestPlan plan, ConnectResult connect, IConnection connection) =>
        (IHttpStreamSession?)(connection as Http3Session) ?? (SpeaksHttp2(plan, connect) ? new Http2Session(connection) : null);

    /// <summary>
    /// Fails a request whose <c>-z</c> time curl 8.21.0 on Windows cannot write into its header
    /// (<see cref="HttpTimeConditionLimit" />): exit 43 before any byte is sent, the connection
    /// left intact, as measured (BL-381 Notes).
    /// </summary>
    private static HttpAttemptOutcome TimeValueRefused(HttpRequestPlan plan, ConnectResult connect)
    {
        TransferReport report = new()
        {
            UsedProxy = plan.Options.ForwardProxy is not null,
            LocalEndPoint = connect.LocalEndPoint,
            Timings = new TransferTimings(plan.Started, connect.Timings, null, null, null, plan.Context.TimeProvider.GetTimestamp()),
        };
        TransferResult refused = TransferResult.Failure(CurlExitCode.BadFunctionArgument, HttpTimeConditionLimit.InvalidTimeValue) with { Report = report };
        return new HttpAttemptOutcome(refused, null, KeepsAlive: true);
    }

    /// <summary>
    /// Reports what became of the connection once it is disposed, as curl 8.21.0's <c>-v</c>
    /// does (ADR-0050): left intact when marked reusable, closed when the transfer failed, and
    /// shut down otherwise - its response did not persist, or it died before its response, in
    /// which case the line that the request goes out again follows.
    /// </summary>
    private static void ReportConnectionEnd(ITransferContext context, ConnectTarget target, ConnectResult connect, HttpAttemptOutcome outcome)
    {
        context.Events.ReportInfo(ConnectionEndLine(target, connect.ConnectionNumber, outcome));
        if (outcome.DiedBeforeResponse)
        {
            context.Events.ReportInfo(HttpConnectionInfoLines.IssueAnotherRequest(context.Url));
        }
    }

    /// <summary>
    /// Gives curl 8.21.0's <c>-v</c> line for what became of connection <paramref name="number" />
    /// (ADR-0050): left intact, closing, or shutting down.
    /// </summary>
    private static string ConnectionEndLine(ConnectTarget target, long number, HttpAttemptOutcome outcome) =>
        outcome switch
        {
            { ReportsLeftIntact: true } => HttpConnectionInfoLines.LeftIntact(number, target.Host, target.Port),
            { DiedBeforeResponse: false, Result.ExitCode: not CurlExitCode.Ok } => HttpConnectionInfoLines.Closing(number),
            _ => HttpConnectionInfoLines.ShuttingDown(number),
        };

    /// <summary>
    /// Sends the request on <paramref name="transport" /> and reads the response into the
    /// transfer's outputs; or, when the response is one this handler retries (a 401 it
    /// answers, or a 417 to a request whose body waited for <c>100 Continue</c>), writes its
    /// head and trailers, reads and discards its body, and returns the retry's plan. Over
    /// HTTP/2 or HTTP/3 the exchange runs on a new stream of <paramref name="streams" />
    /// (<see cref="IHttpStreamConnection" />), which the same code reads and writes.
    /// </summary>
    private async ValueTask<HttpAttemptOutcome> ExchangeAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection transport,
        TransferReport? earlier,
        bool newConnection,
        IHttpStreamSession? streams)
    {
        ITransferContext context = plan.Context;
        HttpRequestOptions options = plan.Options;
        HttpRequestFraming framing = plan.Framing;
        CancellationToken cancellationToken = plan.Deadline.Token;
        IHttpStreamConnection? requestStream = CreateRequestStream(plan, streams);
        IConnection connection = ExchangeConnectionOf(plan, requestStream, transport);
        byte[] request = FormatRequestHead(plan, streams, connection is HttpH2cUpgradeConnection);
        HttpFirstByteTimingConnection timedConnection = new(connection, context.TimeProvider);
        IConnection responseConnection = framing.AwaitsContinue ? new HttpContinueWaitConnection(timedConnection) : timedConnection;
        HttpRequestBodyWriter upload = new(connection)
        {
            SharedHeadLength = framing.AwaitsContinue ? 0 : request.Length,
            HeldHead = request,
            IsUpload = framing.IsUpload,
            Progress = plan.Progress,
            Events = context.Events,
            EarlyResponseWatch = responseConnection as HttpContinueWaitConnection,
        };
        HttpExchange exchange = new(connect, connection, framing.Method, request.Length, upload, earlier, newConnection)
        {
            UsedProxy = options.ForwardProxy is not null,
            Started = plan.Started,
            TimeProvider = context.TimeProvider,
            ResponseConnection = timedConnection,
            RedirectCount = plan.RedirectsFollowed - options.RedirectsFollowed,
        };
        HttpResponseBodyReader body = new(responseConnection)
        {
            PassesTransferCoding = options.Raw,
            IgnoresContentLength = options.IgnoreContentLength,
            DecodesTransferCoding = options.TransferEncoding,
        };
        int cookiesStored = 0;
        HttpResponseHeadReader headReader = new(responseConnection)
        {
            Events = context.Events,
            HeaderReceived = header => cookiesStored = StoreCookie(context, header, cookiesStored),
            FindRefusal = head => body.FindHeadRefusal(head, context.NoBody, DecodesContent(options)),
            IsHttp2OrHttp3 = requestStream is not null,
            IsSwitchedToHttp2 = () => IsSwitchedToHttp2(connection),
        };
        HttpRequestPlan? retry = null;
        HttpResponseHead? actedOn = null;
        HttpBodyDelivery delivery = HttpBodyDelivery.Deliver;
        ReportProtocolChosen(context.Events, newConnection, streams);
        try
        {
            ThrowIfRefused(framing);
            exchange.RequestReady = context.TimeProvider.GetTimestamp();
            bool bodyLeftUnsent = await SendBodyAsync(context, framing, responseConnection, upload, cancellationToken).ConfigureAwait(false);
            ReportRequestSent(context.Events, framing, upload, bodyLeftUnsent);
            exchange.RequestSent = context.TimeProvider.GetTimestamp();
            exchange.Head = await headReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            HttpHeadRefusal? refusal = headReader.Refusal;
            exchange.Head = HeadCurlRead(exchange.Head, refusal);
            actedOn = headReader.HeadActedOn(exchange.Head);
            exchange.RedirectUrl = HttpRedirectLocation.Find(context.Url, actedOn);
            await WriteHeadersAsync(context.HeaderOutput, exchange.Head.HeadBytes, cancellationToken).ConfigureAwait(false);
            ThrowIfHeaderRefused(refusal);
            ThrowIfClosedBeforeContentLength(plan, actedOn, headReader);
            headReader.ReportHeaderHeldAtClose();
            ReportNoEndOfMessageIndicator(plan, actedOn, headReader);
            retry = await RetryOfAsync(plan, actedOn, bodyLeftUnsent, upload, cancellationToken).ConfigureAwait(false);
            HttpFailMode fail = retry is null ? options.Fail : HttpFailMode.None;
            ThrowIfFailing(fail, HttpFailMode.Fail, actedOn);
            bool discardsBody = retry is not null || (options.FollowRedirects && exchange.RedirectUrl is not null);
            ReportIgnoredBody(plan, actedOn, discardsBody);
            headReader.ReportHeldLines();
            delivery = DeliveryOf(plan, actedOn, discardsBody);
            await ReadBodyAsync(plan, actedOn, body, requestStream, delivery, discardsBody, cancellationToken).ConfigureAwait(false);
            ThrowIfFailing(fail, HttpFailMode.FailWithBody, actedOn);
        }
        catch (HttpTransferException failure)
        {
            headReader.ReportHeldLines();
            ReportReceiveFailure(context.Events, failure);
            TransferResult failed = TransferResult.Failure(failure.ExitCode, failure.Message, body.BytesWritten)
                with
            { Report = exchange.Report(body) };
            return FailedOutcome(plan, failed, DiedBeforeResponse(plan, connect, headReader, failure));
        }
        catch (OperationCanceledException) when (plan.Deadline.EndedByLimit)
        {
            headReader.ReportHeldLines();
            string message = HttpTransferMessages.OperationTimedOut(plan.Deadline.OperationElapsedMilliseconds, body.BytesWritten, body.ExpectedLength);
            TransferResult timedOut = TransferResult.Failure(CurlExitCode.OperationTimedOut, message, body.BytesWritten)
                with
            { Report = exchange.Report(body) };
            return new HttpAttemptOutcome(timedOut, null, KeepsAlive: false);
        }

        TransferResult result = Succeeded(delivery, actedOn!, exchange.Report(body));
        return new HttpAttemptOutcome(result, retry, KeepsAlive(plan, actedOn, upload, headReader, delivery, streams))
        {
            LeftIntactAfterServerClosed = LeftIntactAfterServerClosed(plan, actedOn, upload, headReader, delivery),
        };
    }

    /// <summary>
    /// Creates the HTTP/2 or HTTP/3 stream the exchange runs on, with the request body's length (0 for
    /// none), or gives <see langword="null" /> when the connection speaks HTTP/1.x.
    /// </summary>
    private static IHttpStreamConnection? CreateRequestStream(HttpRequestPlan plan, IHttpStreamSession? streams) =>
        streams?.CreateStream(plan.Context.Url.Scheme, plan.Framing.Body is null ? 0 : plan.Framing.KnownLength);

    /// <summary>
    /// Gives the connection the exchange reads and writes: the HTTP/2 or HTTP/3 stream when there is one;
    /// else, for <see cref="HttpVersionPreference.Http2" /> over cleartext, the transport watched for an
    /// h2c upgrade's <c>101</c> (<see cref="HttpH2cUpgradeConnection" />, BL-716); and else the transport itself.
    /// </summary>
    private static IConnection ExchangeConnectionOf(HttpRequestPlan plan, IHttpStreamConnection? requestStream, IConnection transport) =>
        (IConnection?)requestStream ?? (UpgradesToH2c(plan, transport) ? new HttpH2cUpgradeConnection(transport, plan.Context.Events, plan.Context.Url.Scheme) : transport);

    /// <summary>
    /// Decides whether an HTTP/1.x exchange asks to upgrade to h2c: for <c>--http2</c>
    /// (<see cref="HttpVersionPreference.Http2" />) on a connection without TLS, as curl does
    /// (measured, BL-716 Notes).
    /// </summary>
    private static bool UpgradesToH2c(HttpRequestPlan plan, IConnection transport) =>
        plan.Options.Version == HttpVersionPreference.Http2 && !transport.IsSecure;

    /// <summary>
    /// Tells whether the exchange's connection has switched to HTTP/2 after an h2c upgrade's <c>101</c>.
    /// </summary>
    private static bool IsSwitchedToHttp2(IConnection connection) =>
        connection is HttpH2cUpgradeConnection { IsUpgraded: true };

    /// <summary>
    /// Formats the request head: the HTTP/1.1 head, asking to upgrade to h2c when
    /// <paramref name="upgradesToH2c" />, or over HTTP/2 or HTTP/3 the same head naming
    /// <c>HTTP/2</c> or <c>HTTP/3</c> in its request line, which is what is reported sent and what the stream
    /// turns into its HEADERS (<see cref="Http2RequestHeaders" />).
    /// </summary>
    private byte[] FormatRequestHead(HttpRequestPlan plan, IHttpStreamSession? streams, bool upgradesToH2c)
    {
        ITransferContext context = plan.Context;
        byte[] head = HttpRequestHeadFormatter.Format(
            context.Url,
            plan.Options,
            context.NoBody,
            plan.Authorization,
            CookieHeaderFor(context),
            plan.ForwardProxy is not null,
            plan.ProxyAuthorization,
            HttpRangeHeader.ValueFor(context, plan.Framing.Body is not null),
            context.TimeCondition,
            plan.Framing,
            upgradesToH2c);
        return streams is null ? head : Http2RequestHeaders.WithRequestLineVersion(head, streams.VersionName);
    }

    /// <summary>
    /// Reports <c>using HTTP/1.x</c>, <c>using HTTP/2</c> or <c>using HTTP/3</c>, before the first request on a
    /// connection this transfer opened; curl 8.21.0 prints nothing of the kind for a connection
    /// it reuses (measured, BL-407 Notes).
    /// </summary>
    private static void ReportProtocolChosen(ITransferEvents events, bool newConnection, IHttpStreamSession? streams)
    {
        if (newConnection)
        {
            events.ReportInfo(streams?.UsingLine ?? HttpConnectionInfoLines.UsingHttp1);
        }
    }

    /// <summary>
    /// Reports that the request went out, as curl 8.21.0 does (measured, BL-407 Notes):
    /// <c>Request completely sent off</c> for a request without a body, and
    /// <c>upload completely sent off: N bytes</c> once a whole body has been sent; nothing for
    /// a body a final status stopped.
    /// </summary>
    private static void ReportRequestSent(ITransferEvents events, HttpRequestFraming framing, HttpRequestBodyWriter upload, bool bodyLeftUnsent)
    {
        if (framing.Body is null)
        {
            events.ReportInfo(HttpConnectionInfoLines.RequestSent);
        }
        else if (!bodyLeftUnsent && !upload.CutShort)
        {
            events.ReportInfo(HttpConnectionInfoLines.UploadSent(upload.BytesSent));
        }
    }

    /// <summary>
    /// Reports <c>no chunk, no close, no size. Assume close to signal end</c> for a head whose
    /// body can only end when the server closes, as curl 8.21.0 does once the head is accepted:
    /// before the <c>-f</c> failure and the head's empty line; nothing for a head the peer
    /// closed before its empty line (measured, BL-467 Notes).
    /// </summary>
    private static void ReportNoEndOfMessageIndicator(HttpRequestPlan plan, HttpResponseHead head, HttpResponseHeadReader headReader)
    {
        if (headReader.EndedAtEmptyLine
            && HttpConnectionPersistence.LacksEndOfMessageIndicator(head, plan.Context.NoBody, plan.Options.IgnoreContentLength))
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.NoEndOfMessageIndicator);
        }
    }

    /// <summary>
    /// Reports that a body is read only to be discarded, as curl 8.21.0 does before the
    /// head's empty line (measured, BL-449 Notes): <c>Ignoring the response-body</c> when the
    /// body is discarded - a redirect <c>-L</c> follows, or a response answered with a retry -
    /// on a connection that stays open, and then <c>setting size while ignoring</c> when its
    /// length is known from a Content-Length. Nothing when the connection closes after it:
    /// curl reads no such body at all.
    /// </summary>
    private static void ReportIgnoredBody(HttpRequestPlan plan, HttpResponseHead head, bool discardsBody)
    {
        HttpRequestOptions options = plan.Options;
        if (!discardsBody
            || !HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, options.Raw, options.IgnoreContentLength, options.TransferEncoding))
        {
            return;
        }

        ITransferEvents events = plan.Context.Events;
        events.ReportInfo(HttpConnectionInfoLines.IgnoringBody);
        if (IgnoredBodyLength(plan, head) is not null)
        {
            events.ReportInfo(HttpConnectionInfoLines.SettingSizeWhileIgnoring);
        }
    }

    /// <summary>
    /// Gives the Content-Length of a body read only to be discarded, or <see langword="null" />
    /// when it has none it stops at: framed as <see cref="HttpResponseBodyFraming" /> says when
    /// the response has a body, and for a response to HEAD, which has none, its Content-Length
    /// unless it is chunked or <c>--ignore-content-length</c> is given (measured, BL-449 Notes).
    /// </summary>
    private static long? IgnoredBodyLength(HttpRequestPlan plan, HttpResponseHead head)
    {
        HttpRequestOptions options = plan.Options;
        if (HttpResponseBodyReader.HasBody(head, plan.Context.NoBody))
        {
            return HttpResponseBodyFraming.Of(head.Headers, options.Raw, options.IgnoreContentLength, options.TransferEncoding).ContentLength;
        }

        return options.IgnoreContentLength || HttpTransferEncoding.ListsChunked(head.Headers) ? null : HttpContentLength.Find(head.Headers);
    }

    /// <summary>
    /// Reports a read the peer reset as the <c>-v</c> line curl 8.21.0 prints for it,
    /// <c>Recv failure: Connection was reset</c>, before the connection's end is reported
    /// (measured, BL-449 Notes). Any other failure is left to the transfer's result.
    /// </summary>
    private static void ReportReceiveFailure(ITransferEvents events, HttpTransferException failure)
    {
        if (failure.Message == HttpTransferMessages.ConnectionReset)
        {
            events.ReportInfo(failure.Message);
        }
    }

    /// <summary>
    /// Builds the outcome of an exchange that failed: sent again once on a fresh connection when
    /// its pooled connection <paramref name="diedBeforeResponse" />, and final otherwise.
    /// </summary>
    private static HttpAttemptOutcome FailedOutcome(HttpRequestPlan plan, TransferResult failed, bool diedBeforeResponse) =>
        diedBeforeResponse
            ? new HttpAttemptOutcome(failed, plan.OnFreshConnection(), KeepsAlive: false) { DiedBeforeResponse = true }
            : new HttpAttemptOutcome(failed, null, KeepsAlive: false);

    /// <summary>
    /// Decides whether the connection can carry another request: not after a body a status of
    /// 300 or above cut short, which curl 8.21.0 shuts the connection on (measured, BL-319 and
    /// BL-395 Notes), nor after a 101, nor when the body was left unread; and else as
    /// <see cref="HttpConnectionPersistence" /> says (ADR-0050), for a head the peer closed among
    /// its headers as for one with no body, since curl 8.21.0 decides that a body runs until the
    /// server closes only at the head's empty line and leaves such a connection intact
    /// (measured, BL-483 Notes).
    /// </summary>
    /// <remarks>
    /// Over HTTP/2 and HTTP/3 the stream, not the connection, ends with the response, so the connection
    /// carries another request once the exchange ran whole unless the peer sent GOAWAY or
    /// closed it (<see cref="IHttpStreamSession.AcceptsNewStreams" />).
    /// </remarks>
    private static bool KeepsAlive(HttpRequestPlan plan, HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery, IHttpStreamSession? streams) =>
        DeliveredWhole(upload, headReader, delivery)
            && (streams?.AcceptsNewStreams
                ?? HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody || !headReader.EndedAtEmptyLine, plan.Options.Raw, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding));

    /// <summary>
    /// Decides whether the connection is reported left intact although the server closed it
    /// to end the body, as curl 8.21.0 does for an HTTP/1.0 keep-alive response with no length
    /// (measured, BL-471 Notes; ADR-0109). It is marked reusable, as curl pools it, and the pool
    /// reports it dead before any reuse (ADR-0112).
    /// </summary>
    private static bool LeftIntactAfterServerClosed(HttpRequestPlan plan, HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        DeliveredWhole(upload, headReader, delivery)
            && HttpConnectionPersistence.KeepsHttp10AliveUntilServerCloses(head, plan.Context.NoBody, plan.Options.Raw, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding);

    /// <summary>
    /// Decides whether the exchange ran whole: its request body was not cut short, its
    /// response body was delivered, and the response did not switch protocols.
    /// </summary>
    private static bool DeliveredWhole(HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        !upload.CutShort
            && delivery == HttpBodyDelivery.Deliver
            && !headReader.SwitchedProtocols;

    /// <summary>
    /// Decides whether a failed exchange was on a pooled connection that died while idle, so
    /// the request is sent again once on a fresh one, as curl 8.21.0 does (BL-336 Notes): the
    /// connection was reused, it failed sending or receiving before any byte of the response
    /// arrived, the request has not already been sent again for this reason, and its body, if
    /// any, is bytes that can be sent again.
    /// </summary>
    private static bool DiedBeforeResponse(HttpRequestPlan plan, ConnectResult connect, HttpResponseHeadReader headReader, HttpTransferException failure) =>
        !headReader.HasReceived
            && failure.ExitCode is CurlExitCode.GotNothing or CurlExitCode.SendError or CurlExitCode.RecvError
            && CanSendAgainOnFreshConnection(plan, connect);

    /// <summary>
    /// Decides whether <paramref name="plan" /> may be sent again on a fresh connection: it went
    /// out on a pooled one, it has not already been sent again, and its body, if any, is bytes
    /// that can be sent again.
    /// </summary>
    private static bool CanSendAgainOnFreshConnection(HttpRequestPlan plan, ConnectResult connect) =>
        connect.IsReused
            && !plan.SentOnFreshConnection
            && plan.Framing.Body is not StreamBody;

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
    /// Gives the part of <paramref name="head" /> curl 8.21.0 reads, writes and reports: all of
    /// it, or the part before the header it refused (<see cref="HttpResponseHead.Before" />).
    /// </summary>
    private static HttpResponseHead HeadCurlRead(HttpResponseHead head, HttpHeadRefusal? refusal) =>
        refusal is null ? head : head.Before(refusal.HeaderIndex);

    /// <summary>
    /// Fails the transfer with the failure of the header curl refused while reading the head
    /// (<see cref="HttpResponseBodyReader.FindHeadRefusal" />), once the head before it is
    /// written.
    /// </summary>
    /// <exception cref="HttpTransferException"><paramref name="refusal" /> is not <see langword="null" />.</exception>
    private static void ThrowIfHeaderRefused(HttpHeadRefusal? refusal)
    {
        if (refusal is not null)
        {
            throw refusal.Failure;
        }
    }

    /// <summary>
    /// Fails a head the peer closed among its headers when the part curl 8.21.0 acted on frames
    /// a body by a Content-Length above zero: exit 18, <c>transfer closed with N bytes remaining
    /// to read</c>, its <c>-v</c> line reported before the head's last header line and ahead of
    /// any <c>-f</c> failure (measured, BL-485 Notes). A body a response has none of -
    /// <c>-I</c>, 204, 304 - and <c>--ignore-content-length</c> fail nothing.
    /// </summary>
    private static void ThrowIfClosedBeforeContentLength(HttpRequestPlan plan, HttpResponseHead head, HttpResponseHeadReader headReader)
    {
        if (headReader.EndedAtEmptyLine || !HttpResponseBodyReader.HasBody(head, plan.Context.NoBody))
        {
            return;
        }

        HttpRequestOptions options = plan.Options;
        if (HttpResponseBodyFraming.Of(head.Headers, options.Raw, options.IgnoreContentLength, options.TransferEncoding).ContentLength is > 0 and { } remaining)
        {
            string message = HttpTransferMessages.TransferClosedWithBytesRemaining(remaining);
            plan.Context.Events.ReportInfo(message);
            throw new HttpTransferException(CurlExitCode.PartialFile, message);
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
    /// Reads the body into the transfer's output, or into nothing when it is discarded, as
    /// <see cref="SetBodyLimitAndSinks" /> sets it up, then writes a chunked body's trailers; or reads
    /// nothing when <paramref name="delivery" /> says there is no body to deliver. Over HTTP/2 and HTTP/3
    /// the trailers are the stream's trailing field section, read once the stream has ended.
    /// </summary>
    private static async ValueTask ReadBodyAsync(
        HttpRequestPlan plan,
        HttpResponseHead head,
        HttpResponseBodyReader body,
        IHttpStreamConnection? requestStream,
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
        SetBodyLimitAndSinks(plan, body, discardsBody);
        await body.CopyAsync(head, context.NoBody, bodyOutput, DecodesContent(plan.Options, discardsBody), plan.Options.TransferEncoding && !discardsBody, cancellationToken)
            .ConfigureAwait(false);
        ReadOnlyMemory<byte> trailers = await TrailersOfAsync(body, requestStream, cancellationToken).ConfigureAwait(false);
        await WriteHeadersAsync(context.HeaderOutput, trailers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the trailers of a body just read: a chunked body's, or an HTTP/2 or HTTP/3 stream's once it
    /// has been read to its end (<see cref="IHttpStreamConnection.ReadToEndAsync" />).
    /// </summary>
    private static async ValueTask<ReadOnlyMemory<byte>> TrailersOfAsync(HttpResponseBodyReader body, IHttpStreamConnection? requestStream, CancellationToken cancellationToken)
    {
        if (requestStream is null)
        {
            return body.TrailerBytes;
        }

        await requestStream.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return requestStream.TrailerBytes;
    }

    /// <summary>
    /// Sets where a body's bytes are held and reported: to the <c>--max-filesize</c> limit, the
    /// progress meter and the transfer's events, or, for a discarded body, to none of them -
    /// curl 8.21.0 reports no data for a body it ignores (measured, BL-407 Notes).
    /// </summary>
    private static void SetBodyLimitAndSinks(HttpRequestPlan plan, HttpResponseBodyReader body, bool discardsBody)
    {
        body.MaximumBodySize = discardsBody ? null : HttpDownloadConditions.LimitOf(plan.Context.MaxFileSize);
        body.Progress = discardsBody ? HttpTransferProgress.Silent : plan.Progress;
        body.Events = discardsBody ? NoTransferEvents.Instance : plan.Context.Events;
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
    /// Hands <paramref name="header" />, when it is a <c>Set-Cookie</c> header and cookies are
    /// on, to the cookie store with the transfer's events for its <c>-v</c> lines, as it arrives
    /// in any head of the response, 1xx heads' included (measured, BL-468 Notes).
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <param name="header">A whole header of the response.</param>
    /// <param name="storedFromResponse">How many cookies the store has stored from this request's responses.</param>
    /// <returns>The count the store gives back, or <paramref name="storedFromResponse" /> when the store is not asked.</returns>
    private int StoreCookie(ITransferContext context, HttpResponseHeader header, int storedFromResponse) =>
        CookieStore is { } store && string.Equals(header.Name, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
            ? store.StoreFromResponse(context.Url, header.Value, storedFromResponse, context.TimeProvider.GetUtcNow(), context.Events)
            : storedFromResponse;

    /// <summary>
    /// Decides whether a response is answered with one more request, and which: the same
    /// request with the <c>Proxy-Authorization</c> value <see cref="RetryProxyAuthorizationAsync" />
    /// gives, or with the <c>Authorization</c> value <see cref="RetryAuthorizationAsync" /> gives, or
    /// else, for a 417 <see cref="RetriesWithoutExpect" /> accepts, the same request without
    /// <c>Expect</c> and with the body <paramref name="upload" /> rewinds; <see langword="null" />
    /// when the response is the result. A resend after a 417 that arrived while the body was
    /// being sent keeps an <c>-H</c> <c>Expect: 100-continue</c> wait, so it can draw another
    /// (measured, BL-396 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">
    /// The resend would pass <see cref="HttpRequestOptions.MaxRedirects" /> (exit 47).
    /// </exception>
    private async ValueTask<HttpRequestPlan?> RetryOfAsync(HttpRequestPlan plan, HttpResponseHead head, bool bodyLeftUnsent, HttpRequestBodyWriter upload, CancellationToken cancellationToken)
    {
        if (await RetryProxyAuthorizationAsync(plan, head, cancellationToken).ConfigureAwait(false) is { } proxyAuthorization)
        {
            return plan.WithProxyAuthorization(proxyAuthorization);
        }

        if (await RetryAuthorizationAsync(plan, head, cancellationToken).ConfigureAwait(false) is { } authorization)
        {
            return plan.WithAuthorization(authorization);
        }

        if (!RetriesWithoutExpect(plan, head, bodyLeftUnsent || upload.CutShort))
        {
            return null;
        }

        ThrowIfRedirectLimitReached(plan);
        return plan.WithoutExpect(upload.Rewound(plan.Framing.Body!), keepsCustomWait: !bodyLeftUnsent);
    }

    /// <summary>
    /// Ends the transfer with exit 47 when one more resend would pass
    /// <see cref="HttpRequestOptions.MaxRedirects" />: curl 8.21.0 counts each resend after a
    /// 417 as a followed redirect, with or without <c>-L</c>, sharing the count with the hops
    /// <c>-L</c> followed before this request, and <c>--max-redirs 0</c> refuses even the first
    /// (measured, BL-396 Notes). The 417's head is written first.
    /// </summary>
    /// <exception cref="HttpTransferException">The limit is reached (exit 47).</exception>
    private static void ThrowIfRedirectLimitReached(HttpRequestPlan plan)
    {
        int limit = plan.Options.MaxRedirects;
        if (limit >= 0 && plan.RedirectsFollowed >= limit)
        {
            throw new HttpTransferException(CurlExitCode.TooManyRedirects, HttpTransferMessages.MaximumRedirectsFollowed(limit));
        }
    }

    /// <summary>
    /// Decides whether <paramref name="head" /> is answered by resending the request without
    /// <c>Expect</c>, as curl 8.21.0 does (measured, BL-260 and BL-319 Notes): a 417 that
    /// arrived before the whole body was sent - while it waited for <c>100 Continue</c>, or
    /// while it was being sent once the wait ran out - on a connection the 417 leaves open, and
    /// not under <c>-f</c>, which fails on the 417 itself.
    /// </summary>
    private static bool RetriesWithoutExpect(HttpRequestPlan plan, HttpResponseHead head, bool bodyStopped) =>
        bodyStopped
            && head.StatusLine.StatusCode == 417
            && plan.Options.Fail != HttpFailMode.Fail
            && HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, plan.Options.Raw, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding);

    /// <summary>
    /// Decides whether a response is answered with one more request, and with what
    /// <c>Authorization</c> value: only a 401, only when its body can be sent again, and only
    /// when the authenticator answers the response's <c>WWW-Authenticate</c> challenges -
    /// through <see cref="IHttpAuthenticator.ContinueAuthorizationAsync" /> when the request
    /// that drew it already sent one, which only a handshake of more than one leg (NTLM)
    /// answers, so a credential sent up front and refused ends the transfer, as in curl 8.21.0
    /// (ADR-0181).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />).</exception>
    private ValueTask<string?> RetryAuthorizationAsync(HttpRequestPlan plan, HttpResponseHead head, CancellationToken cancellationToken) =>
        AnswerChallengesAsync(
            plan.AuthRequest,
            plan.Authorization,
            plan.AuthorizationAnswersChallenge,
            MayRetry(plan, head, 401) ? ValuesOf(head, "WWW-Authenticate") : [],
            cancellationToken);

    /// <summary>
    /// Decides whether a response is answered with one more request, and with what
    /// <c>Proxy-Authorization</c> value: only a 407 from a forward proxy, on the same terms as
    /// <see cref="RetryAuthorizationAsync" /> sets for a 401, with the response's
    /// <c>Proxy-Authenticate</c> challenges and the proxy's request (ADR-0187).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />).</exception>
    private ValueTask<string?> RetryProxyAuthorizationAsync(HttpRequestPlan plan, HttpResponseHead head, CancellationToken cancellationToken) =>
        plan.ProxyAuthRequest is { } proxyRequest && MayRetry(plan, head, 407)
            ? AnswerChallengesAsync(proxyRequest, plan.ProxyAuthorization, plan.ProxyAuthorizationAnswersChallenge, ValuesOf(head, "Proxy-Authenticate"), cancellationToken)
            : ValueTask.FromResult<string?>(null);

    /// <summary>
    /// Asks the authenticator to answer <paramref name="challenges" /> for
    /// <paramref name="request" />: nothing when there are none; through
    /// <see cref="IHttpAuthenticator.ContinueAuthorizationAsync" /> when the request that drew
    /// them already sent <paramref name="sent" />; and else afresh.
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />).</exception>
    private async ValueTask<string?> AnswerChallengesAsync(HttpAuthRequest request, string? sent, bool sentAnswersChallenge, string[] challenges, CancellationToken cancellationToken)
    {
        if (challenges.Length == 0)
        {
            return null;
        }

        try
        {
            return sent is not null
                ? await Authenticator.ContinueAuthorizationAsync(request, sent, !sentAnswersChallenge, challenges, cancellationToken).ConfigureAwait(false)
                : await Authenticator.CreateAuthorizationAsync(request, challenges, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpAuthenticationFailedException failure)
        {
            throw new HttpTransferException(failure.ExitCode, failure.Message);
        }
    }

    /// <summary>
    /// Decides whether <paramref name="head" /> may be answered with a retry at all: a
    /// <paramref name="statusCode" /> response to a request whose body is not a stream.
    /// </summary>
    private static bool MayRetry(HttpRequestPlan plan, HttpResponseHead head, int statusCode) =>
        head.StatusLine.StatusCode == statusCode && plan.Framing.Body is not StreamBody;

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
        DecodesContent(options) && !discardsBody;

    /// <summary>
    /// Decides whether Content-Encoding is decoded at all: for <c>--compressed</c> without
    /// <c>--raw</c>, which is also when curl 8.21.0 holds the response to
    /// <see cref="HttpContentDecoder.MaximumCodings" /> (measured, BL-364 Notes).
    /// </summary>
    private static bool DecodesContent(HttpRequestOptions options) =>
        options.Compressed && !options.Raw;

    /// <summary>
    /// Sends the request head and body, if there is one: the head alone when there is no body
    /// or the request waits for <c>100 Continue</c>, and else with the body's first read, so a
    /// body whose first read fails sends nothing (measured, BL-184 Notes); the body at once, or
    /// once <paramref name="responseConnection" /> has waited for <c>100 Continue</c> and not
    /// been answered with a final status instead.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when a final status arrived during the wait and the body was left
    /// unsent; <see langword="false" /> when the body was sent or there is none.
    /// </returns>
    private static async ValueTask<bool> SendBodyAsync(
        ITransferContext context,
        HttpRequestFraming framing,
        IConnection responseConnection,
        HttpRequestBodyWriter upload,
        CancellationToken cancellationToken)
    {
        if (framing.Body is not { } requestBody)
        {
            await upload.WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (responseConnection is HttpContinueWaitConnection waiting)
        {
            await upload.WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
            if (!await waiting.WaitForContinueAsync(context.TimeProvider, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            ReportContinueWaitRanOut(context.Events, waiting);
        }

        await upload.WriteAsync(requestBody, framing.IsChunked, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Reports <c>Done waiting for 100-continue</c> before the body when the wait for
    /// <c>100 Continue</c> ran out with nothing received, as curl 8.21.0 does (measured,
    /// BL-449 Notes); nothing when a <c>100 Continue</c> ended it.
    /// </summary>
    private static void ReportContinueWaitRanOut(ITransferEvents events, HttpContinueWaitConnection waiting)
    {
        if (waiting.WaitRanOut)
        {
            events.ReportInfo(HttpConnectionInfoLines.DoneWaitingForContinue);
        }
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
        /// Gets how many resends after a 417 this transfer made before this exchange, the
        /// report's <see cref="TransferReport.RedirectCount" /> (BL-396 Notes).
        /// </summary>
        internal int RedirectCount { get; init; }

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
        /// <param name="body">
        /// The body reader, whose bytes accepted before and after content decoding are
        /// <c>%{size_download}</c> and <c>%{size_delivered}</c> (BL-516).
        /// </param>
        /// <returns>The report.</returns>
        internal TransferReport Report(HttpResponseBodyReader body)
        {
            TransferReport report = new()
            {
                Method = method,
                RequestSize = (earlier?.RequestSize ?? 0) + headSize + upload.BytesWritten,
                UploadSize = upload.BytesWritten,
                DownloadSize = body.BytesWritten,
                DeliveredSize = body.BytesDelivered,
                HeaderSize = earlier?.HeaderSize ?? 0,
                ConnectionCount = (earlier?.ConnectionCount ?? 0) + (newConnection ? 1 : 0),
                RedirectCount = RedirectCount,
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
        /// Gets the request as the authenticator is asked about it for <see cref="ForwardProxy" />,
        /// or <see langword="null" /> when there is none.
        /// </summary>
        public HttpAuthRequest? ProxyAuthRequest { get; init; }

        /// <summary>
        /// Gets the <c>Proxy-Authorization</c> value to send, or <see langword="null" /> to
        /// send none.
        /// </summary>
        public string? ProxyAuthorization { get; init; }

        /// <summary>
        /// Gets a value indicating whether <see cref="ProxyAuthorization" /> answers a 407's
        /// challenge; <see langword="false" /> for the value made before any challenge.
        /// </summary>
        public bool ProxyAuthorizationAnswersChallenge { get; init; }

        /// <summary>
        /// Gets a value indicating whether the request is being sent again on a fresh connection
        /// because a pooled one died before its response, which happens at most once.
        /// </summary>
        public bool SentOnFreshConnection { get; init; }

        /// <summary>
        /// Gets how many redirects the transfer has followed before this request: the chain's
        /// count (<see cref="HttpRequestOptions.RedirectsFollowed" />) and one for each resend
        /// after a 417, as curl 8.21.0 counts them (BL-396 Notes).
        /// </summary>
        public int RedirectsFollowed { get; init; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Authorization" /> answers a challenge, as
        /// every value a retry sends does; <see langword="false" /> for the transfer's first
        /// request, whose value was made before any challenge.
        /// </summary>
        public bool AuthorizationAnswersChallenge { get; init; }

        /// <summary>
        /// Makes the same request sent with <paramref name="authorization" /> instead, in answer
        /// to a challenge.
        /// </summary>
        /// <param name="authorization">The <c>Authorization</c> value the retry is sent with.</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithAuthorization(string authorization) =>
            With(Framing, authorization, SentOnFreshConnection, RedirectsFollowed, authorizationAnswersChallenge: true);

        /// <summary>
        /// Makes the same request sent with <paramref name="proxyAuthorization" /> instead, in
        /// answer to a 407's challenge, keeping its <c>Authorization</c> value.
        /// </summary>
        /// <param name="proxyAuthorization">The <c>Proxy-Authorization</c> value the retry is sent with.</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithProxyAuthorization(string proxyAuthorization) =>
            With(Framing, Authorization, SentOnFreshConnection, RedirectsFollowed, AuthorizationAnswersChallenge, proxyAuthorization, proxyAuthorizationAnswersChallenge: true);

        /// <summary>
        /// Makes the same request without curl's own <c>Expect</c> and without the wait for
        /// <c>100 Continue</c> (<see cref="HttpRequestFraming.WithoutExpect" />), sending
        /// <paramref name="body" />.
        /// </summary>
        /// <param name="body">The body to resend.</param>
        /// <param name="keepsCustomWait">
        /// <see langword="true" /> to keep an <c>-H</c> <c>Expect: 100-continue</c> wait.
        /// </param>
        /// <returns>The resent request's plan, one more redirect followed.</returns>
        public HttpRequestPlan WithoutExpect(HttpRequestBody body, bool keepsCustomWait) =>
            With(Framing.WithoutExpect(body, keepsCustomWait), Authorization, SentOnFreshConnection, RedirectsFollowed + 1, AuthorizationAnswersChallenge);

        /// <summary>
        /// Makes the same request framed for an HTTP/2 or HTTP/3 stream (<see cref="HttpRequestFraming.ForHttp2OrHttp3" />).
        /// </summary>
        /// <returns>The HTTP/2 or HTTP/3 request's plan.</returns>
        public HttpRequestPlan ForHttp2OrHttp3() => With(Framing.ForHttp2OrHttp3(), Authorization);

        /// <summary>
        /// Makes the same request, marked as sent again on a fresh connection.
        /// </summary>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan OnFreshConnection() => With(Framing, Authorization, sentOnFreshConnection: true, RedirectsFollowed, AuthorizationAnswersChallenge);

        private HttpRequestPlan With(HttpRequestFraming framing, string? authorization) =>
            With(framing, authorization, SentOnFreshConnection, RedirectsFollowed, AuthorizationAnswersChallenge);

        private HttpRequestPlan With(HttpRequestFraming framing, string? authorization, bool sentOnFreshConnection, int redirectsFollowed, bool authorizationAnswersChallenge) =>
            With(framing, authorization, sentOnFreshConnection, redirectsFollowed, authorizationAnswersChallenge, ProxyAuthorization, ProxyAuthorizationAnswersChallenge);

        private HttpRequestPlan With(
            HttpRequestFraming framing,
            string? authorization,
            bool sentOnFreshConnection,
            int redirectsFollowed,
            bool authorizationAnswersChallenge,
            string? proxyAuthorization,
            bool proxyAuthorizationAnswersChallenge) =>
            new(Context, Options, framing, AuthRequest, authorization)
            {
                Started = Started,
                Deadline = Deadline,
                Progress = Progress,
                ForwardProxy = ForwardProxy,
                ProxyAuthRequest = ProxyAuthRequest,
                ProxyAuthorization = proxyAuthorization,
                ProxyAuthorizationAnswersChallenge = proxyAuthorizationAnswersChallenge,
                SentOnFreshConnection = sentOnFreshConnection,
                RedirectsFollowed = redirectsFollowed,
                AuthorizationAnswersChallenge = authorizationAnswersChallenge,
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
    /// <see langword="true" /> when the response was read to its end and leaves the connection
    /// open, so a retry may be sent on it and it is marked reusable.
    /// </param>
    private sealed class HttpAttemptOutcome(TransferResult Result, HttpRequestPlan? Retry, bool KeepsAlive)
    {
        /// <summary>
        /// Gets a value indicating whether the exchange ran on a pooled connection that died
        /// before its response began, so <see cref="Retry" /> resends the request on a fresh one.
        /// </summary>
        public bool DiedBeforeResponse { get; init; }

        /// <summary>
        /// Gets a value indicating whether the connection is reported left intact although the
        /// server closed it to end the body (ADR-0109); it is marked reusable all the same, and
        /// the pool finds it dead (ADR-0112).
        /// </summary>
        public bool LeftIntactAfterServerClosed { get; init; }

        /// <summary>
        /// Gets a value indicating whether the connection is reported left intact: it
        /// <see cref="KeepsAlive" />, or it is <see cref="LeftIntactAfterServerClosed" />.
        /// </summary>
        public bool ReportsLeftIntact => KeepsAlive || LeftIntactAfterServerClosed;

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
