namespace Curl.Quic;

/// <summary>
/// A NEW_CONNECTION_ID frame (RFC 9000 section 19.15): a further connection ID the peer may use.
/// </summary>
/// <param name="SequenceNumber">The connection ID's sequence number.</param>
/// <param name="RetirePriorTo">The sequence number below which connection IDs are to be retired; not above <paramref name="SequenceNumber" />.</param>
/// <param name="ConnectionId">The connection ID, 1 to 20 bytes.</param>
/// <param name="StatelessResetToken">The 16-byte stateless reset token for it.</param>
public sealed record QuicNewConnectionIdFrame(ulong SequenceNumber, ulong RetirePriorTo, ReadOnlyMemory<byte> ConnectionId, ReadOnlyMemory<byte> StatelessResetToken) : QuicFrame
{
    /// <summary>The length of a stateless reset token.</summary>
    public const int StatelessResetTokenLength = 16;

    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.NewConnectionId;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(SequenceNumber);
        writer.WriteVariableLengthInteger(RetirePriorTo);
        writer.WriteByte((byte)ConnectionId.Length);
        writer.WriteBytes(ConnectionId.Span);
        writer.WriteBytes(StatelessResetToken.Span);
    }
}
