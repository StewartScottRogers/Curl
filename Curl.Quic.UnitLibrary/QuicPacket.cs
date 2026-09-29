namespace Curl.Quic;

/// <summary>
/// One QUIC version 1 packet as it is on the wire, with header protection removed (RFC 9000
/// section 17). <see cref="QuicPacketCodec" /> reads and writes them; each derived record is
/// one header form.
/// </summary>
public abstract record QuicPacket
{
    /// <summary>
    /// Writes the packet.
    /// </summary>
    /// <param name="writer">Where to write it.</param>
    internal abstract void WriteTo(QuicWriter writer);

    /// <summary>
    /// Writes a long header's connection ID, length first.
    /// </summary>
    /// <param name="writer">Where to write it.</param>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="maximumLength">The longest the header form allows.</param>
    private protected static void WriteConnectionId(QuicWriter writer, ReadOnlyMemory<byte> connectionId, int maximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(connectionId.Length, maximumLength, nameof(connectionId));
        writer.WriteByte((byte)connectionId.Length);
        writer.WriteBytes(connectionId.Span);
    }
}
