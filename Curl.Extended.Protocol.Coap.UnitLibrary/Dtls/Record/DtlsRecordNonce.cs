using System.Buffers.Binary;

namespace Curl.Extended.Protocol.Coap.Dtls.Record;

/// <summary>
/// The 12-byte AES-GCM or AES-CCM nonce of a DTLS 1.2 record (RFC 5288 section 3): the 4-byte write IV
/// followed by the 8-byte explicit part, which the sender sets to the record's 64-bit epoch and
/// sequence number and sends before the ciphertext.
/// </summary>
internal static class DtlsRecordNonce
{
    /// <summary>The length in bytes of the explicit part sent in the record.</summary>
    public const int ExplicitLength = 8;

    /// <summary>The length in bytes of the authentication tag that follows the ciphertext.</summary>
    public const int TagLength = 16;

    /// <summary>Writes the 8-byte explicit part for <paramref name="epochAndSequenceNumber" />.</summary>
    public static byte[] ExplicitPart(ulong epochAndSequenceNumber)
    {
        byte[] explicitPart = new byte[ExplicitLength];
        BinaryPrimitives.WriteUInt64BigEndian(explicitPart, epochAndSequenceNumber);
        return explicitPart;
    }

    /// <summary>Joins the 4-byte write IV and the 8-byte explicit part into the nonce.</summary>
    public static byte[] Combine(ReadOnlySpan<byte> writeIv, ReadOnlySpan<byte> explicitPart) => [.. writeIv, .. explicitPart];
}
