using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats the head of an HTTP/1.1 request - the request line, the headers and the empty
/// line after them - byte for byte as curl 8.21.0 sends it for <c>-X</c>, <c>-H</c>,
/// <c>-A</c> and <c>-e</c>. Every rule was measured (BL-172 Notes).
/// </summary>
/// <remarks>
/// curl's own headers come first, in the order <c>Host</c>, <c>User-Agent</c>,
/// <c>Accept</c>, <c>Referer</c>, each left out when an <c>-H</c> value names it; the
/// <c>-H</c> values follow in command-line order. A custom <c>Host</c> is the exception: it
/// takes the <c>Host</c> slot. Text is sent one byte per character (Latin-1), as curl
/// sends a command-line argument on Windows; a character above U+00FF takes Latin-1's
/// best fit, such as <c>A</c> for U+0100, or else <c>?</c>.
/// </remarks>
internal static class HttpRequestHeadFormatter
{
    /// <summary>
    /// The <c>User-Agent</c> curl 8.21.0 sends when <c>-A</c> is not given.
    /// </summary>
    internal const string DefaultUserAgent = "curl/8.21.0";

    private const string HostName = "Host";

    /// <summary>
    /// Formats the request head for <paramref name="url" />.
    /// </summary>
    /// <param name="url">The URL requested; its path and query form the request target.</param>
    /// <param name="options">
    /// The HTTP options, or <see langword="null" /> for every option at its default.
    /// </param>
    /// <returns>The head's bytes, ending in the empty line.</returns>
    internal static byte[] Format(Uri url, HttpRequestOptions? options)
    {
        options ??= new HttpRequestOptions();
        HttpCustomHeader[] customHeaders = [.. options.Headers.Select(HttpCustomHeader.Parse)];
        StringBuilder head = new();
        head.Append(options.CustomMethod ?? "GET").Append(' ').Append(url.PathAndQuery).Append(" HTTP/1.1\r\n");
        string? hostLine = FormatHostLine(url, customHeaders);
        if (hostLine is not null)
        {
            head.Append(hostLine).Append("\r\n");
        }

        AppendUnlessOverridden(head, customHeaders, "User-Agent", options.UserAgent ?? DefaultUserAgent);
        AppendUnlessOverridden(head, customHeaders, "Accept", "*/*");
        AppendUnlessOverridden(head, customHeaders, "Referer", options.Referer);
        AppendCustomHeaders(head, customHeaders, hostLine is not null);
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
}
