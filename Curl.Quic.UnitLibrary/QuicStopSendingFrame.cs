namespace Curl.Quic;

/// <summary>
/// A STOP_SENDING frame (RFC 9000 section 19.5): the receiver asks the peer to stop sending on a stream.
/// </summary>
/// <param name="StreamId">The stream.</param>
/// <param name="ApplicationErrorCode">Why, in the application protocol's terms.</param>
public sealed record QuicStopSendingFrame(ulong StreamId, ulong ApplicationErrorCode) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.StopSending;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(ApplicationErrorCode);
    }
}
