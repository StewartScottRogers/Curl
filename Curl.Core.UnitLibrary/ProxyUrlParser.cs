using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Turns proxy text - the value of <c>-x</c>/<c>--proxy</c> or of a proxy environment
/// variable - into the <see cref="ProxyEndpoint" /> curl 8.21.0 connects to, or into the
/// failure curl reports for it.
/// </summary>
/// <remarks>
/// <para>
/// The text is <c>[scheme:/[/[/]]][user[:password]@]host[:port][/path]</c>. The scheme,
/// compared without regard to case, names the <see cref="ProxyKind" />: <c>http</c> is
/// <see cref="ProxyKind.Http" /> on port 80, <c>https</c> is <see cref="ProxyKind.Https" />
/// on 443, <c>socks</c> and <c>socks4</c> are <see cref="ProxyKind.Socks4" />, and
/// <c>socks4a</c>, <c>socks5</c> and <c>socks5h</c> are the other SOCKS kinds, all on 1080.
/// Text with no scheme is the kind the caller names - <see cref="ProxyKind.Http" /> for
/// <c>-x</c> and the environment, the option's kind for <c>--socks4</c>, <c>--socks4a</c>,
/// <c>--socks5</c> and <c>--socks5-hostname</c> - on that kind's default port; a scheme in
/// the text wins over the option's kind. The user information is split at the
/// first <c>@</c> and the first <c>:</c> and percent-decoded; a malformed escape is kept
/// as written. The path, query and fragment are ignored, but for a SOCKS proxy whose host is
/// <c>localhost</c>: a path other than <c>/</c> there, percent-decoded, is the Unix domain socket the
/// proxy is reached over (<see cref="ProxyEndpoint.UnixSocketPath" />), as upstream's
/// <c>parse_proxy</c> reads it (curl-8_22_0, BL-2955).
/// </para>
/// <para>
/// Text curl cannot parse is exit 5, <see cref="CurlExitCode.CouldntResolveProxy" />, with
/// <c>Unsupported proxy syntax in '&lt;text&gt;': &lt;reason&gt;</c>; a well-formed URL with
/// another scheme is exit 7, <see cref="CurlExitCode.CouldntConnect" />, with
/// <c>Unsupported proxy scheme for '&lt;text&gt;'</c>; port 0 is kept, and the connect to it
/// fails with exit 7 and the line curl prints for that attempt (BL-3172). An empty port
/// after <c>host:</c> is the default port when the text names a scheme and a port error
/// when it does not. Every case was measured against curl 8.21.0 on 2026-09-26.
/// </para>
/// </remarks>
public static class ProxyUrlParser
{
    /// <summary>The reason curl gives for a space or control character anywhere in the text.</summary>
    public const string MalformedReason = "Malformed input to a URL function";

    /// <summary>The reason curl gives for four or more slashes after the scheme.</summary>
    public const string SlashesReason = "Unsupported number of slashes following scheme";

    /// <summary>The reason curl gives for an unclosed or invalid bracketed IPv6 address.</summary>
    public const string BadIPv6Reason = "Bad IPv6 address";

    /// <summary>The reason curl gives for a port that is not a decimal number up to 65535.</summary>
    public const string BadPortReason = "Port number was not a decimal number between 0 and 65535";

    /// <summary>The reason curl gives for an empty host.</summary>
    public const string NoHostReason = "No host present";

    /// <summary>The reason curl gives for a host holding a character no host name may hold.</summary>
    public const string BadHostnameReason = "Bad hostname";

    private static readonly FrozenDictionary<string, (ProxyKind Kind, int DefaultPort)> KindsByScheme =
        new Dictionary<string, (ProxyKind Kind, int DefaultPort)>
        {
            ["http"] = (ProxyKind.Http, 80),
            ["https"] = (ProxyKind.Https, 443),
            ["socks"] = (ProxyKind.Socks4, 1080),
            ["socks4"] = (ProxyKind.Socks4, 1080),
            ["socks4a"] = (ProxyKind.Socks4a, 1080),
            ["socks5"] = (ProxyKind.Socks5, 1080),
            ["socks5h"] = (ProxyKind.Socks5Hostname, 1080),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private const string ForbiddenHostCharacters = " \r\n\t/:#?!@{}[]\\$'\"^`*<>=;,+&()%|";

    /// <summary>Parses <paramref name="proxyText" /> into the proxy to connect to.</summary>
    /// <param name="proxyText">The proxy text; never empty, since empty text means no proxy.</param>
    /// <param name="proxy">The proxy; <see langword="null" /> when parsing failed.</param>
    /// <param name="failure">The failure; <see langword="null" /> when parsing succeeded.</param>
    /// <returns><see langword="true" /> when <paramref name="proxy" /> was produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proxyText" /> is <see langword="null" />.</exception>
    public static bool TryParse(
        string proxyText,
        [NotNullWhen(true)] out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure) =>
        TryParse(proxyText, ProxyKind.Http, out proxy, out failure);

    /// <summary>
    /// Parses <paramref name="proxyText" /> into the proxy to connect to, reading text with no
    /// scheme as a proxy of <paramref name="kindWithoutScheme" />.
    /// </summary>
    /// <param name="proxyText">The proxy text; never empty, since empty text means no proxy.</param>
    /// <param name="kindWithoutScheme">
    /// The kind of proxy the text names when it has no scheme: the kind of the option that gave it.
    /// </param>
    /// <param name="proxy">The proxy; <see langword="null" /> when parsing failed.</param>
    /// <param name="failure">The failure; <see langword="null" /> when parsing succeeded.</param>
    /// <param name="portWhenTextGivesNone">
    /// <c>CURLOPT_PROXYPORT</c>: the port used when the text names none, in place of the kind's
    /// default; 0 (the default) keeps the kind's default port.
    /// </param>
    /// <returns><see langword="true" /> when <paramref name="proxy" /> was produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proxyText" /> is <see langword="null" />.</exception>
    public static bool TryParse(
        string proxyText,
        ProxyKind kindWithoutScheme,
        [NotNullWhen(true)] out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure,
        int portWhenTextGivesNone = 0)
    {
        ArgumentNullException.ThrowIfNull(proxyText);

        proxy = null;
        string? reason = ReadParts(proxyText, out string? scheme, out (string Host, int? Port, NetworkCredential? Credential) parts, out string path);
        if (reason is not null)
        {
            failure = TransferResult.Failure(
                CurlExitCode.CouldntResolveProxy,
                $"Unsupported proxy syntax in '{proxyText}': {reason}");
            return false;
        }

        if (!TryKind(scheme, kindWithoutScheme, out (ProxyKind Kind, int DefaultPort) kind))
        {
            failure = TransferResult.Failure(CurlExitCode.CouldntConnect, $"Unsupported proxy scheme for '{proxyText}'");
            return false;
        }

        int port = parts.Port ?? (portWhenTextGivesNone > 0 ? portWhenTextGivesNone : kind.DefaultPort);
        failure = null;
        proxy = new ProxyEndpoint(kind.Kind, parts.Host, port, parts.Credential) { UnixSocketPath = UnixSocketPathOf(kind.Kind, parts.Host, path) };
        return true;
    }

    /// <summary>
    /// The Unix domain socket a SOCKS proxy at <c>localhost</c> is reached over: its URL's path,
    /// percent-decoded, unless that is empty or <c>/</c>, as upstream's <c>parse_proxy</c> takes
    /// <c>UNIX_SOCKET_PREFIX</c>'s path; <see langword="null" /> for any other proxy.
    /// </summary>
    private static string? UnixSocketPathOf(ProxyKind kind, string host, string path) =>
        kind is ProxyKind.Socks4 or ProxyKind.Socks4a or ProxyKind.Socks5 or ProxyKind.Socks5Hostname
            && string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            && path.Length > 1
            ? Uri.UnescapeDataString(path)
            : null;

    private static string? ReadParts(string text, out string? scheme, out (string Host, int? Port, NetworkCredential? Credential) parts, out string path)
    {
        parts = default;
        scheme = null;
        path = string.Empty;
        if (text.AsSpan().ContainsAnyInRange('\0', ' ') || text.Contains('\x7F', StringComparison.Ordinal))
        {
            return MalformedReason;
        }

        if (SplitScheme(text, out scheme, out string rest) is { } slashesReason)
        {
            return slashesReason;
        }

        int authorityEnd = rest.AsSpan().IndexOfAny('/', '?', '#');
        string authority = authorityEnd >= 0 ? rest[..authorityEnd] : rest;
        path = PathOf(rest, authorityEnd);
        return ReadAuthority(authority, scheme is not null, out parts);
    }

    /// <summary>
    /// Takes the scheme and the slashes after it off <paramref name="text" />, when it has one;
    /// <see cref="SlashesReason" /> for four or more slashes, else <see langword="null" />.
    /// </summary>
    private static string? SplitScheme(string text, out string? scheme, out string rest)
    {
        scheme = null;
        rest = text;
        if (!UrlSchemeGuesser.HasScheme(text))
        {
            return null;
        }

        int colon = text.IndexOf(':', StringComparison.Ordinal);
        scheme = text[..colon];
        rest = text[(colon + 1)..];
        int slashes = rest.Length - rest.TrimStart('/').Length;
        rest = rest[slashes..];
        return slashes > 3 ? SlashesReason : null;
    }

    /// <summary>The path after the authority ending at <paramref name="authorityEnd" />: up to its query or fragment.</summary>
    private static string PathOf(string rest, int authorityEnd)
    {
        string afterAuthority = authorityEnd >= 0 ? rest[authorityEnd..] : string.Empty;
        int end = afterAuthority.AsSpan().IndexOfAny('?', '#');
        return end >= 0 ? afterAuthority[..end] : afterAuthority;
    }

    private static string? ReadAuthority(string authority, bool hasScheme, out (string Host, int? Port, NetworkCredential? Credential) parts)
    {
        parts = default;
        NetworkCredential? credential = null;
        int at = authority.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0)
        {
            credential = ReadCredential(authority[..at]);
            authority = authority[(at + 1)..];
        }

        int? port = null;
        string? reason = SplitHostAndPort(authority, out string host, out string? portText)
            ?? ReadPort(portText, hasScheme, out port);
        reason ??= host.Length == 0 ? NoHostReason : null;
        reason ??= CheckHost(ref host);
        parts = (host, port, credential);
        return reason;
    }

    private static NetworkCredential? ReadCredential(string userInformation)
    {
        if (userInformation.Length == 0)
        {
            return null;
        }

        int colon = userInformation.IndexOf(':', StringComparison.Ordinal);
        string user = colon >= 0 ? userInformation[..colon] : userInformation;
        string password = colon >= 0 ? userInformation[(colon + 1)..] : string.Empty;
        return new NetworkCredential(Uri.UnescapeDataString(user), Uri.UnescapeDataString(password));
    }

    private static string? SplitHostAndPort(string authority, out string host, out string? portText)
    {
        host = authority;
        portText = null;
        if (authority.StartsWith('['))
        {
            return SplitBracketedHost(authority, out host, out portText);
        }

        int colon = authority.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            host = authority[..colon];
            portText = authority[(colon + 1)..];
        }

        return null;
    }

    private static string? SplitBracketedHost(string authority, out string host, out string? portText)
    {
        host = string.Empty;
        portText = null;
        int close = authority.IndexOf(']', StringComparison.Ordinal);
        if (close < 0 || !IsIPv6(authority[1..close], out host))
        {
            return BadIPv6Reason;
        }

        string after = authority[(close + 1)..];
        if (after.Length == 0)
        {
            return null;
        }

        portText = after[1..];
        return after[0] == ':' ? null : BadPortReason;
    }

    private static bool IsIPv6(string bracketed, out string address)
    {
        int zone = bracketed.IndexOf('%', StringComparison.Ordinal);
        address = zone >= 0 ? bracketed[..zone] : bracketed;
        return IPAddress.TryParse(address, out IPAddress? parsed)
            && parsed.AddressFamily == AddressFamily.InterNetworkV6;
    }

    private static string? ReadPort(string? portText, bool hasScheme, out int? port)
    {
        port = null;
        if (portText is null || (portText.Length == 0 && hasScheme))
        {
            return null;
        }

        if (!TryParsePort(portText, out int value))
        {
            return BadPortReason;
        }

        port = value;
        return null;
    }

    private static bool TryParsePort(string portText, out int port) =>
        int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= 65535;

    private static string? CheckHost(ref string host)
    {
        // Only a bracketed IPv6 address, already validated, can hold a colon here.
        if (host.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        host = Uri.UnescapeDataString(host);
        if (IPAddress.TryParse(host, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork)
        {
            // curl reads the host as a URL's, so IPv4 shorthand becomes a dotted quad: -x
            // http://127.1:8080 dials 127.0.0.1, which the Windows resolver needs (BL-3575, ADR-0820).
            host = address.ToString();
        }

        return host.AsSpan().ContainsAny(ForbiddenHostCharacters) ? BadHostnameReason : null;
    }

    private static bool TryKind(string? scheme, ProxyKind kindWithoutScheme, out (ProxyKind Kind, int DefaultPort) kind)
    {
        kind = (kindWithoutScheme, DefaultPort(kindWithoutScheme));
        return scheme is null || KindsByScheme.TryGetValue(scheme, out kind);
    }

    private static int DefaultPort(ProxyKind kind) => kind switch
    {
        ProxyKind.Http or ProxyKind.Http10 => 80,
        ProxyKind.Https => 443,
        _ => 1080,
    };
}
