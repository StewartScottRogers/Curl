namespace Curl.Quic;

/// <summary>
/// A Retry packet (RFC 9000 section 17.2.5): a long header, a token the client echoes in
/// its next Initial, and the 16-byte Retry Integrity Tag of RFC 9001 section 5.8.
/// </summary>
/// <param name="Version">The QUIC version, 1.</param>
/// <param name="DestinationConnectionId">The destination connection ID, at most 20 bytes.</param>
/// <param name="SourceConnectionId">The source connection ID, at most 20 bytes.</param>
/// <param name="RetryToken">The token; never empty.</param>
/// <param name="RetryIntegrityTag">The 16-byte integrity tag.</param>
/// <param name="UnusedBits">The four Unused bits of the first byte.</param>
public sealed record QuicRetryPacket(
    uint Version,
    ReadOnlyMemory<byte> DestinationConnectionId,
    ReadOnlyMemory<byte> SourceConnectionId,
    ReadOnlyMemory<byte> RetryToken,
    ReadOnlyMemory<byte> RetryIntegrityTag,
    byte UnusedBits = 0) : QuicPacket
{
    /// <summary>The length of a Retry Integrity Tag.</summary>
    public const int RetryIntegrityTagLength = 16;

    /// <inheritdoc />
    internal override void WriteTo(QuicWriter writer)
    {
        if (RetryIntegrityTag.Length != RetryIntegrityTagLength)
        {
            throw new ArgumentException($"A Retry Integrity Tag is {RetryIntegrityTagLength} bytes, not {RetryIntegrityTag.Length}.", nameof(RetryIntegrityTag));
        }

        writer.WriteByte((byte)(0xf0 | UnusedBits & 0x0f));
        writer.WriteUInt(Version, 4);
        WriteConnectionId(writer, DestinationConnectionId, QuicFrameCodec.MaximumConnectionIdLength);
        WriteConnectionId(writer, SourceConnectionId, QuicFrameCodec.MaximumConnectionIdLength);
        writer.WriteBytes(RetryToken.Span);
        writer.WriteBytes(RetryIntegrityTag.Span);
    }
}
