using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>
/// Tells whether one <c>oci://</c> hop's host is exempt from the transfer's proxy under its
/// <see cref="Curl.Protocol.Abstractions.ITransferContext.NoProxy" /> list, read as curl 8.21.0's
/// <c>Curl_check_noproxy</c> reads it (ADR-1290): the list <c>*</c> alone exempts every host; entries are
/// separated by commas with blanks around them, and a blank not followed by a comma ends the list; a name
/// matches an entry equal to it or ending in <c>.</c> plus the entry, ignoring ASCII case, one trailing dot
/// and one leading dot of the entry; an address matches only an address entry, with an optional decimal
/// <c>/bits</c> prefix length.
/// </summary>
internal static class HopNoProxyMatcher
{
    private const string Blanks = " \t";

    /// <summary>Tells whether <paramref name="host" /> must be reached without the proxy.</summary>
    /// <param name="host">The hop's host; an IPv6 address may carry its brackets.</param>
    /// <param name="noProxy">The transfer's no-proxy list; <see langword="null" /> exempts nothing.</param>
    /// <returns><see langword="true" /> when an entry of <paramref name="noProxy" /> exempts the host.</returns>
    public static bool Matches(string host, string? noProxy)
    {
        if (string.IsNullOrEmpty(noProxy))
        {
            return false;
        }

        if (noProxy == "*")
        {
            return true;
        }

        string name = host.StartsWith('[') ? host[1..^1] : host;
        byte[]? address = ParseAddress(name);
        return Entries(noProxy).Any(entry => address is null ? NameMatches(entry, WithoutTrailingDot(name)) : AddressMatches(entry, address));
    }

    /// <summary>Reads the list's non-empty entries up to the first blank that no comma follows.</summary>
    private static List<string> Entries(string noProxy)
    {
        var entries = new List<string>();
        ReadOnlySpan<char> rest = noProxy;
        while (!rest.IsEmpty)
        {
            rest = rest.TrimStart(Blanks);
            int length = rest.IndexOfAny(" \t,");
            length = length < 0 ? rest.Length : length;
            entries.Add(rest[..length].ToString());
            rest = rest[length..].TrimStart(Blanks);
            if (!rest.StartsWith(','))
            {
                break;
            }

            rest = rest.TrimStart(',');
        }

        entries.RemoveAll(entry => entry.Length == 0);
        return entries;
    }

    private static string WithoutTrailingDot(string text) => text.EndsWith('.') ? text[..^1] : text;

    private static bool NameMatches(string entry, string name)
    {
        string token = WithoutTrailingDot(entry);
        token = token.StartsWith('.') ? token[1..] : token;
        return name.Equals(token, StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("." + token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AddressMatches(string entry, byte[] host)
    {
        if (!TrySplitPrefix(entry, out string addressText, out int bits))
        {
            return false;
        }

        byte[]? address = ParseAddress(addressText);
        return address is not null && address.Length == host.Length && PrefixMatches(address, host, bits);
    }

    /// <summary>Splits an address entry at its <c>/</c>; <paramref name="bits" /> is 0 without one, and a length that is not all digits fails.</summary>
    private static bool TrySplitPrefix(string entry, out string address, out int bits)
    {
        bits = 0;
        int slash = entry.IndexOf('/');
        address = slash < 0 ? entry : entry[..slash];
        return slash < 0 || int.TryParse(entry.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out bits);
    }

    private static bool PrefixMatches(byte[] address, byte[] host, int bits)
    {
        int addressBits = host.Length * 8;
        return bits <= addressBits && LeadingBitsEqual(address, host, bits == 0 ? addressBits : bits);
    }

    private static bool LeadingBitsEqual(byte[] entry, byte[] host, int bits)
    {
        int wholeBytes = bits / 8;
        int remainingBits = bits % 8;
        int mask = (0xFF << (8 - remainingBits)) & 0xFF;
        return entry.AsSpan(0, wholeBytes).SequenceEqual(host.AsSpan(0, wholeBytes))
            && (remainingBits == 0 || (entry[wholeBytes] & mask) == (host[wholeBytes] & mask));
    }

    /// <summary>
    /// Parses a dotted IPv4 address as <c>inet_pton</c> does (four decimal parts, no leading zero), or an IPv6
    /// address with no brackets and no zone; anything else is a name.
    /// </summary>
    private static byte[]? ParseAddress(string text) => ParseIPv4(text) ?? ParseIPv6(text);

    private static byte[]? ParseIPv4(string text)
    {
        string[] parts = text.Split('.');
        return parts.Length == 4 && parts.All(IsOctet)
            ? [.. parts.Select(part => byte.Parse(part, CultureInfo.InvariantCulture))]
            : null;
    }

    private static byte[]? ParseIPv6(string text) =>
        !text.AsSpan().ContainsAny("[]%") && IPAddress.TryParse(text, out IPAddress? address)
            && address.AddressFamily == AddressFamily.InterNetworkV6 ? address.GetAddressBytes() : null;

    private static bool IsOctet(string part) =>
        part.Length is > 0 and <= 3
        && (part.Length == 1 || part[0] != '0')
        && byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
