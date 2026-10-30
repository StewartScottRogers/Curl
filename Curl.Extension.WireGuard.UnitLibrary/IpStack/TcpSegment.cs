namespace Curl.Extension.WireGuard.IpStack;

/// <summary>One TCP segment (RFC 9293 section 3.1).</summary>
/// <param name="SourcePort">The sender's port.</param>
/// <param name="DestinationPort">The receiver's port.</param>
/// <param name="SequenceNumber">The sequence number of the first octet, or of the SYN.</param>
/// <param name="AcknowledgementNumber">The next sequence number the sender expects, when <see cref="TcpFlags.Ack" /> is set.</param>
/// <param name="Flags">The control bits.</param>
/// <param name="Window">The receive window the sender advertises.</param>
/// <param name="MaximumSegmentSize">The Maximum Segment Size option, or <see langword="null" /> when the segment has none.</param>
/// <param name="Payload">The data the segment carries.</param>
public sealed record TcpSegment(
    ushort SourcePort,
    ushort DestinationPort,
    uint SequenceNumber,
    uint AcknowledgementNumber,
    TcpFlags Flags,
    ushort Window,
    ushort? MaximumSegmentSize,
    byte[] Payload)
{
    /// <summary>How much sequence space the segment takes: its data, plus one each for SYN and FIN.</summary>
    public uint SequenceLength =>
        (uint)Payload.Length + (Flags.HasFlag(TcpFlags.Syn) ? 1u : 0u) + (Flags.HasFlag(TcpFlags.Fin) ? 1u : 0u);
}
