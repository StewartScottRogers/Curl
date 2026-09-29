using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The record MAC of RFC 5246 section 6.2.3.1:
/// <c>HMAC(MAC_write_key, seq_num + type + version + length + content)</c>.
/// </summary>
internal sealed class Tls12RecordMac : IDisposable
{
    private readonly HashAlgorithmName hash;

    private readonly byte[] macKey;

    private readonly IncrementalHash hmac;

    public Tls12RecordMac(HashAlgorithmName hash, byte[] key)
    {
        this.hash = hash;
        macKey = key.ToArray();
        hmac = IncrementalHash.CreateHMAC(hash, key);
    }

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

    /// <summary>
    /// Returns whether a decrypted MAC-then-encrypt record, <c>content || MAC || padding</c>,
    /// carries the MAC of its first <paramref name="contentLength" /> bytes, in time that does
    /// not depend on <paramref name="contentLength" /> (the Lucky Thirteen countermeasure,
    /// BL-795): the HMAC hashes the same number of blocks whatever the padding length
    /// (<see cref="FixedBlockHmac" />), and the received MAC is copied out of the record with
    /// masks over every position it could start at.
    /// </summary>
    /// <param name="sequenceNumber">The record's sequence number.</param>
    /// <param name="contentType">The record's content type.</param>
    /// <param name="version">The record's protocol version.</param>
    /// <param name="plaintext">The decrypted record, at least <see cref="Length" /> bytes.</param>
    /// <param name="contentLength">
    /// The secret content length: at most <c>plaintext.Length - Length</c>, and at most 256
    /// less, the most a padding can take.
    /// </param>
    public bool VerifyInFixedBlocks(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> plaintext, int contentLength)
    {
        int longestContent = plaintext.Length - Length;
        int shortestContent = Math.Max(0, longestContent - Tls12CbcPadding.MaximumPaddingLength - 1);
        Span<byte> header = stackalloc byte[Tls12RecordCipher.AdditionalDataLength];
        Span<byte> expected = stackalloc byte[Length];
        Span<byte> received = stackalloc byte[Length];
        try
        {
            Tls12RecordCipher.WriteAdditionalData(header, sequenceNumber, contentType, version, contentLength);
            FixedBlockHmac.Compute(hash, macKey, header, plaintext[..longestContent], contentLength, shortestContent, expected);
            CopyMacAt(plaintext, contentLength, shortestContent, received);
            return CryptographicOperations.FixedTimeEquals(expected, received);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(received);
        }
    }

    public void Dispose()
    {
        hmac.Dispose();
        CryptographicOperations.ZeroMemory(macKey);
    }

    /// <summary>
    /// Copies the <c>destination.Length</c> bytes at the secret <paramref name="offset" /> of
    /// <paramref name="plaintext" /> into <paramref name="destination" />, reading every byte
    /// from <paramref name="lowestOffset" /> on and selecting with masks (OpenSSL's
    /// <c>ssl3_cbc_copy_mac</c>, without its rotation).
    /// </summary>
    private static void CopyMacAt(ReadOnlySpan<byte> plaintext, int offset, int lowestOffset, Span<byte> destination)
    {
        destination.Clear();
        for (int position = lowestOffset; position < plaintext.Length; position++)
        {
            uint distance = (uint)(position - offset);
            for (int index = 0; index < destination.Length; index++)
            {
                destination[index] |= (byte)(plaintext[position] & ~Tls12CbcPadding.NotEqualMask(distance, (uint)index));
            }
        }
    }
}
