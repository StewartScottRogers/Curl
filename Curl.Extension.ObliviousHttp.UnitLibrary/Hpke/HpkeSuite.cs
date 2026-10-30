using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary>
/// One HPKE cipher suite (RFC 9180 section 7): the KEM, KDF and AEAD identifiers an Oblivious
/// HTTP key configuration names (RFC 9458 section 3).
/// </summary>
/// <param name="Kem">The key encapsulation mechanism, DHKEM(X25519, HKDF-SHA256) for Oblivious HTTP.</param>
/// <param name="Kdf">The key derivation function, HKDF-SHA256.</param>
/// <param name="Aead">The AEAD: AES-128-GCM, AES-256-GCM or ChaCha20Poly1305.</param>
public readonly record struct HpkeSuite(HpkeKem Kem, HpkeKdf Kdf, HpkeAead Aead);
