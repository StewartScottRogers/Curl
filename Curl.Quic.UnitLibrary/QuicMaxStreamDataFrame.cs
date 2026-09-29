namespace Curl.Quic;

/// <summary>
/// A MAX_STREAM_DATA frame (RFC 9000 section 19.10): one stream's flow control limit.
/// </summary>
/// <param name="StreamId">The stream.</param>
/// <param name="MaximumStreamData">The most bytes the peer may send on it.</param>
public sealed record QuicMaxStreamDataFrame(ulong StreamId, ulong MaximumStreamData) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.MaxStreamData;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(MaximumStreamData);
    }
}
