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
    /// Whether a host name that is not ASCII is converted from its bytes read as Windows-1252, every
    /// byte and not only those that are not UTF-8, as the Windows curl 8.22.0 build's
    /// <c>idn2_lookup_ul</c> reads the name in the ANSI code page (CGR-059, ADR-0939).
    /// </summary>
    private static bool HostAsWindows1252 => HostRuleChoice.Value ?? OperatingSystem.IsWindows();

    private static readonly AsyncLocal<bool?> HostRuleChoice = new();

    /// <summary>
    /// Reads every host this flow parses with the Windows-1252 rule when <paramref name="hostAsWindows1252" />
    /// is set and the UTF-8 rule otherwise, until the result is disposed, so a test can build the
    /// <see cref="CurlUrl" /> the other platform's curl reads on any platform.
    /// </summary>
    internal static IDisposable ReadingHostAs(bool hostAsWindows1252)
    {
        bool? previous = HostRuleChoice.Value;
        HostRuleChoice.Value = hostAsWindows1252;

        return new HostRuleRestorer(previous);
    }

    private sealed class HostRuleRestorer(bool? previous) : IDisposable
    {
        public void Dispose() => HostRuleChoice.Value = previous;
    }

    /// <summary>
    /// Whether a host name that is not ASCII is refused because the locale is not UTF-8: off Windows,
    /// libidn2 cannot convert such a name under <c>LC_ALL=C</c> and curl exits 3 (CGR-238).
    /// </summary>
    private static readonly bool LocaleRejectsNonAsciiName =
        LocaleRejectsNonAsciiNameOn(OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable);

    /// <summary>Whether the platform and locale make libidn2 refuse a name that is not ASCII: any non-Windows platform not in a UTF-8 locale.</summary>
    internal static bool LocaleRejectsNonAsciiNameOn(bool isWindows, Func<string, string?> getEnvironmentVariable) =>
        !isWindows && !LocaleIsUtf8(getEnvironmentVariable);

    /// <summary>
    /// Whether the locale the environment names is UTF-8: the first non-empty of <c>LC_ALL</c>,
    /// <c>LC_CTYPE</c> and <c>LANG</c> holds <c>UTF-8</c> or <c>utf8</c>; none set is the C locale.
    /// </summary>
    internal static bool LocaleIsUtf8(Func<string, string?> getEnvironmentVariable)
    {
        foreach (string name in new[] { "LC_ALL", "LC_CTYPE", "LANG" })
        {
            string? value = getEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
            {
                return value.Contains("UTF-8", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("UTF8", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

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
        TryNormalizeReadingHost(host, refuseUnconvertibleName, HostAsWindows1252, out normalized, out idnHost, out zoneId, out rejection, LocaleRejectsNonAsciiName);

    /// <summary>
    /// Normalises <paramref name="host" /> as <see cref="TryNormalize(string, bool, out string, out string, out string?, out CurlUrlRejection)" />
    /// does, converting a name that is not ASCII from its bytes read as Windows-1252 when
    /// <paramref name="hostAsWindows1252" /> is set (<see cref="ReadHostAsWindows1252" />) and from
    /// its UTF-8 otherwise, so both platforms' rules can be tested on either.
    /// </summary>
    internal static bool TryNormalizeReadingHost(
        string host,
        bool refuseUnconvertibleName,
        bool hostAsWindows1252,
        out string normalized,
        out string idnHost,
        out string? zoneId,
        out CurlUrlRejection rejection,
        bool localeRejectsNonAsciiName = false)
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

        string nameToConvert = hostAsWindows1252 ? ReadHostAsWindows1252(host) : ReadLatin1BytesAsUtf8(decoded);
        rejection = NormalizeAddressOrName(decoded, nameToConvert, refuseUnconvertibleName, localeRejectsNonAsciiName, out normalized, out idnHost);

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
        string nameToConvert,
        bool refuseUnconvertibleName,
        bool localeRejectsNonAsciiName,
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

        return NormalizeName(host, nameToConvert, refuseUnconvertibleName, localeRejectsNonAsciiName, out normalized, out idnHost);
    }

    /// <summary>
    /// Checks a name and converts <paramref name="nameToConvert" />, the name as the platform's curl
    /// reads it for IDNA, to punycode. A name IDNA cannot convert is
    /// <see cref="CurlUrlRejection.IdnConversionFailed" /> when <paramref name="refuseUnconvertibleName" />
    /// is set, which curl 8.22.0 fails with exit 3 and <c>URL using bad/illegal format or missing URL</c>
    /// rather than a <c>URL rejected:</c> text, as its IDN conversion runs after the URL parsed.
    /// </summary>
    private static CurlUrlRejection NormalizeName(
        string decoded,
        string nameToConvert,
        bool refuseUnconvertibleName,
        bool localeRejectsNonAsciiName,
        out string normalized,
        out string idnHost)
    {
        normalized = idnHost = string.Empty;
        if (!IsUsableName(decoded))
        {
            return CurlUrlRejection.BadHostname;
        }

        string? converted = Ascii.IsValid(decoded) ? decoded : (localeRejectsNonAsciiName ? null : ToPunycode(nameToConvert));
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
        byte[] decoded = PercentDecodeBytes(UrlByteText.ToBytes(text));

        return decoded.Any(value => value < 0x20) ? null : UrlByteText.ToText(decoded);
    }

    /// <summary>
    /// The host as the Windows curl 8.22.0 build hands it to <c>idn2_lookup_ul</c>, read back as
    /// Windows-1252 (CGR-059, ADR-0939): a character typed in the URL is its Windows-1252 byte, as the
    /// ANSI <c>argv</c> holds it (<c>ü</c> is <c>FC</c>), or its UTF-8 when Windows-1252 lacks it; an
    /// escape is the byte it decodes to, so <c>%C3%BC</c> is <c>Ã¼</c> and not <c>ü</c>. A byte
    /// Windows-1252 leaves undefined stays a lone <c>U+DC80</c> to <c>U+DCFF</c>, which no label converts
    /// with (<c>a%D7%90</c>).
    /// </summary>
    private static string ReadHostAsWindows1252(string host) =>
        new([.. PercentDecodeBytes(ToWindows1252Bytes(host)).Select(Windows1252CharacterOf)]);

    /// <summary>
    /// The host as curl off Windows hands it to <c>idn2_lookup_ul</c> in a UTF-8 locale: a name whose
    /// every character is one byte, as a <c>Location</c> header's bytes reach <c>CurlUrl</c>, and whose
    /// bytes are valid UTF-8 is that UTF-8, so <c>Ã¥Ã¤Ã¶</c> is <c>åäö</c> (upstream test1448,
    /// ADR-0960); any other name is kept as decoded.
    /// </summary>
    private static string ReadLatin1BytesAsUtf8(string name)
    {
        if (name.Any(character => character > 'ÿ'))
        {
            return name;
        }

        byte[] bytes = Encoding.Latin1.GetBytes(name);

        return System.Text.Unicode.Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : name;
    }

    private static byte[] ToWindows1252Bytes(string text)
    {
        var bytes = new List<byte>(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            int length = char.IsSurrogatePair(text, index) ? 2 : 1;
            int windows1252Byte = Windows1252ByteOf(text[index]);
            bytes.AddRange(length == 1 && windows1252Byte >= 0 ? [(byte)windows1252Byte] : UrlByteText.ToBytes(text.Substring(index, length)));
            index += length - 1;
        }

        return [.. bytes];
    }

    /// <summary>The Windows-1252 byte of <paramref name="character" />, or -1 when Windows-1252 lacks it.</summary>
    private static int Windows1252ByteOf(char character) =>
        character is < '\u0080' or (>= ' ' and <= 'ÿ')
            ? character
            : Windows1252ByteFrom80To9FOf(character);

    private static int Windows1252ByteFrom80To9FOf(char character)
    {
        int index = character == '￿' ? -1 : Windows1252Characters80To9F.IndexOf(character, StringComparison.Ordinal);

        return index < 0 ? -1 : 0x80 + index;
    }

    private static byte[] PercentDecodeBytes(byte[] encoded)
    {
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

        return [.. decoded];
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
    /// when a label cannot be converted (<see cref="ToAsciiLabel" />). A byte that is not UTF-8
    /// (in a UTF-8 locale) or not Windows-1252 (on Windows, <see cref="ReadHostAsWindows1252" />)
    /// makes the name unconvertible (<c>invalid-utf8-%e2%90.local</c>, upstream test 1034).
    /// curl fails a transfer to such a host with exit 3, while its URL API keeps the name (ADR-0734).
    /// </summary>
    private static string? ToPunycode(string name)
    {
        string mapped = CurlUrlHostNameMapping.Map(name);
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
    /// UTF-8), a control character (<c>U+0090</c>, upstream test 1034), the replacement character
    /// <c>U+FFFD</c>, which UTS #46 disallows (test 1034's bytes as a config file off Windows reads
    /// them, BL-3946), or longer than 63 characters once converted (upstream test 1035) is refused,
    /// as is an <c>xn--</c> label that does not decode (<see cref="KeepAsciiLabelUnlessUndecodable" />).
    /// </summary>
    /// <returns>The ASCII label, or <see langword="null" /> when it cannot be converted.</returns>
    private static string? ToAsciiLabel(string label)
    {
        if (HasMisplacedHyphen(label))
        {
            return null;
        }

        string? converted = Ascii.IsValid(label) ? KeepAsciiLabelUnlessUndecodable(label) : EncodeLabel(label);

        return converted?.Length > MaximumLabelLength ? null : converted;
    }

    /// <summary>
    /// Passes an ASCII label as written, but refuses an <c>xn--</c> label that libidn2 cannot decode
    /// (BL-3945): one that is not punycode (<c>xn--zz</c>), decodes to no character outside ASCII
    /// (<c>xn--</c>, <c>xn--abc-</c>), or decodes to a character IDNA refuses (<c>xn--a</c> is
    /// <c>U+0080</c>, <c>xn--ib9b</c> a lone surrogate).
    /// </summary>
    private static string? KeepAsciiLabelUnlessUndecodable(string label) =>
        !label.StartsWith("xn--", StringComparison.Ordinal) || IsDecodableLabel(CurlUrlPunycode.Decode(label[4..]))
            ? label
            : null;

    private static bool IsDecodableLabel(List<int>? codePoints) =>
        codePoints is not null
        && codePoints.Exists(codePoint => codePoint >= 0x80)
        && codePoints.TrueForAll(codePoint => Rune.IsValid(codePoint) && !IsRefusedInLabel(new Rune(codePoint)));

    private static bool HasMisplacedHyphen(string label) =>
        label.StartsWith('-')
        || label.EndsWith('-')
        || (label.Length > 3 && label.AsSpan(2, 2) is "--" && !label.StartsWith("xn--", StringComparison.OrdinalIgnoreCase));

    private static string? EncodeLabel(string label)
    {
        // Enumerating runes reads a lone surrogate as U+FFFD, which IsRefusedInLabel refuses.
        var codePoints = new List<int>(label.Length);
        foreach (Rune rune in label.EnumerateRunes())
        {
            if (IsRefusedInLabel(rune) || codePoints.Count == MaximumLabelLength)
            {
                return null;
            }

            codePoints.Add(rune.Value);
        }

        return "xn--" + CurlUrlPunycode.Encode(codePoints);
    }

    /// <summary>
    /// Whether IDNA refuses <paramref name="rune" /> in a label: a control character or <c>U+FFFD</c>,
    /// which a lone surrogate (a byte that is not UTF-8) reads as.
    /// </summary>
    private static bool IsRefusedInLabel(Rune rune) => Rune.IsControl(rune) || rune == Rune.ReplacementChar;

    /// <summary>
    /// The Windows-1252 character of <paramref name="value" />, or the lone <c>U+DC80</c> to
    /// <c>U+DCFF</c> <see cref="UrlByteText" /> carries the byte as when Windows-1252 leaves it undefined.
    /// </summary>
    private static char Windows1252CharacterOf(byte value)
    {
        char mapped = value is < 0x80 or >= 0xA0 ? (char)value : Windows1252Characters80To9F[value - 0x80];

        return mapped == '￿' ? (char)(0xDC00 + value) : mapped;
    }

    /// <summary>
    /// Checks and normalises a bracketed IPv6 address as curl's <c>ipv6_parse</c> does:
    /// only hexadecimal digits, colons and dots, then optionally <c>%</c> (a decoded <c>%25</c>)
    /// and a zone id of 1 to 15 characters, and the address printed back in its shortest
    /// form when that is no longer than the address as written (zone id aside), so
    /// <c>[::FFFF:7f00:0001]</c> is <c>[::ffff:127.0.0.1]</c> but <c>[::ffff:7f00:1]</c> stays
    /// as written (CGR-058). A zone id longer than 15 characters is a bad IPv6 address to curl, as is every other fault (CGR-191); a control character in the host
    /// is a bad host name before this point.
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
            return CurlUrlRejection.BadIPv6;
        }

        zoneId = id;

        return CurlUrlRejection.None;
    }
}
