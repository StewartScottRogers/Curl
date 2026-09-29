namespace Curl.Quic;

/// <summary>
/// A DATA_BLOCKED frame (RFC 9000 section 19.12): the sender has data to send but the
/// connection-level limit stops it.
/// </summary>
/// <param name="MaximumData">The connection-level limit it is blocked at.</param>
public sealed record QuicDataBlockedFrame(ulong MaximumData) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.DataBlocked;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteVariableLengthInteger(MaximumData);
}
