using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// One datagram an <see cref="IDatagramChannel" /> received: how many bytes of the
/// buffer it filled, and where it came from.
/// </summary>
/// <param name="Length">
/// The number of bytes of the receive buffer the datagram filled, starting at its first
/// byte.
/// </param>
/// <param name="RemoteEndPoint">
/// The endpoint the datagram came from, which lets a TFTP handler learn the server's
/// transfer identifier from the first reply and reject datagrams from any other source
/// afterwards (RFC 1350 section 4).
/// </param>
public sealed record DatagramReceived(int Length, EndPoint RemoteEndPoint);
