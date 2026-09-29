using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Parses a <c>--dns-servers</c> value the way curl 8.22.0's c-ares 1.34.8 build accepts it
/// (measured, BL-694): entries separated by commas, each trimmed of spaces, empty ones
/// skipped; an entry is an IPv4 address with an optional <c>:port</c>, a bare IPv6 address, or
/// a bracketed IPv6 address with an optional <c>:port</c>. Port 0 or no port means 53. A host
/// name, a port over 65535, any other separator and a list with no entry are refused, which
/// curl reports as exit 43 when it resolves a name.
/// </summary>
public static class DnsServerList
{
    /// <summary>The port a server entry without one is sent to.</summary>
    public const int DefaultPort = 53;

    /// <summary>Parses <paramref name="text" /> into the servers, in the order given.</summary>
    /// <param name="text">The <c>--dns-servers</c> value.</param>
    /// <param name="servers">The servers, when the list parsed.</param>
    /// <returns><see langword="true" /> when every entry parsed and there is at least one.</returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out IReadOnlyList<IPEndPoint>? servers)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parsed = new List<IPEndPoint>();
        foreach (var entry in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseEntry(entry, out var server))
            {
                servers = null;
                return false;
            }

            parsed.Add(server);
        }

        servers = parsed.Count > 0 ? parsed : null;
        return servers is not null;
    }

    private static bool TryParseEntry(string entry, [NotNullWhen(true)] out IPEndPoint? server)
    {
        server = null;
        if (entry.StartsWith('['))
        {
            return TryParseBracketed(entry, out server);
        }

        var firstColon = entry.IndexOf(':', StringComparison.Ordinal);
        if (firstColon >= 0 && entry.IndexOf(':', firstColon + 1) >= 0)
        {
            return TryMakeEndPoint(entry, AddressFamily.InterNetworkV6, string.Empty, out server);
        }

        return firstColon < 0
            ? TryMakeEndPoint(entry, AddressFamily.InterNetwork, string.Empty, out server)
            : TryMakeEndPoint(entry[..firstColon], AddressFamily.InterNetwork, entry[firstColon..], out server);
    }

    private static bool TryParseBracketed(string entry, out IPEndPoint? server)
    {
        server = null;
        var close = entry.IndexOf(']', StringComparison.Ordinal);
        return close > 0 && TryMakeEndPoint(entry[1..close], AddressFamily.InterNetworkV6, entry[(close + 1)..], out server);
    }

    /// <summary>Makes the end point for an address of <paramref name="family" /> and a port suffix, empty or <c>:port</c>.</summary>
    private static bool TryMakeEndPoint(string address, AddressFamily family, string portSuffix, out IPEndPoint? server)
    {
        server = null;
        if (!TryParseAddress(address, family, out var parsedAddress) || !TryParsePortSuffix(portSuffix, out var port))
        {
            return false;
        }

        server = new IPEndPoint(parsedAddress, port);
        return true;
    }

    /// <summary>
    /// Parses an address of <paramref name="family" /> strictly: an IPv4 address is four dotted
    /// decimal numbers, as <c>inet_pton</c> takes it, not the short forms <see cref="IPAddress.TryParse(string, out IPAddress)" /> allows.
    /// </summary>
    /// <param name="text">The address text.</param>
    /// <param name="family">The family the address must be.</param>
    /// <param name="address">The address, when it parsed.</param>
    /// <returns><see langword="true" /> when <paramref name="text" /> is an address of <paramref name="family" />.</returns>
    internal static bool TryParseAddress(string text, AddressFamily family, [NotNullWhen(true)] out IPAddress? address)
    {
        address = null;
        var shapeFits = family == AddressFamily.InterNetworkV6 || text.Count(character => character == '.') == 3;
        if (!shapeFits || !IPAddress.TryParse(text, out var parsed) || parsed.AddressFamily != family)
        {
            return false;
        }

        address = parsed;
        return true;
    }

    private static bool TryParsePortSuffix(string suffix, out int port)
    {
        port = DefaultPort;
        if (suffix.Length == 0)
        {
            return true;
        }

        if (suffix[0] != ':' || !int.TryParse(suffix.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var given) || given > IPEndPoint.MaxPort)
        {
            return false;
        }

        port = given == 0 ? DefaultPort : given;
        return true;
    }
}
