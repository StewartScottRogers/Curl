using System.Diagnostics.CodeAnalysis;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// A URL parsed as curl 8.21.0's URL API parses the URL given to a transfer: the text
/// exactly as typed, and each part as curl holds it (ADR-0010).
/// </summary>
/// <remarks>
/// <para>
/// The parts follow libcurl's <c>curl_url_get</c> (<see href="https://curl.se/libcurl/c/curl_url_get.html" />)
/// and <c>lib/urlapi.c</c> at curl 8.21.0, parsed with the flags libcurl uses for a
/// transfer: a URL with no scheme has one guessed from its host, and an unknown scheme
/// is accepted here and refused later by the dispatcher.
/// </para>
/// <para>
/// The <see cref="Uri" /> members handlers read (<see cref="Scheme" />, <see cref="Host" />,
/// <see cref="IdnHost" />, <see cref="Port" />, <see cref="IsDefaultPort" />,
/// <see cref="AbsolutePath" />, <see cref="Query" />, <see cref="Fragment" /> and
/// <see cref="OriginalString" />) exist here under the same names. Where curl and
/// <see cref="Uri" /> disagree, the value is curl's: the host keeps its case, the path
/// keeps <c>%2F</c> and, when parsed with path-as-is, its dot segments, and
/// <see cref="Query" /> and <see cref="Fragment" /> do not carry their <c>?</c> or <c>#</c>.
/// </para>
/// <para>
/// Two values are equal when every part, <see cref="OriginalString" /> included, is equal.
/// </para>
/// </remarks>
public sealed record CurlUrl
{
    internal CurlUrl(
        string originalString,
        string scheme,
        CurlUrlAuthority authority,
        string absolutePath,
        string? query,
        string? fragment)
    {
        OriginalString = originalString;
        Scheme = scheme;
        User = authority.User;
        Password = authority.Password;
        Options = authority.Options;
        Host = authority.Host;
        IdnHost = authority.IdnHost;
        ZoneId = authority.ZoneId;
        int? defaultPort = CurlUrlScheme.DefaultPort(scheme);
        Port = authority.Port ?? defaultPort ?? -1;
        IsDefaultPort = authority.Port is null || authority.Port == defaultPort;
        AbsolutePath = absolutePath;
        Query = query;
        Fragment = fragment;
    }

    /// <summary>Gets the URL exactly as it was typed.</summary>
    public string OriginalString { get; }

    /// <summary>
    /// Gets the scheme in lower case, such as <c>http</c> or <c>file</c>; the guessed
    /// one when the URL was typed without a scheme.
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets the user name as written before the <c>@</c>, still percent-encoded, or
    /// <see langword="null" /> when the URL has no <c>@</c>. An empty user, as in
    /// <c>http://@host/</c> or <c>http://:secret@host/</c>, is the empty string.
    /// </summary>
    public string? User { get; }

    /// <summary>
    /// Gets the password as written after the first <c>:</c> of the user information, or
    /// <see langword="null" /> when there is no <c>:</c> there.
    /// </summary>
    public string? Password { get; }

    /// <summary>
    /// Gets the login options written after a <c>;</c> in the user information, such as
    /// <c>AUTH=PLAIN</c>, or <see langword="null" />. Only <c>imap</c>, <c>pop3</c> and
    /// <c>smtp</c> and their TLS forms have options; for any other scheme a <c>;</c> is
    /// part of the user name or password, as it is in curl.
    /// </summary>
    public string? Options { get; }

    /// <summary>
    /// Gets the host as curl holds it: a name percent-decoded and in the case it was
    /// typed, an IPv4 address in dotted-quad form (<c>0x7f.1</c> becomes
    /// <c>127.0.0.1</c>), or an IPv6 address in brackets, normalised and without its
    /// zone id. Empty for a <c>file</c> URL.
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// Gets the host to resolve: <see cref="Host" /> with a name that is not ASCII
    /// converted to punycode, or an IPv6 address without brackets and with
    /// <c>%</c> and the zone id appended when there is one.
    /// </summary>
    public string IdnHost { get; }

    /// <summary>
    /// Gets the IPv6 zone id, such as <c>eth0</c> from <c>[fe80::1%25eth0]</c>, or
    /// <see langword="null" /> when there is none.
    /// </summary>
    public string? ZoneId { get; }

    /// <summary>
    /// Gets the port written in the URL, else the scheme's default port, else -1 for a
    /// scheme that has none (<c>file</c>, and a scheme curl does not know).
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Port" /> is the scheme's default: no
    /// port was written, or the one written equals the default.
    /// </summary>
    public bool IsDefaultPort { get; }

    /// <summary>
    /// Gets the path as curl sends it: as written, with <c>\</c> turned into <c>/</c>,
    /// <c>/</c> when none was written, and dot segments removed unless the URL was parsed
    /// with path-as-is. Never percent-decoded. A <c>file</c> URL's path loses the slash
    /// before a drive letter on Windows, so <c>file:///C:/x</c> has the path <c>C:/x</c>.
    /// </summary>
    public string AbsolutePath { get; }

    /// <summary>
    /// Gets the text after the <c>?</c>, which may be empty, or <see langword="null" />
    /// when the URL has no <c>?</c>.
    /// </summary>
    public string? Query { get; }

    /// <summary>
    /// Gets the text after the <c>#</c>, which may be empty, or <see langword="null" />
    /// when the URL has no <c>#</c>.
    /// </summary>
    public string? Fragment { get; }

    /// <summary>
    /// Parses <paramref name="text" /> as curl 8.21.0 parses the URL of a transfer.
    /// </summary>
    /// <param name="text">The URL as typed.</param>
    /// <param name="pathAsIs">
    /// <see langword="true" /> to keep <c>.</c> and <c>..</c> path segments, as curl's
    /// <c>--path-as-is</c> (<c>CURLU_PATH_AS_IS</c>) does; <see langword="false" /> to
    /// remove them as RFC 3986 section 5.2.4 describes.
    /// </param>
    /// <param name="url">The parsed URL, or <see langword="null" /> when curl rejects the text.</param>
    /// <returns>
    /// <see langword="true" /> when curl accepts the text. A caller answers
    /// <see langword="false" /> as curl does: the transfer ends with
    /// <see cref="CurlExitCode.UrlMalformat" /> (3) before any handler runs.
    /// </returns>
    /// <remarks>
    /// Drive letters in <c>file</c> URLs follow the platform's curl: accepted on Windows,
    /// rejected elsewhere.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
    public static bool TryParse(string text, bool pathAsIs, [NotNullWhen(true)] out CurlUrl? url) =>
        TryParse(text, pathAsIs, OperatingSystem.IsWindows(), out url);

    /// <summary>
    /// Parses <paramref name="text" /> as <see cref="TryParse(string, bool, out CurlUrl)" />
    /// does, for text already known to be a URL curl accepts.
    /// </summary>
    /// <param name="text">The URL as typed.</param>
    /// <param name="pathAsIs">
    /// <see langword="true" /> to keep <c>.</c> and <c>..</c> path segments, as curl's
    /// <c>--path-as-is</c> does.
    /// </param>
    /// <returns>The parsed URL.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
    /// <exception cref="FormatException">curl rejects <paramref name="text" />.</exception>
    public static CurlUrl Parse(string text, bool pathAsIs = false) =>
        TryParse(text, pathAsIs, out CurlUrl? url)
            ? url
            : throw new FormatException($"curl rejects the URL \"{text}\".");

    /// <summary>
    /// Parses <paramref name="text" /> with the drive-letter rules of the chosen platform,
    /// so both can be tested on either.
    /// </summary>
    internal static bool TryParse(
        string text,
        bool pathAsIs,
        bool driveLetters,
        [NotNullWhen(true)] out CurlUrl? url)
    {
        ArgumentNullException.ThrowIfNull(text);

        url = CurlUrlParser.Parse(text, pathAsIs, driveLetters);

        return url is not null;
    }
}
