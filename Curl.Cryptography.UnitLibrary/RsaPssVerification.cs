using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Verifies RSASSA-PSS signatures (RFC 8017, sections 8.1.2 and 9.1.2) with any salt length and
/// with MGF1 over a hash that need not be the message's. The base class library's
/// <see cref="RSASignaturePadding.Pss" /> verifies only a salt as long as the hash, with MGF1 over
/// that same hash. Verification uses the public key alone, so nothing here is secret and nothing
/// need run in constant time.
/// </summary>
public static class RsaPssVerification
{
    private const byte TrailerByte = 0xBC;

    /// <summary>Returns whether <paramref name="signature" /> is a signature of <paramref name="data" /> under <paramref name="publicKey" />.</summary>
    /// <param name="publicKey">The signer's modulus and public exponent, big-endian.</param>
    /// <param name="data">The signed bytes.</param>
    /// <param name="signature">The signature, as long as the modulus.</param>
    /// <param name="hash">The hash of the message.</param>
    /// <param name="maskHash">The hash MGF1 uses.</param>
    /// <param name="saltLength">The exact length of the salt in bytes; a signature with a salt of another length fails.</param>
    /// <returns><see langword="true" /> when the signature verifies.</returns>
    /// <exception cref="ArgumentException"><paramref name="publicKey" /> has no modulus or no exponent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="saltLength" /> is negative.</exception>
    public static bool Verify(
        RSAParameters publicKey,
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        HashAlgorithmName hash,
        HashAlgorithmName maskHash,
        int saltLength)
    {
        if (publicKey.Modulus is null || publicKey.Exponent is null)
        {
            throw new ArgumentException("The key needs a modulus and a public exponent.", nameof(publicKey));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(saltLength);
        var modulus = new BigInteger(publicKey.Modulus, isUnsigned: true, isBigEndian: true);
        var representative = new BigInteger(signature, isUnsigned: true, isBigEndian: true);
        if (signature.Length != modulus.GetByteCount(isUnsigned: true) || representative >= modulus)
        {
            return false;
        }

        var message = BigInteger.ModPow(representative, new BigInteger(publicKey.Exponent, isUnsigned: true, isBigEndian: true), modulus);
        var encodedBits = (int)modulus.GetBitLength() - 1;
        return TryEncodeToLength(message, (encodedBits + 7) / 8, out var encoded)
            && IsEncodingOf(encoded, encodedBits, CryptographicOperations.HashData(hash, data), hash, maskHash, saltLength);
    }

    // I2OSP: the integer as exactly length big-endian bytes, or false when it needs more.
    private static bool TryEncodeToLength(BigInteger value, int length, out byte[] encoded)
    {
        encoded = new byte[length];
        var needed = value.GetByteCount(isUnsigned: true);
        return needed <= length && value.TryWriteBytes(encoded.AsSpan(length - needed), out _, isUnsigned: true, isBigEndian: true);
    }

    // EMSA-PSS-VERIFY: the encoding is maskedDB || H || 0xBC, and DB is zeros || 0x01 || salt.
    private static bool IsEncodingOf(
        byte[] encoded, int encodedBits, byte[] messageHash, HashAlgorithmName hash, HashAlgorithmName maskHash, int saltLength)
    {
        var hashLength = messageHash.Length;
        var paddingLength = encoded.Length - hashLength - 2 - (long)saltLength;
        if (paddingLength < 0 || encoded[^1] != TrailerByte)
        {
            return false;
        }

        // The bits of the first byte above the encoding's length must be zero.
        var unusedBits = (byte)(0xFF << (8 - (8 * encoded.Length - encodedBits)));
        if ((encoded[0] & unusedBits) != 0)
        {
            return false;
        }

        var block = encoded[..^(hashLength + 1)];
        var embeddedHash = encoded.AsSpan(block.Length, hashLength);
        ApplyMask(block, embeddedHash, maskHash);
        block[0] &= (byte)~unusedBits;
        if (block.AsSpan(0, (int)paddingLength).ContainsAnyExcept((byte)0) || block[(int)paddingLength] != 0x01)
        {
            return false;
        }

        byte[] saltedMessage = [.. new byte[8], .. messageHash, .. block.AsSpan(block.Length - saltLength)];
        return embeddedHash.SequenceEqual(CryptographicOperations.HashData(hash, saltedMessage));
    }

    // MGF1 (RFC 8017, B.2.1), exclusive-ored over the block.
    private static void ApplyMask(byte[] block, ReadOnlySpan<byte> seed, HashAlgorithmName maskHash)
    {
        byte[] input = [.. seed, 0, 0, 0, 0];
        var offset = 0;
        for (var counter = 0; offset < block.Length; counter++)
        {
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(seed.Length), counter);
            var mask = CryptographicOperations.HashData(maskHash, input);
            for (var index = 0; index < mask.Length && offset < block.Length; index++)
            {
                block[offset++] ^= mask[index];
            }
        }
    }
}
