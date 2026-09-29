namespace Curl.Quic;

/// <summary>
/// A STREAMS_BLOCKED frame (RFC 9000 section 19.14): the sender wants to open a stream of
/// one direction but the peer's limit stops it.
/// </summary>
/// <param name="IsUnidirectional">Whether the limit is for unidirectional streams (type 0x17) rather than bidirectional ones (type 0x16).</param>
/// <param name="MaximumStreams">The stream limit it is blocked at, at most 2^60.</param>
public sealed record QuicStreamsBlockedFrame(bool IsUnidirectional, ulong MaximumStreams) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => IsUnidirectional ? QuicFrameType.StreamsBlockedUnidirectional : QuicFrameType.StreamsBlockedBidirectional;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteVariableLengthInteger(MaximumStreams);
}
