using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats the head of an HTTP/1.1 or HTTP/1.0 request - the request line, the headers and the empty
/// line after them - byte for byte as curl 8.21.0 sends it for <c>-X</c>, <c>-H</c>,
/// <c>-A</c>, <c>-e</c>, <c>-I</c>, <c>--compressed</c>, <c>--tr-encoding</c>, an <c>Authorization</c> value, a
/// <c>Cookie</c> value, a request body, a forward proxy, <c>--proxy-header</c>, <c>-r</c>, <c>-C</c> and <c>-z</c>. Every
/// rule was measured (BL-172, BL-175, BL-177, BL-178, BL-180, BL-181, BL-182, BL-183, BL-296, BL-306, BL-315 and BL-332 Notes).
/// </summary>
/// <remarks>
/// The request line ends in <c>HTTP/1.0</c> for <c>-0</c> and in <c>HTTP/1.1</c> otherwise;
/// the headers are the same for both. curl's own headers come first, in the order <c>Host</c>, <c>Proxy-Authorization</c>,
/// <c>Authorization</c>, <c>Range</c>, <c>Content-Range</c> (for a <c>-T</c> upload resumed with
/// <c>-C</c>, or <c>-r</c> on a <c>-d</c> body or a <c>-T</c> upload, <see cref="HttpRequestFraming.ContentRange" />), <c>User-Agent</c>, <c>Accept</c>, <c>TE: gzip</c> (for
/// <c>--tr-encoding</c>), <c>Accept-Encoding</c> (for <c>--compressed</c>), <c>Referer</c>, <c>Proxy-Connection: Keep-Alive</c> (through a forward
/// proxy), <c>Alt-Used</c> (to an alternative service, <see cref="HttpRequestOptions.AltSvcRoute" />),
/// each left out when an <c>-H</c> value names it, then the cookie store's
/// <c>Cookie</c>, then <c>If-Modified-Since</c> or <c>If-Unmodified-Since</c> for <c>-z</c>, also
/// left out when an <c>-H</c> value names it; <c>Cookie</c> and <c>Proxy-Authorization</c> are sent even when an <c>-H</c>
/// value names them. Through a forward proxy the request target is the absolute form,
/// <c>http://host[:port]/path?query</c>, with no user information or fragment. The <c>-H</c> values follow in
/// command-line order, then, through a forward proxy only, the <c>--proxy-header</c> values under the
/// same rules; of curl's own headers they override only <c>Proxy-Connection</c> (BL-296 Notes). A custom <c>Host</c> is the exception: it takes the <c>Host</c> slot. A request with a body ends with <c>Content-Length</c> (or
/// <c>Transfer-Encoding: chunked</c> when the length is unknown), <c>Content-Type</c> and
/// <c>Expect: 100-continue</c> as <see cref="HttpRequestFraming" /> decides, each again left
/// out when an <c>-H</c> value names it. The <c>Connection</c> lines come last of all
/// (<see cref="AppendConnection" />), with <c>TE</c> added for <c>--tr-encoding</c>. The <c>-H</c>, <c>--proxy-header</c>, <c>-A</c> and
/// <c>-e</c> text is sent as its bytes in <see cref="HttpRequestOptions.CommandLineTextEncoding" />,
/// the platform curl's argument encoding (ADR-0067); with none set that is Latin-1, where a
/// character above U+00FF takes Latin-1's best fit, such as <c>A</c> for U+0100, or else <c>?</c>.
/// </remarks>
internal static class HttpRequestHeadFormatter
{
    /// <summary>
    /// The <c>User-Agent</c> curl 8.21.0 sends when <c>-A</c> is not given.
    /// </summary>
    internal const string DefaultUserAgent = "curl/8.21.0";

    /// <summary>
    /// The <c>Accept-Encoding</c> value <c>--compressed</c> sends: curl 8.21.0's, the same
    /// four tokens on the Schannel and OpenSSL builds (measured, BL-861 Notes; ADR-0287).
    /// </summary>
    internal const string AcceptEncoding = "deflate, gzip, br, zstd";

    private const string HostName = "Host";

    private const string ConnectionName = "Connection";

    private const string ContentTypeName = "Content-Type";

    private const string ContentLengthName = "Content-Length";

    private const string FormBoundaryPrefix = "multipart/form-data; boundary=";

    /// <summary>
    /// Formats the request head for <paramref name="url" />.
    /// </summary>
    /// <param name="url">The URL requested; its path and query form the request target.</param>
    /// <param name="options">
    /// The HTTP options, or <see langword="null" /> for every option at its default.
    /// </param>
    /// <param name="noBody">
    /// <see langword="true" /> for <c>-I</c>/<c>--head</c>, which sends HEAD unless
    /// <c>-X</c> names another method.
    /// </param>
    /// <param name="authorization">
    /// The <c>Authorization</c> value the authenticator gave, or <see langword="null" /> to
    /// send none.
    /// </param>
    /// <param name="cookie">
    /// The <c>Cookie</c> value the cookie store gave, or <see langword="null" /> to send none.
    /// </param>
    /// <param name="forwardProxy">
    /// <see langword="true" /> when the request is sent to a forward proxy rather than the
    /// origin: its target is the absolute form unless <c>--request-target</c> replaces it, <c>Proxy-Connection: Keep-Alive</c> is sent and so
    /// are the <c>--proxy-header</c> values.
    /// </param>
    /// <param name="proxyAuthorization">
    /// The <c>Proxy-Authorization</c> value the authenticator gave, or <see langword="null" />
    /// to send none.
    /// </param>
    /// <param name="range">
    /// The <c>Range</c> value (<see cref="HttpRangeHeader" />), or <see langword="null" /> to
    /// send none.
    /// </param>
    /// <param name="timeCondition">
    /// The <c>-z</c> condition, or <see langword="null" /> to send no conditional header.
    /// </param>
    /// <param name="framing">
    /// The request's framing, or <see langword="null" /> to decide it from
    /// <paramref name="options" />; the resend after a 417 passes
    /// <see cref="HttpRequestFraming.WithoutExpect" />.
    /// </param>
    /// <param name="upgradesToH2c">
    /// <see langword="true" /> to ask to upgrade to h2c, for <c>--http2</c> over cleartext
    /// (<see cref="AppendH2cUpgrade" />).
    /// </param>
    /// <returns>The head's bytes, ending in the empty line.</returns>
    internal static byte[] Format(
        CurlUrl url,
        HttpRequestOptions? options,
        bool noBody = false,
        string? authorization = null,
        string? cookie = null,
        bool forwardProxy = false,
        string? proxyAuthorization = null,
        string? range = null,
        TimeCondition? timeCondition = null,
        HttpRequestFraming? framing = null,
        bool upgradesToH2c = false)
    {
        options ??= new HttpRequestOptions();
        HttpCustomHeader[] customHeaders = CustomHeadersOf(options.Headers, options);
        HttpCustomHeader[] proxyHeaders = forwardProxy ? CustomHeadersOf(options.ProxyHeaders, options) : [];
        framing = FramingOf(framing, options, customHeaders, noBody);
        StringBuilder head = new();
        AppendRequestLine(head, framing.Method, url, forwardProxy, options);
        string? hostLine = FormatHostLine(url, customHeaders);
        if (hostLine is not null)
        {
            head.Append(hostLine).Append("\r\n");
        }

        AppendAlways(head, "Proxy-Authorization", proxyAuthorization);
        AppendAuthorization(head, customHeaders, authorization);
        AppendUnlessOverridden(head, customHeaders, "Range", range);
        AppendUnlessOverridden(head, customHeaders, "Content-Range", framing.ContentRange);
        AppendClientHeaders(head, customHeaders, options);
        AppendUnlessOverridden(head, [.. customHeaders, .. proxyHeaders], "Proxy-Connection", forwardProxy ? "Keep-Alive" : null);
        AppendUnlessOverridden(head, customHeaders, "Alt-Used", AltUsedOf(options.AltSvcRoute));
        AppendH2cUpgrade(head, upgradesToH2c);
        AppendAlways(head, "Cookie", cookie);
        AppendTimeCondition(head, customHeaders, timeCondition);
        string? formContentType = FormContentTypeOf(customHeaders, framing);
        AppendCustomHeaders(head, customHeaders, hostLine is not null, formContentType is not null, framing.IsAuthProbe);
        AppendCustomHeaders(head, proxyHeaders, hostLine is not null, false, framing.IsAuthProbe);
        AppendBodyHeaders(head, customHeaders, framing, formContentType);
        AppendConnection(head, customHeaders, SendsTe(options, customHeaders), upgradesToH2c);
        head.Append("\r\n");
        return Encoding.Latin1.GetBytes(head.ToString());
    }

    /// <summary>
    /// Reads the <c>-H</c> or <c>--proxy-header</c> <paramref name="entries" /> after turning
    /// each into its bytes in <see cref="HttpRequestOptions.CommandLineTextEncoding" />
    /// (<see cref="HeadText" />), as curl reads the bytes of its <c>argv</c> (ADR-0067).
    /// </summary>
    /// <param name="entries">The values, verbatim.</param>
    /// <param name="options">The options that name the encoding.</param>
    /// <returns>The headers, one character per byte.</returns>
    internal static HttpCustomHeader[] CustomHeadersOf(IReadOnlyList<string> entries, HttpRequestOptions options) =>
        [.. entries.Select(entry => HttpCustomHeader.Parse(HeadText(entry, options)))];

    /// <summary>
    /// Gives command-line <paramref name="text" /> as its bytes in
    /// <see cref="HttpRequestOptions.CommandLineTextEncoding" />, one character per byte, so the
    /// head's closing <see cref="Encoding.Latin1" /> step puts those bytes on the wire unchanged.
    /// </summary>
    [return: NotNullIfNotNull(nameof(text))]
    private static string? HeadText(string? text, HttpRequestOptions options) =>
        text is null ? null : Encoding.Latin1.GetString(options.CommandLineTextEncoding.GetBytes(text));

    /// <summary>
    /// Gives <paramref name="framing" />, or the framing <see cref="HttpRequestFraming.Of" />
    /// decides from <paramref name="options" /> when none was passed.
    /// </summary>
    private static HttpRequestFraming FramingOf(HttpRequestFraming? framing, HttpRequestOptions options, HttpCustomHeader[] customHeaders, bool noBody) =>
        framing ?? HttpRequestFraming.Of(options, customHeaders, noBody);

    /// <summary>
    /// Appends the request line: the method, the target (<see cref="TargetOf" />) and the
    /// version.
    /// </summary>
    private static void AppendRequestLine(StringBuilder head, string method, CurlUrl url, bool forwardProxy, HttpRequestOptions options) =>
        head.Append(method).Append(' ').Append(TargetOf(url, forwardProxy, options)).Append(VersionOf(options)).Append("\r\n");

    /// <summary>
    /// Gives the request line's target: <see cref="HttpRequestOptions.RequestTarget" />
    /// verbatim when given, its UTF-8 bytes one character each as
    /// <see cref="HttpUrlText.RequestTarget" /> writes a query; otherwise the absolute form
    /// for a forward proxy and the path and query for the origin. curl 8.21.0 was measured
    /// (BL-186 Notes) sending <c>--request-target</c> in place of both forms.
    /// </summary>
    private static string TargetOf(CurlUrl url, bool forwardProxy, HttpRequestOptions options)
    {
        if (options.RequestTarget is { } target)
        {
            return Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(target));
        }

        return forwardProxy ? AbsoluteForm(url) : HttpUrlText.RequestTarget(url);
    }

    /// <summary>
    /// Gives the request line's version: <c>HTTP/1.0</c> for <c>-0</c>, <c>HTTP/1.1</c> otherwise.
    /// </summary>
    private static string VersionOf(HttpRequestOptions options) =>
        options.Version == HttpVersionPreference.Http10 ? " HTTP/1.0" : " HTTP/1.1";

    /// <summary>
    /// Appends <c>User-Agent</c>, <c>Accept</c>, <c>TE: gzip</c> (for <c>--tr-encoding</c>),
    /// <c>Accept-Encoding</c> (for <c>--compressed</c>) and <c>Referer</c>, each unless an
    /// <c>-H</c> value names it.
    /// </summary>
    private static void AppendClientHeaders(StringBuilder head, HttpCustomHeader[] customHeaders, HttpRequestOptions options)
    {
        AppendUnlessOverridden(head, customHeaders, "User-Agent", HeadText(options.UserAgent, options) ?? DefaultUserAgent);
        AppendUnlessOverridden(head, customHeaders, "Accept", "*/*");
        AppendUnlessOverridden(head, customHeaders, "TE", options.TransferEncoding ? "gzip" : null);
        AppendUnlessOverridden(head, customHeaders, "Accept-Encoding", options.Compressed ? AcceptEncoding : null);
        AppendUnlessOverridden(head, customHeaders, "Referer", HeadText(options.Referer, options));
    }

    /// <summary>
    /// Appends <c>If-Modified-Since</c> for <c>-z date</c> or <c>If-Unmodified-Since</c> for
    /// <c>-z -date</c>, the time in RFC 1123 form in GMT (<see cref="HttpConditionDate" />),
    /// unless an <c>-H</c> value names it.
    /// </summary>
    private static void AppendTimeCondition(StringBuilder head, HttpCustomHeader[] customHeaders, TimeCondition? timeCondition)
    {
        if (timeCondition is null)
        {
            return;
        }

        string name = timeCondition.Kind == TimeConditionKind.IfUnmodifiedSince ? "If-Unmodified-Since" : "If-Modified-Since";
        AppendUnlessOverridden(head, customHeaders, name, HttpConditionDate.Format(timeCondition));
    }

    /// <summary>
    /// Formats the <c>Host</c> line: the first <c>-H</c> value naming <c>Host</c> with its
    /// name written <c>Host:</c>, or none when that value is exactly <c>Host:</c> in any case, or else
    /// the URL's host as written, bracketed if IPv6, with its port unless it is the
    /// default of an <c>http</c> or <c>https</c> URL (<see cref="HttpUrlText.HostHeaderAuthority" />).
    /// </summary>
    private static string? FormatHostLine(CurlUrl url, HttpCustomHeader[] customHeaders)
    {
        foreach (HttpCustomHeader header in customHeaders)
        {
            if (header.Names(HostName))
            {
                return string.Equals(header.Entry, "Host:", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : string.Concat("Host:", header.Entry.AsSpan(HostName.Length + 1));
            }
        }

        return $"Host: {HttpUrlText.HostHeaderAuthority(url)}";
    }

    /// <summary>
    /// Gives the value of the first <c>-H</c> value naming <c>Host</c>, its name matched in any
    /// case, or <see langword="null" /> when none names it or the one that does sends no value.
    /// </summary>
    internal static string? CustomHostOf(HttpRequestOptions options)
    {
        foreach (HttpCustomHeader header in CustomHeadersOf(options.Headers, options))
        {
            if (header.Names(HostName))
            {
                return header.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Formats the absolute-form request target a forward proxy is sent: the scheme, the host
    /// and port as the <c>Host</c> line has them, then the path and query.
    /// </summary>
    private static string AbsoluteForm(CurlUrl url) => $"{url.Scheme}://{HttpUrlText.HostAndPort(url)}{HttpUrlText.RequestTarget(url)}";

    /// <summary>
    /// Appends the authenticator's <c>Authorization</c> value as curl 8.21.0 does: a Digest, NTLM
    /// or Negotiate value always, before any <c>-H</c> one, and any other (Basic, Bearer,
    /// <c>--aws-sigv4</c>) only when no <c>-H</c> value names <c>Authorization</c>, because
    /// libcurl's <c>output_auth_headers</c> checks the custom headers for Basic and Bearer alone
    /// (measured, BL-986 Notes).
    /// </summary>
    private static void AppendAuthorization(StringBuilder head, HttpCustomHeader[] customHeaders, string? authorization)
    {
        if (IsSentBesideAuthorizationHeader(authorization))
        {
            AppendAlways(head, "Authorization", authorization);
        }
        else
        {
            AppendUnlessOverridden(head, customHeaders, "Authorization", authorization);
        }
    }

    /// <summary>
    /// Tells whether curl sends an <c>Authorization</c> value even when an <c>-H</c> value names
    /// <c>Authorization</c>: when its scheme is Digest, NTLM or Negotiate.
    /// </summary>
    private static bool IsSentBesideAuthorizationHeader(string? authorization) =>
        authorization?.Split(' ', 2)[0] is "Digest" or "NTLM" or "Negotiate";

    private static void AppendUnlessOverridden(StringBuilder head, HttpCustomHeader[] customHeaders, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !customHeaders.Any(header => header.Names(name)))
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }

    /// <summary>
    /// Appends a line whatever the <c>-H</c> values name, as curl 8.21.0 sends its cookie
    /// engine's <c>Cookie</c> and its <c>Proxy-Authorization</c> beside custom ones (BL-182 and
    /// BL-183 Notes).
    /// </summary>
    private static void AppendAlways(StringBuilder head, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }

    /// <summary>
    /// Appends each <c>-H</c> or <c>--proxy-header</c> value that sends a line, in order,
    /// leaving out every <c>Host:</c> line when a <c>Host</c> line was already written, and
    /// every value naming <c>Connection</c>, which <see cref="AppendConnection" /> places, and
    /// every value naming <c>Content-Type</c> when <paramref name="leavesOutContentType" />,
    /// because <see cref="AppendBodyHeaders" /> sends it merged with the form's boundary, and
    /// every value naming <c>Content-Length</c> when <paramref name="leavesOutContentLength" />,
    /// because a Digest probe sends its own <c>Content-Length: 0</c> (BL-1835).
    /// </summary>
    private static void AppendCustomHeaders(StringBuilder head, HttpCustomHeader[] customHeaders, bool hostLineWritten, bool leavesOutContentType, bool leavesOutContentLength)
    {
        foreach (HttpCustomHeader header in customHeaders)
        {
            if (header.SentLine is { } line
                && !header.Names(ConnectionName)
                && !(leavesOutContentType && header.Names(ContentTypeName))
                && !(leavesOutContentLength && header.Names(ContentLengthName))
                && !(hostLineWritten && line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)))
            {
                head.Append(line).Append("\r\n");
            }
        }
    }

    /// <summary>
    /// Gives the <c>Content-Length</c> value: the body's length, or <see langword="null" /> when
    /// the body is sent chunked or its length is unknown (an HTTP/2 body,
    /// <see cref="HttpRequestFraming.ForHttp2OrHttp3" />).
    /// </summary>
    private static string? ContentLengthOf(HttpRequestFraming framing) =>
        framing.IsChunked ? null : framing.KnownLength?.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Appends the body's framing headers: <c>Content-Length</c> as
    /// <see cref="ContentLengthOf" /> gives it, <c>Transfer-Encoding: chunked</c> when its
    /// length is unknown, its <c>Content-Type</c> unless it is a <c>-T</c> upload, and curl's
    /// own <c>Expect: 100-continue</c>. A <c>-F</c> form's <c>Content-Type</c> is
    /// <paramref name="formContentType" /> when an <c>-H</c> value names one.
    /// </summary>
    private static void AppendBodyHeaders(StringBuilder head, HttpCustomHeader[] customHeaders, HttpRequestFraming framing, string? formContentType)
    {
        if (framing.Body is not { } body)
        {
            return;
        }

        AppendUnlessOverridden(head, framing.IsAuthProbe ? [] : customHeaders, ContentLengthName, ContentLengthOf(framing));
        AppendUnlessOverridden(head, customHeaders, "Transfer-Encoding", framing.KnownLength is null ? "chunked" : null);
        AppendAlways(head, ContentTypeName, formContentType);
        AppendUnlessOverridden(head, customHeaders, ContentTypeName, framing.IsUpload ? null : body.ContentType);
        AppendUnlessOverridden(head, customHeaders, "Expect", framing.AddsExpect ? "100-continue" : null);
    }

    /// <summary>
    /// Gives a <c>-F</c> form's <c>Content-Type</c> when an <c>-H</c> value names one, as curl
    /// 8.21.0 sends it (upstream tests 277 and 669, BL-1811): the first such value, then
    /// <c>; boundary=</c> and the form's boundary, in the generated header's place after
    /// <c>Content-Length</c>, every <c>-H</c> <c>Content-Type</c> line left out. Gives
    /// <see langword="null" /> when the body is not a <c>multipart/form-data</c> form or no
    /// <c>-H</c> value names a non-empty <c>Content-Type</c>.
    /// </summary>
    private static string? FormContentTypeOf(HttpCustomHeader[] customHeaders, HttpRequestFraming framing)
    {
        string? userType = customHeaders
            .Where(header => header.Names(ContentTypeName))
            .Select(header => header.Value)
            .FirstOrDefault(value => !string.IsNullOrEmpty(value));
        return framing is { Body.ContentType: { } bodyType }
            && userType is not null
            && bodyType.StartsWith(FormBoundaryPrefix, StringComparison.Ordinal)
            ? $"{userType}; boundary={bodyType[FormBoundaryPrefix.Length..]}"
            : null;
    }

    /// <summary>
    /// Tells whether <c>TE: gzip</c> is sent: for <c>--tr-encoding</c>, unless an <c>-H</c>
    /// value names <c>TE</c>.
    /// </summary>
    private static bool SendsTe(HttpRequestOptions options, HttpCustomHeader[] customHeaders) =>
        options.TransferEncoding && !customHeaders.Any(header => header.Names("TE"));

    /// <summary>
    /// Gives the <c>Alt-Used</c> value for a transfer sent to an alternative service: its host
    /// and port as curl 8.21.0 writes them, <c>&lt;host&gt;:&lt;port&gt;</c> (measured, BL-878
    /// Notes); <see langword="null" /> to send none.
    /// </summary>
    private static string? AltUsedOf(AltSvcRoute? route) =>
        route is null ? null : $"{route.Alternative.Host}:{route.Alternative.Port}";

    /// <summary>
    /// Appends the h2c upgrade request's <c>Upgrade: h2c</c> and <c>HTTP2-Settings</c> lines
    /// (<see cref="Http2Session.UpgradeSettings" />) when <paramref name="upgradesToH2c" />, after
    /// <c>Proxy-Connection</c> and before <c>Cookie</c>, whatever the <c>-H</c> values name, as
    /// curl sends them for <c>--http2</c> over cleartext (measured, BL-716 Notes).
    /// </summary>
    private static void AppendH2cUpgrade(StringBuilder head, bool upgradesToH2c)
    {
        if (upgradesToH2c)
        {
            head.Append("Upgrade: h2c\r\nHTTP2-Settings: ").Append(Http2Session.UpgradeSettings).Append("\r\n");
        }
    }

    /// <summary>
    /// Appends the <c>Connection</c> lines last, after <c>Expect</c>, as curl 8.21.0 does
    /// (measured, BL-315 Notes): the first <c>-H</c> value naming <c>Connection</c> that has a
    /// value, without the white space around it and with <c>, TE</c> added when
    /// <paramref name="sendsTe" /> and <c>, Upgrade, HTTP2-Settings</c> after that when
    /// <paramref name="upgradesToH2c" /> (measured, BL-716 Notes), or those options alone when
    /// there is no such value; then every later one verbatim. A <c>Connection</c> value with
    /// nothing after its colon or semicolon, and a <c>--proxy-header</c> naming <c>Connection</c>,
    /// is not sent at all.
    /// </summary>
    /// <param name="head">The head being written.</param>
    /// <param name="customHeaders">The <c>-H</c> values.</param>
    /// <param name="sendsTe"><see langword="true" /> when <c>TE: gzip</c> was sent.</param>
    /// <param name="upgradesToH2c"><see langword="true" /> when the request asks to upgrade to h2c.</param>
    private static void AppendConnection(StringBuilder head, HttpCustomHeader[] customHeaders, bool sendsTe, bool upgradesToH2c)
    {
        HttpCustomHeader[] connections = [.. customHeaders.Where(header => header.Names(ConnectionName) && header.Value is not null)];
        string[] options =
        [
            .. connections.Take(1).Select(header => header.Value!),
            .. sendsTe ? ["TE"] : Array.Empty<string>(),
            .. upgradesToH2c ? ["Upgrade", "HTTP2-Settings"] : Array.Empty<string>(),
        ];
        AppendAlways(head, ConnectionName, string.Join(", ", options));
        foreach (HttpCustomHeader header in connections.Skip(1))
        {
            head.Append(header.Entry).Append("\r\n");
        }
    }
}
