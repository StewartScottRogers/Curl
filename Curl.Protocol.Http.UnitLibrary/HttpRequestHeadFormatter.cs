using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats the head of an HTTP/1.1 or HTTP/1.0 request - the request line, the headers and the empty
/// line after them - byte for byte as curl 8.21.0 sends it for <c>-X</c>, <c>-H</c>,
/// <c>-A</c>, <c>-e</c>, <c>-I</c>, <c>--compressed</c>, an <c>Authorization</c> value, a
/// <c>Cookie</c> value, a request body, a forward proxy, <c>--proxy-header</c>, <c>-r</c>, <c>-C</c> and <c>-z</c>. Every
/// rule was measured (BL-172, BL-175, BL-177, BL-178, BL-180, BL-181, BL-182, BL-183 and BL-296 Notes).
/// </summary>
/// <remarks>
/// The request line ends in <c>HTTP/1.0</c> for <c>-0</c> and in <c>HTTP/1.1</c> otherwise;
/// the headers are the same for both. curl's own headers come first, in the order <c>Host</c>, <c>Proxy-Authorization</c>,
/// <c>Authorization</c>, <c>Range</c>, <c>User-Agent</c>, <c>Accept</c>, <c>Accept-Encoding</c> (for
/// <c>--compressed</c>), <c>Referer</c>, <c>Proxy-Connection: Keep-Alive</c> (through a forward
/// proxy), each left out when an <c>-H</c> value names it, then the cookie store's
/// <c>Cookie</c>, then <c>If-Modified-Since</c> or <c>If-Unmodified-Since</c> for <c>-z</c>, also
/// left out when an <c>-H</c> value names it; <c>Cookie</c> and <c>Proxy-Authorization</c> are sent even when an <c>-H</c>
/// value names them. Through a forward proxy the request target is the absolute form,
/// <c>http://host[:port]/path?query</c>, with no user information or fragment. The <c>-H</c> values follow in
/// command-line order, then, through a forward proxy only, the <c>--proxy-header</c> values under the
/// same rules; of curl's own headers they override only <c>Proxy-Connection</c> (BL-296 Notes). A custom <c>Host</c> is the exception: it takes the <c>Host</c> slot. A request with a body ends with <c>Content-Length</c> (or
/// <c>Transfer-Encoding: chunked</c> when the length is unknown), <c>Content-Type</c> and
/// <c>Expect: 100-continue</c> as <see cref="HttpRequestFraming" /> decides, each again left
/// out when an <c>-H</c> value names it. Text is sent one byte per character (Latin-1), as curl
/// sends a command-line argument on Windows; a character above U+00FF takes Latin-1's
/// best fit, such as <c>A</c> for U+0100, or else <c>?</c>.
/// </remarks>
internal static class HttpRequestHeadFormatter
{
    /// <summary>
    /// The <c>User-Agent</c> curl 8.21.0 sends when <c>-A</c> is not given.
    /// </summary>
    internal const string DefaultUserAgent = "curl/8.21.0";

    /// <summary>
    /// The <c>Accept-Encoding</c> value <c>--compressed</c> sends: curl 8.21.0's without
    /// <c>zstd</c>, which Curl cannot decode (ADR-0020).
    /// </summary>
    internal const string AcceptEncoding = "deflate, gzip, br";

    private const string HostName = "Host";

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
    /// origin: its target is the absolute form, <c>Proxy-Connection: Keep-Alive</c> is sent and so
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
        HttpRequestFraming? framing = null)
    {
        options ??= new HttpRequestOptions();
        HttpCustomHeader[] customHeaders = [.. options.Headers.Select(HttpCustomHeader.Parse)];
        HttpCustomHeader[] proxyHeaders = forwardProxy ? [.. options.ProxyHeaders.Select(HttpCustomHeader.Parse)] : [];
        framing = FramingOf(framing, options, customHeaders, noBody);
        StringBuilder head = new();
        AppendRequestLine(head, framing.Method, url, forwardProxy, options);
        string? hostLine = FormatHostLine(url, customHeaders);
        if (hostLine is not null)
        {
            head.Append(hostLine).Append("\r\n");
        }

        AppendAlways(head, "Proxy-Authorization", proxyAuthorization);
        AppendUnlessOverridden(head, customHeaders, "Authorization", authorization);
        AppendUnlessOverridden(head, customHeaders, "Range", range);
        AppendClientHeaders(head, customHeaders, options);
        AppendUnlessOverridden(head, [.. customHeaders, .. proxyHeaders], "Proxy-Connection", forwardProxy ? "Keep-Alive" : null);
        AppendAlways(head, "Cookie", cookie);
        AppendTimeCondition(head, customHeaders, timeCondition);
        AppendCustomHeaders(head, customHeaders, hostLine is not null);
        AppendCustomHeaders(head, proxyHeaders, hostLine is not null);
        AppendBodyHeaders(head, customHeaders, framing);
        head.Append("\r\n");
        return Encoding.Latin1.GetBytes(head.ToString());
    }

    /// <summary>
    /// Gives <paramref name="framing" />, or the framing <see cref="HttpRequestFraming.Of" />
    /// decides from <paramref name="options" /> when none was passed.
    /// </summary>
    private static HttpRequestFraming FramingOf(HttpRequestFraming? framing, HttpRequestOptions options, HttpCustomHeader[] customHeaders, bool noBody) =>
        framing ?? HttpRequestFraming.Of(options, customHeaders, noBody);

    /// <summary>
    /// Appends the request line: the method, the target - the absolute form for a forward
    /// proxy, the path and query otherwise - and the version.
    /// </summary>
    private static void AppendRequestLine(StringBuilder head, string method, CurlUrl url, bool forwardProxy, HttpRequestOptions options) =>
        head.Append(method).Append(' ').Append(forwardProxy ? AbsoluteForm(url) : HttpUrlText.RequestTarget(url)).Append(VersionOf(options)).Append("\r\n");

    /// <summary>
    /// Gives the request line's version: <c>HTTP/1.0</c> for <c>-0</c>, <c>HTTP/1.1</c> otherwise.
    /// </summary>
    private static string VersionOf(HttpRequestOptions options) =>
        options.Version == HttpVersionPreference.Http10 ? " HTTP/1.0" : " HTTP/1.1";

    /// <summary>
    /// Appends <c>User-Agent</c>, <c>Accept</c>, <c>Accept-Encoding</c> (for
    /// <c>--compressed</c>) and <c>Referer</c>, each unless an <c>-H</c> value names it.
    /// </summary>
    private static void AppendClientHeaders(StringBuilder head, HttpCustomHeader[] customHeaders, HttpRequestOptions options)
    {
        AppendUnlessOverridden(head, customHeaders, "User-Agent", options.UserAgent ?? DefaultUserAgent);
        AppendUnlessOverridden(head, customHeaders, "Accept", "*/*");
        AppendUnlessOverridden(head, customHeaders, "Accept-Encoding", options.Compressed ? AcceptEncoding : null);
        AppendUnlessOverridden(head, customHeaders, "Referer", options.Referer);
    }

    /// <summary>
    /// Appends <c>If-Modified-Since</c> for <c>-z date</c> or <c>If-Unmodified-Since</c> for
    /// <c>-z -date</c>, the time in RFC 1123 form in GMT, unless an <c>-H</c> value names it.
    /// </summary>
    private static void AppendTimeCondition(StringBuilder head, HttpCustomHeader[] customHeaders, TimeCondition? timeCondition)
    {
        if (timeCondition is null)
        {
            return;
        }

        string name = timeCondition.Kind == TimeConditionKind.IfUnmodifiedSince ? "If-Unmodified-Since" : "If-Modified-Since";
        AppendUnlessOverridden(head, customHeaders, name, timeCondition.Value.UtcDateTime.ToString("r", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Formats the <c>Host</c> line: the first <c>-H</c> value naming <c>Host</c> with its
    /// name written <c>Host:</c>, or none when that value is exactly <c>Host:</c>, or else
    /// the URL's host as written, bracketed if IPv6, with its port unless it is the
    /// scheme's default.
    /// </summary>
    private static string? FormatHostLine(CurlUrl url, HttpCustomHeader[] customHeaders)
    {
        foreach (HttpCustomHeader header in customHeaders)
        {
            if (header.Names(HostName))
            {
                return header.Entry == "Host:" ? null : string.Concat("Host:", header.Entry.AsSpan(HostName.Length + 1));
            }
        }

        return $"Host: {HttpUrlText.HostAndPort(url)}";
    }

    /// <summary>
    /// Formats the absolute-form request target a forward proxy is sent: the scheme, the host
    /// and port as the <c>Host</c> line has them, then the path and query.
    /// </summary>
    private static string AbsoluteForm(CurlUrl url) => $"{url.Scheme}://{HttpUrlText.HostAndPort(url)}{HttpUrlText.RequestTarget(url)}";

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
    /// leaving out every <c>Host:</c> line when a <c>Host</c> line was already written.
    /// </summary>
    private static void AppendCustomHeaders(StringBuilder head, HttpCustomHeader[] customHeaders, bool hostLineWritten)
    {
        foreach (HttpCustomHeader header in customHeaders)
        {
            if (header.SentLine is { } line && !(hostLineWritten && line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)))
            {
                head.Append(line).Append("\r\n");
            }
        }
    }

    /// <summary>
    /// Appends the body's framing headers: <c>Content-Length</c> unless the body is sent
    /// chunked, <c>Transfer-Encoding: chunked</c> when its length is unknown, its
    /// <c>Content-Type</c> unless it is a <c>-T</c> upload, and curl's own
    /// <c>Expect: 100-continue</c>.
    /// </summary>
    private static void AppendBodyHeaders(StringBuilder head, HttpCustomHeader[] customHeaders, HttpRequestFraming framing)
    {
        if (framing.Body is not { } body)
        {
            return;
        }

        string? contentLength = framing.IsChunked ? null : framing.KnownLength.GetValueOrDefault().ToString(CultureInfo.InvariantCulture);
        AppendUnlessOverridden(head, customHeaders, "Content-Length", contentLength);
        AppendUnlessOverridden(head, customHeaders, "Transfer-Encoding", framing.KnownLength is null ? "chunked" : null);
        AppendUnlessOverridden(head, customHeaders, "Content-Type", framing.IsUpload ? null : body.ContentType);
        AppendUnlessOverridden(head, customHeaders, "Expect", framing.AddsExpect ? "100-continue" : null);
    }
}
