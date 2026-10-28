using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Checks and normalises the host of a URL as curl 8.21.0's <c>parse_authority</c> does:
/// an IPv6 literal in brackets, an IPv4 address in any of the forms <c>inet_aton</c>
/// reads, or a name.
/// </summary>
internal static class CurlUrlHost
{
    /// <summary>
    /// The characters curl 8.21.0 refuses in a host name after percent-decoding, besides
    /// space and the control characters below it; measured one by one with
    /// <c>http://a%XXb/</c>.
    /// </summary>
    private const string CharactersRejectedInName = "!\"#$%&'()*+,/:;<=>?@[\\]^`{|}";

    /// <summary>
    /// A dot and the characters IDNA (UTS #46, transitional) maps to nothing; see
    /// <see cref="IsMappedToNothingOrDot" />.
    /// </summary>
    private const string CharactersMappedToNothingOrDot = "." + CurlUrlHostNameMapping.CharactersMappedToNothing;

    /// <summary>The longest zone id curl keeps; a longer one is rejected.</summary>
    private const int MaximumZoneIdLength = 15;

    private const string IPv6Characters = "0123456789abcdefABCDEF:.";

    /// <summary>The longest label, in characters once converted, IDNA accepts.</summary>
    private const int MaximumLabelLength = 63;

    /// <summary>
    /// The characters Windows-1252 gives the bytes <c>0x80</c> to <c>0x9F</c>, <c>U+FFFF</c>
    /// for the five it leaves undefined (<c>0x81</c>, <c>0x8D</c>, <c>0x8F</c>, <c>0x90</c>, <c>0x9D</c>).
    /// </summary>
    private const string Windows1252Characters80To9F =
        "€￿‚ƒ„…†‡ˆ‰Š‹Œ￿Ž￿"
        + "￿‘’“”•–—˜™š›œ￿žŸ";

    /// <summary>The characters IDNA reads as a label separator: the dot and the ideographic and full-width ones.</summary>
    private static readonly char[] LabelSeparators = ['.', '。', '．', '｡'];

    /// <summary>
    /// Whether a byte of a host name that is not UTF-8 is read as Windows-1252, as the Windows
    /// curl 8.22.0 build's <c>idn2_lookup_ul</c> reads the name in the ANSI code page (CGR-059).
    /// </summary>
    private static readonly bool StrayBytesAsWindows1252 = OperatingSystem.IsWindows();

    /// <summary>
    /// Normalises <paramref name="host" />, or returns <see langword="false" /> when curl
    /// rejects it, with <paramref name="rejection" /> saying why. With
    /// <paramref name="refuseUnconvertibleName" />, as for the URL of a transfer, a name
    /// IDNA cannot convert is <see cref="CurlUrlRejection.IdnConversionFailed" />;
    /// without it the name is kept as decoded and resolved as written.
    /// </summary>
    public static bool TryNormalize(
        string host,
        bool refuseUnconvertibleName,
        out string normalized,
        out string idnHost,
        out string? zoneId,
        out CurlUrlRejection rejection) =>
        TryNormalizeReadingStrayBytes(host, refuseUnconvertibleName, StrayBytesAsWindows1252, out normalized, out idnHost, out zoneId, out rejection);

    /// <summary>
    /// Normalises <paramref name="host" /> as <see cref="TryNormalize(string, bool, out string, out string, out string?, out CurlUrlRejection)" />
    /// does, reading a byte that is not UTF-8 as Windows-1252 when <paramref name="strayBytesAsWindows1252" />
    /// is set, so both platforms' rules can be tested on either.
    /// </summary>
    internal static bool TryNormalizeReadingStrayBytes(
        string host,
        bool refuseUnconvertibleName,
        bool strayBytesAsWindows1252,
        out string normalized,
        out string idnHost,
        out string? zoneId,
        out CurlUrlRejection rejection)
    {
        zoneId = null;
        normalized = idnHost = string.Empty;
        string? decoded = PercentDecode(host);
        if (decoded is null)
        {
            rejection = CurlUrlRejection.BadHostname;

            return false;
        }

        if (decoded.StartsWith('['))
        {
            rejection = NormalizeIPv6(decoded, out normalized, out idnHost, out zoneId);

            return rejection == CurlUrlRejection.None;
        }

        rejection = NormalizeAddressOrName(decoded, refuseUnconvertibleName, strayBytesAsWindows1252, out normalized, out idnHost);

        return rejection == CurlUrlRejection.None;
    }

    /// <summary>
    /// Normalises a percent-decoded host that is not bracketed: an IPv4 address in any form
    /// <c>inet_aton</c> reads, or else a name. One trailing dot is allowed; an empty host
    /// or two trailing dots are not. curl 8.22.0's <c>parse_authority</c> decodes the host
    /// before <c>ipv4_normalize</c>, so <c>127.0.0.%31.</c> is <c>127.0.0.1</c> (lib1560).
    /// </summary>
    /// <returns>Why curl rejects the host, or <see cref="CurlUrlRejection.None" />.</returns>
    private static CurlUrlRejection NormalizeAddressOrName(
        string host,
        bool refuseUnconvertibleName,
        bool strayBytesAsWindows1252,
        out string normalized,
        out string idnHost)
    {
        idnHost = normalized = string.Empty;
        string withoutTrailingDot = host.EndsWith('.') ? host[..^1] : host;
        if (withoutTrailingDot.Length == 0 || withoutTrailingDot.EndsWith('.'))
        {
            return CurlUrlRejection.BadHostname;
        }

        if (CurlUrlIPv4Address.TryNormalize(withoutTrailingDot, out string address))
        {
            idnHost = normalized = address;

            return CurlUrlRejection.None;
        }

        return NormalizeName(host, refuseUnconvertibleName, strayBytesAsWindows1252, out normalized, out idnHost);
    }

    /// <summary>
    /// Checks a name and converts it to punycode. A name IDNA cannot convert is
    /// <see cref="CurlUrlRejection.IdnConversionFailed" /> when <paramref name="refuseUnconvertibleName" />
    /// is set, which curl 8.22.0 fails with exit 3 and <c>URL using bad/illegal format or missing URL</c>
    /// rather than a <c>URL rejected:</c> text, as its IDN conversion runs after the URL parsed.
    /// </summary>
    private static CurlUrlRejection NormalizeName(
        string decoded,
        bool refuseUnconvertibleName,
        bool strayBytesAsWindows1252,
        out string normalized,
        out string idnHost)
    {
        normalized = idnHost = string.Empty;
        if (!IsUsableName(decoded))
        {
            return CurlUrlRejection.BadHostname;
        }

        string? converted = Ascii.IsValid(decoded) ? decoded : ToPunycode(decoded, strayBytesAsWindows1252);
        if (converted is null && refuseUnconvertibleName)
        {
            return CurlUrlRejection.IdnConversionFailed;
        }

        normalized = decoded;
        idnHost = converted ?? decoded;

        return CurlUrlRejection.None;
    }

    private static bool IsUsableName(string decoded) =>
        !decoded.Any(IsRejectedInName) && !decoded.All(IsMappedToNothingOrDot);

    /// <summary>
    /// Percent-decodes a host name as <see cref="UrlByteText" /> reads bytes, leaving a <c>%</c> that does not start a
    /// valid escape in place, or returns <see langword="null" /> when a decoded byte is a
    /// control character.
    /// </summary>
    private static string? PercentDecode(string text)
    {
        byte[] encoded = UrlByteText.ToBytes(text);
        var decoded = new List<byte>(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            byte value = encoded[index];
            if (value == '%' && TryReadEscape(encoded, index + 1, out byte escaped))
            {
                value = escaped;
                index += 2;
            }

            decoded.Add(value);
        }

        return decoded.Any(value => value < 0x20) ? null : UrlByteText.ToText([.. decoded]);
    }

    private static bool TryReadEscape(byte[] bytes, int start, out byte value)
    {
        value = 0;

        return bytes.Length - start >= 2
            && byte.TryParse(bytes.AsSpan(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsRejectedInName(char character) =>
        character <= ' ' || CharactersRejectedInName.Contains(character);

    /// <summary>
    /// Whether IDNA (UTS #46, transitional) maps <paramref name="character" /> to nothing -
    /// soft hyphen, combining grapheme joiner, Mongolian variation selectors, zero-width
    /// space, non-joiner and joiner, word joiner, variation selectors and the byte order
    /// mark - or it is a dot. A name made only of these converts to an empty name, which
    /// curl 8.21.0 rejects with exit 3 (upstream test 763).
    /// </summary>
    private static bool IsMappedToNothingOrDot(char character) =>
        CharactersMappedToNothingOrDot.Contains(character);

    /// <summary>
    /// Maps a host name that is not ASCII as UTS #46 does (<see cref="CurlUrlHostNameMapping" />,
    /// so <c>BÜCHER</c> is <c>xn--bcher-kva</c> and <c>Ａb</c> is <c>ab</c>) and converts it to
    /// punycode label by label as libidn2 does for curl 8.22.0, or returns <see langword="null" />
    /// when a label cannot be converted (<see cref="ToAsciiLabel" />). Under
    /// <paramref name="strayBytesAsWindows1252" /> a byte that is not UTF-8 is first read as
    /// Windows-1252 (<c>a%FFb</c> is <c>xn--ab-6ka</c>); otherwise, as in a UTF-8 locale, it
    /// makes the name unconvertible (<c>invalid-utf8-%e2%90.local</c>, upstream test 1034).
    /// curl fails a transfer to such a host with exit 3, while its URL API keeps the name (ADR-0734).
    /// </summary>
    private static string? ToPunycode(string name, bool strayBytesAsWindows1252)
    {
        string mapped = CurlUrlHostNameMapping.Map(strayBytesAsWindows1252 ? ReadStrayBytesAsWindows1252(name) : name);
        if (Ascii.IsValid(mapped))
        {
            return mapped;
        }

        string?[] labels = [.. mapped.Split(LabelSeparators).Select(ToAsciiLabel)];

        return labels.Contains(null) ? null : string.Join('.', labels);
    }

    /// <summary>
    /// Converts one label as libidn2 does for curl 8.22.0 (CGR-059): an empty label stays empty
    /// (<c>ü..example</c> is <c>xn--tda..example</c>); a label that starts or ends with a hyphen,
    /// or has two at its third and fourth characters without being an <c>xn--</c> label, is refused
    /// (<c>-ü</c>, <c>ab--c</c>), ASCII or not; bidirectional text is not checked
    /// (<c>aא</c> is <c>xn--a-0hc</c>); and a label with a lone surrogate (a byte that is not
    /// UTF-8), a control character (<c>U+0090</c>, upstream test 1034) or longer than 63 characters
    /// once converted (upstream test 1035) is refused.
    /// </summary>
    /// <returns>The ASCII label, or <see langword="null" /> when it cannot be converted.</returns>
    private static string? ToAsciiLabel(string label)
    {
        if (HasMisplacedHyphen(label))
        {
            return null;
        }

        string? converted = Ascii.IsValid(label) ? label : EncodeLabel(label);

        return converted?.Length > MaximumLabelLength ? null : converted;
    }

    private static bool HasMisplacedHyphen(string label) =>
        label.StartsWith('-')
        || label.EndsWith('-')
        || (label.Length > 3 && label.AsSpan(2, 2) is "--" && !label.StartsWith("xn--", StringComparison.OrdinalIgnoreCase));

    private static string? EncodeLabel(string label)
    {
        var codePoints = new List<int>(label.Length);
        ReadOnlySpan<char> rest = label;
        while (!rest.IsEmpty && codePoints.Count <= MaximumLabelLength)
        {
            if (Rune.DecodeFromUtf16(rest, out Rune rune, out int consumed) != OperationStatus.Done || Rune.IsControl(rune))
            {
                return null;
            }

            codePoints.Add(rune.Value);
            rest = rest[consumed..];
        }

        return codePoints.Count > MaximumLabelLength ? null : "xn--" + CurlUrlPunycode.Encode(codePoints);
    }

    /// <summary>
    /// Replaces each lone <c>U+DC80</c> to <c>U+DCFF</c> (a byte that is not UTF-8, as
    /// <see cref="UrlByteText" /> carries it) with the Windows-1252 character of that byte,
    /// keeping it when Windows-1252 leaves the byte undefined. A low surrogate after a high one
    /// is part of a character and stays.
    /// </summary>
    private static string ReadStrayBytesAsWindows1252(string name)
    {
        var text = new StringBuilder(name.Length);
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            bool stray = character is >= '\uDC80' and <= '\uDCFF' && (index == 0 || !char.IsHighSurrogate(name[index - 1]));
            text.Append(stray ? Windows1252CharacterOf(character) : character);
        }

        return text.ToString();
    }

    private static char Windows1252CharacterOf(char strayByte)
    {
        int value = strayByte - 0xDC00;
        char mapped = value >= 0xA0 ? (char)value : Windows1252Characters80To9F[value - 0x80];

        return mapped == '￿' ? strayByte : mapped;
    }

    /// <summary>
    /// Checks and normalises a bracketed IPv6 address as curl's <c>ipv6_parse</c> does:
    /// only hexadecimal digits, colons and dots, then optionally <c>%</c> (a decoded <c>%25</c>)
    /// and a zone id of 1 to 15 characters, and the address printed back in its shortest
    /// form when that is no longer than the address as written (zone id aside), so
    /// <c>[::FFFF:7f00:0001]</c> is <c>[::ffff:127.0.0.1]</c> but <c>[::ffff:7f00:1]</c> stays
    /// as written (CGR-058). A zone id longer than 15 characters is a bad host name to curl; every other
    /// fault is a bad IPv6 address.
    /// </summary>
    /// <returns>Why curl rejects the address, or <see cref="CurlUrlRejection.None" />.</returns>
    private static CurlUrlRejection NormalizeIPv6(string host, out string normalized, out string idnHost, out string? zoneId)
    {
        normalized = idnHost = string.Empty;
        CurlUrlRejection rejection = SplitBracketedAddress(host, out string inner, out zoneId);
        if (rejection != CurlUrlRejection.None)
        {
            return rejection;
        }

        if (!IPAddress.TryParse(inner, out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return CurlUrlRejection.BadIPv6;
        }

        string shortest = address.ToString();
        string text = shortest.Length <= inner.Length ? shortest : inner;
        normalized = $"[{text}]";
        idnHost = zoneId is null ? text : $"{text}%{zoneId}";

        return CurlUrlRejection.None;
    }

    /// <summary>
    /// Splits a bracketed host into the address and its zone id. curl 8.22.0's <c>ipv6_parse</c>
    /// refuses a host with no closing bracket (unit1675: <c>[::1</c>).
    /// </summary>
    /// <returns>Why curl rejects the host, or <see cref="CurlUrlRejection.None" />.</returns>
    private static CurlUrlRejection SplitBracketedAddress(string host, out string address, out string? zoneId)
    {
        address = string.Empty;
        zoneId = null;
        if (host.Length < 2 || host[^1] != ']')
        {
            return CurlUrlRejection.BadIPv6;
        }

        address = host[1..^1];
        int addressLength = address.AsSpan().IndexOfAnyExcept(IPv6Characters);
        if (addressLength < 0)
        {
            return CurlUrlRejection.None;
        }

        CurlUrlRejection zoneRejection = ReadZoneId(address[addressLength..], out zoneId);
        address = address[..addressLength];
        return zoneRejection;
    }

    /// <summary>
    /// Reads the zone id from the text after an IPv6 address, which starts with
    /// <c>%</c>; the host was percent-decoded first, so <c>%2525</c> is the zone id <c>25</c>.
    /// </summary>
    /// <returns>Why curl rejects the zone id, or <see cref="CurlUrlRejection.None" />.</returns>
    private static CurlUrlRejection ReadZoneId(string text, out string? zoneId)
    {
        zoneId = null;
        if (!text.StartsWith('%'))
        {
            return CurlUrlRejection.BadIPv6;
        }

        string id = text[1..];
        if (id.Length == 0)
        {
            return CurlUrlRejection.BadIPv6;
        }

        if (id.Length > MaximumZoneIdLength)
        {
            return CurlUrlRejection.BadHostname;
        }

        zoneId = id;

        return CurlUrlRejection.None;
    }
}
