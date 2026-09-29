namespace Curl.Quic;

/// <summary>
/// A run of PADDING frames (RFC 9000 section 19.1): each is one zero byte, and a run of
/// them is read as one record.
/// </summary>
/// <param name="Length">How many PADDING frames, and so how many zero bytes; at least 1.</param>
public sealed record QuicPaddingFrame(int Length) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.Padding;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteBytes(new byte[Length - 1]);
}
