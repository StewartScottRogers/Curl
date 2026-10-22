using Curl.Api.Options.Http;

namespace Curl.Api;

/// <summary>
/// HTTP settings, mapped onto <see cref="EasyHttpOptions"/> (BL-2503, ADR-0640). Each member starts at
/// libcurl 8.22.0's default, so <c>new CurlHttpOptions()</c> changes nothing. Set on
/// <see cref="CurlClientOptions.Http"/> for every request or on <see cref="CurlRequest.Http"/> for one;
/// a request's record replaces the client's whole. The request's own headers, method and body stay
/// <see cref="CurlRequest"/>'s, so the record has no <c>CURLOPT_HTTPHEADER</c>, <c>CURLOPT_POST</c>,
/// <c>CURLOPT_POSTFIELDS</c>, <c>CURLOPT_HTTPGET</c> or MIME member. An invalid value throws as it is set.
/// </summary>
public sealed record CurlHttpOptions
{
    /// <summary><c>CURLOPT_USERAGENT</c>. Default <see langword="null"/>: keep the client's <see cref="CurlClientOptions.UserAgent"/>.</summary>
    public string? UserAgent { get; init; }

    /// <summary><c>CURLOPT_REFERER</c>: the <c>Referer</c> header. Default <see langword="null"/>: none.</summary>
    public string? Referer { get; init; }

    /// <summary><c>CURLOPT_AUTOREFERER</c>: a followed redirect sends the previous URL as <c>Referer</c>. Default <see langword="false"/>.</summary>
    public bool AutoReferer { get; init; }

    /// <summary><c>CURLOPT_ACCEPT_ENCODING</c>: the encodings asked for and decoded; empty lists every one built in. Default <see langword="null"/>: none.</summary>
    public string? AcceptEncoding { get; init; }

    /// <summary><c>CURLOPT_TRANSFER_ENCODING</c>: ask for a compressed transfer encoding. Default <see langword="false"/>.</summary>
    public bool TransferEncoding { get; init; }

    /// <summary><c>CURLOPT_HTTP_CONTENT_DECODING</c>: decode the content encoding. Default <see langword="true"/>.</summary>
    public bool HttpContentDecoding { get; init; } = true;

    /// <summary><c>CURLOPT_HTTP_TRANSFER_DECODING</c>: decode the transfer encoding. Default <see langword="true"/>.</summary>
    public bool HttpTransferDecoding { get; init; } = true;

    /// <summary><c>CURLOPT_FOLLOWLOCATION</c>: whether and how redirects are followed. Default <see cref="FollowLocation.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="Options.Http.FollowLocation"/> value.</exception>
    public FollowLocation FollowLocation
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(FollowLocation));
    }

    /// <summary><c>CURLOPT_MAXREDIRS</c>: the most redirects followed, -1 for no limit. Default 30.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is below -1.</exception>
    public long MaxRedirects
    {
        get;
        init => field = TransportOptionGuard.InRange(value, -1, long.MaxValue, nameof(MaxRedirects));
    } = 30;

    /// <summary><c>CURLOPT_POSTREDIR</c>: the redirect codes after which a <c>POST</c> stays a <c>POST</c>. Default <see cref="PostRedirect.None"/>.</summary>
    public PostRedirect PostRedirect { get; init; }

    /// <summary><c>CURLOPT_HTTP_VERSION</c>: the HTTP version asked for. Default <see cref="HttpVersion.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="Options.Http.HttpVersion"/> value.</exception>
    public HttpVersion HttpVersion
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(HttpVersion));
    }

    /// <summary><c>CURLOPT_HTTP09_ALLOWED</c>: accept an HTTP/0.9 response. Default <see langword="false"/>.</summary>
    public bool Http09Allowed { get; init; }

    /// <summary><c>CURLOPT_IGNORE_CONTENT_LENGTH</c>: read the body until the connection closes. Default <see langword="false"/>.</summary>
    public bool IgnoreContentLength { get; init; }

    /// <summary><c>CURLOPT_EXPECT_100_TIMEOUT_MS</c>: how long to wait for a <c>100 Continue</c>. Default 1 second.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Expect100Timeout
    {
        get;
        init => field = TransportOptionGuard.NonNegative(value, nameof(Expect100Timeout));
    } = TimeSpan.FromSeconds(1);

    /// <summary><c>CURLOPT_HTTP200ALIASES</c>: status lines also treated as <c>HTTP/1.0 200 OK</c>. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> Http200Aliases
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(Http200Aliases));
    } = [];

    /// <summary><c>CURLOPT_REQUEST_TARGET</c>: the request target sent instead of the URL's path. Default <see langword="null"/>.</summary>
    public string? RequestTarget { get; init; }

    /// <summary><c>CURLOPT_COOKIE</c>: the <c>Cookie</c> header's <c>name=value</c> pairs. Default <see langword="null"/>.</summary>
    public string? Cookie { get; init; }

    /// <summary><c>CURLOPT_COOKIEFILE</c>: the files cookies are read from; an empty name only starts the cookie engine. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> CookieFiles
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(CookieFiles));
    } = [];

    /// <summary><c>CURLOPT_COOKIEJAR</c>: the file every cookie is written to. Default <see langword="null"/>.</summary>
    public string? CookieJar { get; init; }

    /// <summary><c>CURLOPT_COOKIESESSION</c>: discard the session cookies loaded. Default <see langword="false"/>.</summary>
    public bool CookieSession { get; init; }

    /// <summary><c>CURLOPT_COOKIELIST</c>: cookie lines and commands, in order. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> CookieList
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(CookieList));
    } = [];

    /// <summary><c>CURLOPT_ALTSVC</c>: the <c>Alt-Svc</c> cache file. Default <see langword="null"/>.</summary>
    public string? AltSvc { get; init; }

    /// <summary><c>CURLOPT_ALTSVC_CTRL</c>: how <c>Alt-Svc</c> is handled. Default <see cref="AltSvcControl.None"/>.</summary>
    public AltSvcControl AltSvcControl { get; init; }

    /// <summary><c>CURLOPT_HSTS</c>: the HSTS cache file. Default <see langword="null"/>.</summary>
    public string? Hsts { get; init; }

    /// <summary><c>CURLOPT_HSTS_CTRL</c>: whether HSTS is used. Default <see cref="HstsControl.None"/>.</summary>
    public HstsControl HstsControl { get; init; }

    /// <summary><c>CURLOPT_PIPEWAIT</c>: wait for a connection to multiplex on. Default <see langword="false"/>.</summary>
    public bool PipeWait { get; init; }

    /// <summary><c>CURLOPT_STREAM_WEIGHT</c>: the HTTP/2 stream's weight, 1 to 256. Default 16.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 1 to 256.</exception>
    public int StreamWeight
    {
        get;
        init => field = (int)TransportOptionGuard.InRange(value, 1, 256, nameof(StreamWeight));
    } = 16;

    /// <summary><c>CURLOPT_SSL_ENABLE_ALPN</c>: offer the HTTP versions through ALPN. Default <see langword="true"/>.</summary>
    public bool SslEnableAlpn { get; init; } = true;

    /// <summary><paramref name="http"/> with every member of this record set, its other members kept; <see cref="UserAgent"/> keeps the handle's value when this record has none, so the client's <see cref="CurlClientOptions.UserAgent"/> survives.</summary>
    /// <param name="http">The handle's HTTP options so far.</param>
    /// <returns>The seam's HTTP options.</returns>
    internal EasyHttpOptions ApplyTo(EasyHttpOptions http) => http with
    {
        UserAgent = UserAgent ?? http.UserAgent,
        Referer = Referer,
        AutoReferer = AutoReferer,
        AcceptEncoding = AcceptEncoding,
        TransferEncoding = TransferEncoding,
        HttpContentDecoding = HttpContentDecoding,
        HttpTransferDecoding = HttpTransferDecoding,
        FollowLocation = FollowLocation,
        MaxRedirects = MaxRedirects,
        PostRedirect = PostRedirect,
        HttpVersion = HttpVersion,
        Http09Allowed = Http09Allowed,
        IgnoreContentLength = IgnoreContentLength,
        Expect100Timeout = Expect100Timeout,
        Http200Aliases = Http200Aliases,
        RequestTarget = RequestTarget,
        Cookie = Cookie,
        CookieFiles = CookieFiles,
        CookieJar = CookieJar,
        CookieSession = CookieSession,
        CookieList = CookieList,
        AltSvc = AltSvc,
        AltSvcControl = AltSvcControl,
        Hsts = Hsts,
        HstsControl = HstsControl,
        PipeWait = PipeWait,
        StreamWeight = StreamWeight,
        SslEnableAlpn = SslEnableAlpn,
    };
}
