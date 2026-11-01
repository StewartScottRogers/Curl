using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Writes the parts of a transfer's <see cref="CurlUrl" /> that an HTTP request and its
/// redirects put on the wire: the request target, the host and port, and the origin.
/// </summary>
/// <remarks>
/// curl 8.21.0 was measured (BL-294 Notes) sending <c>http://h/ä?ö=1</c> as
/// <c>GET /%E4?</c>, the byte <c>0xF6</c>, <c>=1</c>: it percent-encodes each byte above <c>0x7F</c> in the path and
/// sends the query's bytes as they are. The bytes it had were its <c>argv</c>'s code page; the
/// request line takes them in <see cref="Curl.Protocol.Abstractions.HttpRequestOptions.CommandLineTextEncoding" />
/// (CGR-015, BL-3840), while a redirect's URL and the lines <c>-v</c> prints use the text's
/// UTF-8, as <see cref="CurlUrl.IdnHost" /> reads a host (ADR-0010).
/// </remarks>
internal static class HttpUrlText
{
    /// <summary>
    /// Gives the origin-form request target as the characters of a head written in Latin-1:
    /// <see cref="Path(CurlUrl, Encoding)" />, then <c>?</c> and the query when the URL has one, each of the
    /// query's bytes (<see cref="UrlByteText.ToBytes" />: its UTF-8, an argument byte that was not
    /// valid UTF-8 itself) one character, so the head carries them unchanged.
    /// </summary>
    internal static string RequestTarget(CurlUrl url) => RequestTarget(url, Encoding.UTF8);

    /// <summary>
    /// Gives the origin-form request target from the URL's text as its bytes in
    /// <paramref name="encoding" />: <see cref="Path(CurlUrl, Encoding)" />, then <c>?</c> and the
    /// query's bytes one character each, unencoded. curl 8.22.0 on Windows, whose <c>argv</c> is
    /// the ANSI code page, sends <c>http://h/é?q=é</c> as <c>GET /%E9?q=</c> and the byte
    /// <c>0xE9</c> (measured with cp1252, CGR-015, BL-3840).
    /// </summary>
    internal static string RequestTarget(CurlUrl url, Encoding encoding) =>
        Path(url, encoding) + (url.Query is null ? string.Empty : "?" + HttpRequestHeadFormatter.WireText(url.Query, encoding));

    /// <summary>
    /// Gives the origin-form request target the request line and authentication send:
    /// <see cref="RequestTarget(CurlUrl)" /> with the path's <c>%XX</c> escapes in upper case and the
    /// query as typed, as curl 8.22.0 sends <c>/a%2fb?x=%2f</c> as <c>/a%2Fb?x=%2f</c>. The
    /// absolute form a forward proxy gets keeps the escapes as typed (ADR-0911, CGR-054).
    /// </summary>
    internal static string OriginTarget(CurlUrl url) => OriginTarget(url, Encoding.UTF8);

    /// <summary>
    /// Gives <see cref="OriginTarget(CurlUrl)" /> from the URL's text as its bytes in
    /// <paramref name="encoding" /> (<see cref="RequestTarget(CurlUrl, Encoding)" />).
    /// </summary>
    internal static string OriginTarget(CurlUrl url, Encoding encoding) => OriginTarget(url, encoding, rereadsUrl: false);

    /// <summary>
    /// Gives <see cref="OriginTarget(CurlUrl, Encoding)" />, with the query's escapes in upper case
    /// too when <paramref name="rereadsUrl" />: curl re-reads the URL for the request that answers a
    /// 401 and sends <c>/a%2fb?q=%2f</c> as <c>/a%2Fb?q=%2F</c> (measured with 8.22.0, BL-3856).
    /// </summary>
    internal static string OriginTarget(CurlUrl url, Encoding encoding, bool rereadsUrl)
    {
        string path = Path(url, encoding);
        string target = UrlPathEscapes.WithUpperCaseHexDigits(path) + RequestTarget(url, encoding)[path.Length..];
        return rereadsUrl ? UrlPathEscapes.WithUpperCaseHexDigits(target) : target;
    }

    /// <summary>
    /// Gives the path as its bytes in <paramref name="encoding" />
    /// (<see cref="HttpRequestHeadFormatter.WireText" />) with each byte above <c>~</c>
    /// percent-encoded: <c>/café</c> in cp1252 is <c>/caf%E9</c>, in UTF-8 <c>/caf%C3%A9</c>.
    /// </summary>
    internal static string Path(CurlUrl url, Encoding encoding)
    {
        StringBuilder encoded = new(url.AbsolutePath.Length);
        foreach (char value in HttpRequestHeadFormatter.WireText(url.AbsolutePath, encoding))
        {
            if (value <= '~')
            {
                encoded.Append(value);
            }
            else
            {
                encoded.Append('%').Append(((int)value).ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    /// <summary>
    /// Gives <c>?</c> and the query as written, or the empty string when the URL has no
    /// <c>?</c>: the query a relative redirect keeps.
    /// </summary>
    internal static string Query(CurlUrl url) => url.Query is null ? string.Empty : "?" + url.Query;

    /// <summary>
    /// Gives the URL as curl 8.21.0 prints it for <c>%{url_effective}</c> and names it in the
    /// <c>OPENED stream for</c> line of an HTTP/2 or HTTP/3 stream: the scheme in lower case,
    /// the authority as typed (user and password included), then the path, the query and the
    /// fragment. curl printed <c>https://EXAMPLE.com:443/?x</c> for <c>https://EXAMPLE.com:443?x</c>
    /// and kept <c>A:B@</c> and <c>#frag</c> (measured, BL-660 Notes).
    /// </summary>
    internal static string Effective(CurlUrl url)
    {
        string typed = url.OriginalString;
        int start = typed.StartsWith(url.Scheme + ":", StringComparison.OrdinalIgnoreCase) ? url.Scheme.Length + 1 : 0;
        string rest = typed[start..].TrimStart('/');
        int end = rest.IndexOfAny(['/', '?', '#']);
        string authority = end < 0 ? rest : rest[..end];
        string fragment = url.Fragment is null ? string.Empty : "#" + url.Fragment;
        return url.Scheme + "://" + authority + url.AbsolutePath + Query(url) + fragment;
    }

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
        url.Scheme is "http" or "https" ? WireHostAndPort(url) : string.Create(CultureInfo.InvariantCulture, $"{WireHost(url)}:{url.Port}");

    /// <summary>
    /// Gives <see cref="HostAndPort" /> with a host name that is not ASCII in its punycode form,
    /// <see cref="CurlUrl.IdnHost" />: the authority the <c>Host</c> line and a forward proxy's
    /// absolute-form target carry, as curl sends <c>Host: xn--4cab6c.se</c> for <c>åäö.se</c>
    /// (upstream tests 165 and 1448, BL-2861).
    /// </summary>
    internal static string WireHostAndPort(CurlUrl url) =>
        url.IsDefaultPort ? WireHost(url) : string.Create(CultureInfo.InvariantCulture, $"{WireHost(url)}:{url.Port}");

    /// <summary>
    /// Gives the host as written when it is ASCII and <see cref="CurlUrl.IdnHost" /> otherwise.
    /// </summary>
    internal static string WireHost(CurlUrl url) => Ascii.IsValid(url.Host) ? url.Host : url.IdnHost;

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
}
