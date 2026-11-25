using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

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
/// <param name="http2PushSink">
/// Who is offered each HTTP/2 server push (BL-2516), or <see langword="null" /> for none: with one
/// the handler's HTTP/2 connections send SETTINGS_ENABLE_PUSH 1 and each PUSH_PROMISE on a
/// transfer's stream goes to it; without one they send 0, as curl does with no push callback.
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
/// A response's <c>Location</c>, whatever its status, is resolved against the request URL into
/// <see cref="TransferReport.RedirectUrl" /> (<see cref="HttpRedirectLocation" />), with or
/// without <c>-L</c>. Under <see cref="HttpRequestOptions.FollowRedirects" /> the body of a
/// 3xx response that has one is read and discarded, counted in the download size as curl counts
/// it, while its head and trailers are still written to the header output (BL-179 Notes).
/// </para>
/// <para>
/// <see cref="HttpRequestOptions.Compressed" /> sends <c>Accept-Encoding</c> with
/// <see cref="HttpRequestOptions.AcceptEncoding" />, <c>deflate, gzip, br, zstd</c> by default (ADR-0287, ADR-0777) and, unless <see cref="HttpRequestOptions.PassesContentCoding" /> is set, decodes the body as
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
/// with exit 25 once connected, sending nothing. <see cref="HttpRequestOptions.PassesTransferCoding" /> writes a
/// chunked body undecoded, chunk lines included, through its zero-length chunk and trailers, and refuses no
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
/// order. Measured on curl 8.21.0 (BL-603 Notes, ADR-0239). The exception is an NTLM Type 3
/// message, sent once: neither retry sends it again (BL-2030, BL-5892).
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
/// pass <see cref="HttpRequestOptions.MaxRedirects" /> is not sent: the 417's head is written,
/// its body is read and discarded as before a resend, <c>-v</c> reports what became of the
/// connection and then <c>Maximum (N) redirects followed</c>, and the transfer ends with exit
/// 47. Measured on curl 8.21.0 (BL-396 Notes) and 8.22.0 (BL-5944 Notes).
/// </para>
/// <para>
/// With a cookie store, every request, an authentication retry included, asks it afresh for
/// the <c>Cookie</c> value (sent over TLS for <c>https</c>, at the time from
/// <see cref="ITransferContext.TimeProvider" />), and every final response head read, a 3xx
/// or a 401 included, hands its <c>Set-Cookie</c> values to it in received order, before the
/// head is written. Measured on curl 8.21.0 (BL-182 Notes).
/// </para>
/// <para>
/// With <see cref="HttpRequestOptions.AltSvcRoute" /> the connect target carries the alternative
/// for the connector to dial and the request sends <c>Alt-Used</c> naming it; with
/// <see cref="HttpRequestOptions.AltSvcStore" /> each <c>Alt-Svc</c> header is handed to the store
/// as it arrives (the store decides whether a plain <c>http</c> origin's is learned), and each alternative the store added is
/// reported as <c>Added alt-svc: &lt;host&gt;:&lt;port&gt; over &lt;id&gt;</c> before the header
/// line. Measured on curl 8.21.0 (BL-623 and BL-878 Notes).
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
public sealed partial class HttpProtocolHandler(
    IConnector connector,
    IHttpAuthenticator authenticator,
    ICookieStore? cookieStore = null,
    HttpAuthSchemes proxyAuthSchemes = HttpAuthSchemes.Basic,
    IHttp2PushSink? http2PushSink = null) : IProtocolHandler
{
    /// <summary>
    /// How many times a request whose HTTP/3 stream the server refused is sent again on a new
    /// connection before the transfer gives up: curl's <c>CONN_MAX_RETRIES</c> (ADR-0187).
    /// </summary>
    internal const int MaximumStreamRefusedRetries = 5;

    /// <summary>
    /// The protocols a request downgraded to HTTP/1.1 offers through ALPN (CGR-032).
    /// </summary>
    private static readonly IReadOnlyList<string> Http11ApplicationProtocols = ["http/1.1"];

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

    /// <summary>
    /// Gets who is offered each HTTP/2 server push (BL-2516), or <see langword="null" /> when the
    /// handler takes none.
    /// </summary>
    internal IHttp2PushSink? Http2PushSink { get; } = http2PushSink;

    /// <summary>
    /// Gets or sets whether an HTTP/2 transfer writes curl 8.21.0's <c>--trace-config http/2</c> lines
    /// (<see cref="Http2FrameTrace" />, BL-1167): the session's creation, every frame sent and received,
    /// the server's settings and each stream's close. Off by default.
    /// </summary>
    public bool TracesHttp2Frames { get; init; }

    /// <summary>
    /// Gets or sets how much longer than the happy-eyeballs timeout the first QUIC attempt of the
    /// process (see <see cref="QuicColdStartGate" />) leads TCP, to cover the start-up cost of the
    /// hand-built QUIC and TLS code (BL-5660). Zero, the default, gives no extra head start.
    /// </summary>
    public TimeSpan QuicColdStartAllowance { get; init; }

    /// <summary>Gets or sets the gate that says which QUIC attempt is the process's first.</summary>
    public QuicColdStartGate QuicColdStart { get; init; } = QuicColdStartGate.Process;

    /// <summary>
    /// Gets or sets whether an HTTP/3 transfer writes curl 8.21.0's <c>--trace-config http/3</c> lines
    /// (<see cref="Http3StreamTrace" />, BL-1168): each response head's end, each piece of body and
    /// the stream's close. Off by default.
    /// </summary>
    public bool TracesHttp3Streams { get; init; }

    /// <summary>
    /// Gets or sets whether an HTTP/1.x request body's reads write curl 8.21.0's <c>--trace-config read</c>
    /// lines (<see cref="HttpClientReaderTraceLines" />, BL-1189, BL-1214): the reader added for a <c>-d</c>
    /// body or a <c>-T</c> upload, and each read into the upload buffer, chunked or not, held back for
    /// <c>100 Continue</c> or not, a <c>-F</c> body's included. Off by default.
    /// </summary>
    public bool TracesClientReaders { get; init; }

    /// <summary>
    /// Gets or sets what is told, with the wait's length, when a request starts waiting for <c>100 Continue</c>
    /// (<see cref="HttpContinueWaitConnection.WaitBegins" />), so a transfer driven by
    /// <c>curl_multi_socket_action</c> can hold the expect-100 deadline (BL-3864); <see langword="null" />, the
    /// default, for nothing.
    /// </summary>
    public Action<TimeSpan>? ContinueWaitBegins { get; init; }

    /// <summary>
    /// Gets or sets what is told when a request's wait for <c>100 Continue</c> ends, the answer come or the wait
    /// run out (<see cref="HttpContinueWaitConnection.WaitEnds" />, BL-3864); <see langword="null" />, the
    /// default, for nothing.
    /// </summary>
    public Action? ContinueWaitEnds { get; init; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long started = context.TimeProvider.GetTimestamp();
        HttpRequestOptions options = OptionsOf(context);
        HttpRequestFraming framing = HttpRequestFraming.Of(options, HttpRequestHeadFormatter.CustomHeadersOf(options.Headers, options), context.NoBody, context.Upload, context.ResumeFrom, context.RangeText, context.ResumeUploadFromUnknownOffset, context.ConvertLineEndings);
        HttpAuthRequest authRequest = OriginAuthRequestOf(context, options, framing);
        ProxyEndpoint? forwardProxy = ForwardProxyOf(context.Url, options);
        HttpAuthRequest? proxyAuthRequest = ProxyAuthRequestOrNull(authRequest, forwardProxy);
        using HttpTransferDeadline deadline = new(context);
        HttpInfoLineRecorder authorizationLines = new();
        HttpInfoLineRecorder proxyAuthorizationLines = new();
        string? proxyAuthorization = await CreateFirstProxyAuthorizationAsync(proxyAuthRequest, proxyAuthorizationLines, context.CancellationToken).ConfigureAwait(false);
        (string? authorization, HttpTransferException? authorizationFailure) = await CreateFirstAuthorizationAsync(authRequest with { Events = authorizationLines }, context.CancellationToken).ConfigureAwait(false);
        (string? keptAuthorization, bool answersKeptChallenges) = authorization is null
            ? await KeptDigestAuthorizationAsync(options, authRequest, context.CancellationToken).ConfigureAwait(false)
            : (null, false);
        authorization ??= keptAuthorization;
        bool probes = SendsFirstProbe(framing, authRequest, authorization, proxyAuthRequest, proxyAuthorization);
        HttpRequestPlan plan = new(context, options, probes ? framing.AsAuthProbe() : framing, authRequest, authorization)
        {
            ProbedFraming = probes ? framing : null,
            AuthorizationFailure = authorizationFailure,
            AuthorizationInfoLines = authorizationLines.Lines,
            AuthorizationAnswersKeptDigestChallenges = answersKeptChallenges,
            HoldsDigestNonce = OffersDigest(options.KeptDigestChallenges),
            Started = started,
            Deadline = deadline,
            Progress = new HttpTransferProgress(context.Progress),
            ForwardProxy = forwardProxy,
            ProxyAuthRequest = proxyAuthRequest,
            ProxyAuthorization = proxyAuthorization,
            ProxyAuthorizationInfoLines = proxyAuthorizationLines.Lines,
            RedirectsFollowed = options.RedirectsFollowed,
        };
        TransferResult result = await ExchangeOfPlanAsync(plan).ConfigureAwait(false);
        return plan.OpenedConnection ? WithFirstAuthorizationFailure(result, authRequest, authorizationLines.Lines) : result;
    }

    /// <summary>
    /// Gives the options the transfer asked for, or the defaults when it asked for none.
    /// </summary>
    private static HttpRequestOptions OptionsOf(ITransferContext context) =>
        context.Http ?? new HttpRequestOptions();

    /// <summary>
    /// Sends the plan's first request: refused or tried without HTTP/3 when
    /// <see cref="Http3RefusalOf" /> says so, else connected and exchanged as asked.
    /// </summary>
    private async ValueTask<TransferResult> ExchangeOfPlanAsync(HttpRequestPlan plan) =>
        Http3RefusalOf(plan) is { } refusal
            ? await ExchangeWithoutHttp3Async(plan, refusal).ConfigureAwait(false)
            : await ConnectAndExchangeAsync(plan, earlier: null).ConfigureAwait(false);
}
