using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// Signs with RSASSA-PSS (RFC 8017, 8.1.1 and 9.1.1) with any salt length and mask hash, the
/// shapes <see cref="RSASignaturePadding.Pss" /> never writes (BL-6010).
/// </summary>
internal static class TestRsaPssSignature
{
    /// <summary>Signs <paramref name="data" /> with <paramref name="key" />'s private exponent.</summary>
    /// <param name="key">The signing key, exportable.</param>
    /// <param name="data">The bytes signed.</param>
    /// <param name="hash">The hash of the message.</param>
    /// <param name="maskHash">The hash MGF1 uses.</param>
    /// <param name="saltLength">The salt's length in bytes.</param>
    /// <returns>The signature, as long as the modulus.</returns>
    internal static byte[] Sign(RSA key, byte[] data, HashAlgorithmName hash, HashAlgorithmName maskHash, int saltLength)
    {
        var parameters = key.ExportParameters(includePrivateParameters: true);
        var modulus = new BigInteger(parameters.Modulus, isUnsigned: true, isBigEndian: true);
        var salt = RandomNumberGenerator.GetBytes(saltLength);
        byte[] salted = [.. new byte[8], .. CryptographicOperations.HashData(hash, data), .. salt];
        var embeddedHash = CryptographicOperations.HashData(hash, salted);
        var encodedBits = (int)modulus.GetBitLength() - 1;
        var encoded = new byte[(encodedBits + 7) / 8];
        var block = new byte[encoded.Length - embeddedHash.Length - 1];
        block[^(saltLength + 1)] = 1;
        salt.CopyTo(block.AsSpan(block.Length - saltLength));
        Mask(block, embeddedHash, maskHash);
        block[0] &= (byte)(0xFF >> (8 * encoded.Length - encodedBits));
        block.CopyTo(encoded.AsSpan());
        embeddedHash.CopyTo(encoded.AsSpan(block.Length));
        encoded[^1] = 0xBC;

        var signed = BigInteger.ModPow(
            new BigInteger(encoded, isUnsigned: true, isBigEndian: true),
            new BigInteger(parameters.D, isUnsigned: true, isBigEndian: true),
            modulus).ToByteArray(isUnsigned: true, isBigEndian: true);
        var signature = new byte[parameters.Modulus!.Length];
        signed.CopyTo(signature.AsSpan(signature.Length - signed.Length));
        return signature;
    }

    private static void Mask(byte[] block, byte[] seed, HashAlgorithmName maskHash)
    {
        byte[] input = [.. seed, 0, 0, 0, 0];
        var offset = 0;
        for (var counter = 0; offset < block.Length; counter++)
        {
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(seed.Length), counter);
            foreach (var mask in CryptographicOperations.HashData(maskHash, input).Take(block.Length - offset))
            {
                block[offset++] ^= mask;
            }
        }
    }
}
