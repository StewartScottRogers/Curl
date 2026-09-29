namespace Curl.Cryptography;

/// <summary>
/// The HPKE key derivation functions <see cref="Hpke" /> supports, valued by their
/// RFC 9180 section 7.2 identifier.
/// </summary>
public enum HpkeKdf : ushort
{
    /// <summary>HKDF-SHA256, <c>0x0001</c>, from the BCL's <see cref="System.Security.Cryptography.HKDF" />.</summary>
    HkdfSha256 = 0x0001,
}
