using System.Security.Cryptography;

namespace Curl.Quic;

/// <summary>
/// The Retry Integrity Tag of QUIC version 1 (RFC 9001 section 5.8): <c>AEAD_AES_128_GCM</c>
/// under a fixed key and nonce over an empty plaintext, with the Retry pseudo-packet as
/// associated data: the Original Destination Connection ID with a one-byte length, then the
/// Retry packet without its tag. A Retry whose tag does not match is discarded.
/// </summary>
public static class QuicRetryIntegrity
{
    private static ReadOnlySpan<byte> Key =>
    [
        0xbe, 0x0c, 0x69, 0x0b, 0x9f, 0x66, 0x57, 0x5a, 0x1d, 0x76, 0x6b, 0x54, 0xe3, 0x68, 0xc8, 0x4e,
    ];

    private static ReadOnlySpan<byte> Nonce =>
    [
        0x46, 0x15, 0x99, 0xd3, 0x5d, 0x63, 0x2b, 0xf2, 0x23, 0x98, 0x25, 0xbb,
    ];

    /// <summary>Returns the tag a Retry packet sent in answer to a client Initial must carry.</summary>
    /// <param name="originalDestinationConnectionId">The Destination Connection ID of the client Initial the Retry answers.</param>
    /// <param name="packet">The Retry packet; its own tag is ignored.</param>
    /// <returns>The 16-byte tag.</returns>
    /// <exception cref="ArgumentException"><paramref name="originalDestinationConnectionId" /> is longer than 20 bytes.</exception>
    public static byte[] ComputeTag(ReadOnlySpan<byte> originalDestinationConnectionId, QuicRetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(originalDestinationConnectionId.Length, QuicFrameCodec.MaximumConnectionIdLength, nameof(originalDestinationConnectionId));
        byte[] encoded = QuicPacketCodec.Encode(packet with { RetryIntegrityTag = new byte[QuicRetryPacket.RetryIntegrityTagLength] });
        var pseudoPacket = new byte[1 + originalDestinationConnectionId.Length + encoded.Length - QuicRetryPacket.RetryIntegrityTagLength];
        pseudoPacket[0] = (byte)originalDestinationConnectionId.Length;
        originalDestinationConnectionId.CopyTo(pseudoPacket.AsSpan(1));
        encoded.AsSpan(0, encoded.Length - QuicRetryPacket.RetryIntegrityTagLength).CopyTo(pseudoPacket.AsSpan(1 + originalDestinationConnectionId.Length));
        var tag = new byte[QuicRetryPacket.RetryIntegrityTagLength];
        using var aesGcm = new AesGcm(Key, QuicRetryPacket.RetryIntegrityTagLength);
        aesGcm.Encrypt(Nonce, [], [], tag, pseudoPacket);
        return tag;
    }

    /// <summary>Returns whether a Retry packet's tag is the one <see cref="ComputeTag" /> gives, compared in fixed time.</summary>
    /// <param name="originalDestinationConnectionId">The Destination Connection ID of the client Initial the Retry answers.</param>
    /// <param name="packet">The Retry packet as received.</param>
    /// <returns>Whether the Retry is authentic; a client discards one that is not.</returns>
    public static bool HasValidTag(ReadOnlySpan<byte> originalDestinationConnectionId, QuicRetryPacket packet) =>
        CryptographicOperations.FixedTimeEquals(ComputeTag(originalDestinationConnectionId, packet), packet.RetryIntegrityTag.Span);
}
