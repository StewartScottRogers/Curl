using System.Globalization;
using Curl.Api.Options.Network;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Normalises a transfer's URL into the one curl 8.22.0 prints for <c>%{url_effective}</c>:
/// the scheme in lower case, the user info and host name as typed, an IP address and the port
/// as curl holds them, and the path as curl sends it.
/// </summary>
/// <remarks>
/// Measured on Windows with curl 8.21.0 on 2026-09-27 (BL-371 and BL-444 Notes):
/// <c>HTTP://LocalHost:1</c> prints <c>http://LocalHost:1/</c>, <c>http://localhost:1/a/../b</c>
/// prints <c>http://localhost:1/b</c> (<c>/a/../b</c> under <c>--path-as-is</c>),
/// <c>http://localhost:1?q=1</c> prints <c>http://localhost:1/?q=1</c>, the query and fragment
/// keep their dot segments, <c>file://localhost/C:/x</c> prints <c>file://C:/x</c>, and a URL
/// curl rejects, such as <c>http://local host</c>, prints as it is. Measured with curl 8.22.0 on
/// 2026-10-22 (BL-3173 Notes): <c>http://0x7f.1:09/</c> prints <c>http://127.0.0.1:9/</c>,
/// <c>http://127.0.0.1.:9/</c> drops the dot, <c>http://h:/</c> drops the empty port,
/// <c>[0:0:0:0:0:0:0:1]</c> prints <c>[::1]</c>, and a zone id prints as <c>%25</c> and the zone id decoded once (CGR-193), so <c>[::1%lo]</c> prints <c>[::1%25lo]</c>.
/// </remarks>
internal static class UrlEffective
{
    private const string FileScheme = "file";

    private static readonly char[] AuthorityEnds = ['/', '\\', '?', '#'];

    private static readonly char[] Slashes = ['/', '\\'];

    /// <summary>
    /// Rebuilds <paramref name="url" /> from its lower-case scheme, its normalised authority, and
    /// its parsed path, query and fragment.
    /// </summary>
    /// <param name="url">The URL transferred, with its query.</param>
    /// <param name="pathAsIs">Whether <c>--path-as-is</c> keeps the path's dot segments.</param>
    /// <returns>The URL <c>%{url_effective}</c> prints; <paramref name="url" /> when curl rejects it.</returns>
    internal static string Normalize(string url, bool pathAsIs) => Normalize(url, url, pathAsIs);

    /// <summary>
    /// Rebuilds <paramref name="url" /> as <see cref="Normalize(string, bool)" /> does, or returns
    /// <paramref name="typedUrl" /> when curl rejects it, as curl prints a rejected URL as it was
    /// given, before any scheme was guessed for it.
    /// </summary>
    /// <param name="url">The URL transferred, with its guessed scheme and its query.</param>
    /// <param name="typedUrl">The URL as it was given, with its query.</param>
    /// <param name="pathAsIs">Whether <c>--path-as-is</c> keeps the path's dot segments.</param>
    /// <returns>The URL <c>%{url_effective}</c> prints.</returns>
    internal static string Normalize(string url, string typedUrl, bool pathAsIs)
    {
        if (!CurlUrl.TryParse(url, pathAsIs, out CurlUrl? parsed))
        {
            return typedUrl;
        }

        string authority = parsed.Scheme == FileScheme ? string.Empty : Authority(TypedAuthority(url, parsed.Scheme), parsed);
        string query = parsed.Query is null ? string.Empty : "?" + parsed.Query;
        string fragment = parsed.Fragment is null ? string.Empty : "#" + parsed.Fragment;

        return parsed.Scheme + "://" + authority + parsed.AbsolutePath + query + fragment;
    }

    /// <summary>
    /// Returns the URL set with <c>CURLOPT_URL</c>, normalised, in place of <paramref name="effectiveUrl" /> when that
    /// is the URL <c>CURLOPT_PORT</c> wrote its port into and the transfer followed no redirect: libcurl 8.22.0 fixes the
    /// effective URL before <c>parse_remote_port</c> sets the port on its URL handle, so the port is not shown (report 23,
    /// BL-3194). A URL from <c>CURLOPT_CURLU</c> keeps the port, as upstream test1906 shows.
    /// </summary>
    /// <param name="effectiveUrl">The normalised effective URL.</param>
    /// <param name="transferUrl">The URL the perform transferred, with <c>CURLOPT_PORT</c> written in.</param>
    /// <param name="network">The handle's network options, as set.</param>
    /// <returns>The effective URL <c>CURLINFO_EFFECTIVE_URL</c> answers.</returns>
    internal static string WithoutPortOverride(string effectiveUrl, string transferUrl, EasyNetworkOptions network) =>
        network is { Port: > 0, UrlHandle: null, Url: { } setUrl } && effectiveUrl == Normalize(transferUrl, network.PathAsIs)
            ? Normalize(setUrl, network.PathAsIs)
            : effectiveUrl;

    /// <summary>
    /// Returns the authority of <paramref name="url" /> as typed: the text after its scheme,
    /// when it was typed, and the slashes after that, up to the path, query or fragment.
    /// </summary>
    private static string TypedAuthority(string url, string scheme)
    {
        int start = url.StartsWith(scheme + ":", StringComparison.OrdinalIgnoreCase) ? scheme.Length + 1 : 0;
        string rest = url[start..].TrimStart(Slashes);
        int end = rest.IndexOfAny(AuthorityEnds);

        return end < 0 ? rest : rest[..end];
    }

    /// <summary>
    /// Returns <paramref name="typed" /> with its user info as typed, its host as
    /// <see cref="Host" /> gives it, and its port in decimal, or none when it was typed empty.
    /// </summary>
    private static string Authority(string typed, CurlUrl parsed)
    {
        int at = typed.LastIndexOf('@');
        string userInfo = typed[..(at + 1)];
        string hostAndPort = typed[(at + 1)..];
        int portStart = hostAndPort.LastIndexOf(':');
        if (portStart < hostAndPort.LastIndexOf(']'))
        {
            portStart = -1;
        }

        string typedHost = portStart < 0 ? hostAndPort : hostAndPort[..portStart];
        bool portTyped = portStart >= 0 && portStart < hostAndPort.Length - 1;
        string port = portTyped ? ":" + parsed.Port.ToString(CultureInfo.InvariantCulture) : string.Empty;

        return userInfo + Host(typedHost, parsed) + port;
    }

    /// <summary>
    /// Returns the host <c>%{url_effective}</c> prints: an IPv6 address normalised with its typed
    /// zone id, an IPv4 address in dotted-quad form, and a name as typed.
    /// </summary>
    private static string Host(string typedHost, CurlUrl parsed)
    {
        if (parsed.Host.StartsWith('['))
        {
            string zone = parsed.ZoneId is null ? string.Empty : "%25" + parsed.ZoneId;

            return parsed.Host[..^1] + zone + "]";
        }

        return IsDottedQuad(parsed.Host) || typedHost.Contains('%') ? parsed.Host : typedHost;
    }

    private static bool IsDottedQuad(string host) =>
        host.Count(character => character == '.') == 3 && host.All(character => char.IsAsciiDigit(character) || character == '.');
}
