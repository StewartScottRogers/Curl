namespace Curl.Quic;

/// <summary>What <see cref="QuicDatagramAssembler" /> built: the datagrams to send and, for loss recovery, each packet they carry with its space.</summary>
/// <param name="Datagrams">The datagrams, in order.</param>
/// <param name="Packets">Each packet, in the order sent.</param>
internal sealed record QuicAssembly(List<byte[]> Datagrams, List<(QuicPacketNumberSpace Space, QuicSentPacket Packet)> Packets);
