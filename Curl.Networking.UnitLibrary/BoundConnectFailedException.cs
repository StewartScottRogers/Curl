using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// A connect that failed from a socket already bound to a local port (<c>--local-port</c>): the
/// failure's <see cref="SocketError" /> and message, and the local end point the socket was bound to,
/// which curl names in its <c>connect to ... from 0.0.0.0 port 49000 failed</c> line (BL-3306).
/// </summary>
public sealed class BoundConnectFailedException : SocketException
{
    /// <summary>Creates the failure.</summary>
    /// <param name="error">The connect's error.</param>
    /// <param name="message">The connect's message, kept as the operating system gave it.</param>
    /// <param name="localEndPoint">The local end point the socket was bound to.</param>
    public BoundConnectFailedException(SocketError error, string message, IPEndPoint localEndPoint)
        : base((int)error, message)
    {
        ArgumentNullException.ThrowIfNull(localEndPoint);

        LocalEndPoint = localEndPoint;
    }

    /// <summary>Gets the local end point the socket was bound to.</summary>
    public IPEndPoint LocalEndPoint { get; }
}
