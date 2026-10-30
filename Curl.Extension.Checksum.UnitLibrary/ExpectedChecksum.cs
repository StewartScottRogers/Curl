using System.Security.Cryptography;

namespace Curl.Extension.Checksum;

/// <summary>The digest <c>--checksum</c> expects: a FIPS 180-4 hash algorithm and its digest bytes (ADR-1096).</summary>
/// <param name="AlgorithmName">The algorithm as the value named it, in lower case, such as <c>sha256</c>.</param>
/// <param name="CreateHash">Starts a hash of the algorithm.</param>
/// <param name="Digest">The expected digest.</param>
internal sealed record ExpectedChecksum(string AlgorithmName, Func<IChecksumHash> CreateHash, byte[] Digest)
{
    private static readonly Dictionary<string, (Func<IChecksumHash>? CreateHash, int Length)> Algorithms = new()
    {
        ["sha1"] = (() => new BclChecksumHash(HashAlgorithmName.SHA1), 20),
        ["sha224"] = (() => new Sha256Hash(Sha2Constants.InitialHash224, 28), 28),
        ["sha256"] = (() => new BclChecksumHash(HashAlgorithmName.SHA256), 32),
        ["sha384"] = (() => new BclChecksumHash(HashAlgorithmName.SHA384), 48),
        ["sha512"] = (() => new BclChecksumHash(HashAlgorithmName.SHA512), 64),
        ["sha512-224"] = (() => new Sha512Hash(Sha2Constants.InitialHash512Truncated(224), 28), 28),
        ["sha512-256"] = (() => new Sha512Hash(Sha2Constants.InitialHash512Truncated(256), 32), 32),
    };

    /// <summary>Reads <c>&lt;alg&gt;:&lt;hex&gt;</c>, the algorithm and hexadecimal digits in either case.</summary>
    /// <param name="text">The option's value.</param>
    /// <returns>
    /// The expected digest, or <see langword="null" /> when the algorithm is not <c>sha1</c>, <c>sha224</c>, <c>sha256</c>,
    /// <c>sha384</c>, <c>sha512</c>, <c>sha512-224</c> or <c>sha512-256</c>, or the digits are not hexadecimal or not that algorithm's digest length.
    /// </returns>
    public static ExpectedChecksum? TryParse(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? string.Empty : text[..colon].ToLowerInvariant();
        var (createHash, length) = DigestOf(name);
        var hex = text[(colon + 1)..];
        return length > 0 && hex.Length == length * 2 && IsHexadecimal(hex)
            ? new ExpectedChecksum(name, createHash!, Convert.FromHexString(hex))
            : null;
    }

    /// <summary>Gives the value's digest in <c>&lt;alg&gt;:&lt;hex&gt;</c> form, in lower case.</summary>
    /// <param name="digest">The digest to write.</param>
    /// <returns>The algorithm name, a colon and the digest's lower-case hexadecimal digits.</returns>
    public string Format(byte[] digest) => $"{AlgorithmName}:{Convert.ToHexStringLower(digest)}";

    /// <summary>Gives the algorithm a name stands for and its digest length in bytes (FIPS 180-4 §1).</summary>
    private static (Func<IChecksumHash>? CreateHash, int Length) DigestOf(string name) =>
        Algorithms.TryGetValue(name, out var algorithm) ? algorithm : (null, 0);

    private static bool IsHexadecimal(string hex) => hex.All(char.IsAsciiHexDigit);
}
