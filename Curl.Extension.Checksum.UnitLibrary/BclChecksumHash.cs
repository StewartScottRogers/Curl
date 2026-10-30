using System.Security.Cryptography;

namespace Curl.Extension.Checksum;

/// <summary>A hash the base class library provides (SHA-1, SHA-256, SHA-384 or SHA-512).</summary>
/// <param name="algorithm">The base class library's algorithm.</param>
internal sealed class BclChecksumHash(HashAlgorithmName algorithm) : IChecksumHash
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(algorithm);

    public void AppendData(ReadOnlySpan<byte> data) => _hash.AppendData(data);

    public byte[] GetHashAndReset() => _hash.GetHashAndReset();

    public void Dispose() => _hash.Dispose();
}
