namespace Curl.Api.Options.Http;

/// <summary>
/// An easy handle's HTTP options (ADR-0506, plan row <c>EasyHttpOptions</c>): headers, user
/// agent and referer, encodings, redirects, the POST body and MIME form, the HTTP version,
/// cookies, Alt-Svc, HSTS and HTTP/2 stream options. Every default is libcurl 8.22.0's.
/// HTTP authentication is <c>EasyAuthenticationOptions</c>' and proxy headers are
/// <c>EasyProxyOptions</c>'.
/// </summary>
public sealed record EasyHttpOptions
{
    /// <summary>
    /// <c>CURLOPT_HTTPHEADER</c>: extra request headers, each <c>Name: value</c>; a
    /// <c>Name:</c> removes an internal header and <c>Name;</c> sends it empty. Default empty
    /// (libcurl's <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> HttpHeaders { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_USERAGENT</c>: the <c>User-Agent</c> header. Default
    /// <see langword="null"/>: none is sent.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// <c>CURLOPT_REFERER</c>: the <c>Referer</c> header. Default <see langword="null"/>:
    /// none is sent.
    /// </summary>
    public string? Referer { get; init; }

    /// <summary>
    /// <c>CURLOPT_AUTOREFERER</c>: whether a followed redirect sends the previous URL as
    /// <c>Referer</c>. Default <see langword="false"/> (0).
    /// </summary>
    public bool AutoReferer { get; init; }

    /// <summary>
    /// <c>CURLOPT_ACCEPT_ENCODING</c>: the <c>Accept-Encoding</c> header, and the content
    /// encodings decoded; an empty string lists every encoding built in. Default
    /// <see langword="null"/>: none is sent and nothing is decoded.
    /// </summary>
    public string? AcceptEncoding { get; init; }

    /// <summary>
    /// <c>CURLOPT_TRANSFER_ENCODING</c>: whether a compressed <c>Transfer-Encoding</c> is
    /// asked for (<c>TE: gzip</c>). Default <see langword="false"/> (0).
    /// </summary>
    public bool TransferEncoding { get; init; }

    /// <summary>
    /// <c>CURLOPT_HTTP_CONTENT_DECODING</c>: whether a response's content encoding is
    /// decoded. Default <see langword="true"/> (1).
    /// </summary>
    public bool HttpContentDecoding { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_HTTP_TRANSFER_DECODING</c>: whether a response's transfer encoding is
    /// decoded. Default <see langword="true"/> (1).
    /// </summary>
    public bool HttpTransferDecoding { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_FOLLOWLOCATION</c>: whether and how redirects are followed. Default
    /// <see cref="FollowLocation.None"/> (0).
    /// </summary>
    public FollowLocation FollowLocation { get; init; }

    /// <summary>
    /// <c>CURLOPT_MAXREDIRS</c>: the most redirects followed; -1 is unlimited. Default 30.
    /// </summary>
    public long MaxRedirects { get; init; } = 30;

    /// <summary>
    /// <c>CURLOPT_POSTREDIR</c>: the redirect codes after which a <c>POST</c> stays a
    /// <c>POST</c>. Default <see cref="PostRedirect.None"/> (<c>CURL_REDIR_GET_ALL</c>).
    /// </summary>
    public PostRedirect PostRedirect { get; init; }

    /// <summary>
    /// <c>CURLOPT_POST</c>: whether the request is a <c>POST</c> whose body comes from
    /// <see cref="PostFields"/> or the read callback. Default <see langword="false"/> (0).
    /// </summary>
    public bool Post { get; init; }

    /// <summary>
    /// <c>CURLOPT_POSTFIELDS</c> and <c>CURLOPT_COPYPOSTFIELDS</c>: the <c>POST</c> body's
    /// bytes; the record holds them, so the two options set the same property. Default
    /// <see langword="null"/>: no body given.
    /// </summary>
    public ReadOnlyMemory<byte>? PostFields { get; init; }

    /// <summary>
    /// <c>CURLOPT_POSTFIELDSIZE</c> and <c>CURLOPT_POSTFIELDSIZE_LARGE</c>: the <c>POST</c>
    /// body's length in bytes. Default -1: the length of <see cref="PostFields"/>.
    /// </summary>
    public long PostFieldSize { get; init; } = -1;

    /// <summary>
    /// <c>CURLOPT_HTTPGET</c>: whether the request is forced back to a <c>GET</c>. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool HttpGet { get; init; }

    /// <summary>
    /// <c>CURLOPT_MIMEPOST</c>: a multipart <c>POST</c> body built with <c>curl_mime_*</c>.
    /// Default <see langword="null"/>: none.
    /// </summary>
    public EasyMimeBody? MimePost { get; init; }

    /// <summary>
    /// <c>CURLOPT_MIME_OPTIONS</c>: how MIME bodies are built. Default
    /// <see cref="MimeOptions.None"/> (0).
    /// </summary>
    public MimeOptions MimeOptions { get; init; }

    /// <summary>
    /// <c>CURLOPT_HTTPPOST</c>: a multipart <c>POST</c> body built with the deprecated
    /// <c>curl_formadd</c>, which builds the same shape as <see cref="MimePost"/>. Default
    /// <see langword="null"/>: none.
    /// </summary>
    public EasyMimeBody? HttpPost { get; init; }

    /// <summary>
    /// <c>CURLOPT_HTTP_VERSION</c>: the HTTP version asked for. Default
    /// <see cref="HttpVersion.None"/> (<c>CURL_HTTP_VERSION_NONE</c>, since curl 8.13.0).
    /// </summary>
    public HttpVersion HttpVersion { get; init; }

    /// <summary>
    /// <c>CURLOPT_HTTP09_ALLOWED</c>: whether an HTTP/0.9 response is accepted. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool Http09Allowed { get; init; }

    /// <summary>
    /// <c>CURLOPT_IGNORE_CONTENT_LENGTH</c>: whether <c>Content-Length</c> is ignored and the
    /// body read until the connection closes. Default <see langword="false"/> (0).
    /// </summary>
    public bool IgnoreContentLength { get; init; }

    /// <summary>
    /// <c>CURLOPT_EXPECT_100_TIMEOUT_MS</c>: how long a request with
    /// <c>Expect: 100-continue</c> waits for the <c>100</c> before sending its body. Default
    /// 1000 milliseconds.
    /// </summary>
    public TimeSpan Expect100Timeout { get; init; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// <c>CURLOPT_HTTP200ALIASES</c>: status lines also treated as <c>HTTP/1.0 200 OK</c>.
    /// Default empty (libcurl's <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> Http200Aliases { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_REQUEST_TARGET</c>: the request target sent instead of the URL's path.
    /// Default <see langword="null"/>: the URL's path and query.
    /// </summary>
    public string? RequestTarget { get; init; }

    /// <summary>
    /// <c>CURLOPT_COOKIE</c>: the <c>Cookie</c> header's <c>name=value</c> pairs. Default
    /// <see langword="null"/>: none.
    /// </summary>
    public string? Cookie { get; init; }

    /// <summary>
    /// <c>CURLOPT_COOKIEFILE</c>: the files cookies are read from, one per setting; an empty
    /// name only enables the cookie engine. Default empty: the cookie engine is off.
    /// </summary>
    public IReadOnlyList<string> CookieFiles { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_COOKIEJAR</c>: the file every cookie is written to when the handle is
    /// cleaned up; <c>-</c> is standard output. Default <see langword="null"/>: none.
    /// </summary>
    public string? CookieJar { get; init; }

    /// <summary>
    /// <c>CURLOPT_COOKIESESSION</c>: whether session cookies loaded from
    /// <see cref="CookieFiles"/> are discarded, starting a new session. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool CookieSession { get; init; }

    /// <summary>
    /// <c>CURLOPT_COOKIELIST</c>: the cookie lines and commands (<c>ALL</c>, <c>SESS</c>,
    /// <c>FLUSH</c>, <c>RELOAD</c>) given, in the order given. Default empty.
    /// </summary>
    public IReadOnlyList<string> CookieList { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_ALTSVC</c>: the <c>Alt-Svc</c> cache file. Default <see langword="null"/>:
    /// none.
    /// </summary>
    public string? AltSvc { get; init; }

    /// <summary>
    /// <c>CURLOPT_ALTSVC_CTRL</c>: how <c>Alt-Svc</c> is handled. Default
    /// <see cref="AltSvcControl.None"/> (0): disabled.
    /// </summary>
    public AltSvcControl AltSvcControl { get; init; }

    /// <summary>
    /// <c>CURLOPT_HSTS</c>: the HSTS cache file. Default <see langword="null"/>: none.
    /// </summary>
    public string? Hsts { get; init; }

    /// <summary>
    /// <c>CURLOPT_HSTS_CTRL</c>: whether HSTS is used and its file written. Default
    /// <see cref="HstsControl.None"/> (0): disabled.
    /// </summary>
    public HstsControl HstsControl { get; init; }

    /// <summary>
    /// <c>CURLOPT_PIPEWAIT</c>: whether the transfer waits for a connection it can multiplex
    /// on rather than open a new one. Default <see langword="false"/> (0).
    /// </summary>
    public bool PipeWait { get; init; }

    /// <summary>
    /// <c>CURLOPT_STREAM_DEPENDS</c> and <c>CURLOPT_STREAM_DEPENDS_E</c>: the HTTP/2 stream
    /// this one depends on. Default <see langword="null"/>: none.
    /// </summary>
    public EasyStreamDependency? StreamDependency { get; init; }

    /// <summary>
    /// <c>CURLOPT_STREAM_WEIGHT</c>: the HTTP/2 stream's weight, 1 to 256. Default 16.
    /// </summary>
    public int StreamWeight { get; init; } = 16;
}
