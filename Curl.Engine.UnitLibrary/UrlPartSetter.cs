using System.Buffers;
using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Sets and clears one part of a URL handle other than the whole URL, as upstream curl 8.22.0's
/// <c>curl_url_set</c> does in <c>lib/urlapi.c</c> (ADR-0602): the parts it checks (scheme, host,
/// port) refused with upstream's code, the text encoded under <c>CURLU_URLENCODE</c> or else its
/// percent escapes upper cased, and the query appended to under <c>CURLU_APPENDQUERY</c>.
/// </summary>
internal static class UrlPartSetter
{
    /// <summary>The longest scheme upstream accepts (<c>MAX_SCHEME_LEN</c>).</summary>
    private const int MaximumSchemeLength = 40;

    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>The bytes upstream's set leaves as they are under <c>CURLU_URLENCODE</c> (<c>ISUNRESERVED</c>).</summary>
    private static readonly SearchValues<byte> Unreserved =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~"u8);

    /// <summary>
    /// The bytes besides the unreserved ones a path set under <c>CURLU_URLENCODE</c> keeps as they are
    /// (upstream's <c>allowed_in_path</c>).
    /// </summary>
    private static readonly SearchValues<byte> AllowedInPath = SearchValues.Create("!$&'(){}[]*+,;=:@/"u8);

    /// <summary>The characters a host name that is not an IPv6 address may not hold, as they end it in a URL.</summary>
    private static readonly SearchValues<char> HostDelimiters = SearchValues.Create("/?#@:");

    /// <summary>What clearing each <see cref="CurlUrlPart" /> leaves, by its number.</summary>
    private static readonly Func<UrlHandleParts, UrlHandleParts>[] Clearers =
    [
        _ => UrlHandleParts.Empty,
        parts => parts with { Scheme = null, SchemeGuessed = false },
        parts => parts with { User = null },
        parts => parts with { Password = null },
        parts => parts with { Options = null },
        parts => parts with { Host = null },
        parts => parts with { Port = null },
        parts => parts with { Path = null },
        parts => parts with { Query = null },
        parts => parts with { Fragment = null },
        parts => parts with { ZoneId = null },
    ];

    /// <summary>The setter of each <see cref="CurlUrlPart" /> after <see cref="CurlUrlPart.Url" />, by its number less one.</summary>
    private static readonly Func<UrlHandleParts, string, CurlUrlFlags, UrlSetResult>[] Setters =
    [
        SetScheme,
        (parts, value, flags) => UrlSetResult.Set(parts with { User = Stored(value, flags) }),
        (parts, value, flags) => UrlSetResult.Set(parts with { Password = Stored(value, flags) }),
        (parts, value, flags) => UrlSetResult.Set(parts with { Options = Stored(value, flags) }),
        SetHost,
        (parts, value, _) => SetPort(parts, value),
        (parts, value, flags) => UrlSetResult.Set(parts with { Path = WithLeadingSlash(Stored(value, flags, inPath: true)) }),
        SetQuery,
        (parts, value, flags) => UrlSetResult.Set(parts with { Fragment = Stored(value, flags) }),
        (parts, value, flags) => UrlSetResult.Set(parts with { ZoneId = Stored(value, flags) }),
    ];

    /// <summary>The parts with <paramref name="part" /> cleared, as <c>curl_url_set</c> with a null value does.</summary>
    /// <param name="parts">The handle's parts.</param>
    /// <param name="part">The part, one upstream defines; <see cref="CurlUrlPart.Url" /> clears them all.</param>
    /// <returns>The new parts.</returns>
    public static UrlHandleParts Cleared(UrlHandleParts parts, CurlUrlPart part) => Clearers[(int)part](parts);

    /// <summary>Sets <paramref name="part" /> to <paramref name="value" />, as <c>curl_url_set</c> does.</summary>
    /// <param name="parts">The handle's parts.</param>
    /// <param name="part">The part, one upstream defines other than <see cref="CurlUrlPart.Url" />.</param>
    /// <param name="value">The part's new text.</param>
    /// <param name="flags">The set's flags.</param>
    /// <returns>The new parts, or the <see cref="CurlUrlCode" /> that refuses the value.</returns>
    public static UrlSetResult Set(UrlHandleParts parts, CurlUrlPart part, string value, CurlUrlFlags flags) =>
        Setters[(int)part - 1](parts, value, flags);

    /// <summary>
    /// A scheme: 1 to 40 characters, one libcurl supports unless <c>CURLU_NON_SUPPORT_SCHEME</c>, a
    /// letter then letters, digits, <c>+</c>, <c>-</c> or <c>.</c>; checked in upstream's order.
    /// </summary>
    private static UrlSetResult SetScheme(UrlHandleParts parts, string value, CurlUrlFlags flags)
    {
        string scheme = value.ToLowerInvariant();
        CurlUrlCode code = SchemeCode(value, scheme, flags);

        return code == CurlUrlCode.Ok
            ? UrlSetResult.Set(parts with { Scheme = scheme, SchemeGuessed = false })
            : UrlSetResult.Refused(code);
    }

    private static CurlUrlCode SchemeCode(string value, string scheme, CurlUrlFlags flags)
    {
        if (!HasSchemeLength(value))
        {
            return CurlUrlCode.BadScheme;
        }

        if (!flags.HasFlag(CurlUrlFlags.NonSupportScheme) && !UrlHandleParts.IsSupportedScheme(scheme))
        {
            return CurlUrlCode.UnsupportedScheme;
        }

        return IsSchemeText(value) ? CurlUrlCode.Ok : CurlUrlCode.BadScheme;
    }

    private static bool HasSchemeLength(string value) => value.Length is > 0 and <= MaximumSchemeLength;

    /// <summary>RFC 3986's scheme: a letter, then letters, digits, <c>+</c>, <c>-</c> or <c>.</c>.</summary>
    private static bool IsSchemeText(string value) => char.IsAsciiLetter(value[0]) && value.All(IsSchemeCharacter);

    /// <summary>A path always starts with <c>/</c>: upstream adds one when the text set has none.</summary>
    private static string WithLeadingSlash(string path) => path.StartsWith('/') ? path : "/" + path;

    private static bool IsSchemeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.';

    /// <summary>
    /// A host, which clears the zone id: empty only under <c>CURLU_NO_AUTHORITY</c>; encoded under
    /// <c>CURLU_URLENCODE</c>; else a name or bracketed IPv6 address Curl's URL parser accepts, its
    /// zone id kept apart, or <see cref="CurlUrlCode.BadHostname" />.
    /// </summary>
    private static UrlSetResult SetHost(UrlHandleParts parts, string value, CurlUrlFlags flags)
    {
        string host = Stored(value, flags);
        if (host.Length == 0)
        {
            return flags.HasFlag(CurlUrlFlags.NoAuthority)
                ? UrlSetResult.Set(parts with { Host = host, ZoneId = null })
                : UrlSetResult.Refused(CurlUrlCode.BadHostname);
        }

        return flags.HasFlag(CurlUrlFlags.UrlEncode)
            ? UrlSetResult.Set(parts with { Host = host, ZoneId = null })
            : CheckedHost(parts, host);
    }

    private static UrlSetResult CheckedHost(UrlHandleParts parts, string host)
    {
        bool delimited = host.StartsWith('[')
            ? host.AsSpan().IndexOfAny('/', '?', '#') >= 0
            : host.AsSpan().IndexOfAny(HostDelimiters) >= 0;
        if (delimited || !CurlUrl.TryParse("http://" + host + "/", pathAsIs: true, out CurlUrl? url))
        {
            return UrlSetResult.Refused(CurlUrlCode.BadHostname);
        }

        return UrlSetResult.Set(parts with { Host = url.Host, ZoneId = url.ZoneId });
    }

    /// <summary>A port: decimal digits up to 65535 and nothing else, kept without leading zeros.</summary>
    private static UrlSetResult SetPort(UrlHandleParts parts, string value)
    {
        if (value.Length > 0
            && value.All(char.IsAsciiDigit)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int port)
            && port <= ushort.MaxValue)
        {
            return UrlSetResult.Set(parts with { Port = port });
        }

        return UrlSetResult.Refused(CurlUrlCode.BadPortNumber);
    }

    /// <summary>
    /// A query: under <c>CURLU_URLENCODE</c> a space is <c>+</c>, and under <c>CURLU_APPENDQUERY</c> the
    /// first <c>=</c> stays and the text is joined to a query already set with one <c>&amp;</c>.
    /// </summary>
    private static UrlSetResult SetQuery(UrlHandleParts parts, string value, CurlUrlFlags flags)
    {
        bool append = flags.HasFlag(CurlUrlFlags.AppendQuery);
        string query = Stored(value, flags, plusForSpace: true, keepFirstEquals: append);
        string? existing = parts.Query;
        if (append && existing is { Length: > 0 })
        {
            query = existing + (existing.EndsWith('&') ? string.Empty : "&") + query;
        }

        return UrlSetResult.Set(parts with { Query = query });
    }

    /// <summary>
    /// The text a set stores: under <c>CURLU_URLENCODE</c> every UTF-8 byte but the unreserved ones
    /// (and, in a path, the ones <see cref="AllowedInPath" /> holds, or the first <c>=</c> where asked)
    /// as an upper-case <c>%XX</c>, a space as <c>+</c> where asked; else the text with the hex digits
    /// of its percent escapes upper cased.
    /// </summary>
    private static string Stored(
        string value,
        CurlUrlFlags flags,
        bool inPath = false,
        bool plusForSpace = false,
        bool keepFirstEquals = false) =>
        flags.HasFlag(CurlUrlFlags.UrlEncode)
            ? Encoded(value, inPath, plusForSpace, keepFirstEquals)
            : UpperCasedEscapes(value);

    private static string Encoded(string value, bool inPath, bool plusForSpace, bool keepFirstEquals)
    {
        StringBuilder encoded = new();
        foreach (byte character in Encoding.UTF8.GetBytes(value))
        {
            bool kept = Unreserved.Contains(character) || (inPath && AllowedInPath.Contains(character)) || (character == '=' && keepFirstEquals);
            keepFirstEquals &= character != '=';
            encoded.Append(EncodedByte(character, kept, plusForSpace));
        }

        return encoded.ToString();
    }

    private static string EncodedByte(byte character, bool kept, bool plusForSpace)
    {
        if (character == ' ' && plusForSpace)
        {
            return "+";
        }

        return kept ? ((char)character).ToString() : $"%{HexDigits[character >> 4]}{HexDigits[character & 0xf]}";
    }

    private static string UpperCasedEscapes(string value)
    {
        char[] text = value.ToCharArray();
        for (int index = value.IndexOf('%'); index >= 0 && index + 2 < text.Length; index = value.IndexOf('%', index + 1))
        {
            if (char.IsAsciiHexDigit(text[index + 1]) && char.IsAsciiHexDigit(text[index + 2]))
            {
                text[index + 1] = char.ToUpperInvariant(text[index + 1]);
                text[index + 2] = char.ToUpperInvariant(text[index + 2]);
            }
        }

        return new string(text);
    }
}
