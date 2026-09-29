using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Checks the generic <see cref="GaloisCounterMode" /> by running it over the BCL's AES
/// and matching the BCL's <see cref="AesGcm" /> on seeded random keys, nonces, plaintexts
/// and associated data of every length around the block boundaries.
/// </summary>
[TestClass]
public sealed class GaloisCounterModeTests
{
    [TestMethod]
    [DataRow(16, 1)]
    [DataRow(24, 2)]
    [DataRow(32, 3)]
    public void EncryptAndTryDecrypt_OverAes_MatchAesGcmOnRandomInputs(int keyLength, int seed)
    {
        Random random = new(seed);
        for (int plaintextLength = 0; plaintextLength <= 67; plaintextLength++)
        {
            byte[] key = RandomBytes(random, keyLength);
            byte[] nonce = RandomBytes(random, GaloisCounterMode.NonceSize);
            byte[] plaintext = RandomBytes(random, plaintextLength);
            byte[] associatedData = RandomBytes(random, random.Next(0, 40));
            byte[] expectedCiphertext = new byte[plaintextLength];
            byte[] expectedTag = new byte[GaloisCounterMode.TagSize];
            using (AesGcm aesGcm = new(key, GaloisCounterMode.TagSize))
            {
                aesGcm.Encrypt(nonce, plaintext, expectedCiphertext, expectedTag, associatedData);
            }

            using AesBlockCipher aes = new(key);
            using GaloisCounterMode mode = new(aes);
            byte[] ciphertext = new byte[plaintextLength];
            byte[] tag = new byte[GaloisCounterMode.TagSize];
            byte[] decrypted = new byte[plaintextLength];

            mode.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            bool succeeded = mode.TryDecrypt(nonce, expectedCiphertext, expectedTag, decrypted, associatedData);

            Assert.AreEqual(Convert.ToHexString(expectedCiphertext), Convert.ToHexString(ciphertext), $"ciphertext of {plaintextLength} bytes");
            Assert.AreEqual(Convert.ToHexString(expectedTag), Convert.ToHexString(tag), $"tag of {plaintextLength} bytes");
            Assert.IsTrue(succeeded);
            Assert.AreEqual(Convert.ToHexString(plaintext), Convert.ToHexString(decrypted));
        }
    }

    [TestMethod]
    public void EncryptThenTryDecrypt_InPlace_RoundTrips()
    {
        Random random = new(4);
        byte[] key = RandomBytes(random, 16);
        byte[] nonce = RandomBytes(random, GaloisCounterMode.NonceSize);
        byte[] plaintext = RandomBytes(random, 50);
        byte[] buffer = (byte[])plaintext.Clone();
        byte[] tag = new byte[GaloisCounterMode.TagSize];
        using AesBlockCipher aes = new(key);
        using GaloisCounterMode mode = new(aes);

        mode.Encrypt(nonce, buffer, buffer, tag, []);
        bool succeeded = mode.TryDecrypt(nonce, buffer, tag, buffer, []);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(Convert.ToHexString(plaintext), Convert.ToHexString(buffer));
    }

    // GF(2^128) with bit 0 the most significant: the element 1 is 0x80 || 0^120, and
    // multiplying by it gives the other factor back.
    [TestMethod]
    public void Multiply_ByOne_GivesTheOtherFactor()
    {
        UInt128 one = new(0x8000000000000000UL, 0);
        UInt128 value = new(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL);

        Assert.AreEqual(value, GaloisCounterMode.Multiply(value, one));
        Assert.AreEqual(value, GaloisCounterMode.Multiply(one, value));
        Assert.AreEqual(UInt128.Zero, GaloisCounterMode.Multiply(value, UInt128.Zero));
    }

    private static byte[] RandomBytes(Random random, int length)
    {
        byte[] bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    /// <summary>The BCL's AES, one block at a time in ECB, as the block cipher GCM runs over.</summary>
    private sealed class AesBlockCipher(byte[] key) : IBlockCipher, IDisposable
    {
        private readonly Aes aes = CreateAes(key);

        public void EncryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            aes.EncryptEcb(source, destination, PaddingMode.None);
        }

        public void Dispose() => aes.Dispose();

        private static Aes CreateAes(byte[] key)
        {
            Aes aes = Aes.Create();
            aes.Key = key;
            return aes;
        }
    }
}
