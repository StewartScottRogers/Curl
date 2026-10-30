using System.Formats.Asn1;
using System.Numerics;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// An issuer's 2048-bit RSA public key for token type <c>0x0002</c> (RFC 9578 §6.5): the modulus and public exponent of
/// the <c>SubjectPublicKeyInfo</c> the <c>token-key</c> parameter carries, whose algorithm is <c>id-RSASSA-PSS</c>.
/// </summary>
/// <param name="Modulus">The modulus <c>n</c>.</param>
/// <param name="Exponent">The public exponent <c>e</c>.</param>
public sealed record BlindRsaPublicKey(BigInteger Modulus, BigInteger Exponent)
{
    /// <summary>The OID of <c>id-RSASSA-PSS</c> (RFC 4055 §3.1).</summary>
    public const string RsassaPssOid = "1.2.840.113549.1.1.10";

    /// <summary>The length of the modulus, a signature and a blinded message in bytes, <c>Nk</c> (RFC 9578 §6).</summary>
    public const int ModulusLength = 256;

    /// <summary>Reads the key from a DER <c>SubjectPublicKeyInfo</c> (RFC 5280 §4.1) holding an <c>RSAPublicKey</c> (RFC 8017 §A.1.1).</summary>
    /// <param name="subjectPublicKeyInfo">The DER bytes.</param>
    /// <returns>The key, or <see langword="null"/> when the bytes are not an <c>id-RSASSA-PSS</c> key with a 2048-bit modulus.</returns>
    public static BlindRsaPublicKey? Decode(byte[] subjectPublicKeyInfo)
    {
        ArgumentNullException.ThrowIfNull(subjectPublicKeyInfo);
        try
        {
            var spki = new AsnReader(subjectPublicKeyInfo, AsnEncodingRules.DER).ReadSequence();
            var algorithm = spki.ReadSequence().ReadObjectIdentifier();
            var rsaPublicKey = new AsnReader(spki.ReadBitString(out _), AsnEncodingRules.DER).ReadSequence();
            var key = new BlindRsaPublicKey(rsaPublicKey.ReadInteger(), rsaPublicKey.ReadInteger());
            return algorithm == RsassaPssOid && key.Modulus.GetBitLength() == ModulusLength * 8 ? key : null;
        }
        catch (AsnContentException)
        {
            return null;
        }
    }
}
