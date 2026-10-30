using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// The signature algorithms RFC 8624 section 3.1 says a validator MUST or is RECOMMENDED to support: 8 RSA/SHA-256
/// and 10 RSA/SHA-512 (RFC 5702, keys as RFC 3110 section 2 lays them out), 13 ECDSA P-256/SHA-256 and 14 ECDSA
/// P-384/SHA-384 (RFC 6605 section 4, key Q = x | y and signature r | s), and 15 Ed25519 and 16 Ed448 (RFC 8080).
/// </summary>
internal static class DnssecSignatureAlgorithm
{
    /// <summary>Whether <paramref name="algorithm" /> can be verified here.</summary>
    /// <param name="algorithm">A DNSSEC algorithm number.</param>
    /// <returns>True for 8, 10, 13, 14, 15 and 16.</returns>
    public static bool IsSupported(byte algorithm) => algorithm is 8 or 10 or 13 or 14 or 15 or 16;

    /// <summary>Verifies <paramref name="signature" /> over <paramref name="data" />.</summary>
    /// <param name="algorithm">The algorithm number.</param>
    /// <param name="publicKey">The DNSKEY's public key field.</param>
    /// <param name="data">The signed octets.</param>
    /// <param name="signature">The RRSIG's signature field.</param>
    /// <returns>True only when the signature is valid; false for a malformed key or an unsupported algorithm.</returns>
    public static bool Verify(byte algorithm, byte[] publicKey, byte[] data, byte[] signature) => algorithm switch
    {
        8 => VerifyRsa(publicKey, data, signature, HashAlgorithmName.SHA256),
        10 => VerifyRsa(publicKey, data, signature, HashAlgorithmName.SHA512),
        13 => VerifyEcdsa(publicKey, data, signature, ECCurve.NamedCurves.nistP256, 32, HashAlgorithmName.SHA256),
        14 => VerifyEcdsa(publicKey, data, signature, ECCurve.NamedCurves.nistP384, 48, HashAlgorithmName.SHA384),
        15 => VerifyEd25519(publicKey, data, signature),
        16 => VerifyEd448(publicKey, data, signature),
        _ => false,
    };

    /// <summary>
    /// Reads an RFC 3110 section 2 RSA public key: the exponent's length (one octet, or a zero octet and two), the
    /// exponent, then the modulus.
    /// </summary>
    /// <param name="publicKey">The DNSKEY's public key field.</param>
    /// <param name="parameters">The exponent and modulus.</param>
    /// <returns>False when the fields do not fit or either is empty.</returns>
    internal static bool TryReadRsaPublicKey(byte[] publicKey, out RSAParameters parameters)
    {
        parameters = default;
        if (!TryReadExponentLength(publicKey, out int lengthSize, out int exponentLength))
        {
            return false;
        }

        int modulusStart = lengthSize + exponentLength;
        if (exponentLength == 0 || modulusStart >= publicKey.Length)
        {
            return false;
        }

        parameters = new RSAParameters { Exponent = publicKey[lengthSize..modulusStart], Modulus = publicKey[modulusStart..] };
        return true;
    }

    private static bool TryReadExponentLength(byte[] publicKey, out int lengthSize, out int exponentLength)
    {
        lengthSize = publicKey.Length > 0 && publicKey[0] == 0 ? 3 : 1;
        exponentLength = 0;
        if (publicKey.Length < lengthSize)
        {
            return false;
        }

        exponentLength = lengthSize == 3 ? (publicKey[1] << 8) | publicKey[2] : publicKey[0];
        return true;
    }

    private static bool VerifyRsa(byte[] publicKey, byte[] data, byte[] signature, HashAlgorithmName hash)
    {
        if (!TryReadRsaPublicKey(publicKey, out RSAParameters parameters))
        {
            return false;
        }

        using RSA rsa = RSA.Create();
        try
        {
            rsa.ImportParameters(parameters);
            return rsa.VerifyData(data, signature, hash, RSASignaturePadding.Pkcs1);
        }
        catch (SystemException)
        {
            return false;
        }
    }

    private static bool VerifyEcdsa(
        byte[] publicKey, byte[] data, byte[] signature, ECCurve curve, int coordinateLength, HashAlgorithmName hash)
    {
        if (publicKey.Length != 2 * coordinateLength)
        {
            return false;
        }

        ECParameters parameters = new()
        {
            Curve = curve,
            Q = new ECPoint { X = publicKey[..coordinateLength], Y = publicKey[coordinateLength..] },
        };
        try
        {
            using ECDsa ecdsa = ECDsa.Create(parameters);
            return ecdsa.VerifyData(data, signature, hash);
        }
        catch (SystemException)
        {
            return false;
        }
    }

    private static bool VerifyEd25519(byte[] publicKey, byte[] data, byte[] signature) =>
        publicKey.Length == Ed25519.PublicKeySize && signature.Length == Ed25519.SignatureSize &&
        Ed25519.Verify(publicKey, data, signature);

    private static bool VerifyEd448(byte[] publicKey, byte[] data, byte[] signature) =>
        publicKey.Length == Ed448.PublicKeySize && signature.Length == Ed448.SignatureSize &&
        Ed448.Verify(publicKey, data, signature);
}
