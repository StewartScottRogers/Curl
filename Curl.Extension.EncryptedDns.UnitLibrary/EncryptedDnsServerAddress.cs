using System.Globalization;
using Curl.Extension.Abstractions;

namespace Curl.Extension.EncryptedDns;

/// <summary>
/// Reads the <c>host[:port]</c> an option names its resolver with (ADR-1064): a host name, an IPv4 address or an
/// IPv6 address in brackets, then an optional decimal port from 1 to 65535. A bare IPv6 address without brackets is
/// taken whole as the host, since its colons cannot also separate a port.
/// </summary>
internal static class EncryptedDnsServerAddress
{
    private const int LargestPort = 65535;

    /// <summary>Reads <paramref name="value" /> as the resolver's host and port.</summary>
    /// <param name="value">The option's value.</param>
    /// <param name="option">The option, named in the error.</param>
    /// <param name="defaultPort">The port when the value names none.</param>
    /// <returns>The host, without brackets, and the port.</returns>
    /// <exception cref="ArgumentException">The value is not <c>host[:port]</c>.</exception>
    internal static (string Host, int Port) Parse(string value, ExtensionOption option, int defaultPort)
    {
        (string host, string? port) = Split(value);
        if (host.Length == 0)
        {
            throw Malformed(value, option);
        }

        if (port is null)
        {
            return (host, defaultPort);
        }

        return int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number is >= 1 and <= LargestPort
            ? (host, number)
            : throw Malformed(value, option);
    }

    // The host and the port text, or an empty host for a bracket that is not closed or not followed by ':'.
    private static (string Host, string? Port) Split(string value) =>
        value.StartsWith('[') ? SplitBracketed(value) : SplitUnbracketed(value);

    // A value with one colon splits there; with none, or several (a bare IPv6 address), it is all host.
    private static (string Host, string? Port) SplitUnbracketed(string value)
    {
        int colon = value.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 || colon != value.LastIndexOf(':') ? (value, null) : (value[..colon], value[(colon + 1)..]);
    }

    private static (string Host, string? Port) SplitBracketed(string value)
    {
        int close = value.IndexOf(']', StringComparison.Ordinal);
        string rest = close < 0 ? "?" : value[(close + 1)..];
        return rest.Length == 0 ? (value[1..close], null)
            : rest[0] == ':' ? (value[1..close], rest[1..])
            : (string.Empty, null);
    }

    private static ArgumentException Malformed(string value, ExtensionOption option) =>
        new($"--{option.Name} needs a resolver as host[:port], not '{value}'", "values");
}
