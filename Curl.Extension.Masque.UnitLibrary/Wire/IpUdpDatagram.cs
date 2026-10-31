using System.Net;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// One UDP datagram as a CONNECT-IP tunnel carries it inside an IP packet.
/// </summary>
/// <param name="Source">The sender's address and port.</param>
/// <param name="Destination">The receiver's address and port, of the same family as <paramref name="Source" />.</param>
/// <param name="Payload">The UDP payload.</param>
internal sealed record IpUdpDatagram(IPEndPoint Source, IPEndPoint Destination, ReadOnlyMemory<byte> Payload);
