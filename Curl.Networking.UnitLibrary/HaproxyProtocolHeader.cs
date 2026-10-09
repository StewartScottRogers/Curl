using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// The HAProxy PROXY protocol v1 line <c>--haproxy-protocol</c> and <c>--haproxy-clientip</c> send
/// first on a connection, before any TLS or HTTP bytes, as curl 8.21.0 writes it (measured, BL-616
/// Notes): <c>PROXY TCP4 &lt;local ip&gt; &lt;remote ip&gt; &lt;local port&gt; &lt;remote port&gt;\r\n</c>,
/// or <c>TCP6</c> for an IPv6 connection. With a client IP, that value stands in for both addresses,
/// verbatim and unvalidated, and the line says <c>TCP4</c> only when it is a strict dotted quad
/// (four decimal parts of at most 255 with no leading zero), <c>TCP6</c> for anything else. Over a
/// Unix domain socket, which has no addresses, the line is <c>PROXY UNKNOWN\r\n</c>.
/// </summary>
/// <param name="ClientIp">
/// The <c>--haproxy-clientip</c> value, or <see langword="null" /> to send the connection's own
/// local address.
/// </param>
public sealed record HaproxyProtocolHeader(string? ClientIp)
{
    /// <summary>
    /// Builds the line for a connection between <paramref name="localEndPoint" /> and
    /// <paramref name="remoteEndPoint" />, the socket's own ends, so through a proxy they are the
    /// proxy connection's.
    /// </summary>
    /// <param name="localEndPoint">The connection's local end, <see langword="null" /> when unknown.</param>
    /// <param name="remoteEndPoint">The connection's remote end, <see langword="null" /> over a Unix domain socket.</param>
    /// <returns>The line's UTF-8 bytes (plain ASCII unless a <c>--haproxy-clientip</c> value is not), ending in CRLF.</returns>
    public byte[] Build(IPEndPoint? localEndPoint, IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
        {
            return Encoding.ASCII.GetBytes("PROXY UNKNOWN\r\n");
        }

        var local = localEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
        var (family, source, destination) = ClientIp is { } clientIp
            ? (IsDottedQuad(clientIp) ? "TCP4" : "TCP6", clientIp, clientIp)
            : (remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? "TCP6" : "TCP4", AddressText(local.Address), AddressText(remoteEndPoint.Address));
        return Encoding.UTF8.GetBytes($"PROXY {family} {source} {destination} {local.Port} {remoteEndPoint.Port}\r\n");
    }

    /// <summary>
    /// Whether <paramref name="text" /> is an IPv4 address as curl's <c>inet_pton</c> reads one: four
    /// dot-separated decimal parts, each at most 255, none with a leading zero.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true" /> for a strict dotted quad.</returns>
    internal static bool IsDottedQuad(string text)
    {
        var parts = text.Split('.');
        return parts.Length == 4 && parts.All(IsOctet);
    }

    // Digits only, at most 255, and written back the same, so no leading zero.
    private static bool IsOctet(string part) =>
        byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var octet)
        && part == octet.ToString(CultureInfo.InvariantCulture);

    // inet_ntop writes no scope id, so an IPv6 address is rebuilt from its bytes without one.
    private static string AddressText(IPAddress address) => new IPAddress(address.GetAddressBytes()).ToString();
}
