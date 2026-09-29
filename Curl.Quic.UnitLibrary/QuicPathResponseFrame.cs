namespace Curl.Quic;

/// <summary>
/// A PATH_RESPONSE frame (RFC 9000 section 19.18): the eight bytes of a PATH_CHALLENGE, echoed.
/// </summary>
/// <param name="Data">The eight bytes.</param>
public sealed record QuicPathResponseFrame(ReadOnlyMemory<byte> Data) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.PathResponse;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteBytes(Data.Span);
}
