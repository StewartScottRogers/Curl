using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// The client side of RSABSSA-SHA384-PSS-Deterministic (RFC 9474 §4.2, §4.3, §5), the blind signature token type
/// <c>0x0002</c> uses (RFC 9578 §6): <see cref="Blind"/> hides the message from the issuer and <see cref="Finalize"/>
/// unblinds the issuer's blind signature and checks it is a valid RSASSA-PSS signature (RFC 8017 §8.1.2).
/// </summary>
/// <param name="key">The issuer's public key.</param>
/// <param name="randomBytes">Gives cryptographically random octets: the PSS salt and the blinding factor.</param>
public sealed class BlindRsa(BlindRsaPublicKey key, Func<int, byte[]> randomBytes)
{
    /// <summary>The PSS salt length, SHA-384's output length (RFC 9474 §5).</summary>
    public const int SaltLength = 48;

    // Octets beyond the modulus length drawn for the blinding factor, so reducing them modulo n - 1 is unbiased to 2^-64.
    private const int ExtraRandomLength = 8;

    private BigInteger inverse;

    /// <summary>
    /// Blinds <paramref name="message"/> (RFC 9474 §4.2): encodes it by EMSA-PSS (RFC 8017 §9.1.1), draws the blinding
    /// factor <c>r</c> in [1, n) and gives <c>m * r^e mod n</c>; keeps <c>r</c>'s inverse for <see cref="Finalize"/>.
    /// </summary>
    /// <param name="message">The message to be signed, the token input.</param>
    /// <returns>The blinded message, <see cref="BlindRsaPublicKey.ModulusLength"/> bytes.</returns>
    /// <exception cref="CryptographicException">The blinding factor has no inverse modulo n.</exception>
    public byte[] Blind(byte[] message)
    {
        var n = key.Modulus;
        var m = FromBytes(EncodePss(message, (int)n.GetBitLength() - 1));
        var r = (FromBytes(randomBytes(BlindRsaPublicKey.ModulusLength + ExtraRandomLength)) % (n - 1)) + 1;
        inverse = ModularInverse(r, n) ?? throw new CryptographicException("The blinding factor has no inverse modulo n.");
        return ToBytes(m * BigInteger.ModPow(r, key.Exponent, n) % n);
    }

    /// <summary>Unblinds <paramref name="blindSignature"/> (RFC 9474 §4.3) and checks the signature against <paramref name="message"/>.</summary>
    /// <param name="message">The message <see cref="Blind"/> was given.</param>
    /// <param name="blindSignature">The issuer's blind signature, <see cref="BlindRsaPublicKey.ModulusLength"/> bytes.</param>
    /// <returns>The signature, or <see langword="null"/> when it does not verify.</returns>
    public byte[]? Finalize(byte[] message, byte[] blindSignature)
    {
        ArgumentNullException.ThrowIfNull(blindSignature);
        var signature = ToBytes(FromBytes(blindSignature) * inverse % key.Modulus);
        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = ToBytes(key.Modulus),
            Exponent = key.Exponent.ToByteArray(isUnsigned: true, isBigEndian: true),
        });
        return rsa.VerifyData(message, signature, HashAlgorithmName.SHA384, RSASignaturePadding.Pss) ? signature : null;
    }

    /// <summary>Gives the inverse of <paramref name="value"/> modulo <paramref name="modulus"/> by the extended Euclidean algorithm.</summary>
    /// <param name="value">The value, in [1, modulus).</param>
    /// <param name="modulus">The modulus.</param>
    /// <returns>The inverse, or <see langword="null"/> when the two are not coprime.</returns>
    internal static BigInteger? ModularInverse(BigInteger value, BigInteger modulus)
    {
        var (oldR, r) = (value, modulus);
        var (oldS, s) = (BigInteger.One, BigInteger.Zero);
        while (!r.IsZero)
        {
            var quotient = oldR / r;
            (oldR, r) = (r, oldR - (quotient * r));
            (oldS, s) = (s, oldS - (quotient * s));
        }

        return oldR.IsOne ? ((oldS % modulus) + modulus) % modulus : null;
    }

    // RFC 8017 §9.1.1 with SHA-384, MGF1-SHA-384 and a 48-byte salt; emBits is modBits - 1, as RSASSA-PSS signs.
    private byte[] EncodePss(byte[] message, int emBits)
    {
        var emLength = (emBits + 7) / 8;
        var salt = randomBytes(SaltLength);
        var hash = SHA384.HashData([.. new byte[8], .. SHA384.HashData(message), .. salt]);
        var dbLength = emLength - hash.Length - 1;
        var db = new byte[dbLength];
        db[dbLength - SaltLength - 1] = 0x01;
        salt.CopyTo(db, dbLength - SaltLength);
        var mask = Mgf1(hash, dbLength);
        for (var index = 0; index < dbLength; index++)
        {
            db[index] ^= mask[index];
        }

        db[0] &= (byte)(0xFF >> ((8 * emLength) - emBits));
        return [.. db, .. hash, 0xBC];
    }

    // RFC 8017 §B.2.1 with SHA-384.
    private static byte[] Mgf1(byte[] seed, int length)
    {
        var mask = new List<byte>(length + SHA384.HashSizeInBytes);
        for (var counter = 0; mask.Count < length; counter++)
        {
            mask.AddRange(SHA384.HashData([.. seed, (byte)(counter >> 24), (byte)(counter >> 16), (byte)(counter >> 8), (byte)counter]));
        }

        return [.. mask.Take(length)];
    }

    private static BigInteger FromBytes(byte[] bytes) => new(bytes, isUnsigned: true, isBigEndian: true);

    private static byte[] ToBytes(BigInteger value)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var padded = new byte[BlindRsaPublicKey.ModulusLength];
        bytes.CopyTo(padded, padded.Length - bytes.Length);
        return padded;
    }
}
