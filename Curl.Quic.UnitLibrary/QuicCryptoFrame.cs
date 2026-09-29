namespace Curl.Quic;

/// <summary>
/// A CRYPTO frame (RFC 9000 section 19.6): TLS handshake bytes at an offset in the
/// packet number space's crypto stream.
/// </summary>
/// <param name="Offset">Where <paramref name="Data" /> starts in the crypto stream.</param>
/// <param name="Data">The handshake bytes.</param>
public sealed record QuicCryptoFrame(ulong Offset, ReadOnlyMemory<byte> Data) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.Crypto;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(Offset);
        writer.WriteLengthPrefixedBytes(Data.Span);
    }
}
