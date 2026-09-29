namespace Curl.Quic;

/// <summary>
/// A CONNECTION_CLOSE frame (RFC 9000 section 19.19). With a <see cref="FrameType" /> it is
/// type 0x1c and carries a transport error; without one it is type 0x1d and carries an
/// application error.
/// </summary>
/// <param name="ErrorCode">The error: a <see cref="QuicTransportErrorCode" /> for type 0x1c, the application protocol's code for 0x1d.</param>
/// <param name="FrameType">The type of the frame that caused a transport error (0 when unknown), or <see langword="null" /> for an application close.</param>
/// <param name="ReasonPhrase">Why, as bytes that should be UTF-8; may be empty.</param>
public sealed record QuicConnectionCloseFrame(ulong ErrorCode, ulong? FrameType, ReadOnlyMemory<byte> ReasonPhrase) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => FrameType is null ? QuicFrameType.ConnectionCloseApplication : QuicFrameType.ConnectionClose;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(ErrorCode);
        if (FrameType is { } frameType)
        {
            writer.WriteVariableLengthInteger(frameType);
        }

        writer.WriteLengthPrefixedBytes(ReasonPhrase.Span);
    }
}
