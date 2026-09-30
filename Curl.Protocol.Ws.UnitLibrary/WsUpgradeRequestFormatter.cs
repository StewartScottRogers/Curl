using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Writes the HTTP/1.1 request head that asks a server to upgrade to WebSocket, byte for byte
/// as curl 8.21.0 writes it (ADR-0128).
/// </summary>
/// <remarks>
/// The order is the request line, <c>Host</c>, <c>Authorization</c>, <c>User-Agent</c>,
/// <c>Accept</c>, <c>Referer</c>, <c>Upgrade: websocket</c>, <c>Sec-WebSocket-Version: 13</c>,
/// <c>Sec-WebSocket-Key</c>, the <c>-H</c> headers, and <c>Connection: Upgrade</c> last; a
/// <c>-H 'Connection: …'</c> value is kept with <c>, Upgrade</c> appended. A <c>-H</c> header
/// of the same name replaces or removes each of curl's own. <c>Host</c> leaves out the port
/// when it is the scheme's default (80 for <c>ws</c>, 443 for <c>wss</c>).
/// </remarks>
internal static class WsUpgradeRequestFormatter
{
    /// <summary>The <c>User-Agent</c> curl 8.21.0 sends when <c>-A</c> is not given.</summary>
    internal const string DefaultUserAgent = "curl/8.21.0";

    private const string HostName = "Host";

    private const string ConnectionName = "Connection";

    /// <summary>Writes the upgrade request head.</summary>
    /// <param name="url">The <c>ws</c> or <c>wss</c> URL.</param>
    /// <param name="options">The HTTP options that reach the request: <c>-H</c>, <c>-A</c>, <c>-e</c>.</param>
    /// <param name="method">The request method: <c>GET</c>, or the one <c>-X</c> names.</param>
    /// <param name="key">The <c>Sec-WebSocket-Key</c> value.</param>
    /// <param name="authorization">The <c>Authorization</c> value, or <see langword="null" /> for none.</param>
    /// <returns>The request head, Latin-1 encoded, ending with the blank line.</returns>
    internal static byte[] Format(CurlUrl url, HttpRequestOptions options, string method, string key, string? authorization)
    {
        WsCustomHeader[] customHeaders = [.. options.Headers.Select(entry => WsCustomHeader.Parse(HeadText(entry, options)))];
        StringBuilder head = new();
        head.Append(method).Append(' ').Append(RequestTarget(url)).Append(" HTTP/1.1\r\n");
        AppendHost(head, url, customHeaders);
        AppendUnlessOverridden(head, customHeaders, "Authorization", authorization);
        AppendUnlessOverridden(head, customHeaders, "User-Agent", HeadText(options.UserAgent, options) ?? DefaultUserAgent);
        AppendUnlessOverridden(head, customHeaders, "Accept", "*/*");
        AppendUnlessOverridden(head, customHeaders, "Referer", HeadText(options.Referer, options));
        AppendUnlessOverridden(head, customHeaders, "Upgrade", "websocket");
        AppendUnlessOverridden(head, customHeaders, "Sec-WebSocket-Version", "13");
        AppendUnlessOverridden(head, customHeaders, "Sec-WebSocket-Key", key);
        AppendCustomHeaders(head, customHeaders);
        AppendConnection(head, customHeaders);
        head.Append("\r\n");
        return Encoding.Latin1.GetBytes(head.ToString());
    }

    /// <summary>
    /// Writes the request target: the URL's path with every character above <c>~</c>
    /// percent-encoded as UTF-8, then <c>?</c> and the query as UTF-8 bytes when there is one.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>The request target, one character per byte.</returns>
    internal static string RequestTarget(CurlUrl url)
    {
        StringBuilder target = new(url.AbsolutePath.Length);
        foreach (Rune rune in url.AbsolutePath.EnumerateRunes())
        {
            AppendEncoded(target, rune);
        }

        return url.Query is null ? target.ToString() : target.Append('?').Append(Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(url.Query))).ToString();
    }

    /// <summary>Decides whether an <c>-H</c> value names the header <paramref name="name" />, in any case, to set, blank or remove it.</summary>
    /// <param name="options">The HTTP options whose <c>-H</c> values are looked at.</param>
    /// <param name="name">The header name.</param>
    /// <returns><see langword="true" /> when an <c>-H</c> value names the header.</returns>
    internal static bool HeadersName(HttpRequestOptions options, string name) =>
        options.Headers.Any(entry => WsCustomHeader.Parse(HeadText(entry, options)).Names(name));

    [return: NotNullIfNotNull(nameof(text))]
    private static string? HeadText(string? text, HttpRequestOptions options) =>
        text is null ? null : Encoding.Latin1.GetString(options.CommandLineTextEncoding.GetBytes(text));

    private static void AppendEncoded(StringBuilder target, Rune rune)
    {
        if (rune.Value <= '~')
        {
            target.Append((char)rune.Value);
            return;
        }

        Span<byte> octets = stackalloc byte[4];
        foreach (byte octet in octets[..rune.EncodeToUtf8(octets)])
        {
            target.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
        }
    }

    private static void AppendHost(StringBuilder head, CurlUrl url, WsCustomHeader[] customHeaders)
    {
        foreach (WsCustomHeader header in customHeaders.Where(header => header.Names(HostName)).Take(1))
        {
            if (header.Entry != "Host:")
            {
                head.Append("Host:").Append(header.Entry.AsSpan(HostName.Length + 1)).Append("\r\n");
            }

            return;
        }

        string authority = url.IsDefaultPort ? url.Host : string.Create(CultureInfo.InvariantCulture, $"{url.Host}:{url.Port}");
        head.Append("Host: ").Append(authority).Append("\r\n");
    }

    private static void AppendUnlessOverridden(StringBuilder head, WsCustomHeader[] customHeaders, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !customHeaders.Any(header => header.Names(name)))
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }

    private static void AppendCustomHeaders(StringBuilder head, WsCustomHeader[] customHeaders)
    {
        foreach (WsCustomHeader header in customHeaders)
        {
            if (header.SentLine is { } line && !header.Names(ConnectionName) && !header.Names(HostName))
            {
                head.Append(line).Append("\r\n");
            }
        }
    }

    private static void AppendConnection(StringBuilder head, WsCustomHeader[] customHeaders)
    {
        string? kept = customHeaders.Where(header => header.Names(ConnectionName)).Select(header => header.Value).FirstOrDefault(value => value is not null);
        head.Append("Connection: ").Append(kept is null ? "Upgrade" : kept + ", Upgrade").Append("\r\n");
    }
}
