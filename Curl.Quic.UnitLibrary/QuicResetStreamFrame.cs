namespace Curl.Quic;

/// <summary>
/// A RESET_STREAM frame (RFC 9000 section 19.4): the sender abandons the sending part of a stream.
/// </summary>
/// <param name="StreamId">The stream.</param>
/// <param name="ApplicationErrorCode">Why, in the application protocol's terms.</param>
/// <param name="FinalSize">The final size of the stream, in bytes.</param>
public sealed record QuicResetStreamFrame(ulong StreamId, ulong ApplicationErrorCode, ulong FinalSize) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.ResetStream;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(ApplicationErrorCode);
        writer.WriteVariableLengthInteger(FinalSize);
    }
}
