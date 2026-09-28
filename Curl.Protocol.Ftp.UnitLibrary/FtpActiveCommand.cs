using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Writes the <c>EPRT</c> and <c>PORT</c> commands that announce an active-mode listening
/// port, byte for byte as curl 8.21.0 sends them (ADR-0102's BL-437 addendum).
/// </summary>
internal static class FtpActiveCommand
{
    private const int PortByteBase = 256;

    /// <summary>
    /// Writes <c>EPRT |1|127.0.0.1|40000|</c>, or <c>EPRT |2|::1|40000|</c> for IPv6.
    /// </summary>
    /// <param name="listening">The address and port the listener is bound to.</param>
    /// <returns>The command, without its line end.</returns>
    public static string Eprt(IPEndPoint listening)
    {
        char family = listening.AddressFamily == AddressFamily.InterNetworkV6 ? '2' : '1';
        return string.Create(CultureInfo.InvariantCulture, $"EPRT |{family}|{listening.Address}|{listening.Port}|");
    }

    /// <summary>
    /// Writes <c>PORT 127,0,0,1,156,64</c>: the four address bytes, then the port's high
    /// and low byte.
    /// </summary>
    /// <param name="listening">The IPv4 address and port the listener is bound to.</param>
    /// <returns>The command, without its line end.</returns>
    public static string Port(IPEndPoint listening)
    {
        byte[] address = listening.Address.GetAddressBytes();
        int port = listening.Port;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"PORT {address[0]},{address[1]},{address[2]},{address[3]},{port / PortByteBase},{port % PortByteBase}");
    }
}
