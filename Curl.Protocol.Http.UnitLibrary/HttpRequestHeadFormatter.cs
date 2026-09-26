using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats the head of an HTTP/1.1 request - the request line, the headers and the empty
/// line after them - byte for byte as curl 8.21.0 sends it for <c>-X</c>, <c>-H</c>,
/// <c>-A</c>, <c>-e</c>, <c>-I</c>, <c>--compressed</c> and a request body. Every rule was
/// measured (BL-172, BL-175 and BL-177 Notes).
/// </summary>
/// <remarks>
/// curl's own headers come first, in the order <c>Host</c>, <c>User-Agent</c>,
/// <c>Accept</c>, <c>Accept-Encoding</c> (for <c>--compressed</c>), <c>Referer</c>, each
/// left out when an <c>-H</c> value names it; the <c>-H</c> values follow in command-line
/// order. A custom <c>Host</c> is the exception: it takes the <c>Host</c> slot. A request with a body ends with <c>Content-Length</c> (or
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
    /// <returns>The head's bytes, ending in the empty line.</returns>
    internal static byte[] Format(Uri url, HttpRequestOptions? options, bool noBody = false)
    {
        options ??= new HttpRequestOptions();
        HttpCustomHeader[] customHeaders = [.. options.Headers.Select(HttpCustomHeader.Parse)];
        HttpRequestFraming framing = HttpRequestFraming.Of(options, customHeaders, noBody);
        StringBuilder head = new();
        head.Append(framing.Method).Append(' ').Append(url.PathAndQuery).Append(" HTTP/1.1\r\n");
        string? hostLine = FormatHostLine(url, customHeaders);
        if (hostLine is not null)
        {
            head.Append(hostLine).Append("\r\n");
        }

        AppendUnlessOverridden(head, customHeaders, "User-Agent", options.UserAgent ?? DefaultUserAgent);
        AppendUnlessOverridden(head, customHeaders, "Accept", "*/*");
        AppendUnlessOverridden(head, customHeaders, "Accept-Encoding", options.Compressed ? AcceptEncoding : null);
        AppendUnlessOverridden(head, customHeaders, "Referer", options.Referer);
        AppendCustomHeaders(head, customHeaders, hostLine is not null);
        AppendBodyHeaders(head, customHeaders, framing);
        head.Append("\r\n");
        return Encoding.Latin1.GetBytes(head.ToString());
    }

    /// <summary>
    /// Formats the <c>Host</c> line: the first <c>-H</c> value naming <c>Host</c> with its
    /// name written <c>Host:</c>, or none when that value is exactly <c>Host:</c>, or else
    /// the URL's host as written, bracketed if IPv6, with its port unless it is the
    /// scheme's default.
    /// </summary>
    private static string? FormatHostLine(Uri url, HttpCustomHeader[] customHeaders)
    {
        foreach (HttpCustomHeader header in customHeaders)
        {
            if (header.Names(HostName))
            {
                return header.Entry == "Host:" ? null : string.Concat("Host:", header.Entry.AsSpan(HostName.Length + 1));
            }
        }

        string port = url.IsDefaultPort ? string.Empty : string.Create(CultureInfo.InvariantCulture, $":{url.Port}");
        return $"Host: {HostAsWritten(url)}{port}";
    }

    /// <summary>
    /// Returns the URL's host in the letter case it was written in, which curl keeps and
    /// <see cref="Uri.Host" /> lower-cases.
    /// </summary>
    private static string HostAsWritten(Uri url)
    {
        string host = url.Host;
        int start = url.OriginalString.IndexOf(host, StringComparison.OrdinalIgnoreCase);
        return start < 0 ? host : url.OriginalString.Substring(start, host.Length);
    }

    private static void AppendUnlessOverridden(StringBuilder head, HttpCustomHeader[] customHeaders, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !customHeaders.Any(header => header.Names(name)))
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }

    /// <summary>
    /// Appends each <c>-H</c> value that sends a line, in order, leaving out every
    /// <c>Host:</c> line when a <c>Host</c> line was already written.
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
    /// <c>Content-Type</c>, and curl's own <c>Expect: 100-continue</c>.
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
        AppendUnlessOverridden(head, customHeaders, "Content-Type", body.ContentType);
        AppendUnlessOverridden(head, customHeaders, "Expect", framing.AddsExpect ? "100-continue" : null);
    }
}
