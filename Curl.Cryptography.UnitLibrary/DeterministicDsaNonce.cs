using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// RFC 6979 section 3.2's HMAC_DRBG that derives a DSA nonce k from the private key and the
/// message hash: <see cref="NextCandidate" /> gives the candidates of step h in order, and
/// the caller rejects one outside 1 &lt;= k &lt; q (or one that gives r or s of 0) and asks
/// for the next. Its inputs, x and bits2octets(h1), are the subprime's length, which is a
/// whole number of bytes for every DSA size FIPS 186-4 allows, so bits2int only truncates.
/// </summary>
/// <remarks>
/// HMAC is the BCL's for SHA-1, SHA-256, SHA-384 and SHA-512, and
/// <see cref="FixedBlockHmac" />'s on <see cref="Sha224" /> for SHA-224, which the BCL
/// lacks. K and V are zeroed on <see cref="Dispose" />, every temporary before its method
/// returns.
/// </remarks>
internal sealed class DeterministicDsaNonce : IDisposable
{
    private readonly HashAlgorithmName hash;
    private readonly byte[] key;
    private readonly byte[] value;
    private readonly byte[] mac;
    private bool started;

    /// <summary>Runs steps b to f: V = 0x01..., K = 0x00..., then the two updates with x and the hash.</summary>
    /// <param name="hash">The hash H the message was hashed with: SHA1, SHA224, SHA256, SHA384 or SHA512.</param>
    /// <param name="privateKey">int2octets(x): x, big-endian, the subprime's length.</param>
    /// <param name="reducedHash">bits2octets(h1): the hash's leftmost bits reduced mod q, the subprime's length.</param>
    public DeterministicDsaNonce(HashAlgorithmName hash, ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> reducedHash)
    {
        this.hash = hash;
        int length = DigestLength(hash);
        key = new byte[length];
        value = new byte[length];
        mac = new byte[length];
        value.AsSpan().Fill(0x01);
        Update(0x00, privateKey, reducedHash);
        Update(0x01, privateKey, reducedHash);
    }

    /// <summary>Returns the length in bytes of <paramref name="hash" />'s digest.</summary>
    /// <exception cref="ArgumentException"><paramref name="hash" /> is not SHA1, SHA224, SHA256, SHA384 or SHA512.</exception>
    public static int DigestLength(HashAlgorithmName hash) => hash.Name switch
    {
        "SHA1" => 20,
        "SHA224" => 28,
        "SHA256" => 32,
        "SHA384" => 48,
        "SHA512" => 64,
        _ => throw new ArgumentException($"A DSA signature is made over SHA1, SHA224, SHA256, SHA384 or SHA512, not {hash.Name}.", nameof(hash)),
    };

    /// <summary>
    /// Writes the next candidate k, big-endian, to <paramref name="candidate" /> (the
    /// subprime's length): step h.2's T, cut to qlen bits. Every call after the first runs
    /// step h.3's K = HMAC_K(V || 0x00), V = HMAC_K(V) first.
    /// </summary>
    public void NextCandidate(Span<byte> candidate)
    {
        if (started)
        {
            Update(0x00, [], []);
        }

        started = true;
        for (int offset = 0; offset < candidate.Length; offset += value.Length)
        {
            Mac(value, value);
            value.AsSpan(0, Math.Min(value.Length, candidate.Length - offset)).CopyTo(candidate[offset..]);
        }
    }

    /// <summary>Zeroes K and V.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(value);
        CryptographicOperations.ZeroMemory(mac);
    }

    /// <summary>K = HMAC_K(V || <paramref name="separator" /> || <paramref name="privateKey" /> || <paramref name="reducedHash" />), then V = HMAC_K(V).</summary>
    private void Update(byte separator, ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> reducedHash)
    {
        byte[] message = new byte[value.Length + 1 + privateKey.Length + reducedHash.Length];
        try
        {
            value.CopyTo(message, 0);
            message[value.Length] = separator;
            privateKey.CopyTo(message.AsSpan(value.Length + 1));
            reducedHash.CopyTo(message.AsSpan(value.Length + 1 + privateKey.Length));
            Mac(message, key);
            Mac(value, value);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
        }
    }

    /// <summary>Writes HMAC_K(<paramref name="message" />) to <paramref name="destination" />, which may be K or V.</summary>
    private void Mac(ReadOnlySpan<byte> message, Span<byte> destination)
    {
        if (hash.Name == "SHA224")
        {
            FixedBlockHmac.Compute<Sha224>(key, [], message, message.Length, message.Length, mac);
        }
        else
        {
            CryptographicOperations.HmacData(hash, key, message, mac);
        }

        mac.CopyTo(destination);
    }
}
