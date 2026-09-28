using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>The hashes a TLS 1.3 cipher suite names, and their output lengths.</summary>
internal static class Tls13HashLength
{
    /// <summary>Returns the output length of <paramref name="hashAlgorithm" />, which must be SHA-256 or SHA-384.</summary>
    /// <exception cref="ArgumentException">The hash is neither SHA-256 nor SHA-384.</exception>
    public static int Of(HashAlgorithmName hashAlgorithm)
    {
        if (hashAlgorithm == HashAlgorithmName.SHA256)
        {
            return SHA256.HashSizeInBytes;
        }

        if (hashAlgorithm == HashAlgorithmName.SHA384)
        {
            return SHA384.HashSizeInBytes;
        }

        throw new ArgumentException($"TLS 1.3 cipher suites use SHA-256 or SHA-384, not {hashAlgorithm.Name}.", nameof(hashAlgorithm));
    }
}
