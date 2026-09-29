namespace Curl.Quic;

/// <summary>
/// A HANDSHAKE_DONE frame (RFC 9000 section 19.20): no fields; the server confirms the handshake.
/// </summary>
public sealed record QuicHandshakeDoneFrame : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.HandshakeDone;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
    }
}
