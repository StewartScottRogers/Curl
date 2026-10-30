namespace Curl.Extension.ContentDigest;

/// <summary>
/// The hashing algorithm names the extension uses, from the Hash Algorithms for HTTP Digest Fields registry
/// (RFC 9530 §5, §7.2): the two with status "Active". The deprecated ones (md5, sha, unixsum, unixcksum, adler,
/// crc32c) are ignored when received and never sent (§6).
/// </summary>
internal static class DigestAlgorithms
{
    /// <summary>SHA-256 (RFC 6234).</summary>
    internal const string Sha256 = "sha-256";

    /// <summary>SHA-512 (RFC 6234).</summary>
    internal const string Sha512 = "sha-512";
}
