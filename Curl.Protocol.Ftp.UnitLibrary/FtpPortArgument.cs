using System.Globalization;

namespace Curl.Protocol.Ftp;

/// <summary>
/// A <c>-P</c>/<c>--ftp-port</c> value read as curl 8.21.0 reads it: an address, then an
/// optional <c>:port</c> or <c>:low-high</c> port range (ADR-0102).
/// </summary>
/// <param name="Address">
/// The address part: <c>-</c> or empty for the control connection's own address, an IPv4
/// literal, or an IPv6 literal (written in brackets when a port follows); anything else is
/// a host or interface name, which is not resolved (ADR-0102's BL-437 addendum).
/// </param>
/// <param name="LowPort">The lowest port to listen on; 0 with <paramref name="HighPort" /> 0 for any.</param>
/// <param name="HighPort">The highest port to listen on, never below <paramref name="LowPort" />.</param>
/// <remarks>
/// A port is read as C's <c>atoi</c> reads it, leading digits only, and a range whose low
/// end lies above its high end, or above 65535, means any port, as curl 8.21.0 was
/// measured to take <c>127.0.0.1:40000-39000</c>.
/// </remarks>
internal readonly record struct FtpPortArgument(string Address, int LowPort, int HighPort)
{
    private const int MaxPort = 65535;

    /// <summary>The most digits of a port read before the rest is ignored.</summary>
    private const int MaxPortDigits = 6;

    /// <summary>
    /// Gets a value indicating whether <see cref="Address" /> names the control connection's
    /// own address: <c>-</c>, or nothing before the port.
    /// </summary>
    public bool UsesControlAddress => Address is "" or "-";

    /// <summary>
    /// Reads <paramref name="value" />.
    /// </summary>
    /// <param name="value">The <c>-P</c> value, such as <c>-</c>, <c>192.0.2.1:40000-40010</c> or <c>[::1]:40000</c>.</param>
    /// <returns>The address and port range it names.</returns>
    public static FtpPortArgument Parse(string value)
    {
        (string address, string rest) = SplitAddress(value);
        int colon = rest.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? new FtpPortArgument(address, 0, 0) : WithPorts(address, rest[(colon + 1)..]);
    }

    /// <summary>
    /// Splits the address from what follows it: the text inside brackets, a whole value
    /// with two or more colons (a bare IPv6 literal, with no port), or the text before the
    /// one colon.
    /// </summary>
    private static (string Address, string Remainder) SplitAddress(string value)
    {
        int close = value.IndexOf(']', StringComparison.Ordinal);
        if (value.StartsWith('[') && close > 0)
        {
            return (value[1..close], value[(close + 1)..]);
        }

        int colon = value.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 || value.IndexOf(':', colon + 1) >= 0 ? (value, string.Empty) : (value[..colon], value[colon..]);
    }

    private static FtpPortArgument WithPorts(string address, string ports)
    {
        int dash = ports.IndexOf('-', StringComparison.Ordinal);
        int low = LeadingNumber(dash < 0 ? ports : ports[..dash]);
        int high = dash < 0 ? low : LeadingNumber(ports[(dash + 1)..]);
        return low > high || high > MaxPort ? new FtpPortArgument(address, 0, 0) : new FtpPortArgument(address, low, high);
    }

    /// <summary>The number the leading digits of <paramref name="text" /> make, 0 when it has none.</summary>
    private static int LeadingNumber(string text)
    {
        int digits = 0;
        while (digits < text.Length && digits < MaxPortDigits && char.IsAsciiDigit(text[digits]))
        {
            digits++;
        }

        return digits == 0 ? 0 : int.Parse(text.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
