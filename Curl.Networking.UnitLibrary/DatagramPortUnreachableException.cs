using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// The <see cref="SocketException" /> a <see cref="PortUnreachableReportingDatagramChannel" /> throws when
/// the operating system reports the server's UDP port closed (an ICMP port unreachable, which Windows gives
/// a UDP socket as <see cref="SocketError.ConnectionReset" />), carrying curl's exit 7 text, so the engine
/// ends the transfer with exit 7 rather than letting it escape as exit 2 (BL-4654).
/// </summary>
/// <remarks>
/// It keeps the socket error it replaces, so a handler that already catches that <see cref="SocketException" />,
/// as TFTP's does, answers as before.
/// </remarks>
public sealed class DatagramPortUnreachableException : SocketException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="socketError">The error the socket reported.</param>
    /// <param name="message">curl's exit 7 text for the server.</param>
    public DatagramPortUnreachableException(SocketError socketError, string message)
        : base((int)socketError, message)
    {
    }
}
