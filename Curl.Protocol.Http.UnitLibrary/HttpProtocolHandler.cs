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
/// <see cref="HttpRequestOptions.Compressed" /> sends <c>Accept-Encoding: deflate, gzip, br, zstd</c>
/// (ADR-0287) and, unless <see cref="HttpRequestOptions.Raw" /> is set, decodes the body as
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
/// order. Measured on curl 8.21.0 (BL-603 Notes, ADR-0239).
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
/// With <see cref="HttpRequestOptions.AltSvcRoute" /> the connect target carries the alternative
/// for the connector to dial and the request sends <c>Alt-Used</c> naming it; with
/// <see cref="HttpRequestOptions.AltSvcStore" /> each <c>Alt-Svc</c> header of a response to an
/// <c>https</c> URL is handed to the store as it arrives, and each alternative the store added is
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
public sealed class HttpProtocolHandler(
    IConnector connector,
    IHttpAuthenticator authenticator,
    ICookieStore? cookieStore = null,
    HttpAuthSchemes proxyAuthSchemes = HttpAuthSchemes.Basic) : IProtocolHandler
{
    /// <summary>
    /// How many times a request whose HTTP/3 stream the server refused is sent again on a new
    /// connection before the transfer gives up: curl's <c>CONN_MAX_RETRIES</c> (ADR-0187).
    /// </summary>
    internal const int MaximumStreamRefusedRetries = 5;

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
    /// Gets or sets whether an HTTP/2 transfer writes curl 8.21.0's <c>--trace-config http/2</c> lines
    /// (<see cref="Http2FrameTrace" />, BL-1167): the session's creation, every frame sent and received,
    /// the server's settings and each stream's close. Off by default.
    /// </summary>
    public bool TracesHttp2Frames { get; init; }

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
        HttpRequestFraming framing = HttpRequestFraming.Of(options, HttpRequestHeadFormatter.CustomHeadersOf(options.Headers, options), context.NoBody, context.Upload, context.ResumeFrom, context.RangeText, context.ResumeUploadFromUnknownOffset, context.ConvertLineEndings);
        HttpAuthRequest authRequest = new(
            framing.Method,
            context.Url,
            options.RequestTarget ?? HttpUrlText.RequestTarget(context.Url),
            context.Credentials,
            options.BearerToken,
            AllowedSchemesOf(options),
            IsProxy: false)
        {
            Events = context.Events,
            AwsSigV4 = AwsSigV4InputsOf(context, options, framing),
        };
        ProxyEndpoint? forwardProxy = ForwardProxyOf(context.Url, options);
        HttpAuthRequest? proxyAuthRequest = forwardProxy is null ? null : ProxyAuthRequestOf(authRequest, forwardProxy);
        using HttpTransferDeadline deadline = new(context);
        HttpInfoLineRecorder authorizationLines = new();
        HttpInfoLineRecorder proxyAuthorizationLines = new();
        string? proxyAuthorization = await CreateFirstProxyAuthorizationAsync(proxyAuthRequest, proxyAuthorizationLines, context.CancellationToken).ConfigureAwait(false);
        (string? authorization, HttpTransferException? authorizationFailure) = await CreateFirstAuthorizationAsync(authRequest with { Events = authorizationLines }, context.CancellationToken).ConfigureAwait(false);
        bool probes = SendsFirstProbe(framing, authRequest, authorization, proxyAuthRequest, proxyAuthorization);
        HttpRequestPlan plan = new(context, options, probes ? framing.AsAuthProbe() : framing, authRequest, authorization)
        {
            ProbedFraming = probes ? framing : null,
            AuthorizationFailure = authorizationFailure,
            AuthorizationInfoLines = authorizationLines.Lines,
            Started = started,
            Deadline = deadline,
            Progress = new HttpTransferProgress(context.Progress),
            ForwardProxy = forwardProxy,
            ProxyAuthRequest = proxyAuthRequest,
            ProxyAuthorization = proxyAuthorization,
            ProxyAuthorizationInfoLines = proxyAuthorizationLines.Lines,
            RedirectsFollowed = options.RedirectsFollowed,
        };
        TransferResult result = Http3RefusalOf(plan) is { } refusal
            ? await ExchangeWithoutHttp3Async(plan, refusal).ConfigureAwait(false)
            : await ConnectAndExchangeAsync(plan, earlier: null).ConfigureAwait(false);
        return plan.OpenedConnection ? WithFirstAuthorizationFailure(result, authRequest, authorizationLines.Lines) : result;
    }

    /// <summary>
    /// Decides whether the transfer's first request goes as a probe with an empty body: a Digest
    /// one to the origin or the proxy (<see cref="SendsDigestProbe" />), or one that starts an
    /// NTLM handshake (<see cref="SendsNtlmProbe" />).
    /// </summary>
    private static bool SendsFirstProbe(HttpRequestFraming framing, HttpAuthRequest authRequest, string? authorization, HttpAuthRequest? proxyAuthRequest, string? proxyAuthorization) =>
        SendsDigestProbe(framing, authRequest, authorization)
            || (proxyAuthRequest is not null && SendsDigestProbe(framing, proxyAuthRequest, proxyAuthorization))
            || SendsNtlmProbe(framing, authorization, proxyAuthorization);

    /// <summary>
    /// Decides whether the first request goes as a probe with an empty body
    /// (<see cref="HttpRequestFraming.AsAuthProbe" />): one with a body, sent where Digest is the
    /// one scheme allowed, with credentials and no value made yet, as curl 8.21.0 holds a POST's
    /// or PUT's body back until the Digest challenge is answered (upstream test88, test175,
    /// test1001; ADR-0441).
    /// </summary>
    private static bool SendsDigestProbe(HttpRequestFraming framing, HttpAuthRequest request, string? authorization) =>
        framing.Body is not null
            && request.AllowedSchemes == HttpAuthSchemes.Digest
            && request.Credential is not null
            && authorization is null;

    /// <summary>
    /// Decides whether a request goes as a probe with an empty body
    /// (<see cref="HttpRequestFraming.AsAuthProbe" />) because it starts an NTLM handshake: one
    /// with a body whose <c>Authorization</c> or <c>Proxy-Authorization</c> value is an NTLM
    /// Type 1 message, as curl 8.21.0 holds a POST's or PUT's body back until the Type 3 message
    /// goes (upstream test170, test176, test239, test243, test267; BL-2029).
    /// </summary>
    private static bool SendsNtlmProbe(HttpRequestFraming framing, string? authorization, string? proxyAuthorization) =>
        framing.Body is not null && (IsNtlmType1(authorization) || IsNtlmType1(proxyAuthorization));

    /// <summary>
    /// Decides whether an <c>Authorization</c> or <c>Proxy-Authorization</c> value is an NTLM
    /// Type 1 message: <c>NTLM</c> and the base64 of <c>NTLMSSP\0</c> followed by the message
    /// type 1, whose first twelve bytes encode to exactly <see cref="NtlmType1Prefix" />.
    /// </summary>
    private static bool IsNtlmType1(string? value) =>
        value?.StartsWith(NtlmType1Prefix, StringComparison.Ordinal) is true;

    /// <summary>
    /// The start of every NTLM Type 1 value: <c>NTLM </c> and the base64 of the message's
    /// signature <c>NTLMSSP\0</c> and its type, 1, as a little-endian 32-bit number.
    /// </summary>
    private const string NtlmType1Prefix = "NTLM TlRMTVNTUAABAAAA";

    /// <summary>
    /// Gives the schemes the origin request may answer with: the scheme an earlier hop's server
    /// picked (<see cref="HttpRequestOptions.AuthSchemePicked" />) alone when it is one of
    /// <see cref="HttpRequestOptions.AuthSchemes" />, as libcurl 8.21.0 keeps its picked scheme
    /// across redirects and so sends Basic before any challenge (upstream test1088, BL-1819);
    /// otherwise every scheme allowed.
    /// </summary>
    private static HttpAuthSchemes AllowedSchemesOf(HttpRequestOptions options) =>
        options.AuthSchemePicked != HttpAuthSchemes.None && (options.AuthSchemes & options.AuthSchemePicked) == options.AuthSchemePicked
            ? options.AuthSchemePicked
            : options.AuthSchemes;

    /// <summary>
    /// Gives the scheme a server picked for the request <paramref name="plan" /> sends: the
    /// scheme of an <c>Authorization</c> value that answers a challenge, or else the one the
    /// transfer's earlier hop picked (BL-1819).
    /// </summary>
    private static HttpAuthSchemes AuthSchemePickedBy(HttpRequestPlan plan) =>
        plan.AuthorizationAnswersChallenge && SchemeOfAuthorization(plan.Authorization) is var scheme && scheme != HttpAuthSchemes.None
            ? scheme
            : plan.Options.AuthSchemePicked;

    /// <summary>
    /// Gives the scheme an <c>Authorization</c> value starts with, or
    /// <see cref="HttpAuthSchemes.None" /> for none or one this handler does not pick.
    /// </summary>
    private static HttpAuthSchemes SchemeOfAuthorization(string? authorization) =>
        authorization?.Split(' ', 2)[0] switch
        {
            "Basic" => HttpAuthSchemes.Basic,
            "Digest" => HttpAuthSchemes.Digest,
            "NTLM" => HttpAuthSchemes.Ntlm,
            "Negotiate" => HttpAuthSchemes.Negotiate,
            _ => HttpAuthSchemes.None,
        };

    /// <summary>
    /// Makes the first request's <c>Proxy-Authorization</c> value for a forward proxy, the
    /// authenticator's lines going to <paramref name="lines" />; <see langword="null" /> when
    /// there is no forward proxy.
    /// </summary>
    private async ValueTask<string?> CreateFirstProxyAuthorizationAsync(HttpAuthRequest? proxyAuthRequest, HttpInfoLineRecorder lines, CancellationToken cancellationToken) =>
        proxyAuthRequest is null
            ? null
            : await Authenticator.CreateAuthorizationAsync(proxyAuthRequest with { Events = lines }, [], cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gives a failed transfer the message of the first line the authenticator reported while
    /// it made the first request's <c>Authorization</c> value, a Negotiate context's failure, as
    /// curl 8.21.0 does: its <c>failf</c> for the context fills the error buffer before any later
    /// one, so <c>curl: (22)</c> after <c>-f</c> meets the 401 carries it rather than
    /// <c>The requested URL returned error: 401</c> (measured, BL-955 Notes; ADR-0344).
    /// </summary>
    /// <remarks>
    /// An <c>--aws-sigv4</c> transfer keeps its own message: every line its signer reports is a
    /// <c>-v</c> info line, never a failure, so the string to sign never becomes the error
    /// message (BL-1454). A transfer whose first connection never opened keeps its own message
    /// too: curl makes the context only once connected, so a refused connect or a failed
    /// <c>--interface</c> bind never meets it (measured, BL-1684).
    /// </remarks>
    private static TransferResult WithFirstAuthorizationFailure(TransferResult result, HttpAuthRequest request, IReadOnlyList<string> authorizationLines) =>
        result.ExitCode != CurlExitCode.Ok && request.AwsSigV4 is null && authorizationLines.Count > 0
            ? result with { ErrorMessage = authorizationLines[0] }
            : result;

    /// <summary>
    /// Gives why HTTP/3 is refused for <paramref name="plan" />, in the order curl 8.21.0's
    /// <c>Curl_conn_may_http3</c> checks: over a Unix domain socket, exit 96, for
    /// <c>--http3-only</c> with any URL and <c>--http3</c> with an <c>https://</c> URL (BL-867);
    /// then, exit 3, <c>--http3</c> or <c>--http3-only</c> with an <c>https://</c> URL through a
    /// SOCKS proxy (ADR-0223; an HTTP or HTTPS proxy is tunnelled through, BL-942); or <see langword="null" />
    /// when HTTP/3 is not refused.
    /// </summary>
    private static Http3Refusal? Http3RefusalOf(HttpRequestPlan plan)
    {
        if (plan.Options.Version is not (HttpVersionPreference.Http3 or HttpVersionPreference.Http3Only))
        {
            return null;
        }

        bool https = plan.Context.Url.Scheme == "https";
        return RefusesHttp3OverUnixSocket(plan, https)
            ? new Http3Refusal(HttpTransferMessages.Http3NotOverUnixSocket, CurlExitCode.QuicConnectError)
            : Http3ProxyRefusalOf(plan, https);
    }

    /// <summary>
    /// Decides whether a Unix domain socket refuses HTTP/3 for <paramref name="plan" />:
    /// <c>--http3-only</c> with any URL, <c>--http3</c> only with an <c>https://</c> one.
    /// </summary>
    private static bool RefusesHttp3OverUnixSocket(HttpRequestPlan plan, bool https) =>
        plan.Options.OverUnixSocket && (https || plan.Options.Version == HttpVersionPreference.Http3Only);

    /// <summary>
    /// Gives a SOCKS proxy's refusal of HTTP/3 for an <c>https://</c> URL, exit 3, or
    /// <see langword="null" /> without a proxy, through an HTTP or HTTPS proxy, which QUIC
    /// tunnels through with CONNECT-UDP as curl 8.21.0 does (BL-942), or for any other URL.
    /// </summary>
    private static Http3Refusal? Http3ProxyRefusalOf(HttpRequestPlan plan, bool https) =>
        https && plan.Options.ForwardProxy is { } proxy && !IsHttpProxy(proxy)
            ? new Http3Refusal(HttpTransferMessages.Http3NotOverSocksProxy, CurlExitCode.UrlMalformat)
            : null;

    /// <summary>Decides whether <paramref name="proxy" /> is an HTTP or HTTPS proxy rather than a SOCKS one.</summary>
    private static bool IsHttpProxy(ProxyEndpoint proxy) =>
        proxy.Kind is ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https;

    /// <summary>
    /// Why HTTP/3 is refused before connecting: the <paramref name="Message" /> curl writes into
    /// its error buffer, and the <paramref name="ExitCode" /> <c>--http3-only</c> fails with.
    /// </summary>
    /// <param name="Message">The refusal's message.</param>
    /// <param name="ExitCode">The exit code <c>--http3-only</c> fails with.</param>
    private readonly record struct Http3Refusal(string Message, CurlExitCode ExitCode);

    /// <summary>
    /// Runs a transfer whose HTTP/3 a SOCKS proxy (measured on curl.se's ngtcp2 build, ADR-0223) or a
    /// Unix domain socket (read from curl 8.21.0, BL-867) rules out: reports the refusal's
    /// message; then fails <c>--http3-only</c> with the refusal's exit code and message before
    /// connecting; and runs <c>--http3</c> over TCP or the Unix socket, where a failure keeps
    /// its own exit code but is reported with the refusal's message, the first message curl
    /// wrote into its error buffer.
    /// </summary>
    private async ValueTask<TransferResult> ExchangeWithoutHttp3Async(HttpRequestPlan plan, Http3Refusal refusal)
    {
        plan.Context.Events.ReportInfo(refusal.Message);
        if (plan.Options.Version == HttpVersionPreference.Http3Only)
        {
            return FailBeforeConnecting(plan, refusal.ExitCode, refusal.Message);
        }

        TransferResult result = await ConnectAndExchangeAsync(plan, earlier: null).ConfigureAwait(false);
        return result.ExitCode == CurlExitCode.Ok ? result : result with { ErrorMessage = refusal.Message };
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
            : urlTarget with
            {
                Proxy = plan.Options.ForwardProxy,
                AltSvcRoute = plan.Options.AltSvcRoute,
                ApplicationProtocols = AltSvcApplicationProtocols.Of(plan.Options.AltSvcRoute),
            };
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
    /// method and request target (Digest's <c>uri</c> is the origin form, as curl 8.21.0
    /// sends it), the proxy's own URL (NTLM and Negotiate ask for <c>HTTP</c> on the proxy's
    /// host, as curl's Type 3 names <c>HTTP/&lt;proxy&gt;</c>, BL-604 Notes), the proxy's
    /// credential, no bearer token, and <see cref="ProxyAuthSchemes" />. Answered with no
    /// challenges it gives the pre-emptive <c>Proxy-Authorization</c>: Basic's, or NTLM's Type 1
    /// or Negotiate's first token when that is the one scheme allowed (ADR-0270).
    /// </summary>
    private HttpAuthRequest ProxyAuthRequestOf(HttpAuthRequest originRequest, ProxyEndpoint proxy) =>
        originRequest with { Url = ProxyUrlOf(proxy), Credential = proxy.Credential, BearerToken = null, AllowedSchemes = ProxyAuthSchemes, IsProxy = true, AwsSigV4 = null };

    /// <summary>
    /// Gives <paramref name="proxy" />'s own URL, <c>http://host:port/</c> (<c>https</c> for an
    /// HTTPS proxy), an IPv6 literal in brackets.
    /// </summary>
    private static CurlUrl ProxyUrlOf(ProxyEndpoint proxy)
    {
        string host = proxy.Host.Contains(':', StringComparison.Ordinal) && !proxy.Host.StartsWith('[') ? $"[{proxy.Host}]" : proxy.Host;
        return CurlUrl.Parse($"{(proxy.Kind == ProxyKind.Https ? "https" : "http")}://{host}:{proxy.Port}/");
    }

    /// <summary>
    /// Gives what signing the request with <c>--aws-sigv4</c> needs, or <see langword="null" />
    /// without it: the <c>Host</c> value curl's own head carries, the <c>-H</c> headers, the
    /// <c>-d</c> body, the <c>-T</c> upload's size and <c>--path-as-is</c> (BL-629).
    /// </summary>
    private static AwsSigV4Inputs? AwsSigV4InputsOf(ITransferContext context, HttpRequestOptions options, HttpRequestFraming framing) =>
        options.AwsSigV4 is { } parameter
            ? new AwsSigV4Inputs(parameter, HttpUrlText.HostHeaderAuthority(context.Url), options.Headers)
            {
                PostFields = options.Body is BytesBody postFields ? (ReadOnlyMemory<byte>?)postFields.Content : null,
                UploadSize = framing.IsUpload ? framing.KnownLength ?? -1 : -1,
                IsGetOrHead = framing.Body is null && !framing.IsUpload,
                PathAsIs = context.PathAsIs,
            }
            : null;

    /// <summary>
    /// Asks the authenticator for the value the transfer's first request is sent with, before
    /// any challenge; a refusal (<see cref="HttpAuthenticationFailedException" />) is given
    /// back instead, to fail the exchange once connected, where curl 8.21.0 signs with
    /// <c>--aws-sigv4</c> and reports its failure (measured, BL-629 Notes).
    /// </summary>
    private async ValueTask<(string? Authorization, HttpTransferException? Failure)> CreateFirstAuthorizationAsync(HttpAuthRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return (await Authenticator.CreateAuthorizationAsync(request, [], cancellationToken).ConfigureAwait(false), null);
        }
        catch (HttpAuthenticationFailedException failure)
        {
            return (null, new HttpTransferException(failure.ExitCode, failure.Message));
        }
    }

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
        if (FailureBeforeConnecting(plan, earlier) is { } failure)
        {
            return failure;
        }

        ConnectTarget target = TargetOf(plan);
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

        plan.OpenedConnection = true;
        plan.TakeServerCertificateFrom(connect);
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
    /// Gives the failure a request meets before it connects: <c>--http3-only</c> with a URL that
    /// is not <c>https://</c> (<see cref="Http3NeedsHttps" />), or a resend whose <c>-T</c> upload
    /// cannot be rewound (<see cref="UploadRewindFailed" />); <see langword="null" /> for none.
    /// </summary>
    private static TransferResult? FailureBeforeConnecting(HttpRequestPlan plan, TransferReport? earlier)
    {
        if (plan.Options.Version == HttpVersionPreference.Http3Only && plan.Context.Url.Scheme != "https")
        {
            return Http3NeedsHttps(plan);
        }

        return plan.UploadCannotRewind ? UploadRewindFailed(plan, earlier) : null;
    }

    /// <summary>
    /// Fails <c>--http3-only</c> with a URL that is not <c>https://</c> before connecting: exit 3,
    /// <c>HTTP/3 requested for non-HTTPS URL</c>, reported with <c>closing connection #-1</c>, as
    /// curl.se's ngtcp2 build does (measured, ADR-0144).
    /// </summary>
    private static TransferResult Http3NeedsHttps(HttpRequestPlan plan)
    {
        plan.Context.Events.ReportInfo(HttpTransferMessages.Http3NeedsHttps);
        return FailBeforeConnecting(plan, CurlExitCode.UrlMalformat, HttpTransferMessages.Http3NeedsHttps);
    }

    /// <summary>
    /// Fails <c>--http3-only</c> with <paramref name="exitCode" /> and <paramref name="message" />
    /// before connecting, reported with <c>closing connection #-1</c>, as curl.se's ngtcp2 build
    /// does (measured, ADR-0144, ADR-0223; BL-867).
    /// </summary>
    private static TransferResult FailBeforeConnecting(HttpRequestPlan plan, CurlExitCode exitCode, string message)
    {
        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.Closing(-1));
        return TransferResult.Failure(exitCode, message) with
        {
            Report = FailedConnectReport(plan, ConnectResult.Failed(exitCode, message)),
        };
    }

    /// <summary>
    /// Opens the transfer's connection (ADR-0144 section 4, ADR-0172): over QUIC for
    /// <c>--http3-only</c>, whose failure is the transfer's; over QUIC raced against TCP for
    /// <c>--http3</c> (<see cref="RaceQuicAgainstTcpAsync" />); and over TCP for every other
    /// version, for an <c>http://</c> URL and through a SOCKS proxy. A QUIC connection is handed on
    /// as an <see cref="Http3Session" />.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectAsync(HttpRequestPlan plan, ConnectTarget target)
    {
        if (!TriesQuic(plan, target))
        {
            return await plan.Deadline.ConnectAsync(connector, target).ConfigureAwait(false);
        }

        if (plan.Options.Version == HttpVersionPreference.Http3)
        {
            return plan.Options.TriesTcpBeforeQuic
                ? await RaceTcpAgainstQuicAsync(plan, target).ConfigureAwait(false)
                : await RaceQuicAgainstTcpAsync(plan, target).ConfigureAwait(false);
        }

        return await ConnectOverQuicAsync(plan, target).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects over QUIC as an <see cref="Http3Session" />, which a pooling connector shares
    /// between the transfers to the origin, each on a request stream of its own (BL-735).
    /// </summary>
    private ValueTask<ConnectResult> ConnectOverQuicAsync(HttpRequestPlan plan, ConnectTarget target, CancellationToken abandoned = default) =>
        plan.Deadline.ConnectMultiplexedSessionAsync(connector, target, quic => new Http3Session(quic), abandoned);

    /// <summary>
    /// Races QUIC against TCP for <c>--http3</c> as curl's ngtcp2 build does
    /// (<c>lib/cf-https-connect.c</c>, ADR-0144 section 4): the TCP connect starts when the QUIC
    /// connect fails or once <see cref="HttpRequestOptions.HappyEyeballsTimeout" /> has passed on
    /// the transfer's clock without it completing; the first to connect carries the transfer
    /// and the other is cancelled, its connection disposed should it still complete; when both
    /// fail the transfer fails with the QUIC attempt's exit code and message, or through a proxy
    /// with the TCP <c>CONNECT</c>'s, as curl 8.22.0 does after a refused CONNECT-UDP (measured,
    /// BL-942).
    /// </summary>
    private async ValueTask<ConnectResult> RaceQuicAgainstTcpAsync(HttpRequestPlan plan, ConnectTarget target)
    {
        using CancellationTokenSource quicAbandoned = new();
        using CancellationTokenSource tcpAbandoned = new();
        Task<ConnectResult> quic = ConnectOverQuicAsync(plan, target, quicAbandoned.Token).AsTask();
        await FirstAttemptOrHappyEyeballsTimeoutAsync(plan, quic).ConfigureAwait(false);
        if (quic.IsCompleted && (await quic.ConfigureAwait(false)).Connection is not null)
        {
            return quic.Result;
        }

        Task<ConnectResult> tcp = plan.Deadline.ConnectAsync(connector, target, tcpAbandoned.Token).AsTask();
        if (await Task.WhenAny(quic, tcp).ConfigureAwait(false) == tcp && (await tcp.ConfigureAwait(false)).Connection is not null)
        {
            await quicAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(quic);
            return tcp.Result;
        }

        ConnectResult quicResult = await quic.ConfigureAwait(false);
        if (quicResult.Connection is not null)
        {
            await tcpAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(tcp);
            return quicResult;
        }

        ConnectResult tcpResult = await tcp.ConfigureAwait(false);
        return tcpResult.Connection is null && target.Proxy is null
            ? ConnectResult.Failed(quicResult.ExitCode, quicResult.ErrorMessage!, tcpResult.Timings, tcpResult.ConnectionNumber)
            : tcpResult;
    }

    /// <summary>
    /// Races TCP against QUIC for <c>--http3</c> with TCP as the preferred first attempt, as curl
    /// 8.21.0 does for an <c>--alt-svc</c> entry naming the origin itself with <c>h2</c> or
    /// <c>h1</c> (<c>cf_hc_get_pref_alpn</c>, BL-948): <see cref="RaceQuicAgainstTcpAsync" /> with the
    /// two attempts swapped, so the QUIC connect starts when the TCP connect fails or once the
    /// happy-eyeballs timeout has passed, and when both fail the transfer fails with the TCP
    /// attempt's result, the first attempt's, as curl's <c>cf_hc_connect</c> reports it.
    /// Both attempts get one target naming the TCP attempt's version (<see cref="ConnectTarget.TcpFirstAttemptVersion" />),
    /// so a connector can write the <c>[HTTPS-CONNECT]</c> lines of the race (BL-1360).
    /// </summary>
    private async ValueTask<ConnectResult> RaceTcpAgainstQuicAsync(HttpRequestPlan plan, ConnectTarget target)
    {
        target = target with { TcpFirstAttemptVersion = plan.Options.TcpFirstAttemptVersion };
        using CancellationTokenSource quicAbandoned = new();
        using CancellationTokenSource tcpAbandoned = new();
        Task<ConnectResult> tcp = plan.Deadline.ConnectAsync(connector, target, tcpAbandoned.Token).AsTask();
        await FirstAttemptOrHappyEyeballsTimeoutAsync(plan, tcp).ConfigureAwait(false);
        if (tcp.IsCompleted && (await tcp.ConfigureAwait(false)).Connection is not null)
        {
            return tcp.Result;
        }

        Task<ConnectResult> quic = ConnectOverQuicAsync(plan, target, quicAbandoned.Token).AsTask();
        if (await Task.WhenAny(tcp, quic).ConfigureAwait(false) == quic && (await quic.ConfigureAwait(false)).Connection is not null)
        {
            await tcpAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(tcp);
            return quic.Result;
        }

        ConnectResult tcpResult = await tcp.ConfigureAwait(false);
        if (tcpResult.Connection is not null)
        {
            await quicAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(quic);
            return tcpResult;
        }

        ConnectResult quicResult = await quic.ConfigureAwait(false);
        return quicResult.Connection is not null ? quicResult : tcpResult;
    }

    /// <summary>
    /// Waits until the first attempt of a race, <paramref name="first" />, completes or the
    /// happy-eyeballs timeout passes on the transfer's clock, whichever comes first.
    /// </summary>
    private static async Task FirstAttemptOrHappyEyeballsTimeoutAsync(HttpRequestPlan plan, Task first)
    {
        using CancellationTokenSource firstCompleted = new();
        Task timeout = Task.Delay(plan.Options.HappyEyeballsTimeout, plan.Context.TimeProvider, firstCompleted.Token);
        await Task.WhenAny(first, timeout).ConfigureAwait(false);
        await firstCompleted.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>Disposes the connection a connect that lost the race opens anyway.</summary>
    private static async Task DisposeLosingAttemptAsync(Task<ConnectResult> attempt)
    {
        await ((Task)attempt).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (attempt.IsCompletedSuccessfully && attempt.Result.Connection is { } connection)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Decides whether the transfer tries QUIC: <c>--http3</c> or <c>--http3-only</c> with an
    /// <c>https://</c> URL, no Unix domain socket, and no proxy or an HTTP or HTTPS one, which
    /// the connector tunnels QUIC through with CONNECT-UDP (BL-942).
    /// </summary>
    private static bool TriesQuic(HttpRequestPlan plan, ConnectTarget target) =>
        plan.Options.Version is HttpVersionPreference.Http3 or HttpVersionPreference.Http3Only
            && target.UseTls
            && (plan.Options.ForwardProxy is null || IsHttpProxy(plan.Options.ForwardProxy))
            && !plan.Options.OverUnixSocket;

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
            ProxyConnectResponseCode = connect.ProxyConnectResponseCode,
            Timings = new TransferTimings(plan.Started, connect.Timings, failed, failed, failed, failed),
        };
    }

    /// <summary>
    /// Sends <paramref name="plan" /> on <paramref name="connection" />, then each retry that
    /// may go on the same connection; then marks the connection reusable when the last
    /// response is reported left intact - it persists, or it is an HTTP/1.0 keep-alive body the
    /// server closed, which the pool then finds dead as curl does (ADR-0112) - or reports that a
    /// pooled connection that died is being given up. A connection that speaks HTTP/2
    /// (<see cref="StreamSessionOf" />) carries each request on a stream of its own; its
    /// <see cref="Http2Session" /> is handed to the connection, which keeps it for the next
    /// transfer when pooled and sends its closing GOAWAY when it closes, or, on a connection
    /// that holds no session, the GOAWAY is sent here (BL-817). An HTTP/3 connection is marked
    /// reusable while its session takes new requests (BL-735).
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
        Http2Session? unheldSession = UnheldHttp2Session(streams, connection);
        (HttpAttemptOutcome outcome, Http2Session? upgradedSession) = await ExchangeWithRetriesAsync(plan, connect, connection, earlier, streams).ConfigureAwait(false);
        unheldSession ??= UnheldHttp2Session(upgradedSession, connection);
        SettleConnection(plan, connection, streams ?? upgradedSession, outcome);
        await ShutDownAsync(unheldSession).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>
    /// Hands an HTTP/2 session to its connection to hold (BL-817), and gives it back when the
    /// connection holds none, so the handler sends its closing GOAWAY itself; gives
    /// <see langword="null" /> for any other session, or none.
    /// </summary>
    private static Http2Session? UnheldHttp2Session(IHttpStreamSession? streams, IConnection connection) =>
        streams is Http2Session http2 && !connection.TryHoldSession(http2) ? http2 : null;

    /// <summary>Sends the closing GOAWAY of a session no connection holds, if there is one.</summary>
    private static ValueTask ShutDownAsync(Http2Session? unheldSession) =>
        unheldSession?.ShutDownAsync(CancellationToken.None) ?? ValueTask.CompletedTask;

    /// <summary>
    /// Marks the connection reusable when the last response is reported left intact over
    /// HTTP/1.x or HTTP/2, or over HTTP/3 while the session takes new requests, so a pool keeps it (BL-735), or reports that a pooled connection that died is being given up.
    /// </summary>
    private static void SettleConnection(HttpRequestPlan plan, IConnection connection, IHttpStreamSession? streams, HttpAttemptOutcome outcome)
    {
        if (outcome.ReportsLeftIntact && TakesNewRequests(streams))
        {
            connection.MarkReusable();
        }
        else if (outcome.DiedBeforeResponse)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.ConnectionDiedRetrying(outcome.RetryCount));
            HttpExchangeLog.For(plan.Context.DiagnosticLog, streams).RetryingOnFreshConnection(outcome.RetryCount);
        }
    }

    /// <summary>
    /// Tells whether a connection can carry another request once its response is left intact:
    /// always over HTTP/1.x and HTTP/2, and over HTTP/3 while the session takes new streams.
    /// </summary>
    private static bool TakesNewRequests(IHttpStreamSession? streams) =>
        streams is not Http3Session { AcceptsNewStreams: false };

    /// <summary>
    /// Sends <paramref name="plan" />, framed for HTTP/2 or HTTP/3 when <paramref name="streams" /> is set,
    /// then each retry the response asks for while the connection stays open and its peer has not
    /// closed it (<see cref="IConnection.HasPeerClosed" />). After an h2c
    /// upgrade the retries go out on new streams of the session the connection switched to, as
    /// curl sends them (measured, BL-866 Notes), and that session is given back with the outcome.
    /// </summary>
    private async ValueTask<(HttpAttemptOutcome Outcome, Http2Session? UpgradedSession)> ExchangeWithRetriesAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection connection,
        TransferReport? earlier,
        IHttpStreamSession? streams)
    {
        HttpRequestPlan first = streams is null ? plan : plan.ForHttp2OrHttp3();
        HttpAttemptOutcome outcome = await ExchangeAsync(first, connect, connection, earlier, newConnection: !connect.IsReused, streams).ConfigureAwait(false);
        Http2Session? upgradedSession = outcome.UpgradedSession;
        streams ??= upgradedSession;
        while (RetryOnSameConnection(outcome, connection) is { } retry)
        {
            ReportRetryOnSameConnection(retry, connect);
            HttpRequestPlan next = streams is null ? retry : retry.ForHttp2OrHttp3();
            outcome = await ExchangeAsync(next, connect, connection, outcome.Result.Report, newConnection: false, streams).ConfigureAwait(false);
        }

        return (outcome, upgradedSession);
    }

    /// <summary>
    /// Gives the retry <paramref name="outcome" /> asks for when it can go out on the same
    /// connection: the connection persists and its peer has not closed it. curl 8.21.0 asks a
    /// kept connection whether it is alive before it sends on it again; one the server has
    /// closed is left intact and the retry issued anew, so the pool reports it dead and opens a
    /// fresh one (BL-2018, ADR-0467). Gives <see langword="null" /> otherwise.
    /// </summary>
    private static HttpRequestPlan? RetryOnSameConnection(HttpAttemptOutcome outcome, IConnection connection) =>
        outcome.KeepsAlive && !connection.HasPeerClosed ? outcome.Retry : null;

    /// <summary>
    /// Reports what curl 8.21.0 writes between a response it answers with another request and
    /// that request, sent on the same connection (measured, BL-959 Notes): the connection left
    /// intact, <c>Issue another request to this URL: '...'</c>, then the connection reused, which
    /// the console writes as <c>Reusing existing http: connection with host ...</c>.
    /// </summary>
    private static void ReportRetryOnSameConnection(HttpRequestPlan retry, ConnectResult connect)
    {
        ConnectTarget target = TargetOf(retry);
        ITransferEvents events = retry.Context.Events;
        events.ReportInfo(LeftIntactLine(target, connect));
        events.ReportInfo(HttpConnectionInfoLines.IssueAnotherRequest(retry.Context.Url));
        events.ReportConnectionReused(new ConnectionReusedEvent
        {
            Scheme = target.PoolScheme!,
            IsProxy = target.IsForwardProxy || target.Proxy is not null,
            HostName = target.Proxy?.Host ?? target.Host,
            Port = target.Proxy?.Port ?? target.Port,
            ConnectionNumber = connect.ConnectionNumber,
        });
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
    /// the HTTP/3 session a QUIC connect made, which a pooled connection holds (BL-735), the HTTP/2 session an earlier transfer left with
    /// a pooled connection (BL-817), a new HTTP/2 session for a connection that speaks HTTP/2
    /// (<see cref="SpeaksHttp2" />), or <see langword="null" /> for HTTP/1.x.
    /// </summary>
    private static IHttpStreamSession? StreamSessionOf(HttpRequestPlan plan, ConnectResult connect, IConnection connection) =>
        (IHttpStreamSession?)(connection as Http3Session)
            ?? (connection.Session as IHttpStreamSession)
            ?? (SpeaksHttp2(plan, connect) ? new Http2Session(connection) : null);

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
    /// shut down otherwise - its response did not persist, or it died before its response. When
    /// the request goes out again on a new connection - after it died, or as a retry such as the
    /// resend after a 417 (measured, BL-1446 Notes) - the line saying so follows. A connection left intact
    /// that another transfer still shares, on a stream of its own, is reported by the last
    /// transfer on it, not this one (BL-717).
    /// </summary>
    private static void ReportConnectionEnd(ITransferContext context, ConnectTarget target, ConnectResult connect, HttpAttemptOutcome outcome)
    {
        if (outcome.ReportsLeftIntact && connect.Connection!.IsSharedWithAnotherTransfer)
        {
            // curl 8.21.0 reports a multiplexed connection left intact only when its last transfer ends (measured, BL-717 Notes).
            return;
        }

        context.Events.ReportInfo(ConnectionEndLine(target, connect, outcome));
        if (outcome.Retry is not null)
        {
            context.Events.ReportInfo(HttpConnectionInfoLines.IssueAnotherRequest(context.Url));
        }
    }

    /// <summary>
    /// Gives curl 8.21.0's <c>-v</c> line for what became of <paramref name="connect" />'s
    /// connection (ADR-0050): left intact, closing, or shutting down. A connection left intact is
    /// named by the target's host and port, or by its Unix domain socket (BL-794).
    /// </summary>
    private static string ConnectionEndLine(ConnectTarget target, ConnectResult connect, HttpAttemptOutcome outcome) =>
        outcome switch
        {
            { ReportsLeftIntact: true } => LeftIntactLine(target, connect),
            { DiedBeforeResponse: false, Result.ExitCode: not CurlExitCode.Ok } => HttpConnectionInfoLines.Closing(connect.ConnectionNumber),
            _ => HttpConnectionInfoLines.ShuttingDown(connect.ConnectionNumber),
        };

    /// <summary>
    /// Names a connection left intact by its Unix domain socket, else by the <c>--connect-to</c>
    /// destination the connector reports it dialled, else by the alt-svc alternative it was
    /// dialled to, else by the target's host and port: curl 8.21.0 names the host it connected
    /// to, not the origin (BL-623 case 1, BL-900, BL-975), except through a CONNECT tunnel or a
    /// SOCKS proxy, where it names the origin, not the proxy (BL-1074).
    /// </summary>
    private static string LeftIntactLine(ConnectTarget target, ConnectResult connect) =>
        (connect.UnixSocketPath, connect.MappedHost, target.AltSvcRoute) switch
        {
            ({ } socketPath, _, _) => HttpConnectionInfoLines.LeftIntactOverUnixSocket(connect.ConnectionNumber, socketPath),
            (null, { } mappedHost, _) => HttpConnectionInfoLines.LeftIntact(connect.ConnectionNumber, mappedHost, connect.MappedPort),
            (null, null, { Alternative: var alternative }) => HttpConnectionInfoLines.LeftIntact(connect.ConnectionNumber, alternative.Host, alternative.Port),
            _ => HttpConnectionInfoLines.LeftIntact(connect.ConnectionNumber, target.Host, target.Port),
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

        // curl 8.21.0 says which version it uses before it builds the request, so the cookie
        // store's limit lines come after it (measured, BL-1136 Notes).
        ReportProtocolChosen(context.Events, newConnection, streams);
        IHttpStreamConnection? requestStream = CreateRequestStream(plan, streams);
        IConnection connection = ExchangeConnectionOf(plan, requestStream, transport);
        byte[] request = FormatRequestHead(plan, streams, connection is HttpH2cUpgradeConnection, Http1VersionSeen.DowngradesToHttp10(transport));
        HttpFirstByteTimingConnection timedConnection = new(connection, context.TimeProvider);
        IConnection responseConnection = framing.AwaitsContinue
            ? new HttpContinueWaitConnection(timedConnection) { ContinueWait = options.ContinueWait }
            : timedConnection;
        HttpRequestBodyWriter upload = new(connection)
        {
            SharedHeadLength = framing.AwaitsContinue ? 0 : request.Length,
            HeldHead = request,
            IsUpload = framing.IsUpload,
            Progress = plan.Progress,
            Events = context.Events,
            EarlyResponseWatch = responseConnection as HttpContinueWaitConnection,
            TracesReaders = TracesReadersOf(requestStream),
        };
        HttpExchange exchange = new(connect, connection, framing.Method, request.Length, upload, earlier, newConnection)
        {
            UsedProxy = options.ForwardProxy is not null,
            Started = plan.Started,
            TimeProvider = context.TimeProvider,
            ResponseConnection = timedConnection,
            RedirectCount = plan.RedirectsFollowed - options.RedirectsFollowed,
            AuthSchemePicked = AuthSchemePickedBy(plan),
        };
        HttpExchangeLog exchangeLog = HttpExchangeLog.For(context.DiagnosticLog, streams);
        HttpResponseBodyReader body = new(responseConnection)
        {
            PassesTransferCoding = options.Raw,
            IgnoresContentLength = options.IgnoreContentLength,
            LimitsFileSize = HttpDownloadConditions.LimitOf(context.MaxFileSize) is not null,
            DecodesTransferCoding = options.TransferEncoding,
            Log = exchangeLog,
            HeadersStoredBefore = HeadersStoredBefore(earlier, options, connect),
        };
        int cookiesStored = 0;
        HttpAuthProblemLines originProblems = new();
        HttpAuthProblemLines proxyProblems = new();
        HttpResponseHeadReader headReader = new(responseConnection)
        {
            Events = context.Events,
            HeadersStoredBefore = body.HeadersStoredBefore,
            LineReporting = line => (requestStream as Http3StreamConnection)?.EchoResponseLineBefore(line),
            LineReported = line => EchoResponseLineAfter(requestStream, connection, line),
            StatusLineReported = statusLine => ReportUploadRewind(plan, statusLine, upload),
            HeaderReceived = (statusLine, header) =>
            {
                ReportAuthProblemLines(plan, statusLine, header, originProblems, proxyProblems);
                cookiesStored = StoreCookie(context, options, header, cookiesStored);
                StoreAltSvc(context, options.AltSvcStore, statusLine.Version, header);
                StoreHsts(context, options.HstsStore, header);
            },
            FindRefusal = head => body.FindHeadRefusal(head, context.NoBody, DecodesContent(options)),
            IsHttp2OrHttp3 = requestStream is not null,
            AcceptsHttp09 = options.AllowHttp09Reply,
            IsHeadRequest = context.NoBody,
            IgnoresContentLength = options.IgnoreContentLength,
            IsThroughHttpProxy = options.ForwardProxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https },
            IsSwitchedToHttp2 = () => IsSwitchedToHttp2(connection),
            DefersFrom = (statusLine, header) => framing.Body is not StreamBody && IsAuthChallenge(plan, statusLine, header),
        };
        HttpRequestPlan? retry = null;
        HttpResponseHead? actedOn = null;
        HttpBodyDelivery delivery = HttpBodyDelivery.Deliver;
        LogVersionChosen(exchangeLog, plan, transport, newConnection, streams);
        ReportAuthorizationLines(plan);
        try
        {
            ThrowIfRefused(framing);
            ThrowIfAuthorizationFailed(plan);
            exchange.RequestReady = context.TimeProvider.GetTimestamp();
            bool bodyLeftUnsent = await SendBodyAsync(context, framing, responseConnection, upload, cancellationToken).ConfigureAwait(false);
            ReportRequestSent(context.Events, framing, upload, bodyLeftUnsent);
            exchangeLog.RequestSent(framing.Method, context.Url.AbsolutePath, request);
            exchange.RequestSent = context.TimeProvider.GetTimestamp();
            exchange.Head = await ReadHeadAsync(headReader, newConnection, cancellationToken).ConfigureAwait(false);
            ThrowIfVersionMismatched(transport, streams is not null || IsSwitchedToHttp2(connection), exchange.Head);
            HttpHeadRefusal? refusal = headReader.Refusal;
            exchange.Head = HeadCurlRead(exchange.Head, refusal);
            actedOn = headReader.HeadActedOn(exchange.Head);
            body.HeadersStoredBefore = headReader.HeadersStoredWith(exchange.Head);
            exchangeLog.ReplyRead(actedOn);
            exchange.RedirectUrl = HttpRedirectLocation.Find(context.Url, actedOn);
            await WriteHeadersAsync(context.HeaderOutput, exchange.Head.HeadBytes, cancellationToken).ConfigureAwait(false);
            ThrowIfHeaderRefused(refusal);
            ThrowIfClosedBeforeContentLength(plan, actedOn, headReader);
            headReader.ReportHeaderHeldAtClose();
            ReportNoEndOfMessageIndicator(plan, actedOn, headReader);
            retry = await RetryOfAsync(plan, actedOn, bodyLeftUnsent, upload, cancellationToken).ConfigureAwait(false);
            headReader.ReleaseDeferredHeaders();
            HttpFailMode fail = FailModeOf(plan, retry, actedOn);
            ThrowIfFailing(fail, HttpFailMode.Fail, actedOn);
            bool discardsBody = DiscardsBody(options, retry, exchange.RedirectUrl);
            ReportAuthRetryRewind(plan, actedOn, retry);
            ReportIgnoredBody(plan, actedOn, discardsBody && !upload.CutShort);
            ReportUploadStopped(context.Events, upload, retry, discardsBody);
            delivery = DeliveryOf(plan, actedOn, discardsBody);
            HttpDownloadConditions.ReportUndeliveredBody(context, actedOn, delivery);
            bool ignoresBody = IgnoresBody(plan, actedOn, delivery, discardsBody);
            headReader.ReportHeldLines();
            await ReadBodyAsync(plan, actedOn, body, TrailerStreamOf(requestStream, connection), delivery, ignoresBody, cancellationToken).ConfigureAwait(false);
            ThrowIfFailing(fail, HttpFailMode.FailWithBody, actedOn);
        }
        catch (HttpTransferException failure)
        {
            headReader.ReportHeldLines();
            ReportReceiveFailure(context.Events, failure);
            exchangeLog.Failed(failure.ExitCode, failure);
            TransferResult failed = TransferResult.Failure(failure.ExitCode, failure.Message, body.BytesWritten)
                with
            { Report = exchange.Report(body) };
            return FailedOutcome(plan, reusedConnection: !newConnection, upload, headReader, failure, failed);
        }
        catch (OperationCanceledException canceled) when (plan.Deadline.EndedByLimit)
        {
            headReader.ReportHeldLines();
            exchangeLog.Failed(CurlExitCode.OperationTimedOut, canceled);
            string message = HttpTransferMessages.OperationTimedOut(plan.Deadline.OperationElapsedMilliseconds, body.BytesWritten, body.ExpectedLength);
            TransferResult timedOut = TransferResult.Failure(CurlExitCode.OperationTimedOut, message, body.BytesWritten)
                with
            { Report = exchange.Report(body) };
            return new HttpAttemptOutcome(timedOut, null, KeepsAlive: false);
        }

        ReportTransferDone(requestStream, connect);
        TransferResult result = Succeeded(delivery, actedOn!, exchange.Report(body));
        LogExchanged(exchangeLog, context.TimeProvider, exchange.RequestReady, actedOn!, body);
        return new HttpAttemptOutcome(result, retry, KeepsAlive(plan, actedOn, upload, headReader, delivery, StreamSessionAfter(streams, connection)))
        {
            LeftIntactAfterServerClosed = LeftIntactAfterServerClosed(plan, actedOn, upload, headReader, delivery),
            UpgradedSession = UpgradedSessionOf(connection),
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
    /// handler answers it with <paramref name="retry" />, or follows its redirect.
    /// </summary>
    private static bool DiscardsBody(HttpRequestOptions options, HttpRequestPlan? retry, string? redirectUrl) =>
        retry is not null || (options.FollowRedirects && redirectUrl is not null);

    /// <summary>
    /// Marks an HTTP/3 request stream's transfer done with its session and writes its
    /// <c>--trace-config http/3</c> lines (<see cref="Http3StreamConnection.ReportTransferDone" />); other streams need neither.
    /// </summary>
    private static void ReportTransferDone(IHttpStreamConnection? requestStream, ConnectResult connect) =>
        (requestStream as Http3StreamConnection)?.ReportTransferDone(connect.ConnectionNumber);

    /// <summary>
    /// Gives the session the exchange ran on: <paramref name="streams" /> when it began on one,
    /// else the HTTP/2 session its connection switched to after an h2c upgrade's <c>101</c>
    /// (BL-866), else <see langword="null" /> for HTTP/1.x.
    /// </summary>
    private static IHttpStreamSession? StreamSessionAfter(IHttpStreamSession? streams, IConnection connection) =>
        streams ?? UpgradedSessionOf(connection);

    /// <summary>
    /// Gives the HTTP/2 session a connection switched to after an h2c upgrade's <c>101</c>, or
    /// <see langword="null" /> when it did not switch.
    /// </summary>
    private static Http2Session? UpgradedSessionOf(IConnection connection) =>
        (connection as HttpH2cUpgradeConnection)?.UpgradedSession;

    /// <summary>
    /// Gives the stream whose end is read for the response's trailers: the exchange's HTTP/2 or
    /// HTTP/3 stream, else stream 1 of a connection that switched to HTTP/2 after an h2c
    /// upgrade's <c>101</c>, so it is read to its end before the connection carries another
    /// request (BL-866), else <see langword="null" />.
    /// </summary>
    private static IHttpStreamConnection? TrailerStreamOf(IHttpStreamConnection? requestStream, IConnection connection) =>
        requestStream ?? (connection as HttpH2cUpgradeConnection)?.UpgradedStream;

    /// <summary>
    /// Creates the HTTP/2 or HTTP/3 stream the exchange runs on, with the request body's length (0 for
    /// none), reporting curl's <c>OPENED stream</c> lines once it is opened (<see cref="HttpStreamOpenedLines" />),
    /// or gives <see langword="null" /> when the connection speaks HTTP/1.x.
    /// </summary>
    private IHttpStreamConnection? CreateRequestStream(HttpRequestPlan plan, IHttpStreamSession? streams) =>
        streams?.CreateStream(
            plan.Context.Url.Scheme,
            plan.Framing.Body is null ? 0 : plan.Framing.KnownLength,
            plan.Context.NoBody,
            new HttpStreamOpenedLines(plan.Context.Events, HttpUrlText.Effective(plan.Context.Url)),
            plan.Context.DiagnosticLog,
            TracesStreamsOf(streams) ? plan.Context.Events : null);

    /// <summary>
    /// Whether the session's version is traced: <see cref="TracesHttp3Streams" /> for HTTP/3,
    /// <see cref="TracesHttp2Frames" /> for HTTP/2.
    /// </summary>
    private bool TracesStreamsOf(IHttpStreamSession streams) =>
        streams is Http3Session ? TracesHttp3Streams : TracesHttp2Frames;

    /// <summary>
    /// Whether the request body's reads write <see cref="TracesClientReaders" />' lines: only over
    /// HTTP/1.x, without a <paramref name="requestStream" />, the version they were measured on.
    /// </summary>
    private bool TracesReadersOf(IHttpStreamConnection? requestStream) => TracesClientReaders && requestStream is null;

    /// <summary>
    /// Gives the connection the exchange reads and writes: the HTTP/2 or HTTP/3 stream when there is one;
    /// else, for <see cref="HttpVersionPreference.Http2" /> over cleartext, the transport watched for an
    /// h2c upgrade's <c>101</c> (<see cref="HttpH2cUpgradeConnection" />, BL-716); and else the transport itself.
    /// The upgrade's HTTP/2 lines are traced as <see cref="TracesHttp2Frames" /> says (BL-1205).
    /// </summary>
    private IConnection ExchangeConnectionOf(HttpRequestPlan plan, IHttpStreamConnection? requestStream, IConnection transport) =>
        (IConnection?)requestStream ?? (UpgradesToH2c(plan, transport)
            ? new HttpH2cUpgradeConnection(transport, plan.Context.Events, plan.Context.Url.Scheme, TracesHttp2Frames ? plan.Context.Events : null)
            : transport);

    /// <summary>
    /// Echoes a response head line as the exchange's HTTP/2 stream's <c>--trace-config http/2</c>
    /// <c>status:</c> or <c>header:</c> line (BL-1205): the request stream's, else stream 1 of a
    /// connection switched to HTTP/2 after an h2c upgrade's <c>101</c>; or, over HTTP/3, the status line
    /// as the request stream's <c>--trace-config http/3</c> <c>status:</c> line (BL-1208); nothing over HTTP/1.x.
    /// </summary>
    /// <summary>
    /// Reports <c>Need to rewind upload for next request</c> after a <c>3xx</c> status line under
    /// <c>-L</c> when the request sent a body that is not empty, as curl 8.21.0 does whatever the
    /// redirect then does with the body: a <c>307</c> sends it again, a <c>302</c> drops it
    /// (measured, BL-1213 Notes). When the status line arrived while the body was being sent,
    /// <c>close instead of sending N more bytes</c> follows it (measured, BL-1527 Notes).
    /// </summary>
    private static void ReportUploadRewind(HttpRequestPlan plan, HttpStatusLine statusLine, HttpRequestBodyWriter upload)
    {
        if (plan.Options.FollowRedirects
            && statusLine.StatusCode is >= 300 and < 400
            && plan.Framing.Body is not null
            && plan.Framing.KnownLength != 0)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.NeedToRewindUpload);
            ReportUploadClosed(plan.Context.Events, plan.Framing.KnownLength, upload);
        }
    }

    /// <summary>
    /// Reports <c>close instead of sending N more bytes</c> when the body was cut short, N being
    /// what was left of <paramref name="knownLength" />, or <c>unknown amount of</c> without one.
    /// </summary>
    private static void ReportUploadClosed(ITransferEvents events, long? knownLength, HttpRequestBodyWriter upload)
    {
        if (upload.CutShort)
        {
            events.ReportInfo(HttpConnectionInfoLines.CloseInsteadOfSending(knownLength, upload.BytesSent));
        }
    }

    /// <summary>
    /// Reports what curl 8.21.0 writes before the head's empty line when a final status arrived
    /// while the request body was being sent and cut it short (measured, BL-1527 Notes): nothing
    /// for one answered with a resend, which writes its own lines; <c>Keep sending data to get
    /// tossed away</c> for a redirect <c>-L</c> follows; and otherwise <c>HTTP error before end of
    /// send, stop sending</c> and <c>abort upload after having sent N bytes</c>.
    /// </summary>
    private static void ReportUploadStopped(ITransferEvents events, HttpRequestBodyWriter upload, HttpRequestPlan? retry, bool discardsBody)
    {
        if (!upload.CutShort || retry is not null)
        {
            return;
        }

        if (discardsBody)
        {
            events.ReportInfo(HttpConnectionInfoLines.KeepSendingToTossAway);
            return;
        }

        events.ReportInfo(HttpConnectionInfoLines.StopSendingBeforeEndOfSend);
        events.ReportInfo(HttpConnectionInfoLines.AbortUpload(upload.BytesSent));
    }

    private static void EchoResponseLineAfter(IHttpStreamConnection? requestStream, IConnection connection, byte[] line)
    {
        IHttpStreamConnection? stream = TrailerStreamOf(requestStream, connection);
        (stream as Http2StreamConnection)?.EchoResponseLine(line);
        (stream as Http3StreamConnection)?.EchoResponseLineAfter(line);
    }

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
    /// Reads the response head; on a reused connection, a reply that cannot begin <c>HTTP/</c>
    /// fails with exit 8, <c>Invalid status line</c>, as curl 8.21.0 never takes HTTP/0.9 on
    /// a reused connection (upstream test1479), rather than with exit 1.
    /// </summary>
    private static async ValueTask<HttpResponseHead> ReadHeadAsync(HttpResponseHeadReader headReader, bool newConnection, CancellationToken cancellationToken)
    {
        try
        {
            return await headReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpTransferException failure) when (!newConnection && failure.Message == HttpTransferMessages.Http09NotAllowed)
        {
            throw new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.InvalidStatusLine);
        }
    }

    /// <summary>
    /// Fails a response over HTTP/1.x whose major version differs from the one the connection's
    /// earlier response named (upstream test471), and else records its version on the
    /// connection for the next request (<see cref="Http1VersionSeen" />).
    /// </summary>
    private static void ThrowIfVersionMismatched(IConnection transport, bool overStreams, HttpResponseHead head)
    {
        if (overStreams)
        {
            return;
        }

        Version version = Http1VersionSeen.VersionNamed(head.HeadBytes.Span, head.StatusLine.Version);
        Http1VersionSeen.ThrowIfMismatched(transport, version);
        Http1VersionSeen.Record(transport, version);
    }

    /// <summary>
    /// Formats the request head: the HTTP/1.1 head, asking to upgrade to h2c when
    /// <paramref name="upgradesToH2c" />, or over HTTP/2 or HTTP/3 the same head naming
    /// <c>HTTP/2</c> or <c>HTTP/3</c> in its request line, which is what is reported sent and what the stream
    /// turns into its HEADERS (<see cref="Http2RequestHeaders" />).
    /// </summary>
    /// <remarks>
    /// On a connection whose earlier response was HTTP/1.0 the head is formatted as for
    /// <c>-0</c>, as curl downgrades the connection (upstream test1074).
    /// </remarks>
    private byte[] FormatRequestHead(HttpRequestPlan plan, IHttpStreamSession? streams, bool upgradesToH2c, bool downgradesToHttp10)
    {
        ITransferContext context = plan.Context;
        byte[] head = HttpRequestHeadFormatter.Format(
            context.Url,
            downgradesToHttp10 ? plan.Options with { Version = HttpVersionPreference.Http10 } : plan.Options,
            context.NoBody,
            plan.Authorization,
            CookieHeaderFor(context, plan.Options),
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
    /// Logs the version the exchange uses to the diagnostic log (BL-922), and, on a connection
    /// this transfer opened, a downgrade from the version the command line asked for.
    /// </summary>
    private static void LogVersionChosen(HttpExchangeLog exchangeLog, HttpRequestPlan plan, IConnection transport, bool newConnection, IHttpStreamSession? streams)
    {
        string versionName = streams?.VersionName ?? HttpExchangeLog.Http1VersionName;
        exchangeLog.VersionChosen(versionName, newConnection);
        if (newConnection)
        {
            exchangeLog.DowngradeOf(plan.Options.Version, transport.IsSecure, versionName);
        }
    }

    /// <summary>
    /// Logs the end of a successful exchange to the diagnostic log (BL-922), reading the clock
    /// only when <c>info</c> lines are written so a disabled log changes no timing.
    /// </summary>
    private static void LogExchanged(HttpExchangeLog exchangeLog, TimeProvider timeProvider, long? requestReady, HttpResponseHead head, HttpResponseBodyReader body)
    {
        if (exchangeLog.LogsInfo)
        {
            exchangeLog.Exchanged(head.StatusLine.StatusCode, body.BytesWritten, timeProvider.GetElapsedTime(requestReady!.Value));
        }
    }

    /// <summary>
    /// Reports, just before the request is sent, what curl 8.21.0 writes while it picks the
    /// request's auth (measured, BL-843 and BL-954 Notes): <c>Proxy auth using ...</c> for a
    /// forward proxy, then the lines the authenticator reported while it made the request's
    /// <c>Authorization</c> value before any challenge, then <c>Server auth using ...</c>
    /// (<see cref="HttpAuthUsingLines" />).
    /// </summary>
    private static void ReportAuthorizationLines(HttpRequestPlan plan)
    {
        bool headerNamesAuthorization = HttpRequestHeadFormatter.CustomHeadersOf(plan.Options.Headers, plan.Options).Any(header => header.Names("Authorization"));
        if (plan.ProxyAuthRequest is { } proxyRequest)
        {
            ReportInfoLines(plan.Context.Events, plan.ProxyAuthorizationInfoLines);
            ReportAuthUsing(plan.Context.Events, HttpAuthUsingLines.AuthUsing(proxyRequest, plan.ProxyAuthorization, plan.ProxyAuthorizationAnswersChallenge, headerNamesAuthorization));
        }

        ReportInfoLines(plan.Context.Events, plan.AuthorizationInfoLines);
        ReportAuthUsing(plan.Context.Events, HttpAuthUsingLines.AuthUsing(plan.AuthRequest, plan.Authorization, plan.AuthorizationAnswersChallenge, headerNamesAuthorization));
    }

    /// <summary>
    /// Decides whether <paramref name="header" /> of a final head is a challenge the lines the
    /// authenticator reports while it answers are written before: a Negotiate one
    /// (<see cref="HttpNegotiateInfoLines.IsNegotiateChallenge" />), or an NTLM one to the origin's
    /// or the proxy's request (<see cref="HttpNtlmInfoLines.IsNtlmChallenge" />, BL-848).
    /// </summary>
    private static bool IsAuthChallenge(HttpRequestPlan plan, HttpStatusLine statusLine, HttpResponseHeader header) =>
        HttpNegotiateInfoLines.IsNegotiateChallenge(plan.AuthRequest, statusLine, header)
            || HttpNtlmInfoLines.IsNtlmChallenge(plan.AuthRequest, statusLine, header)
            || (plan.ProxyAuthRequest is { } proxyRequest && HttpNtlmInfoLines.IsNtlmChallenge(proxyRequest, statusLine, header));

    /// <summary>
    /// Reports the <c>authentication problem, ignoring.</c> lines curl writes just before a
    /// challenge header refusing the Basic, Bearer or Digest value the request sent to the proxy
    /// or the origin, and its duplicate Digest lines, each read by the head's own
    /// <see cref="HttpAuthProblemLines" /> (BL-1040, BL-1175).
    /// </summary>
    private static void ReportAuthProblemLines(
        HttpRequestPlan plan, HttpStatusLine statusLine, HttpResponseHeader header, HttpAuthProblemLines originProblems, HttpAuthProblemLines proxyProblems)
    {
        if (plan.ProxyAuthRequest is { } proxyRequest)
        {
            ReportInfoLines(plan.Context.Events, [.. proxyProblems.LinesBefore(proxyRequest, plan.ProxyAuthorization, statusLine, header)]);
        }

        ReportInfoLines(plan.Context.Events, [.. originProblems.LinesBefore(plan.AuthRequest, plan.Authorization, statusLine, header)]);
    }

    /// <summary>Reports <paramref name="line" />, or nothing when it is <see langword="null" />.</summary>
    private static void ReportAuthUsing(ITransferEvents events, string? line)
    {
        if (line is not null)
        {
            events.ReportInfo(line);
        }
    }

    /// <summary>
    /// Reports that the request went out, as curl 8.21.0 does (measured, BL-407 Notes):
    /// <c>Request completely sent off</c> for a request without a body or whose body put no
    /// bytes on the wire (an empty <c>-d ''</c> or <c>-T</c> file, BL-1216 Notes), and
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
            events.ReportInfo(upload.BytesSent == 0 ? HttpConnectionInfoLines.RequestSent : HttpConnectionInfoLines.UploadSent(upload.BytesSent));
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
    /// Reports <c>Need to rewind upload for next request</c> when a 401 or 407 is answered with
    /// <paramref name="retry" /> and the request sent a body that is not empty, as curl 8.21.0
    /// writes it after the head's last header, before <c>Ignoring the response-body</c>, for a
    /// <c>-T</c> file, a <c>-F</c> form or <c>-d</c> data, and not for an empty one (measured,
    /// BL-2001 Notes).
    /// </summary>
    private static void ReportAuthRetryRewind(HttpRequestPlan plan, HttpResponseHead head, HttpRequestPlan? retry)
    {
        if (retry is not null
            && head.StatusLine.StatusCode is 401 or 407
            && plan.Framing.Body is not null
            && plan.Framing.KnownLength != 0)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.NeedToRewindUpload);
        }
    }

    /// <summary>
    /// Reports that a body is read only to be discarded, as curl 8.21.0 does before the
    /// head's empty line (measured, BL-449 Notes): <c>Ignoring the response-body</c> when the
    /// body is discarded - a redirect <c>-L</c> follows, or a response answered with a retry -
    /// on a connection that stays open, and then <c>setting size while ignoring</c> when its
    /// length is known from a Content-Length. Nothing when the connection closes after it, as
    /// it does once the request body was cut short (measured, BL-1446 Notes): curl reads no
    /// such body at all.
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
    /// Tells whether the response's body is read and discarded: one <c>-L</c> or a retry
    /// discards, or a 416's to a resume (<see cref="HttpDownloadConditions.IsServerAnswer" />),
    /// which curl 8.21.0 reads and ignores, writing <c>setting size while ignoring</c> before
    /// the head's empty line when a Content-Length gives its size (measured, BL-1412 Notes). A
    /// real 304 under <c>-z</c> has no body to read.
    /// </summary>
    private static bool IgnoresBody(HttpRequestPlan plan, HttpResponseHead head, HttpBodyDelivery delivery, bool discardsBody)
    {
        if (discardsBody)
        {
            return true;
        }

        if (!HttpDownloadConditions.IsServerAnswer(head, delivery))
        {
            return false;
        }

        if (HttpResponseBodyReader.HasBody(head, plan.Context.NoBody) && IgnoredBodyLength(plan, head) is not null)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.SettingSizeWhileIgnoring);
        }

        return true;
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
    /// Reports a read a socket error failed as the <c>-v</c> line curl 8.21.0's <c>failf</c> prints
    /// for it, such as <c>Recv failure: Connection was reset</c>, before the connection's end is
    /// reported (measured, BL-449 Notes), and an HTTP/3 stream the server refused as the line curl 8.21.0's
    /// <c>cf-ngtcp2.c</c> prints for it (ADR-0187), then the failure's own <see cref="HttpTransferException.InfoLines" />. Any other failure is left to the transfer's result.
    /// </summary>
    private static void ReportReceiveFailure(ITransferEvents events, HttpTransferException failure)
    {
        if (failure.IsStreamRefused || failure.Message.StartsWith(HttpTransferMessages.ReceiveFailurePrefix, StringComparison.Ordinal))
        {
            events.ReportInfo(failure.Message);
        }

        foreach (string line in failure.InfoLines)
        {
            events.ReportInfo(line);
        }
    }

    /// <summary>
    /// Builds the outcome of an exchange whose HTTP/3 stream the server refused, as curl
    /// 8.21.0's <c>Curl_retry_request</c> does (ADR-0187): when no byte of the response had
    /// arrived, the request is sent again on a new connection up to
    /// <see cref="MaximumStreamRefusedRetries" /> times, and the refusal after that fails with
    /// <c>Connection died, tried 5 times before giving up</c>; once the response had begun it
    /// fails at once with curl's text for exit 56. A <c>-T</c> upload is rewound for the retry
    /// as curl's <c>Curl_creader_set_rewind</c> does (BL-885, ADR-0279): a seekable one to where
    /// <paramref name="upload" /> began reading it, and one that cannot seek makes the retry fail
    /// with exit 65 before it connects (<see cref="HttpRequestPlan.UploadCannotRewind" />).
    /// </summary>
    private static HttpAttemptOutcome StreamRefusedOutcome(HttpRequestPlan plan, HttpRequestBodyWriter upload, TransferResult refused, bool responseBegan)
    {
        if (responseBegan)
        {
            return new HttpAttemptOutcome(refused with { ErrorMessage = HttpTransferMessages.ReceiveFailed }, null, KeepsAlive: false);
        }

        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.RefusedStreamRetrying);
        return plan.StreamRefusedRetries < MaximumStreamRefusedRetries
            ? new HttpAttemptOutcome(refused, plan.AfterStreamRefused(!TryRewindUpload(plan.Framing.Body, upload)), KeepsAlive: false) { DiedBeforeResponse = true, RetryCount = plan.StreamRefusedRetries + 1 }
            : new HttpAttemptOutcome(refused with { ErrorMessage = HttpTransferMessages.ConnectionDiedGivingUp(MaximumStreamRefusedRetries) }, null, KeepsAlive: false);
    }

    /// <summary>
    /// Rewinds a <c>-T</c> upload read from a seekable stream to where <paramref name="upload" />
    /// began reading it, so it is sent again whole; gives <see langword="false" /> for one that
    /// cannot seek, such as stdin, and <see langword="true" /> for a body of bytes or none.
    /// </summary>
    private static bool TryRewindUpload(HttpRequestBody? body, HttpRequestBodyWriter upload)
    {
        if (body is not StreamBody stream)
        {
            return true;
        }

        if (!stream.Content.CanSeek)
        {
            return false;
        }

        upload.Rewound(stream);
        return true;
    }

    /// <summary>
    /// Fails a retry whose <c>-T</c> upload cannot be rewound before it connects, with exit 65
    /// <c>seek callback returned error 2</c> after the <c>-v</c> lines curl 8.21.0's
    /// <c>cr_in_rewind</c> and <c>Curl_client_start</c> write for it (ADR-0279).
    /// </summary>
    private static TransferResult UploadRewindFailed(HttpRequestPlan plan, TransferReport? earlier)
    {
        plan.Context.Events.ReportInfo(HttpTransferMessages.UploadSeekFailed);
        plan.Context.Events.ReportInfo(HttpTransferMessages.UploadReaderRewindFailed);
        return TransferResult.Failure(CurlExitCode.SendFailRewind, HttpTransferMessages.UploadSeekFailed) with { Report = earlier };
    }

    /// <summary>
    /// Builds the outcome of an exchange that failed with <paramref name="failure" />: as
    /// <see cref="StreamRefusedOutcome" /> says when the server refused its HTTP/3 stream; sent
    /// again once on a fresh connection when a connection that already carried a request died
    /// before the response
    /// (<see cref="DiedBeforeResponse" />); and final otherwise.
    /// </summary>
    private static HttpAttemptOutcome FailedOutcome(HttpRequestPlan plan, bool reusedConnection, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpTransferException failure, TransferResult failed)
    {
        if (failure.IsStreamRefused)
        {
            return StreamRefusedOutcome(plan, upload, failed, headReader.HasReceived);
        }

        return DiedBeforeResponse(plan, reusedConnection, headReader, failure)
            ? new HttpAttemptOutcome(failed, plan.OnFreshConnection(), KeepsAlive: false) { DiedBeforeResponse = true }
            : new HttpAttemptOutcome(failed, null, KeepsAlive: false);
    }

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
        DeliveredWhole(head, upload, headReader, delivery)
            && (streams?.AcceptsNewStreams
                ?? HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody || !headReader.EndedAtEmptyLine, plan.Options.Raw, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding));

    /// <summary>
    /// Decides whether the connection is reported left intact although the server closed it
    /// to end the body, as curl 8.21.0 does for an HTTP/1.0 keep-alive response with no length
    /// (measured, BL-471 Notes; ADR-0324). It is marked reusable, as curl pools it, and the pool
    /// reports it dead before any reuse (ADR-0112).
    /// </summary>
    private static bool LeftIntactAfterServerClosed(HttpRequestPlan plan, HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        DeliveredWhole(head, upload, headReader, delivery)
            && HttpConnectionPersistence.KeepsHttp10AliveUntilServerCloses(head, plan.Context.NoBody, plan.Options.Raw, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding);

    /// <summary>
    /// Decides whether the exchange ran whole: its request body was not cut short, its
    /// response body was delivered, or was the server's own answer to <c>-C</c> or <c>-z</c>
    /// (<see cref="HttpDownloadConditions.IsServerAnswer" />), and the response did not switch
    /// protocols, unless it switched to HTTP/2 after an h2c upgrade's <c>101</c> and was read
    /// from stream 1 (BL-866).
    /// </summary>
    private static bool DeliveredWhole(HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        !upload.CutShort
            && (delivery == HttpBodyDelivery.Deliver || HttpDownloadConditions.IsServerAnswer(head, delivery))
            && (!headReader.SwitchedProtocols || headReader.IsSwitchedToHttp2());

    /// <summary>
    /// Decides whether a failed exchange was on a reused connection that died while idle, so
    /// the request is sent again once on a fresh one, as curl 8.21.0 does (BL-336 Notes): the
    /// connection was reused, from the pool or by an earlier request of this transfer such as
    /// the one a 401 answered (BL-1797), it failed sending or receiving before any byte of the response
    /// arrived, the request has not already been sent again for this reason, and its body, if
    /// any, is bytes that can be sent again.
    /// </summary>
    private static bool DiedBeforeResponse(HttpRequestPlan plan, bool reusedConnection, HttpResponseHeadReader headReader, HttpTransferException failure) =>
        !headReader.HasReceived
            && failure.ExitCode is CurlExitCode.GotNothing or CurlExitCode.SendError or CurlExitCode.RecvError
            && CanSendAgainOnFreshConnection(plan, reusedConnection);

    /// <summary>
    /// Decides whether <paramref name="plan" /> may be sent again on a fresh connection: it went
    /// out on a reused one, it has not already been sent again, and its body, if any, is bytes
    /// that can be sent again.
    /// </summary>
    private static bool CanSendAgainOnFreshConnection(HttpRequestPlan plan, bool reusedConnection) =>
        reusedConnection
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
    /// Fails the exchange, before the request is sent, with the authenticator's refusal to make
    /// the first request's value (<see cref="HttpRequestPlan.AuthorizationFailure" />), as
    /// curl 8.21.0 fails an <c>--aws-sigv4</c> it cannot sign with once connected (measured, BL-629 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator refused.</exception>
    private static void ThrowIfAuthorizationFailed(HttpRequestPlan plan)
    {
        if (plan.AuthorizationFailure is { } failure)
        {
            throw failure;
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
    /// Applies <c>--max-filesize</c> to the head's Content-Length of a body that is kept, then
    /// decides what becomes of it (<see cref="HttpDownloadConditions" />). A discarded body - a
    /// redirect <c>-L</c> follows, a 401 before its retry - is not held to the limit, as curl
    /// 8.21.0 holds only the body it keeps to it (upstream test477, BL-1807).
    /// </summary>
    /// <exception cref="HttpTransferException">
    /// The Content-Length is over the limit (exit 63), or a resume was not honoured (exit 33).
    /// </exception>
    private static HttpBodyDelivery DeliveryOf(HttpRequestPlan plan, HttpResponseHead head, bool discardsBody)
    {
        HttpDownloadConditions.ThrowIfContentLengthExceeds(discardsBody ? null : plan.Context.MaxFileSize, head);
        return discardsBody ? HttpBodyDelivery.Deliver : HttpDownloadConditions.Decide(plan.Context, plan.Framing.Body is not null, head);
    }

    /// <summary>
    /// Reads the body into the transfer's output, or into nothing when it is discarded, as
    /// <see cref="SetBodyLimitAndSinks" /> sets it up, then writes a chunked body's trailers; or reads
    /// nothing when <paramref name="delivery" /> says there is no body to deliver and it is not
    /// being discarded (a 416's is, <see cref="IgnoresBody" />). Over HTTP/2 and HTTP/3
    /// the trailers are the stream's trailing field section, read once the stream has ended. An
    /// HTTP/2 stream's discarded body is not read at all: the stream is given up
    /// (<see cref="Http2StreamConnection.AbandonResponseAsync" />), as curl resets it (BL-970).
    /// </summary>
    private static ValueTask ReadBodyAsync(
        HttpRequestPlan plan,
        HttpResponseHead head,
        HttpResponseBodyReader body,
        IHttpStreamConnection? requestStream,
        HttpBodyDelivery delivery,
        bool discardsBody,
        CancellationToken cancellationToken)
    {
        if ((delivery != HttpBodyDelivery.Deliver && !discardsBody) || AbandonsBody(plan, head, requestStream, delivery, discardsBody))
        {
            return ValueTask.CompletedTask;
        }

        return discardsBody && requestStream is Http2StreamConnection http2Stream
            ? http2Stream.AbandonResponseAsync(cancellationToken)
            : CopyBodyAsync(plan, head, body, requestStream, discardsBody, cancellationToken);
    }

    /// <summary>
    /// Decides whether an HTTP/1.x body discarded for a retry or a followed redirect is not read
    /// at all because the connection closes after it, as curl 8.21.0 stops reading once the head
    /// is in when it has a new request to make and the connection is to close anyway - so a 401
    /// with <c>Connection: close</c> and no length is answered at once, not when the server
    /// closes (measured, BL-2001 Notes). A 416's ignored body (<paramref name="delivery" /> not
    /// <see cref="HttpBodyDelivery.Deliver" />) is still read.
    /// </summary>
    private static bool AbandonsBody(HttpRequestPlan plan, HttpResponseHead head, IHttpStreamConnection? requestStream, HttpBodyDelivery delivery, bool discardsBody)
    {
        HttpRequestOptions options = plan.Options;
        return discardsBody
            && delivery == HttpBodyDelivery.Deliver
            && requestStream is null
            && !HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, options.Raw, options.IgnoreContentLength, options.TransferEncoding);
    }

    /// <summary>
    /// Copies the body into the transfer's output, or into nothing when it is discarded, then
    /// writes its trailers (<see cref="ReadBodyAsync" />).
    /// </summary>
    private static async ValueTask CopyBodyAsync(
        HttpRequestPlan plan,
        HttpResponseHead head,
        HttpResponseBodyReader body,
        IHttpStreamConnection? requestStream,
        bool discardsBody,
        CancellationToken cancellationToken)
    {
        ITransferContext context = plan.Context;
        Stream bodyOutput = discardsBody ? Stream.Null : context.Output;
        SetBodyLimitAndSinks(plan, body, discardsBody);
        HttpTransferException? tooManyTrailers = null;
        try
        {
            await body.CopyAsync(head, context.NoBody, bodyOutput, DecodesContent(plan.Options, discardsBody), plan.Options.TransferEncoding && !discardsBody, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpTransferException failure) when (failure.Message == HttpTransferMessages.TooManyResponseHeaders)
        {
            tooManyTrailers = failure;
        }

        if (tooManyTrailers is not null)
        {
            // curl 8.21.0 writes the trailers it stored before the one past the limit (measured, BL-1448 Notes).
            await WriteHeadersAsync(context.HeaderOutput, body.TrailerBytes, cancellationToken).ConfigureAwait(false);
            throw tooManyTrailers;
        }

        ReadOnlyMemory<byte> trailers = await TrailersOfAsync(body, requestStream, cancellationToken).ConfigureAwait(false);
        int storedLength = StoredLengthOf(trailers.Span, requestStream is null ? int.MaxValue : HttpResponseHeadReader.MaximumHeaderCount - body.HeadersStored);
        await WriteHeadersAsync(context.HeaderOutput, trailers[..storedLength], cancellationToken).ConfigureAwait(false);
        if (storedLength < trailers.Length)
        {
            throw new HttpTransferException(CurlExitCode.TooLarge, HttpTransferMessages.TooManyResponseHeaders)
            {
                InfoLines = [HttpTransferMessages.TooManyResponseHeaders],
            };
        }
    }

    /// <summary>
    /// Gives the length of the first <paramref name="allowed" /> lines of <paramref name="trailers" />, all of
    /// them when there are no more: the HTTP/2 or HTTP/3 trailers curl 8.21.0 stores before the one past its
    /// limit of 5000 response headers, which it counts as it counts chunked trailers (BL-1609 Notes, ADR).
    /// A chunked body's trailers were already cut by <see cref="HttpChunkedDecoder.TrailerLimit" />.
    /// </summary>
    internal static int StoredLengthOf(ReadOnlySpan<byte> trailers, int allowed)
    {
        int length = 0;
        for (int line = 0; line < allowed && length < trailers.Length; line++)
        {
            int end = trailers[length..].IndexOf((byte)'\n');
            length = end < 0 ? trailers.Length : length + end + 1;
        }

        return length;
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
        long? lastModified = HttpLastModified.Find(head);
        return delivery == HttpBodyDelivery.TimeConditionUnmet
            ? TransferResult.TimeConditionNotMet() with { SourceLastWriteUnixSeconds = lastModified, Report = report with { ResponseCode = 304 } }
            : TransferResult.Success(report.DownloadSize) with { SourceLastWriteUnixSeconds = lastModified, Report = report };
    }

    /// <summary>
    /// Asks the cookie store for the <c>Cookie</c> value to send to the transfer's URL, or
    /// gives <see langword="null" /> when cookies are off: the stored cookies alone when
    /// <see cref="HttpRequestOptions.SendsCookieStrings" /> is off (BL-1846). The store reports a
    /// limit that cut the value short to the transfer's events, before the request's header
    /// lines, as curl does.
    /// </summary>
    private string? CookieHeaderFor(ITransferContext context, HttpRequestOptions options) =>
        options.SendsCookieStrings
            ? CookieStore?.GetCookieHeader(CookieUrlOf(context.Url, options), TargetOf(context.Url).UseTls, context.TimeProvider.GetUtcNow(), context.Events)
            : CookieStore?.GetStoredCookieHeader(CookieUrlOf(context.Url, options), TargetOf(context.Url).UseTls, context.TimeProvider.GetUtcNow(), context.Events);

    /// <summary>
    /// Gives the URL cookies are matched and stored against: <paramref name="url" /> with the
    /// host of a <c>-H</c> <c>Host</c> value in its place, as curl takes its cookie host from a
    /// custom <c>Host</c> header, or <paramref name="url" /> itself when there is none.
    /// </summary>
    internal static CurlUrl CookieUrlOf(CurlUrl url, HttpRequestOptions options) =>
        HttpRequestHeadFormatter.CustomHostOf(options) is { } host
        && CurlUrl.TryParse($"{url.Scheme}://{host}{url.AbsolutePath}", false, out CurlUrl? cookieUrl)
            ? cookieUrl
            : url;

    /// <summary>
    /// Hands <paramref name="header" />, when it is a <c>Set-Cookie</c> header and cookies are
    /// on, to the cookie store with the transfer's events for its <c>-v</c> lines, as it arrives
    /// in any head of the response, 1xx heads' included (measured, BL-468 Notes).
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <param name="options">The request's options, whose <c>-H</c> <c>Host</c> value gives the cookie host (<see cref="CookieUrlOf" />).</param>
    /// <param name="header">A whole header of the response.</param>
    /// <param name="storedFromResponse">How many cookies the store has stored from this request's responses.</param>
    /// <returns>The count the store gives back, or <paramref name="storedFromResponse" /> when the store is not asked.</returns>
    private int StoreCookie(ITransferContext context, HttpRequestOptions options, HttpResponseHeader header, int storedFromResponse) =>
        CookieStore is { } store && string.Equals(header.Name, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
            ? store.StoreFromResponse(CookieUrlOf(context.Url, options), header.Value, storedFromResponse, context.TimeProvider.GetUtcNow(), context.Events)
            : storedFromResponse;

    /// <summary>
    /// Hands <paramref name="header" />, when it is an <c>Alt-Svc</c> header of a response to an
    /// <c>https</c> URL and <paramref name="store" /> is set, to the store with the transfer's
    /// URL as the origin, and reports curl 8.21.0's <c>Added alt-svc: &lt;host&gt;:&lt;port&gt; over
    /// &lt;id&gt;</c> for each alternative it added, before the header line (measured, BL-623
    /// Notes), and for each it skipped the <c>lib/altsvc.c</c> line its reason names, in header
    /// order (measured, ADR-0409). curl learns no
    /// alternative over plain <c>http</c>. The store is told
    /// <paramref name="responseVersion" />, the version the response came over, as curl 8.21.0
    /// passes <c>k->httpversion</c> to <c>Curl_altsvc_parse</c> (BL-947).
    /// </summary>
    private static void StoreAltSvc(ITransferContext context, IAltSvcStore? store, Version responseVersion, HttpResponseHeader header)
    {
        if (store is null
            || context.Url.Scheme != "https"
            || !string.Equals(header.Name, "Alt-Svc", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IReadOnlyList<AltSvcHeaderOutcome> outcomes = store.StoreFromResponse(context.Url, header.Value, responseVersion, context.TimeProvider.GetUtcNow());
        foreach (AltSvcHeaderOutcome outcome in outcomes)
        {
            context.Events.ReportInfo(AltSvcOutcomeLine(outcome));
        }
    }

    /// <summary>
    /// Gets curl 8.21.0's <c>-v</c> line for one <c>Alt-Svc</c> outcome: <c>Added alt-svc: ...</c>
    /// for an alternative added, or the <c>lib/altsvc.c</c> text for the reason one was skipped
    /// (ADR-0409).
    /// </summary>
    private static string AltSvcOutcomeLine(AltSvcHeaderOutcome outcome) =>
        outcome switch
        {
            { Added: { } added } => $"Added alt-svc: {added.Host}:{added.Port} over {added.Alpn}",
            { SkipReason: AltSvcSkipReason.BadHostname } => "Bad alt-svc hostname, ignoring.",
            { SkipReason: AltSvcSkipReason.BadIpv6Hostname } => "Bad alt-svc IPv6 hostname, ignoring.",
            _ => "Unknown alt-svc port number, ignoring.",
        };

    /// <summary>
    /// Hands <paramref name="header" />, when it is a <c>Strict-Transport-Security</c> header of a
    /// response to an <c>https</c> URL and <paramref name="store" /> is set, to the store with the
    /// transfer's URL as the origin, and reports curl 8.21.0's <c>Illegal STS header skipped</c>
    /// before the header line when the store refuses it (measured, ADR-0409). curl learns no HSTS
    /// over plain <c>http</c>.
    /// </summary>
    private static void StoreHsts(ITransferContext context, IHstsStore? store, HttpResponseHeader header)
    {
        if (store is not null
            && context.Url.Scheme == "https"
            && string.Equals(header.Name, "Strict-Transport-Security", StringComparison.OrdinalIgnoreCase)
            && !store.StoreFromResponse(context.Url, header.Value, context.TimeProvider.GetUtcNow()))
        {
            context.Events.ReportInfo("Illegal STS header skipped");
        }
    }

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
        EndUnchallengedAuthorizations(plan, head.StatusLine.StatusCode);
        if (await RetryProxyAuthorizationAsync(plan, head, cancellationToken).ConfigureAwait(false) is { } proxyAuthorization)
        {
            RewindForResend(plan, upload);
            return plan.WithProxyAuthorization(proxyAuthorization, RepeatAuthorization(plan));
        }

        if (await RetryWithAuthorizationAsync(plan, head, cancellationToken).ConfigureAwait(false) is { } authorized)
        {
            RewindForResend(plan, upload);
            return authorized;
        }

        if (plan.ProbedFraming is not null && head.StatusLine.StatusCode is >= 200 and < 300)
        {
            return plan.WithProbedBody();
        }

        if (!RetriesWithoutExpect(plan, head, bodyLeftUnsent || upload.CutShort))
        {
            return null;
        }

        plan.Context.Events.ReportInfo(bodyLeftUnsent ? HttpConnectionInfoLines.Got417WhileWaiting : HttpConnectionInfoLines.Got417WhileSending);
        ThrowIfRedirectLimitReached(plan);
        ReportUploadAbandoned(plan.Context.Events, upload, bodyLeftUnsent);
        return plan.WithoutExpect(upload.Rewound(plan.Framing.Body!), keepsCustomWait: !bodyLeftUnsent);
    }

    /// <summary>
    /// Seeks a stream body that <paramref name="upload" /> began reading back to where it
    /// began, so an authentication retry sends it again whole, as curl 8.21.0 rewinds a
    /// <c>-T</c> file or a <c>-F</c> form before it answers a 401 or 407 (upstream test1030,
    /// test259). Only a seekable stream reaches here (<see cref="MayRetry" />); a body of bytes
    /// needs nothing.
    /// </summary>
    private static void RewindForResend(HttpRequestPlan plan, HttpRequestBodyWriter upload)
    {
        if (plan.Framing.Body is StreamBody stream)
        {
            upload.Rewound(stream);
        }
    }

    /// <summary>
    /// Reports what curl 8.21.0 writes after <c>Got HTTP failure 417 while sending data</c>, before
    /// the 417's empty line (measured, BL-1446 Notes): <c>Need to rewind upload for next request</c>
    /// and <c>abort upload after having sent N bytes</c>. Nothing after a 417 that arrived while
    /// the body still waited for <c>100 Continue</c>, as none of it was sent.
    /// </summary>
    private static void ReportUploadAbandoned(ITransferEvents events, HttpRequestBodyWriter upload, bool bodyLeftUnsent)
    {
        if (bodyLeftUnsent)
        {
            return;
        }

        events.ReportInfo(HttpConnectionInfoLines.NeedToRewindUpload);
        events.ReportInfo(HttpConnectionInfoLines.AbortUpload(upload.BytesSent));
    }

    /// <summary>
    /// Tells the authenticator that the handshake behind each value <paramref name="plan" />
    /// sent is over when the response is not that value's challenge - not a 401 for the
    /// <c>Authorization</c> value, not a 407 for the <c>Proxy-Authorization</c> value - so a
    /// Negotiate context kept for its next leg is disposed of. The acceptor's final token in a
    /// 2xx is not checked, as curl 8.21.0 reads <c>WWW-Authenticate</c> only on a 401 and
    /// <c>Proxy-Authenticate</c> only on a 407, so the response is the result whatever the
    /// token (ADR-0248).
    /// </summary>
    private void EndUnchallengedAuthorizations(HttpRequestPlan plan, int statusCode)
    {
        if (plan.Authorization is { } sent && statusCode != 401)
        {
            Authenticator.EndAuthorization(sent);
        }

        if (plan.ProxyAuthorization is { } proxySent && statusCode != 407)
        {
            Authenticator.EndAuthorization(proxySent);
        }
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
    /// that drew it already sent one, which only a handshake of more than one leg (NTLM,
    /// Negotiate) answers, so a credential sent up front and refused ends the transfer, as in
    /// curl 8.21.0 (ADR-0181, ADR-0227).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />).</exception>
    private ValueTask<string?> RetryAuthorizationAsync(HttpRequestPlan plan, HttpAuthRequest request, HttpResponseHead head, CancellationToken cancellationToken) =>
        AnswerChallengesAsync(
            request,
            plan.Authorization,
            plan.AuthorizationAnswersChallenge,
            MayRetry(plan, head, 401) ? ValuesOf(head, "WWW-Authenticate") : [],
            cancellationToken);

    /// <summary>
    /// Makes the request <see cref="RetryAuthorizationAsync" /> answers a 401 with, or gives
    /// <see langword="null" /> when it answers none. What the authenticator reports while it
    /// answers goes where curl 8.21.0 writes it (ADR-0232): before the retry when the request
    /// that drew the 401 was not sent with Negotiate picked, as curl steps that context on the
    /// way out, and else at once, just before the Negotiate challenge header (ADR-0231), as it
    /// does when there is no retry. A refusal to answer when Negotiate was not picked fails the
    /// retry instead, once its lines are written, as curl fails making NTLM's Type 3 message on
    /// the way out (measured, BL-848 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />) while Negotiate is picked.</exception>
    private async ValueTask<HttpRequestPlan?> RetryWithAuthorizationAsync(HttpRequestPlan plan, HttpResponseHead head, CancellationToken cancellationToken)
    {
        HttpInfoLineRecorder retryLines = new();
        bool picksNegotiate = HttpNegotiateInfoLines.PicksNegotiate(plan.AuthRequest, plan.Authorization, plan.AuthorizationAnswersChallenge);
        HttpAuthRequest request = picksNegotiate ? plan.AuthRequest : plan.AuthRequest with { Events = retryLines };
        string? authorization;
        try
        {
            authorization = await RetryAuthorizationAsync(plan, request, head, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpTransferException failure) when (!picksNegotiate)
        {
            return plan.WithAuthorizationFailure(failure, retryLines.Lines, RepeatProxyAuthorization(plan));
        }

        if (authorization is null)
        {
            ReportInfoLines(plan.Context.Events, retryLines.Lines);
            return null;
        }

        return plan.WithAuthorization(authorization, retryLines.Lines, RepeatProxyAuthorization(plan));
    }

    /// <summary>
    /// Makes the <c>Authorization</c> value a retry that answers a 407 keeps: the one
    /// <paramref name="plan" /> sent, as <see cref="IHttpAuthenticator.RepeatAuthorization" />
    /// sends it again, so a Digest answer counts its nonce on as in curl 8.21.0 (BL-869);
    /// <see langword="null" /> when it sent none.
    /// </summary>
    private string? RepeatAuthorization(HttpRequestPlan plan) =>
        plan.Authorization is { } sent ? Authenticator.RepeatAuthorization(plan.AuthRequest, sent) : null;

    /// <summary>
    /// Makes the <c>Proxy-Authorization</c> value a retry that answers a 401 keeps, as
    /// <see cref="RepeatAuthorization" /> makes the <c>Authorization</c> value a 407's retry keeps.
    /// </summary>
    private string? RepeatProxyAuthorization(HttpRequestPlan plan) =>
        plan.ProxyAuthRequest is { } request && plan.ProxyAuthorization is { } sent
            ? Authenticator.RepeatAuthorization(request, sent)
            : plan.ProxyAuthorization;

    /// <summary>Reports each of <paramref name="lines" /> to <paramref name="events" />, in order.</summary>
    private static void ReportInfoLines(ITransferEvents events, IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            events.ReportInfo(line);
        }
    }

    /// <summary>
    /// Decides whether a response is answered with one more request, and with what
    /// <c>Proxy-Authorization</c> value: only a 407 from a forward proxy, on the same terms as
    /// <see cref="RetryAuthorizationAsync" /> sets for a 401, with the response's
    /// <c>Proxy-Authenticate</c> challenges and the proxy's request (ADR-0239).
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
    /// them already sent <paramref name="sent" />, even an empty one, an empty answer to which
    /// sends nothing more, so the request is never sent again without a header twice
    /// (ADR-0232); and else afresh - as is a Digest answer whose nonce the challenges mark
    /// <c>stale=true</c>, which curl 8.21.0 answers again with the new nonce, without limit
    /// (measured, BL-1148 Notes).
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
            return sent is not null && !RenewsStaleDigest(sent, challenges)
                ? NullIfEmpty(await Authenticator.ContinueAuthorizationAsync(request, sent, !sentAnswersChallenge, challenges, cancellationToken).ConfigureAwait(false))
                : await Authenticator.CreateAuthorizationAsync(request, challenges, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpAuthenticationFailedException failure)
        {
            throw new HttpTransferException(failure.ExitCode, failure.Message);
        }
    }

    /// <summary>
    /// Decides whether <paramref name="challenges" /> mark the nonce of the Digest answer
    /// <paramref name="sent" /> stale, so it is answered afresh with the new nonce.
    /// </summary>
    private static bool RenewsStaleDigest(string sent, string[] challenges) =>
        sent.StartsWith("Digest ", StringComparison.Ordinal) && HttpDigestStaleChallenge.IsOfferedIn(challenges);

    /// <summary>Gives <paramref name="value" />, or <see langword="null" /> when it is empty.</summary>
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// Decides whether <paramref name="head" /> may be answered with a retry at all: a
    /// <paramref name="statusCode" /> response to a request whose body can be sent again: bytes,
    /// none, or a stream that can seek back to its start, as a <c>-T</c> file or a <c>-F</c> form
    /// of files can, which curl 8.21.0 rewinds and resends whole (upstream test1030, test259); not
    /// a stream that cannot, such as stdin (ADR-0034).
    /// </summary>
    private static bool MayRetry(HttpRequestPlan plan, HttpResponseHead head, int statusCode) =>
        head.StatusLine.StatusCode == statusCode && (plan.Framing.Body is not StreamBody stream || stream.Content.CanSeek);

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
            await upload.WriteHeadBeforeContinueAsync(requestBody, cancellationToken).ConfigureAwait(false);
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
        /// Gets the scheme a server picked for the exchange's request, the report's
        /// <see cref="TransferReport.AuthSchemePicked" /> (BL-1819).
        /// </summary>
        internal HttpAuthSchemes AuthSchemePicked { get; init; }

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
                HeaderSize = earlier?.HeaderSize ?? connect.ProxyConnectHeaderBytes,
                ConnectionCount = (earlier?.ConnectionCount ?? 0) + (newConnection ? 1 : 0),
                RedirectCount = RedirectCount,
                ResponseHeadersStored = body.HeadersStored,
                AuthSchemePicked = AuthSchemePicked,
                ProxyConnectResponseCode = connect.ProxyConnectResponseCode,
                UsedProxy = UsedProxy,
                LocalEndPoint = connect.LocalEndPoint ?? connection.LocalEndPoint as IPEndPoint,
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

        /// <summary>
        /// Gets the request as the authenticator is asked about it, carrying the connection's
        /// TLS server certificate once <see cref="TakeServerCertificateFrom" /> has run.
        /// </summary>
        public HttpAuthRequest AuthRequest { get; private set; } = AuthRequest;

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
        /// Gets the <c>-v</c> lines the authenticator reported while it made the
        /// <see cref="ProxyAuthorization" /> value before any challenge, written just before
        /// <c>Proxy auth using ...</c> each time that value is sent, as curl 8.21.0 writes a
        /// <c>--proxy-negotiate</c> context's failure there (measured, BL-604 Notes); empty for
        /// a value that answers a 407.
        /// </summary>
        public IReadOnlyList<string> ProxyAuthorizationInfoLines { get; init; } = [];

        /// <summary>
        /// Gets a value indicating whether the request is being sent again on a fresh connection
        /// because a pooled one died before its response, which happens at most once.
        /// </summary>
        public bool SentOnFreshConnection { get; init; }

        /// <summary>
        /// Gets how many times the request has been sent again on a new connection because the
        /// server refused its HTTP/3 stream (ADR-0187); curl gives up after
        /// <see cref="MaximumStreamRefusedRetries" />.
        /// </summary>
        public int StreamRefusedRetries { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this resend's <c>-T</c> upload cannot seek back to where
        /// the refused request began reading it, as stdin cannot, so the resend fails with exit 65
        /// before it connects, as curl 8.21.0's <c>cr_in_rewind</c> fails it (ADR-0279).
        /// </summary>
        public bool UploadCannotRewind { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether a connection was opened for this request, so a
        /// transfer that failed before one opened keeps its own message (BL-1684).
        /// </summary>
        public bool OpenedConnection { get; set; }

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
        /// Gets the <c>-v</c> lines the authenticator reported while it made
        /// <see cref="Authorization" />, written just before the request each time it is sent:
        /// those of the value made before any challenge (BL-843), or of a value answering a
        /// challenge to a request not sent with Negotiate picked (ADR-0232); empty for a value
        /// whose lines were written with the response's head (ADR-0231).
        /// </summary>
        public IReadOnlyList<string> AuthorizationInfoLines { get; init; } = [];

        /// <summary>
        /// Gets why the authenticator refused to make the value the transfer's first request is
        /// sent with, which fails the exchange once connected (<see cref="CreateFirstAuthorizationAsync" />);
        /// <see langword="null" /> when it did not refuse.
        /// </summary>
        public HttpTransferException? AuthorizationFailure { get; init; }

        /// <summary>
        /// Makes the same request sent with <paramref name="authorization" /> instead, in answer
        /// to a challenge; an empty value sends no header (ADR-0232).
        /// </summary>
        /// <param name="authorization">The <c>Authorization</c> value the retry is sent with.</param>
        /// <param name="infoLines">The lines the authenticator reported while it made the value that belong just before the retry.</param>
        /// <param name="keptProxyAuthorization">The <c>Proxy-Authorization</c> value the retry keeps, as sent again (BL-869).</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithAuthorization(string authorization, IReadOnlyList<string> infoLines, string? keptProxyAuthorization) =>
            WithAnswer(authorization, SentOnFreshConnection, RedirectsFollowed, authorizationAnswersChallenge: true, keptProxyAuthorization, ProxyAuthorizationAnswersChallenge, infoLines);

        /// <summary>
        /// Gets the framing the probe (<see cref="HttpRequestFraming.AsAuthProbe" />) held back, sent
        /// once a challenge is answered or the probe drew a 2xx; <see langword="null" /> when this
        /// request is no probe.
        /// </summary>
        public HttpRequestFraming? ProbedFraming { get; init; }

        /// <summary>
        /// Makes the same request with the body the probe held back and no credentials, which
        /// curl 8.21.0 sends when the probe drew a 2xx instead of a challenge (upstream test175):
        /// an NTLM Type 1 value the probe carried is not sent again (upstream test176, BL-2029).
        /// </summary>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan WithProbedBody() =>
            With(ProbedFraming!, WithoutNtlmType1(Authorization), SentOnFreshConnection, RedirectsFollowed, AuthorizationAnswersChallenge, WithoutNtlmType1(ProxyAuthorization), ProxyAuthorizationAnswersChallenge, endsProbe: true);

        private static string? WithoutNtlmType1(string? value) => IsNtlmType1(value) ? null : value;

        /// <summary>
        /// Makes the retry that answers a challenge the authenticator refused to answer: it
        /// writes <paramref name="infoLines" /> and then fails with <paramref name="failure" />
        /// before it is sent (<see cref="AuthorizationFailure" />, BL-848).
        /// </summary>
        /// <param name="failure">The refusal the retry fails with.</param>
        /// <param name="infoLines">The lines the authenticator reported while it refused, written just before the failure.</param>
        /// <param name="keptProxyAuthorization">The <c>Proxy-Authorization</c> value the retry keeps.</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithAuthorizationFailure(HttpTransferException failure, IReadOnlyList<string> infoLines, string? keptProxyAuthorization) =>
            With(Framing, authorization: null, SentOnFreshConnection, RedirectsFollowed, authorizationAnswersChallenge: true, keptProxyAuthorization, ProxyAuthorizationAnswersChallenge, infoLines, failure);

        /// <summary>
        /// Makes the same request sent with <paramref name="proxyAuthorization" /> instead, in
        /// answer to a 407's challenge, keeping its <c>Authorization</c> value.
        /// </summary>
        /// <param name="proxyAuthorization">The <c>Proxy-Authorization</c> value the retry is sent with.</param>
        /// <param name="keptAuthorization">The <c>Authorization</c> value the retry keeps, as sent again (BL-869).</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithProxyAuthorization(string proxyAuthorization, string? keptAuthorization) =>
            WithAnswer(keptAuthorization, SentOnFreshConnection, RedirectsFollowed, AuthorizationAnswersChallenge, proxyAuthorization, proxyAuthorizationAnswersChallenge: true);

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

        /// <summary>
        /// Makes the same request, sent again on a new connection because the server refused its
        /// HTTP/3 stream, with <see cref="StreamRefusedRetries" /> one higher.
        /// </summary>
        /// <param name="uploadCannotRewind">
        /// <see langword="true" /> when its <c>-T</c> upload cannot seek back to its start, so the
        /// resend fails before it connects (<see cref="UploadCannotRewind" />).
        /// </param>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan AfterStreamRefused(bool uploadCannotRewind)
        {
            HttpRequestPlan retry = With(Framing, Authorization);
            retry.StreamRefusedRetries = StreamRefusedRetries + 1;
            retry.UploadCannotRewind = uploadCannotRewind;
            return retry;
        }

        /// <summary>
        /// Sets <see cref="HttpAuthRequest.ServerCertificate" /> on <see cref="AuthRequest" /> to
        /// the DER of <paramref name="connect" />'s TLS server certificate, the first of its
        /// <see cref="ConnectResult.PeerCertificates" />, for an <c>https</c> URL, so hand-built
        /// Negotiate sends <c>tls-server-end-point</c> channel bindings as curl 8.18.0 with MIT
        /// does; empty for an <c>http</c> URL, even through an HTTPS proxy, and the proxy's
        /// request never carries one, as curl takes the bindings only from the origin's TLS
        /// (ADR-0341).
        /// </summary>
        /// <param name="connect">The connection the request goes over.</param>
        public void TakeServerCertificateFrom(ConnectResult connect) =>
            AuthRequest = AuthRequest with
            {
                ServerCertificate = Context.Url.Scheme == "https" && connect.PeerCertificates is [var certificate, ..] ? certificate : default,
            };

        /// <summary>
        /// Makes the retry that answers a challenge with <paramref name="authorization" /> and
        /// <paramref name="proxyAuthorization" />: with the body any probe held back, or, when
        /// either value is an NTLM Type 1 message, as a probe again holding the body back for
        /// the Type 3 message (<see cref="SendsNtlmProbe" />; upstream test243, BL-2029).
        /// </summary>
        private HttpRequestPlan WithAnswer(
            string? authorization,
            bool sentOnFreshConnection,
            int redirectsFollowed,
            bool authorizationAnswersChallenge,
            string? proxyAuthorization,
            bool proxyAuthorizationAnswersChallenge,
            IReadOnlyList<string>? authorizationInfoLines = null)
        {
            HttpRequestFraming full = ProbedFraming ?? Framing;
            bool probes = SendsNtlmProbe(full, authorization, proxyAuthorization);
            return With(
                probes ? full.AsAuthProbe() : full,
                authorization,
                sentOnFreshConnection,
                redirectsFollowed,
                authorizationAnswersChallenge,
                proxyAuthorization,
                proxyAuthorizationAnswersChallenge,
                authorizationInfoLines,
                endsProbe: true,
                nextProbedFraming: probes ? full : null);
        }

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
            bool proxyAuthorizationAnswersChallenge,
            IReadOnlyList<string>? authorizationInfoLines = null,
            HttpTransferException? authorizationFailure = null,
            bool endsProbe = false,
            HttpRequestFraming? nextProbedFraming = null) =>
            new(Context, Options, framing, AuthRequest, authorization)
            {
                ProbedFraming = endsProbe ? nextProbedFraming : ProbedFraming,
                Started = Started,
                Deadline = Deadline,
                Progress = Progress,
                ForwardProxy = ForwardProxy,
                ProxyAuthRequest = ProxyAuthRequest,
                ProxyAuthorization = proxyAuthorization,
                ProxyAuthorizationAnswersChallenge = proxyAuthorizationAnswersChallenge,
                ProxyAuthorizationInfoLines = proxyAuthorizationAnswersChallenge ? [] : ProxyAuthorizationInfoLines,
                SentOnFreshConnection = sentOnFreshConnection,
                RedirectsFollowed = redirectsFollowed,
                AuthorizationAnswersChallenge = authorizationAnswersChallenge,
                AuthorizationInfoLines = authorizationInfoLines ?? AuthorizationInfoLines,
                AuthorizationFailure = authorizationFailure ?? AuthorizationFailure,
                StreamRefusedRetries = StreamRefusedRetries,
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
        /// Gets the transfer's retries on a fresh connection so far, this one included, as
        /// <c>-v</c> counts them when <see cref="DiedBeforeResponse" />.
        /// </summary>
        public int RetryCount { get; init; } = 1;

        /// <summary>
        /// Gets a value indicating whether the connection is reported left intact although the
        /// server closed it to end the body (ADR-0324); it is marked reusable all the same, and
        /// the pool finds it dead (ADR-0112).
        /// </summary>
        public bool LeftIntactAfterServerClosed { get; init; }

        /// <summary>
        /// Gets the HTTP/2 session the exchange's connection switched to after an h2c upgrade's
        /// <c>101</c> (BL-866), or <see langword="null" /> when it did not switch.
        /// </summary>
        public Http2Session? UpgradedSession { get; init; }

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
