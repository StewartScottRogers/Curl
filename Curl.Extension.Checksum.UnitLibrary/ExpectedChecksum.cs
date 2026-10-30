using System.Security.Cryptography;

namespace Curl.Extension.Checksum;

/// <summary>The digest <c>--checksum</c> expects: a FIPS 180-4 hash algorithm and its digest bytes (ADR-1096).</summary>
/// <param name="AlgorithmName">The algorithm as the value named it, in lower case, such as <c>sha256</c>.</param>
/// <param name="Algorithm">The hash algorithm.</param>
/// <param name="Digest">The expected digest.</param>
internal sealed record ExpectedChecksum(string AlgorithmName, HashAlgorithmName Algorithm, byte[] Digest)
{
    /// <summary>Reads <c>&lt;alg&gt;:&lt;hex&gt;</c>, the algorithm and hexadecimal digits in either case.</summary>
    /// <param name="text">The option's value.</param>
    /// <returns>
    /// The expected digest, or <see langword="null" /> when the algorithm is not <c>sha1</c>, <c>sha256</c>,
    /// <c>sha384</c> or <c>sha512</c>, or the digits are not hexadecimal or not that algorithm's digest length.
    /// </returns>
    public static ExpectedChecksum? TryParse(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? string.Empty : text[..colon].ToLowerInvariant();
        var (algorithm, length) = DigestOf(name);
        var hex = text[(colon + 1)..];
        return length > 0 && hex.Length == length * 2 && IsHexadecimal(hex)
            ? new ExpectedChecksum(name, algorithm, Convert.FromHexString(hex))
            : null;
    }

    /// <summary>Gives the value's digest in <c>&lt;alg&gt;:&lt;hex&gt;</c> form, in lower case.</summary>
    /// <param name="digest">The digest to write.</param>
    /// <returns>The algorithm name, a colon and the digest's lower-case hexadecimal digits.</returns>
    public string Format(byte[] digest) => $"{AlgorithmName}:{Convert.ToHexStringLower(digest)}";

    /// <summary>Gives the algorithm a name stands for and its digest length in bytes (FIPS 180-4 §1).</summary>
    private static (HashAlgorithmName Algorithm, int Length) DigestOf(string name) => name switch
    {
        "sha1" => (HashAlgorithmName.SHA1, 20),
        "sha256" => (HashAlgorithmName.SHA256, 32),
        "sha384" => (HashAlgorithmName.SHA384, 48),
        "sha512" => (HashAlgorithmName.SHA512, 64),
        _ => (default, 0),
    };

    private static bool IsHexadecimal(string hex) => hex.All(char.IsAsciiHexDigit);
}
