namespace Curl.Quic;

/// <summary>
/// One frame of a QUIC packet's payload (RFC 9000 section 12.4). <see cref="QuicFrameCodec" />
/// reads and writes them; each derived record is one frame type of section 19.
/// </summary>
public abstract record QuicFrame
{
    /// <summary>
    /// Gets the frame type written first on the wire. For a STREAM frame it includes the
    /// OFF, LEN and FIN flags.
    /// </summary>
    public abstract QuicFrameType Type { get; }

    /// <summary>
    /// Writes the frame, type first.
    /// </summary>
    /// <param name="writer">Where to write it.</param>
    internal void WriteTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger((ulong)Type);
        WriteFieldsTo(writer);
    }

    /// <summary>
    /// Writes the fields that follow the frame type.
    /// </summary>
    /// <param name="writer">Where to write them.</param>
    private protected abstract void WriteFieldsTo(QuicWriter writer);
}
