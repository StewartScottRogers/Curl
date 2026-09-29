namespace Curl.Quic;

/// <summary>
/// A PING frame (RFC 9000 section 19.2): no fields; it makes the packet ack-eliciting.
/// </summary>
public sealed record QuicPingFrame : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.Ping;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
    }
}
