namespace Curl.Quic;

/// <summary>
/// A MAX_DATA frame (RFC 9000 section 19.9): the connection-level flow control limit.
/// </summary>
/// <param name="MaximumData">The most bytes the peer may send on all streams together.</param>
public sealed record QuicMaxDataFrame(ulong MaximumData) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.MaxData;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteVariableLengthInteger(MaximumData);
}
