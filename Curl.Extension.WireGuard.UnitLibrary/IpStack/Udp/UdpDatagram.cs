namespace Curl.Extension.WireGuard.IpStack.Udp;

/// <summary>One UDP datagram (RFC 768) as the IP stack reads it: its ports and the data after its header.</summary>
/// <param name="SourcePort">The sender's port.</param>
/// <param name="DestinationPort">The receiver's port.</param>
/// <param name="Payload">The bytes after the 8-byte header, up to the datagram's Length field.</param>
public sealed record UdpDatagram(ushort SourcePort, ushort DestinationPort, byte[] Payload);
