namespace Curl.Quic;

/// <summary>
/// A DATAGRAM frame (RFC 9221 section 4): application data carried unreliably, ack-eliciting
/// but never sent again when its packet is lost (section 5). Type 0x31 carries a Length
/// field; type 0x30 runs to the end of the packet.
/// </summary>
/// <param name="Data">The application data.</param>
/// <param name="HasLength">Whether the frame carries a Length field (type 0x31) rather than running to the end of the packet (type 0x30).</param>
public sealed record QuicDatagramFrame(ReadOnlyMemory<byte> Data, bool HasLength = true) : QuicFrame
{
    /// <summary>The most bytes a DATAGRAM frame with a Length field adds to its data, for data under 16384 bytes: the type and a two-byte length.</summary>
    public const int Overhead = 3;

    /// <inheritdoc />
    public override QuicFrameType Type => HasLength ? QuicFrameType.DatagramWithLength : QuicFrameType.Datagram;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        if (HasLength)
        {
            writer.WriteVariableLengthInteger((ulong)Data.Length);
        }

        writer.WriteBytes(Data.Span);
    }
}
