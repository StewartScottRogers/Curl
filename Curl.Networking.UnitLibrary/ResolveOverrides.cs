using System.Globalization;
using System.Net;

namespace Curl.Networking;

/// <summary>
/// The <c>--resolve</c> entries: addresses that answer for a <c>host:port</c> pair instead
/// of the system resolver, parsed as curl 8.21.0 parses <c>CURLOPT_RESOLVE</c>.
/// </summary>
/// <remarks>
/// <para>
/// An entry is <c>[+]host:port:address[,address]...</c>, or <c>-host:port</c> to drop an
/// earlier entry. The host matches without regard to case and may be bracketed; <c>*</c>
/// matches every host on that port, after any entry naming the host. A later entry for the
/// same <c>host:port</c> replaces an earlier one. The port is 0 to 65535 in decimal digits
/// only. An address is a dotted-quad IPv4 address without leading zeros or an IPv6 address,
/// without a zone, either bracketed or not; empty items between commas are skipped.
/// </para>
/// <para>
/// Measured on curl 8.21.0: an empty entry, one with an empty host and a malformed removal
/// are ignored; anything else that does not parse fails the transfer with exit 49 and
/// <c>Could not parse CURLOPT_RESOLVE entry '&lt;entry&gt;'</c>, which
/// <see cref="ParseError" /> holds and <see cref="TcpConnector" /> reports.
/// </para>
/// </remarks>
public sealed class ResolveOverrides
{
    private const string AnyHost = "*";

    private readonly Dictionary<string, IReadOnlyList<IPAddress>> _addressesByHostAndPort;

    private ResolveOverrides(Dictionary<string, IReadOnlyList<IPAddress>> addressesByHostAndPort, string? parseError)
    {
        _addressesByHostAndPort = addressesByHostAndPort;
        ParseError = parseError;
    }

    /// <summary>
    /// Gets the overrides of a transfer with no <c>--resolve</c>.
    /// </summary>
    public static ResolveOverrides None { get; } = Parse([]);

    /// <summary>
    /// Gets curl's exit 49 message for the first entry that does not parse, or
    /// <see langword="null" /> when every entry parsed.
    /// </summary>
    public string? ParseError { get; }

    /// <summary>
    /// Parses the <c>--resolve</c> values in the order they were given.
    /// </summary>
    /// <param name="entries">The values, verbatim.</param>
    /// <returns>
    /// The overrides; when an entry does not parse, <see cref="ParseError" /> names it and
    /// the entries before it are kept.
    /// </returns>
    public static ResolveOverrides Parse(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var addressesByHostAndPort = new Dictionary<string, IReadOnlyList<IPAddress>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (!TryApply(entry, addressesByHostAndPort))
            {
                return new ResolveOverrides(addressesByHostAndPort, $"Could not parse CURLOPT_RESOLVE entry '{entry}'");
            }
        }

        return new ResolveOverrides(addressesByHostAndPort, null);
    }

    /// <summary>
    /// Finds the addresses an entry gives for <paramref name="host" /> on
    /// <paramref name="port" />: the entry naming the host, else the <c>*</c> entry for the port.
    /// </summary>
    /// <param name="host">The host to connect to, bracketed or not when it is an IPv6 literal.</param>
    /// <param name="port">The port to connect to.</param>
    /// <returns>The addresses in the entry's order, or <see langword="null" /> when no entry applies.</returns>
    public IReadOnlyList<IPAddress>? Find(string host, int port)
    {
        ArgumentNullException.ThrowIfNull(host);

        return _addressesByHostAndPort.GetValueOrDefault(Key(Unbracket(host), port))
            ?? _addressesByHostAndPort.GetValueOrDefault(Key(AnyHost, port));
    }

    private static bool TryApply(string entry, Dictionary<string, IReadOnlyList<IPAddress>> addressesByHostAndPort) =>
        entry.StartsWith('-')
            ? ApplyRemoval(entry[1..], addressesByHostAndPort)
            // "+" marks an entry curl may time out of its DNS cache; one transfer never outlives it.
            : TryApplyAddition(entry.StartsWith('+') ? entry[1..] : entry, addressesByHostAndPort);

    private static bool ApplyRemoval(string hostAndPort, Dictionary<string, IReadOnlyList<IPAddress>> addressesByHostAndPort)
    {
        // A removal that does not parse is ignored (measured: "-a" and "-a:x").
        if (TrySplitHostAndPort(hostAndPort, out var host, out var port, out _))
        {
            addressesByHostAndPort.Remove(Key(host, port));
        }

        return true;
    }

    private static bool TryApplyAddition(string entry, Dictionary<string, IReadOnlyList<IPAddress>> addressesByHostAndPort)
    {
        if (IsIgnoredAddition(entry))
        {
            return true;
        }

        if (!TrySplitHostAndPort(entry, out var host, out var port, out var addressList)
            || ParseAddresses(addressList) is not { } addresses)
        {
            return false;
        }

        // An empty host is ignored (measured: ":80:127.0.0.1").
        if (host.Length > 0)
        {
            addressesByHostAndPort[Key(host, port)] = addresses;
        }

        return true;
    }

    // An empty entry, and a bracketed host with no "]:", are ignored (measured: "+" and "[::1:80:127.0.0.1").
    private static bool IsIgnoredAddition(string entry) =>
        entry.Length == 0 || (entry.StartsWith('[') && !entry.Contains("]:", StringComparison.Ordinal));

    private static bool TrySplitHostAndPort(string text, out string host, out int port, out string rest)
    {
        host = string.Empty;
        port = 0;
        rest = string.Empty;
        var hostEnd = text.StartsWith('[') ? text.IndexOf("]:", StringComparison.Ordinal) + 1 : text.IndexOf(':', StringComparison.Ordinal);
        if (hostEnd <= 0 && !text.StartsWith(':'))
        {
            return false;
        }

        host = Unbracket(text[..hostEnd]);
        var afterHost = text[(hostEnd + 1)..];
        var portEnd = afterHost.IndexOf(':', StringComparison.Ordinal);
        var portText = portEnd < 0 ? afterHost : afterHost[..portEnd];
        rest = portEnd < 0 ? string.Empty : afterHost[(portEnd + 1)..];
        return TryParsePort(portText, out port);
    }

    private static bool TryParsePort(string text, out int port)
    {
        // Decimal digits only, 0 to 65535 (measured: "080" is 80; "+80", "-1", "80x" and "99999" do not parse).
        var parsed = ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value);
        port = value;
        return parsed;
    }

    private static IPAddress[]? ParseAddresses(string addressList)
    {
        var items = addressList.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (items.Length == 0)
        {
            return null;
        }

        var addresses = new IPAddress[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            if (ParseAddress(items[index]) is not { } address)
            {
                return null;
            }

            addresses[index] = address;
        }

        return addresses;
    }

    private static IPAddress? ParseAddress(string text)
    {
        if (text.StartsWith('['))
        {
            // Either family may be bracketed, and what follows the "]" is ignored (measured:
            // "[127.0.0.1]", "[::1]" and "[::1]x").
            var closingBracket = text.IndexOf(']', StringComparison.Ordinal);
            return closingBracket < 0 ? null : ParseUnbracketedAddress(text[1..closingBracket]);
        }

        return ParseUnbracketedAddress(text);
    }

    private static IPAddress? ParseUnbracketedAddress(string text) =>
        text.Contains(':', StringComparison.Ordinal) ? ParseIPv6(text) : ParseDottedQuad(text);

    private static IPAddress? ParseIPv6(string text) =>
        text.All(character => char.IsAsciiHexDigit(character) || character is ':' or '.')
            && IPAddress.TryParse(text, out var address)
            ? address
            : null;

    private static IPAddress? ParseDottedQuad(string text)
    {
        // inet_pton's IPv4: exactly four decimal parts of 0 to 255, no leading zero (measured: "1.2.3" and "01.2.3.4").
        var parts = text.Split('.');
        return parts.Length == 4 && parts.All(IsDottedQuadPart) ? IPAddress.Parse(text) : null;
    }

    private static bool IsDottedQuadPart(string part) =>
        byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)
        && (part.Length == 1 || part[0] != '0');

    private static string Unbracket(string host) =>
        host.Length >= 2 && host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;

    private static string Key(string host, int port) => $"{host}:{port}";
}
