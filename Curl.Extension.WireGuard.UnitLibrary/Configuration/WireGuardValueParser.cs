using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Curl.Extension.WireGuard.Configuration;

/// <summary>Parses the values of a wg-quick configuration file's keys, naming the line in each error.</summary>
internal static class WireGuardValueParser
{
    private const int KeyLength = 32;

    /// <summary>Parses a base64 key that must decode to exactly 32 bytes.</summary>
    public static byte[] ParseKey(string value, string keyName, int lineNumber)
    {
        var buffer = new byte[KeyLength + 1];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written != KeyLength)
        {
            throw new WireGuardConfigurationException(lineNumber, $"{keyName} is not 32 bytes of base64");
        }

        return buffer[..KeyLength];
    }

    /// <summary>Splits a comma-separated list, trimming each entry and leaving out empty ones.</summary>
    public static IEnumerable<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Parses a comma-separated list of addresses, each with an optional prefix length.</summary>
    public static IEnumerable<WireGuardCidr> ParseCidrList(string value, int lineNumber) =>
        SplitList(value).Select(item => ParseCidr(item, lineNumber)).ToList();

    /// <summary>Parses one address with an optional prefix length; without one the prefix covers the whole address.</summary>
    public static WireGuardCidr ParseCidr(string value, int lineNumber) =>
        TryParseCidr(value) ?? throw new WireGuardConfigurationException(lineNumber, $"'{value}' is not a valid CIDR");

    /// <summary>Parses <c>host:port</c>, or <c>[IPv6]:port</c> for an IPv6 address.</summary>
    public static WireGuardEndpoint ParseEndpoint(string value, int lineNumber)
    {
        var colon = value.LastIndexOf(':');
        var host = colon < 0 ? string.Empty : UnbracketHost(value[..colon]);
        var port = colon < 0 ? null : ParseNumber(value[(colon + 1)..], 1, ushort.MaxValue);
        return port is null || !IsValidHost(host, value)
            ? throw new WireGuardConfigurationException(lineNumber, $"'{value}' is not a valid endpoint")
            : new WireGuardEndpoint(host, port.Value);
    }

    /// <summary>Parses a whole number from <paramref name="minimum" /> to <paramref name="maximum" />.</summary>
    public static int ParseNumber(string value, string keyName, int minimum, int maximum, int lineNumber) =>
        ParseNumber(value, minimum, maximum)
        ?? throw new WireGuardConfigurationException(lineNumber, $"{keyName} '{value}' is not a number from {minimum} to {maximum}");

    private static WireGuardCidr? TryParseCidr(string value)
    {
        var slash = value.IndexOf('/');
        var address = ParseAddressWithoutZone(slash < 0 ? value : value[..slash]);
        if (address is null)
        {
            return null;
        }

        var maximum = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var prefixLength = slash < 0 ? maximum : ParseNumber(value[(slash + 1)..], 0, maximum);
        return prefixLength is null ? null : new WireGuardCidr(address, prefixLength.Value);
    }

    private static IPAddress? ParseAddressWithoutZone(string text) =>
        !text.Contains('%') && IPAddress.TryParse(text, out var address) ? address : null;

    private static string UnbracketHost(string host) =>
        host.Length > 1 && host[0] == '[' && host[^1] == ']' ? host[1..^1] : host;

    private static bool IsValidHost(string host, string endpoint) =>
        host.Length > 0
        && host.IndexOfAny(['[', ']']) < 0
        && (!host.Contains(':') || (endpoint[0] == '[' && IPAddress.TryParse(host, out _)));

    private static int? ParseNumber(string value, int minimum, int maximum)
    {
        var number = IsShortDigitRun(value) ? long.Parse(value, CultureInfo.InvariantCulture) : -1;
        return number >= minimum && number <= maximum ? (int)number : null;
    }

    private static bool IsShortDigitRun(string value) =>
        value.Length is > 0 and <= 10 && value.All(char.IsAsciiDigit);
}
