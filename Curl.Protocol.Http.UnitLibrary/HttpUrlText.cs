using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes the parts of a transfer's <see cref="CurlUrl" /> that an HTTP request and its
/// redirects put on the wire: the request target, the host and port, and the origin.
/// </summary>
/// <remarks>
/// curl 8.21.0 was measured (BL-294 Notes) sending <c>http://h/ä?ö=1</c> as
/// <c>GET /%E4?</c>, the byte <c>0xF6</c>, <c>=1</c>: it percent-encodes each byte above <c>0x7F</c> in the path and
/// sends the query's bytes as they are. The bytes it had were the console's code page; the
/// bytes here are the text's UTF-8, as <see cref="CurlUrl.IdnHost" /> reads a host
/// (ADR-0010).
/// </remarks>
internal static class HttpUrlText
{
    /// <summary>
    /// Gives the origin-form request target as the characters of a head written in Latin-1:
    /// <see cref="Path" />, then <c>?</c> and the query when the URL has one, each of the
    /// query's UTF-8 bytes one character, so the head carries them unchanged.
    /// </summary>
    internal static string RequestTarget(CurlUrl url) =>
        Path(url) + (url.Query is null ? string.Empty : "?" + Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(url.Query)));

    /// <summary>Gives the path with each character above <c>~</c> percent-encoded as UTF-8.</summary>
    internal static string Path(CurlUrl url)
    {
        StringBuilder encoded = new(url.AbsolutePath.Length);
        foreach (Rune rune in url.AbsolutePath.EnumerateRunes())
        {
            AppendEncoded(encoded, rune);
        }

        return encoded.ToString();
    }

    /// <summary>
    /// Gives <c>?</c> and the query as written, or the empty string when the URL has no
    /// <c>?</c>: the query a relative redirect keeps.
    /// </summary>
    internal static string Query(CurlUrl url) => url.Query is null ? string.Empty : "?" + url.Query;

    /// <summary>
    /// Gives the host as written, bracketed if IPv6, with <c>:</c> and the port unless it is
    /// the scheme's default.
    /// </summary>
    internal static string HostAndPort(CurlUrl url) =>
        url.IsDefaultPort ? url.Host : string.Create(CultureInfo.InvariantCulture, $"{url.Host}:{url.Port}");

    /// <summary>
    /// Gives the host and port the <c>Host</c> line carries: <see cref="HostAndPort" /> for
    /// <c>http</c> and <c>https</c>, and the host with its port always for any other scheme,
    /// which reaches this handler only when forwarded through an HTTP proxy. curl 8.21.0 was
    /// measured sending <c>Host: example.com:21</c> for <c>ftp://example.com/f.txt</c>
    /// (BL-330 Notes).
    /// </summary>
    internal static string HostHeaderAuthority(CurlUrl url) =>
        url.Scheme is "http" or "https" ? HostAndPort(url) : string.Create(CultureInfo.InvariantCulture, $"{url.Host}:{url.Port}");

    /// <summary>
    /// Gives the scheme, <c>://</c>, the user information with its <c>@</c> when the URL has
    /// one, and <see cref="HostAndPort" />: the part a relative redirect keeps.
    /// </summary>
    internal static string Origin(CurlUrl url) => $"{url.Scheme}://{UserInformation(url)}{HostAndPort(url)}";

    private static string UserInformation(CurlUrl url)
    {
        if (url.User is null)
        {
            return string.Empty;
        }

        return url.Password is null ? url.User + "@" : $"{url.User}:{url.Password}@";
    }

    private static void AppendEncoded(StringBuilder encoded, Rune rune)
    {
        if (rune.Value <= '~')
        {
            encoded.Append((char)rune.Value);
            return;
        }

        Span<byte> octets = stackalloc byte[4];
        foreach (byte octet in octets[..rune.EncodeToUtf8(octets)])
        {
            encoded.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
        }
    }
}
