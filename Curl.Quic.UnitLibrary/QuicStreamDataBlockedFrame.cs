namespace Curl.Quic;

/// <summary>
/// A STREAM_DATA_BLOCKED frame (RFC 9000 section 19.13): the sender has data to send on a
/// stream but that stream's limit stops it.
/// </summary>
/// <param name="StreamId">The stream.</param>
/// <param name="MaximumStreamData">The stream's limit it is blocked at.</param>
public sealed record QuicStreamDataBlockedFrame(ulong StreamId, ulong MaximumStreamData) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.StreamDataBlocked;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(MaximumStreamData);
    }
}
