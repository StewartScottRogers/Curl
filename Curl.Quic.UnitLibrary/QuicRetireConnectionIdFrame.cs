namespace Curl.Quic;

/// <summary>
/// A RETIRE_CONNECTION_ID frame (RFC 9000 section 19.16): the sender will no longer use a connection ID.
/// </summary>
/// <param name="SequenceNumber">The retired connection ID's sequence number.</param>
public sealed record QuicRetireConnectionIdFrame(ulong SequenceNumber) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.RetireConnectionId;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteVariableLengthInteger(SequenceNumber);
}
