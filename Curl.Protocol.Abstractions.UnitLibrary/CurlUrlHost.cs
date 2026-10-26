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

    private static readonly IdnMapping Idn = new();

    /// <summary>
    /// Normalises <paramref name="host" />, or returns <see langword="false" /> when curl
    /// rejects it, with <paramref name="rejection" /> saying why. With
    /// <paramref name="refuseUnconvertibleName" />, as for the URL of a transfer, a name
    /// <see cref="IdnMapping" /> cannot convert is <see cref="CurlUrlRejection.BadHostname" />;
    /// without it the name is kept as decoded and resolved as written.
    /// </summary>
    public static bool TryNormalize(
        string host,
        bool refuseUnconvertibleName,
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

        bool accepted = TryNormalizeAddressOrName(decoded, refuseUnconvertibleName, out normalized, out idnHost);
        rejection = accepted ? CurlUrlRejection.None : CurlUrlRejection.BadHostname;

        return accepted;
    }

    /// <summary>
    /// Normalises a percent-decoded host that is not bracketed: an IPv4 address in any form
    /// <c>inet_aton</c> reads, or else a name. One trailing dot is allowed; an empty host
    /// or two trailing dots are not. curl 8.22.0's <c>parse_authority</c> decodes the host
    /// before <c>ipv4_normalize</c>, so <c>127.0.0.%31.</c> is <c>127.0.0.1</c> (lib1560).
    /// </summary>
    private static bool TryNormalizeAddressOrName(string host, bool refuseUnconvertibleName, out string normalized, out string idnHost)
    {
        idnHost = normalized = string.Empty;
        string withoutTrailingDot = host.EndsWith('.') ? host[..^1] : host;
        if (withoutTrailingDot.Length == 0 || withoutTrailingDot.EndsWith('.'))
        {
            return false;
        }

        if (CurlUrlIPv4Address.TryNormalize(withoutTrailingDot, out string address))
        {
            idnHost = normalized = address;

            return true;
        }

        return TryNormalizeName(host, refuseUnconvertibleName, out normalized, out idnHost);
    }

    private static bool TryNormalizeName(string decoded, bool refuseUnconvertibleName, out string normalized, out string idnHost)
    {
        normalized = idnHost = string.Empty;
        if (!IsUsableName(decoded))
        {
            return false;
        }

        string? converted = Ascii.IsValid(decoded) ? decoded : ToPunycode(decoded);
        if (converted is null && refuseUnconvertibleName)
        {
            return false;
        }

        normalized = decoded;
        idnHost = converted ?? decoded;

        return true;
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
    /// punycode, or returns <see langword="null" />
    /// when <see cref="IdnMapping" /> cannot convert it: bytes that are not UTF-8
    /// (<c>invalid-utf8-%e2%90.local</c>, upstream test 1034) or a label longer than 63
    /// characters once converted (test 1035). curl 8.22.0 in a UTF-8 locale fails a transfer
    /// to such a host with exit 3, while its URL API keeps the name (ADR-0734).
    /// </summary>
    private static string? ToPunycode(string name)
    {
        string mapped = CurlUrlHostNameMapping.Map(name);
        try
        {
            return Ascii.IsValid(mapped) ? mapped : Idn.GetAscii(mapped);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks and normalises a bracketed IPv6 address as curl's <c>ipv6_parse</c> does:
    /// only hexadecimal digits, colons and dots, then optionally <c>%</c> (a decoded <c>%25</c>)
    /// and a zone id of 1 to 15 characters, and the address printed back in its shortest
    /// form. A zone id longer than 15 characters is a bad host name to curl; every other
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

        string text = address.ToString();
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
