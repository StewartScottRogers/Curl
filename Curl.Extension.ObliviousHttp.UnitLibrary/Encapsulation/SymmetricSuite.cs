using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>One <c>HpkeSymmetricAlgorithms</c> entry of a key configuration (RFC 9458 section 3.1): a KDF and an AEAD.</summary>
/// <param name="Kdf">The HPKE KDF identifier.</param>
/// <param name="Aead">The HPKE AEAD identifier.</param>
public readonly record struct SymmetricSuite(HpkeKdf Kdf, HpkeAead Aead)
{
    /// <summary>Whether this library implements both the KDF and the AEAD.</summary>
    public bool IsSupported => Enum.IsDefined(Kdf) && Enum.IsDefined(Aead);
}
