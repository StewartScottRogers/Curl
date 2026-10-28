using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Tells an IPv4 address written in any form but the strict dotted quad - fewer than four parts
/// (<c>127.1</c>, <c>0</c>, <c>2130706433</c>), a hexadecimal or octal part (<c>0x7f.0.0.1</c>) or a
/// leading zero (<c>127.0.0.01</c>) - from one written as four decimal parts.
/// </summary>
/// <remarks>
/// <see cref="IPAddress.TryParse(string, out IPAddress)" /> reads every such form as <c>inet_aton</c>
/// does, but curl 8.21.0's Schannel build on Windows resolves none of them: <c>--connect-to</c> to
/// <c>127.1</c> is exit 6 <c>Could not resolve host: 127.1</c> and <c>--interface 127.1</c> exit 45,
/// while curl 8.18.0's OpenSSL build on Linux reads them as the system resolver does (measured, BL-3575,
/// ADR-0908). A URL's host never reaches here in shorthand: the URL parser writes it as a dotted quad.
/// </remarks>
internal static class Ipv4ShorthandAddress
{
    /// <summary>
    /// Tells whether <paramref name="host" /> is an IPv4 address in a form other than the strict dotted
    /// quad, which is the only form <see cref="IPAddress.ToString" /> writes.
    /// </summary>
    /// <param name="host">The host as given.</param>
    /// <returns><see langword="true" /> for an IPv4 address in shorthand; otherwise <see langword="false" />.</returns>
    public static bool IsShorthand(string host) =>
        IPAddress.TryParse(host, out var address)
        && address.AddressFamily == AddressFamily.InterNetwork
        && !string.Equals(address.ToString(), host, StringComparison.Ordinal);
}
