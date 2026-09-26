using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Curl.Core;

/// <summary>
/// Tells whether a host is exempt from the proxy under a <c>--noproxy</c> or
/// <c>NO_PROXY</c> list, read as curl 8.21.0's <c>Curl_check_noproxy</c> reads it.
/// </summary>
/// <remarks>
/// <para>
/// The list <c>*</c>, exactly and alone, exempts every host; <c>*</c> anywhere else is an
/// ordinary entry that matches nothing. Entries are separated by commas, with blanks
/// (space or tab) allowed around them; a blank that is not followed by a comma ends the
/// list, so <c>foo example.com</c> is only <c>foo</c>.
/// </para>
/// <para>
/// A host name matches an entry equal to it, or ending in <c>.</c> plus the entry, compared
/// without regard to ASCII case, after one trailing dot is dropped from each and one leading
/// dot from the entry: <c>example.com</c> and <c>.example.com</c> both exempt
/// <c>example.com</c> and <c>www.example.com</c> but not <c>nonexample.com</c>.
/// </para>
/// <para>
/// An IPv4 or IPv6 address matches only an address entry, optionally with a <c>/bits</c>
/// prefix length written as decimal digits: <c>/0</c> or no suffix compares the whole
/// address, a length longer than the address or a suffix that is not all digits matches
/// nothing. Address entries are never compared with host names, so <c>localhost</c> does
/// not exempt <c>127.0.0.1</c>. Every case was measured against curl 8.21.0 on 2026-09-26.
/// </para>
/// </remarks>
public static class NoProxyMatcher
{
    private const string Blanks = " \t";

    private const string BlanksAndComma = " \t,";

    private enum HostType
    {
        Name,
        IPv4,
        IPv6,
    }

    /// <summary>Tells whether <paramref name="hostName" /> is exempt under <paramref name="noProxy" />.</summary>
    /// <param name="hostName">
    /// The URL's host as curl names it: a name, a dotted IPv4 address, or an IPv6 address
    /// without its brackets or zone.
    /// </param>
    /// <param name="noProxy">The <c>--noproxy</c> or <c>NO_PROXY</c> list; <see langword="null" /> exempts nothing.</param>
    /// <returns><see langword="true" /> when the host must be reached without the proxy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hostName" /> is <see langword="null" />.</exception>
    public static bool Matches(string hostName, string? noProxy)
    {
        ArgumentNullException.ThrowIfNull(hostName);

        if (hostName.Length == 0 || string.IsNullOrEmpty(noProxy))
        {
            return false;
        }

        return noProxy == "*" || AnyEntryMatches(hostName, noProxy);
    }

    private static bool AnyEntryMatches(string hostName, ReadOnlySpan<char> rest)
    {
        HostType type = ClassifyHost(hostName);
        ReadOnlySpan<char> name = type == HostType.Name ? WithoutTrailingDot(hostName) : hostName;
        while (!rest.IsEmpty)
        {
            ReadOnlySpan<char> token = ReadEntry(ref rest);
            if (!token.IsEmpty && EntryMatches(token, name, type))
            {
                return true;
            }

            if (!rest.StartsWith(','))
            {
                break;
            }

            rest = rest.TrimStart(',');
        }

        return false;
    }

    /// <summary>
    /// Reads the next entry and the blanks around it off the front of <paramref name="rest" />.
    /// </summary>
    private static ReadOnlySpan<char> ReadEntry(ref ReadOnlySpan<char> rest)
    {
        rest = rest.TrimStart(Blanks);
        int length = rest.IndexOfAny(BlanksAndComma);
        if (length < 0)
        {
            length = rest.Length;
        }

        ReadOnlySpan<char> entry = rest[..length];
        rest = rest[length..].TrimStart(Blanks);
        return entry;
    }

    private static HostType ClassifyHost(string hostName)
    {
        if (TryParseIPv4(hostName, out _))
        {
            return HostType.IPv4;
        }

        return TryParseIPv6(hostName, out _) ? HostType.IPv6 : HostType.Name;
    }

    private static ReadOnlySpan<char> WithoutTrailingDot(ReadOnlySpan<char> text) =>
        text.EndsWith('.') ? text[..^1] : text;

    private static bool EntryMatches(ReadOnlySpan<char> token, ReadOnlySpan<char> name, HostType type) =>
        type == HostType.Name ? NameMatches(token, name) : AddressMatches(token, name, type);

    private static bool NameMatches(ReadOnlySpan<char> token, ReadOnlySpan<char> name)
    {
        token = WithoutTrailingDot(token);
        if (token.StartsWith('.'))
        {
            token = token[1..];
        }

        if (token.Length == name.Length)
        {
            return token.Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        return token.Length < name.Length
            && name[name.Length - token.Length - 1] == '.'
            && name[^token.Length..].Equals(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AddressMatches(ReadOnlySpan<char> token, ReadOnlySpan<char> name, HostType type)
    {
        if (!TrySplitPrefix(token, out ReadOnlySpan<char> address, out int bits))
        {
            return false;
        }

        return type == HostType.IPv4
            ? PrefixMatches(address, name, bits, 32, TryParseIPv4)
            : PrefixMatches(address, name, bits, 128, TryParseIPv6);
    }

    /// <summary>
    /// Splits an address entry at its <c>/</c>; <paramref name="bits" /> is 0 when there is
    /// no prefix length, and the split fails when the length is not all decimal digits.
    /// </summary>
    private static bool TrySplitPrefix(ReadOnlySpan<char> token, out ReadOnlySpan<char> address, out int bits)
    {
        bits = 0;
        address = token;
        int slash = token.IndexOf('/');
        if (slash < 0)
        {
            return true;
        }

        address = token[..slash];
        return TryParseBits(token[(slash + 1)..], out bits);
    }

    private delegate bool AddressParser(ReadOnlySpan<char> text, out byte[] bytes);

    private static bool PrefixMatches(
        ReadOnlySpan<char> entryText,
        ReadOnlySpan<char> hostText,
        int bits,
        int addressBits,
        AddressParser parse)
    {
        if (bits > addressBits || !parse(entryText, out byte[] entry) || !parse(hostText, out byte[] host))
        {
            return false;
        }

        return LeadingBitsEqual(entry, host, bits == 0 ? addressBits : bits);
    }

    private static bool LeadingBitsEqual(byte[] entry, byte[] host, int bits)
    {
        int wholeBytes = bits / 8;
        if (!entry.AsSpan(0, wholeBytes).SequenceEqual(host.AsSpan(0, wholeBytes)))
        {
            return false;
        }

        int remainingBits = bits % 8;
        int mask = (0xFF << (8 - remainingBits)) & 0xFF;
        return remainingBits == 0 || (entry[wholeBytes] & mask) == (host[wholeBytes] & mask);
    }

    private static bool TryParseBits(ReadOnlySpan<char> text, out int bits)
    {
        bits = 0;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out bits);
    }

    /// <summary>
    /// Parses a dotted IPv4 address as <c>inet_pton</c> does: exactly four decimal parts of
    /// at most 255, none with a leading zero, so <c>127.1</c> and <c>0x7f.0.0.1</c> are not
    /// addresses.
    /// </summary>
    private static bool TryParseIPv4(ReadOnlySpan<char> text, out byte[] bytes)
    {
        bytes = new byte[4];
        int part = 0;
        foreach (Range range in text.Split('.'))
        {
            ReadOnlySpan<char> digits = text[range];
            if (part == 4 || !TryParseOctet(digits, out bytes[part]))
            {
                return false;
            }

            part++;
        }

        return part == 4;
    }

    private static bool TryParseOctet(ReadOnlySpan<char> digits, out byte octet)
    {
        octet = 0;
        return digits.Length <= 3
            && (digits.Length == 1 || digits[0] != '0')
            && byte.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out octet);
    }

    /// <summary>
    /// Parses an IPv6 address with no brackets and no zone, as <c>inet_pton</c> does.
    /// </summary>
    private static bool TryParseIPv6(ReadOnlySpan<char> text, out byte[] bytes)
    {
        bytes = [];
        if (text.ContainsAny("[]%") || !IPAddress.TryParse(text, out IPAddress? address)
            || address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        bytes = address.GetAddressBytes();
        return true;
    }
}
