using System.Security.Cryptography;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// A Digest <c>algorithm=</c> value curl 8.21.0 answers: its hash, and whether it is a
/// <c>-sess</c> variant.
/// </summary>
/// <param name="hash">Hashes bytes; MD5, SHA-256 or SHA-512/256.</param>
/// <param name="isSession">
/// <see langword="true" /> for a <c>-sess</c> variant, whose A1 hashes in the nonce and the
/// client nonce.
/// </param>
internal sealed class DigestAlgorithm(Func<byte[], byte[]> hash, bool isSession)
{
    /// <summary>
    /// MD5, the algorithm when the challenge names none.
    /// </summary>
    internal static readonly DigestAlgorithm Md5 = new(MD5.HashData, isSession: false);

    private static readonly (string Name, DigestAlgorithm Algorithm)[] Known =
    [
        ("MD5-sess", new DigestAlgorithm(MD5.HashData, isSession: true)),
        ("MD5", Md5),
        ("SHA-256", new DigestAlgorithm(SHA256.HashData, isSession: false)),
        ("SHA-256-SESS", new DigestAlgorithm(SHA256.HashData, isSession: true)),
        ("SHA-512-256", new DigestAlgorithm(Sha512Slash256.HashData, isSession: false)),
        ("SHA-512-256-SESS", new DigestAlgorithm(Sha512Slash256.HashData, isSession: true)),
    ];

    /// <summary>
    /// Gets whether this is a <c>-sess</c> variant, whose A1 hashes in the nonce and the
    /// client nonce.
    /// </summary>
    internal bool IsSession { get; } = isSession;

    /// <summary>
    /// Finds the algorithm an <c>algorithm=</c> value names, compared in any case as curl
    /// compares it.
    /// </summary>
    /// <param name="name">The value as received.</param>
    /// <returns>The algorithm; <see langword="null" /> for any other name, which curl rejects.</returns>
    internal static DigestAlgorithm? Find(string name) =>
        Array.Find(Known, known => string.Equals(known.Name, name, StringComparison.OrdinalIgnoreCase)).Algorithm;

    /// <summary>
    /// Hashes a byte string and writes the hash as lowercase hexadecimal, as Digest does.
    /// </summary>
    /// <param name="byteString">One character per byte.</param>
    /// <returns>The hash in lowercase hexadecimal.</returns>
    internal string HashToHex(string byteString) =>
        Convert.ToHexStringLower(hash(Encoding.Latin1.GetBytes(byteString)));
}
