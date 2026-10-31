using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The HTTP-only options of one transfer, filled by the command-line layer and read only
/// by an HTTP handler (ADR-0014).
/// </summary>
/// <remarks>
/// <see cref="ITransferContext.Http" /> being <see langword="null" /> means no HTTP option
/// was given, and an HTTP handler treats it exactly as <c>new HttpRequestOptions()</c>,
/// every member at its default. <c>-u</c>/<c>--user</c> stays on
/// <see cref="ITransferContext.Credentials" /> and <c>-T</c>/<c>--upload-file</c> on
/// <see cref="ITransferContext.Upload" />; neither is repeated here.
/// </remarks>
public sealed record HttpRequestOptions
{
    /// <summary>
    /// Gets the method from <c>-X</c>/<c>--request</c>, verbatim, or
    /// <see langword="null" /> to let the handler choose GET, HEAD, POST or PUT. The FTP
    /// handler sends it in place of <c>LIST</c> or <c>NLST</c> for a directory listing
    /// (BL-3165).
    /// </summary>
    public string? CustomMethod { get; init; }

    /// <summary>
    /// Gets each <c>-H</c>/<c>--header</c> value verbatim, in command-line order; empty
    /// when none was given.
    /// </summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>
    /// Gets each <c>--proxy-header</c> value verbatim, in command-line order; empty when
    /// none was given. A request sent to a forward proxy carries them after the
    /// <c>-H</c> values; a request sent to the origin, directly or through a tunnel, never
    /// does.
    /// </summary>
    public IReadOnlyList<string> ProxyHeaders { get; init; } = [];

    /// <summary>
    /// Gets the encoding that turns command-line text in the request head - the
    /// <see cref="Headers" />, <see cref="ProxyHeaders" />, <see cref="UserAgent" /> and
    /// <see cref="Referer" /> values - into bytes, as the platform's curl receives its
    /// arguments (ADR-0067): the system ANSI code page with best fit on Windows, UTF-8
    /// elsewhere. <see cref="Encoding.Latin1" />, one byte per character, when not set.
    /// </summary>
    public Encoding CommandLineTextEncoding { get; init; } = Encoding.Latin1;

    /// <summary>
    /// Gets the value from <c>-A</c>/<c>--user-agent</c>: <see langword="null" /> sends
    /// curl's own <c>User-Agent</c>, and the empty string sends none.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Gets the referring URL from <c>-e</c>/<c>--referer</c>, or <see langword="null" />
    /// to send no <c>Referer</c> header.
    /// </summary>
    public string? Referer { get; init; }

    /// <summary>
    /// Gets the cookie strings from <c>-b name=value</c>, joined as curl joins them, or
    /// <see langword="null" /> when none was given.
    /// </summary>
    public string? CookieStrings { get; init; }

    /// <summary>
    /// Gets a value indicating whether a <c>-b</c> cookie file was given, whose cookies the HTTP
    /// handler's cookie store sends.
    /// </summary>
    public bool HasCookieFiles { get; init; }

    /// <summary>
    /// Gets a value indicating whether a followed redirect sends the URL it came from as its
    /// <c>Referer</c>, without user information or fragment, as <c>-e "...;auto"</c> asks
    /// curl 8.21.0 to (BL-361 Notes).
    /// </summary>
    public bool AutoReferer { get; init; }

    /// <summary>
    /// Gets a value indicating whether this request is a followed redirect to a host other
    /// than the first URL's, which sends the URL's own <c>Host</c> and matches cookies against
    /// it, leaving out any <c>-H</c> <c>Host</c> value, as curl 8.21.0's <c>http_host</c> does
    /// (upstream test184, BL-1845).
    /// </summary>
    public bool FollowedToAnotherHost { get; init; }

    /// <summary>
    /// Gets a value indicating whether an <c>-H</c> value naming <c>Authorization</c> was left out
    /// of this followed request because it goes to another host, port or scheme without
    /// <c>--location-trusted</c>. curl 8.22.0's <c>output_auth_headers</c> still sees that value,
    /// so it generates no Basic, Bearer or <c>--aws-sigv4</c> value from the Location's user
    /// information either, and the request carries no <c>Authorization</c> (CGR-076, BL-3605).
    /// </summary>
    public bool AuthorizationHeaderWithheld { get; init; }

    /// <summary>
    /// Gets a value indicating whether this request sends the <c>-b name=value</c> strings
    /// beside the stored cookies: <see langword="true" /> unless it is a followed redirect to
    /// another scheme, host or port without <c>--location-trusted</c>, where curl 8.21.0's
    /// <c>Curl_auth_allowed_to_host</c> leaves them out (upstream test2015, BL-1846).
    /// </summary>
    public bool SendsCookieStrings { get; init; } = true;

    /// <summary>
    /// Gets the request body from the <c>-d</c> and <c>-F</c> families, already encoded,
    /// or <see langword="null" /> to send none.
    /// </summary>
    public HttpRequestBody? Body { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>-L</c>/<c>--location</c> was given, so the
    /// handler drains a 3xx body instead of writing it.
    /// </summary>
    public bool FollowRedirects { get; init; }

    /// <summary>
    /// Gets the <c>--max-redirs</c> limit: 50 when not given, as in curl 8.21.0, and <c>-1</c>
    /// for no limit. curl counts each resend after a <c>417 Expectation Failed</c> as a
    /// followed redirect, with or without <c>-L</c>, so the handler ends a resend that would
    /// pass it with exit 47 (measured, BL-396 Notes).
    /// </summary>
    public int MaxRedirects { get; init; } = 50;

    /// <summary>
    /// Gets how many redirects the transfer followed before this request: 0 for the first,
    /// and the count so far for a hop <c>-L</c> follows, whose resends count against the same
    /// <see cref="MaxRedirects" /> (BL-396 Notes).
    /// </summary>
    public int RedirectsFollowed { get; init; }

    /// <summary>
    /// Gets how many response headers the transfer stored before this request: 0 for the
    /// first, and the earlier hops' <see cref="TransferReport.ResponseHeadersStored" /> for a
    /// hop <c>-L</c> follows, since curl 8.21.0 counts every header of a transfer - its hops'
    /// heads and trailers - toward its limit of 5000 (measured, BL-1448 Notes).
    /// </summary>
    public int ResponseHeadersStored { get; init; }

    /// <summary>
    /// Gets the scheme a server picked for an earlier hop of the transfer, the earlier hop's
    /// <see cref="TransferReport.AuthSchemePicked" />, or <see cref="HttpAuthSchemes.None" /> for
    /// the first request. When it is one of <see cref="AuthSchemes" />, the hop sends it as
    /// though it were the one scheme allowed, as libcurl 8.21.0 keeps its picked scheme across
    /// the redirects it follows: <c>--anyauth --location-trusted</c> answers a Basic challenge,
    /// then sends Basic to the redirect's new host before any challenge (upstream test1088,
    /// BL-1819).
    /// </summary>
    public HttpAuthSchemes AuthSchemePicked { get; init; }

    /// <summary>
    /// Gets the Digest <c>Authorization</c> value an earlier hop of the transfer last sent, the
    /// earlier hop's <see cref="TransferReport.DigestAuthorizationSent" />, or
    /// <see langword="null" /> for none. With credentials and Digest allowed, the hop sends it
    /// again before any challenge, counted on by
    /// <see cref="IHttpAuthenticator.RepeatAuthorization" /> for this hop's request: the same
    /// nonce and <c>cnonce</c> with the next <c>nc</c>, as curl 8.22.0 keeps its Digest state
    /// across the redirects it follows (upstream test1286, BL-2252).
    /// </summary>
    public string? KeptDigestAuthorization { get; init; }

    /// <summary>
    /// Gets the <c>WWW-Authenticate</c> values of the 401 that ended the easy handle's previous
    /// transfer, or empty for none. When Digest is the one scheme <see cref="AuthSchemes" />
    /// allows and there are credentials, the first request answers them before any new
    /// challenge, as curl 8.22.0 keeps the Digest nonce on the easy handle across transfers, even
    /// one whose <c>CURLOPT_HTTPAUTH</c> did not allow Digest (upstream test2024, BL-2747, ADR-0692).
    /// </summary>
    public IReadOnlyList<string> KeptDigestChallenges { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the easy handle's previous transfer ended with Digest still
    /// marked available, its <see cref="TransferReport.DigestStaysAvailable" />, as libcurl 8.22.0
    /// keeps <c>authhost.avail</c> on the easy handle (upstream test2027, BL-2820, for a Digest challenge
    /// and test2030, BL-2823, for an NTLM one; ADR-0709);
    /// <see langword="false" /> for the handle's first transfer and after <c>curl_easy_reset</c>.
    /// </summary>
    public bool DigestStaysAvailable { get; init; }

    /// <summary>
    /// Gets a value indicating whether a response of an earlier hop of the transfer was
    /// HTTP/1.0, which sends this request as HTTP/1.0, as for <c>-0</c>, since curl 8.21.0 sends
    /// every later request of a transfer as HTTP/1.0 once a response in it was: a <c>-T -</c>
    /// upload <c>-L</c> follows from an HTTP/1.0 3xx fails with exit 25 before the hop is sent
    /// (upstream test1073, BL-2037).
    /// </summary>
    public bool EarlierResponseWasHttp10 { get; init; }

    /// <summary>
    /// Gets how a status of 400 or above ends the transfer; <see cref="HttpFailMode.None" />
    /// when neither <c>-f</c> nor <c>--fail-with-body</c> was given.
    /// </summary>
    public HttpFailMode Fail { get; init; }

    /// <summary>
    /// Gets the HTTP version the request line asks for;
    /// <see cref="HttpVersionPreference.Http11" /> by default.
    /// </summary>
    public HttpVersionPreference Version { get; init; }

    /// <summary>
    /// Gets the happy-eyeballs timeout from <c>--happy-eyeballs-timeout-ms</c>: how long a
    /// <c>--http3</c> transfer waits for the QUIC handshake before it also starts a TCP
    /// connect (ADR-0144 section 4); curl's default of 200 milliseconds when not given.
    /// </summary>
    public TimeSpan HappyEyeballsTimeout { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// How long curl 8.21.0 waits for <c>100 Continue</c> before it sends a request body
    /// anyway when <c>--expect100-timeout</c> is not given, or is given as zero (measured,
    /// BL-624 Notes).
    /// </summary>
    public static readonly TimeSpan DefaultContinueWait = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets how long a request that carries <c>Expect: 100-continue</c> waits for
    /// <c>100 Continue</c> before its body is sent anyway (<c>--expect100-timeout</c>);
    /// <see cref="DefaultContinueWait" /> when not given.
    /// </summary>
    public TimeSpan ContinueWait { get; init; } = DefaultContinueWait;

    /// <summary>
    /// Gets a value indicating whether <c>--compressed</c> was given: ask for and decode a
    /// compressed body.
    /// </summary>
    public bool Compressed { get; init; }

    /// <summary>
    /// The <c>Accept-Encoding</c> value curl 8.22.0 sends for an empty <c>CURLOPT_ACCEPT_ENCODING</c> and
    /// <c>--compressed</c>: every encoding it decodes, the same four on the Schannel and OpenSSL builds (ADR-0287).
    /// </summary>
    public const string DefaultAcceptEncoding = "deflate, gzip, br, zstd";

    /// <summary>
    /// Gets the <c>Accept-Encoding</c> value sent when <see cref="Compressed" /> is set: the
    /// <c>CURLOPT_ACCEPT_ENCODING</c> string exactly as given, or <see cref="DefaultAcceptEncoding" />
    /// (the default) for an empty one (ADR-0777).
    /// </summary>
    public string AcceptEncoding { get; init; } = DefaultAcceptEncoding;

    /// <summary>
    /// Gets a value indicating whether <c>--tr-encoding</c> was given: send <c>TE: gzip</c>
    /// and decode a compressed Transfer-Encoding.
    /// </summary>
    public bool TransferEncoding { get; init; }

    /// <summary>
    /// Gets a value indicating whether Content-Encoding is passed through undecoded, even with
    /// <see cref="Compressed" />: <c>CURLOPT_HTTP_CONTENT_DECODING</c> 0, and half of <c>--raw</c>.
    /// </summary>
    public bool PassesContentCoding { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transfer codings, <c>chunked</c> included, are passed
    /// through undecoded: <c>CURLOPT_HTTP_TRANSFER_DECODING</c> 0, and half of <c>--raw</c>.
    /// </summary>
    public bool PassesTransferCoding { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--ignore-content-length</c> was given: read the
    /// body to close regardless of <c>Content-Length</c>.
    /// </summary>
    public bool IgnoreContentLength { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--http0.9</c> was given: accept an HTTP/0.9
    /// response, one with no status line or headers, as a body read to close instead of
    /// failing with exit 1.
    /// </summary>
    public bool AllowHttp09Reply { get; init; }

    /// <summary>
    /// Gets libcurl's <c>CURLOPT_HTTP200ALIASES</c>: status lines taken as <c>HTTP/1.0 200</c>
    /// when a response's first line begins with one, in any letter case, such as <c>ICY 200 OK</c>.
    /// Empty by default.
    /// </summary>
    public IReadOnlyList<string> Http200Aliases { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether libcurl's <c>CURLOPT_KEEP_SENDING_ON_ERROR</c> was set:
    /// keep sending the request body after the server has answered with an error status,
    /// instead of stopping the upload. <see langword="false" /> by default.
    /// </summary>
    public bool KeepSendingOnError { get; init; }

    /// <summary>
    /// Gets the request line's target from <c>--request-target</c>, verbatim, or
    /// <see langword="null" /> to derive it from <see cref="ITransferContext.Url" />.
    /// </summary>
    public string? RequestTarget { get; init; }

    /// <summary>
    /// Gets the schemes the authenticator may answer the origin with;
    /// <see cref="HttpAuthSchemes.Basic" />, curl's default, when no scheme option was
    /// given.
    /// </summary>
    public HttpAuthSchemes AuthSchemes { get; init; } = HttpAuthSchemes.Basic;

    /// <summary>
    /// Gets the token from <c>--oauth2-bearer</c>, or <see langword="null" /> when not
    /// given.
    /// </summary>
    public string? BearerToken { get; init; }

    /// <summary>
    /// Gets the <c>--aws-sigv4</c> value, verbatim, or <see langword="null" /> when not given.
    /// When given, every request to the origin is signed with AWS Signature Version 4 instead
    /// of any <see cref="AuthSchemes" /> (<see cref="HttpAuthRequest.AwsSigV4" />).
    /// </summary>
    public string? AwsSigV4 { get; init; }

    /// <summary>
    /// Gets the <c>--httpsig-*</c> settings, or <see langword="null" /> when none of those options
    /// is given. When given, every request to the origin is signed with an HTTP message
    /// signature instead of any <see cref="AuthSchemes" /> (<see cref="HttpAuthRequest.HttpSignature" />),
    /// and a followed redirect to another origin drops them unless <c>--location-trusted</c> (BL-2182).
    /// </summary>
    public HttpSignatureSettings? HttpSignature { get; init; }

    /// <summary>
    /// Gets the proxy already chosen for this URL, or <see langword="null" /> to connect
    /// directly.
    /// </summary>
    public ProxyEndpoint? ForwardProxy { get; init; }

    /// <summary>
    /// Gets the transfer type <c>CURLOPT_PROXY_TRANSFER_MODE</c> asks for, <c>a</c> under
    /// <c>CURLOPT_TRANSFERTEXT</c> or <c>i</c>, or <see langword="null" /> when the option is not set or the
    /// transfer is not forwarded through an HTTP or HTTP/1.0 proxy. The absolute-form target an <c>ftp</c> URL
    /// is sent to the proxy puts <c>;type=</c> and it on the path unless the path already names its type,
    /// leaving the URL itself, and so the effective URL and the redirects resolved against it, without it,
    /// as curl 8.22.0's <c>http_target</c> does (upstream tests 549 and 550, BL-2793).
    /// </summary>
    public char? ProxyTransferType { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>-p</c>/<c>--proxytunnel</c> was given: tunnel
    /// through an HTTP-kind proxy with CONNECT even for an <c>http://</c> URL.
    /// </summary>
    public bool ProxyTunnel { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transfer goes over a Unix domain socket
    /// (<c>--unix-socket</c> or <c>--abstract-unix-socket</c>), which rules out HTTP/3 before
    /// connecting, as curl 8.21.0's <c>Curl_conn_may_http3</c> does.
    /// </summary>
    public bool OverUnixSocket { get; init; }

    /// <summary>
    /// Gets the alternative service this transfer connects to in place of its origin
    /// (<c>--alt-svc</c>), or <see langword="null" />, the default, to connect to the origin.
    /// </summary>
    /// <remarks>
    /// The handler puts it on the <see cref="ConnectTarget" /> for the connector to dial and
    /// sends <c>Alt-Used: &lt;host&gt;:&lt;port&gt;</c> naming the alternative, unless an
    /// <c>-H</c> value names <c>Alt-Used</c>.
    /// </remarks>
    public AltSvcRoute? AltSvcRoute { get; init; }

    /// <summary>
    /// Gets a value indicating whether a <see cref="HttpVersionPreference.Http3" /> transfer starts its
    /// TCP connect first and its QUIC connect once the TCP one fails or the
    /// <see cref="HappyEyeballsTimeout" /> passes, rather than the other way round;
    /// <see langword="false" />, the default, to start with QUIC.
    /// </summary>
    /// <remarks>
    /// Set for an <c>--alt-svc</c> entry that names the origin itself with <c>h2</c> or <c>h1</c>, which
    /// curl 8.21.0 makes the preferred first attempt (<c>cf_hc_get_pref_alpn</c>, BL-948). Any other
    /// version ignores it. It is <see langword="true" /> exactly when <see cref="TcpFirstAttemptVersion" /> is set.
    /// </remarks>
    public bool TriesTcpBeforeQuic => TcpFirstAttemptVersion is not null;

    /// <summary>
    /// Gets the HTTP version, <c>h2</c> or <c>h1</c>, of the <c>--alt-svc</c> entry naming the origin
    /// itself that makes TCP the first attempt of a <see cref="HttpVersionPreference.Http3" /> transfer
    /// (<see cref="TriesTcpBeforeQuic" />), or <see langword="null" />, the default, to start with QUIC.
    /// </summary>
    /// <remarks>
    /// The handler puts it on the <see cref="ConnectTarget" /> of the race, where the connector names it in
    /// curl 8.22.0's <c>[HTTPS-CONNECT] 1st attempt uses &lt;version&gt; from preferred version</c> line
    /// (measured, BL-1320 Notes).
    /// </remarks>
    public string? TcpFirstAttemptVersion { get; init; }

    /// <summary>
    /// Gets the store each <c>Alt-Svc</c> header of an HTTPS response is handed to
    /// (<c>--alt-svc</c>), or <see langword="null" />, the default, to learn none.
    /// </summary>
    public IAltSvcStore? AltSvcStore { get; init; }

    /// <summary>
    /// Gets the store each <c>Strict-Transport-Security</c> response header is handed to as it is
    /// read (<c>--hsts</c>, ADR-0409), or <see langword="null" />, the default, to learn none. The
    /// store decides whether a plain <c>http</c> origin's header is learned (ADR-0494).
    /// </summary>
    public IHstsStore? HstsStore { get; init; }

    /// <summary>
    /// Gets the most bytes a second a body of bytes (<c>-d</c>, <c>--data*</c>, <c>--json</c>) is
    /// sent at (<c>--limit-rate</c>, libcurl's <c>CURLOPT_MAX_SEND_SPEED_LARGE</c>), or
    /// <see langword="null" />, the default, to send it at once.
    /// </summary>
    /// <remarks>
    /// A stream body (<c>-T</c>, <c>-F</c>) is held to the rate by the stream the caller hands over,
    /// so the handler limits only a body of bytes (BL-2957).
    /// </remarks>
    public long? SendBytesPerSecond { get; init; }

    /// <summary>
    /// Gets the most bytes a second a response is received at (<c>--limit-rate</c>, libcurl's
    /// <c>CURLOPT_MAX_RECV_SPEED_LARGE</c>), or <see langword="null" />, the default, to receive it
    /// as fast as it arrives.
    /// </summary>
    /// <remarks>
    /// Carried so an HTTP/2 session can size its stream receive window from the rate (BL-4045, BL-3810).
    /// </remarks>
    public long? ReceiveBytesPerSecond { get; init; }
}
