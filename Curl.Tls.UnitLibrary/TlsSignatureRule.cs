using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>What one signature scheme needs of a key: the algorithm, the key's OID, its curve and the hash.</summary>
/// <param name="Kind">The signature algorithm.</param>
/// <param name="KeyOid">The <c>SubjectPublicKeyInfo</c> algorithm OID the key must carry.</param>
/// <param name="CurveOid">The named curve OID the key must be on, or <see langword="null" /> for a key without one.</param>
/// <param name="Hash">The hash the scheme signs with; unused by Ed25519.</param>
/// <param name="PssEncoding">
/// The MGF1 hash and salt length a certificate-style RSASSA-PSS signature states, or
/// <see langword="null" /> for a TLS scheme, whose mask hash is <paramref name="Hash" /> and
/// whose salt is as long as that hash.
/// </param>
internal sealed record TlsSignatureRule(
    TlsSignatureKind Kind,
    string KeyOid,
    string? CurveOid,
    HashAlgorithmName Hash,
    (HashAlgorithmName MaskHash, int SaltLength)? PssEncoding = null);
