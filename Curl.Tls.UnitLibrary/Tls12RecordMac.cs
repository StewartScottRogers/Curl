using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The record MAC of RFC 5246 section 6.2.3.1:
/// <c>HMAC(MAC_write_key, seq_num + type + version + length + content)</c>.
/// </summary>
internal sealed class Tls12RecordMac(HashAlgorithmName hash, byte[] key) : IDisposable
{
    private readonly IncrementalHash hmac = IncrementalHash.CreateHMAC(hash, key);

    /// <summary>Gets the MAC length in bytes.</summary>
    public int Length => hmac.HashLengthInBytes;

    /// <summary>Writes the MAC of <paramref name="data" /> under this record's header into <paramref name="destination" />.</summary>
    public void Compute(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> data, Span<byte> destination)
    {
        Span<byte> header = stackalloc byte[Tls12RecordCipher.AdditionalDataLength];
        Tls12RecordCipher.WriteAdditionalData(header, sequenceNumber, contentType, version, data.Length);
        hmac.AppendData(header);
        hmac.AppendData(data);
        hmac.GetHashAndReset(destination);
    }

    /// <summary>Returns whether <paramref name="received" /> is the MAC of <paramref name="data" />, compared in fixed time.</summary>
    public bool Verify(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> data, ReadOnlySpan<byte> received)
    {
        Span<byte> expected = stackalloc byte[Length];
        Compute(sequenceNumber, contentType, version, data, expected);
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    public void Dispose() => hmac.Dispose();
}
