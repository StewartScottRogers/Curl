namespace Curl.Quic;

/// <summary>
/// A NEW_TOKEN frame (RFC 9000 section 19.7): a token the client may put in a future Initial packet.
/// </summary>
/// <param name="Token">The token; never empty.</param>
public sealed record QuicNewTokenFrame(ReadOnlyMemory<byte> Token) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.NewToken;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteLengthPrefixedBytes(Token.Span);
}
