namespace Curl.Quic;

/// <summary>
/// A STREAM frame (RFC 9000 section 19.8): stream data at an offset. The type's OFF bit is
/// set when <see cref="Offset" /> is not zero, its LEN bit when <see cref="HasLength" />,
/// and its FIN bit when <see cref="IsFin" />.
/// </summary>
/// <param name="StreamId">The stream.</param>
/// <param name="Offset">Where <paramref name="Data" /> starts in the stream.</param>
/// <param name="Data">The stream bytes.</param>
/// <param name="IsFin">Whether the frame ends the stream.</param>
/// <param name="HasLength">Whether a Length field is written; without one the data runs to the end of the packet.</param>
public sealed record QuicStreamFrame(ulong StreamId, ulong Offset, ReadOnlyMemory<byte> Data, bool IsFin, bool HasLength = true) : QuicFrame
{
    /// <summary>The OFF bit of a STREAM frame type.</summary>
    internal const ulong OffsetBit = 0x04;

    /// <summary>The LEN bit of a STREAM frame type.</summary>
    internal const ulong LengthBit = 0x02;

    /// <summary>The FIN bit of a STREAM frame type.</summary>
    internal const ulong FinBit = 0x01;

    /// <inheritdoc />
    public override QuicFrameType Type =>
        QuicFrameType.Stream | (QuicFrameType)((Offset == 0 ? 0 : OffsetBit) | (HasLength ? LengthBit : 0) | (IsFin ? FinBit : 0));

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(StreamId);
        if (Offset != 0)
        {
            writer.WriteVariableLengthInteger(Offset);
        }

        if (HasLength)
        {
            writer.WriteVariableLengthInteger((ulong)Data.Length);
        }

        writer.WriteBytes(Data.Span);
    }
}
