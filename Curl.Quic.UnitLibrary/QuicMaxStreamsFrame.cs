namespace Curl.Quic;

/// <summary>
/// A MAX_STREAMS frame (RFC 9000 section 19.11): how many streams of one direction the peer may open.
/// </summary>
/// <param name="IsUnidirectional">Whether the limit is for unidirectional streams (type 0x13) rather than bidirectional ones (type 0x12).</param>
/// <param name="MaximumStreams">The cumulative number of streams allowed, at most 2^60.</param>
public sealed record QuicMaxStreamsFrame(bool IsUnidirectional, ulong MaximumStreams) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => IsUnidirectional ? QuicFrameType.MaxStreamsUnidirectional : QuicFrameType.MaxStreamsBidirectional;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteVariableLengthInteger(MaximumStreams);
}
