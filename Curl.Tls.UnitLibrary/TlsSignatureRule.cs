using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>What one signature scheme needs of a key: the algorithm, the key's OID, its curve and the hash.</summary>
/// <param name="Kind">The signature algorithm.</param>
/// <param name="KeyOid">The <c>SubjectPublicKeyInfo</c> algorithm OID the key must carry.</param>
/// <param name="CurveOid">The named curve OID the key must be on, or <see langword="null" /> for a key without one.</param>
/// <param name="Hash">The hash the scheme signs with; unused by Ed25519.</param>
internal sealed record TlsSignatureRule(TlsSignatureKind Kind, string KeyOid, string? CurveOid, HashAlgorithmName Hash);
