using System.Net;

namespace Curl.Extension.WireGuard.IpStack;

/// <summary>One IPv4 or IPv6 packet, as the tunnel carries it: its addresses, the protocol it carries and that protocol's bytes.</summary>
/// <param name="Source">The sender's address; its family chooses IPv4 or IPv6.</param>
/// <param name="Destination">The receiver's address, of the same family.</param>
/// <param name="Protocol">The IP protocol number of the payload (IPv4 Protocol, IPv6 Next Header), such as <see cref="IpPacketCodec.TcpProtocol" />.</param>
/// <param name="Payload">The bytes after the IP header.</param>
public sealed record IpPacket(IPAddress Source, IPAddress Destination, byte Protocol, byte[] Payload);
