using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Checks the generic <see cref="GaloisCounterMode" /> by running it over the BCL's AES
/// and matching the BCL's <see cref="AesGcm" /> on seeded random keys, nonces, plaintexts
/// and associated data of every length around the block boundaries, and by pinning it to
/// the published known answers: McGrew and Viega, "The Galois/Counter Mode of Operation
/// (GCM)", Appendix B, Test Cases 1-4 (AES-128), 7-10 (AES-192) and 13-16 (AES-256).
/// Cases 5, 6, 11, 12, 17 and 18 use 64-bit and 480-bit IVs, which
/// <see cref="GaloisCounterMode.NonceSize" /> does not take, so they are left out.
/// </summary>
[TestClass]
public sealed class GaloisCounterModeTests
{
    private const string SpecificationKey = "feffe9928665731c6d6a8f9467308308";
    private const string SpecificationIv = "cafebabefacedbaddecaf888";
    private const string SpecificationAssociatedData = "feedfacedeadbeeffeedfacedeadbeefabaddad2";
    private const string SpecificationPlaintext =
        "d9313225f88406e5a55909c5aff5269a86a7a9531534f7da2e4c303d8a318a72" +
        "1c3c0c95956809532fcf0e2449a6b525b16aedf5aa0de657ba637b391aafd255";
    private const string ZeroIv = "000000000000000000000000";
    private const string ZeroBlock = "00000000000000000000000000000000";

    // Test Case 4: the 64-byte plaintext cut to 60 bytes, with 20 bytes of associated data.
    private const string TestCase4Ciphertext =
        "42831ec2217774244b7221b784d0d49ce3aa212f2c02a4e035c17e2329aca12e" +
        "21d514b25466931c7d8f6a5aac84aa051ba30b396a0aac973d58e091";
    private const string TestCase4Tag = "5bc94fbc3221a5db94fae95ae7121a47";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1, ZeroBlock, ZeroIv, "", "", "", "58e2fccefa7e3061367f1d57a4e7455a")]
    [DataRow(2, ZeroBlock, ZeroIv, ZeroBlock, "", "0388dace60b6a392f328c2b971b2fe78", "ab6e47d42cec13bdf53a67b21257bddf")]
    [DataRow(3, SpecificationKey, SpecificationIv, SpecificationPlaintext, "",
        "42831ec2217774244b7221b784d0d49ce3aa212f2c02a4e035c17e2329aca12e21d514b25466931c7d8f6a5aac84aa051ba30b396a0aac973d58e091473f5985",
        "4d5c2af327cd64a62cf35abd2ba6fab4")]
    [DataRow(4, SpecificationKey, SpecificationIv, SpecificationPlaintext, SpecificationAssociatedData, TestCase4Ciphertext, TestCase4Tag)]
    [DataRow(7, ZeroBlock + "0000000000000000", ZeroIv, "", "", "", "cd33b28ac773f74ba00ed1f312572435")]
    [DataRow(8, ZeroBlock + "0000000000000000", ZeroIv, ZeroBlock, "", "98e7247c07f0fe411c267e4384b0f600", "2ff58d80033927ab8ef4d4587514f0fb")]
    [DataRow(9, SpecificationKey + "feffe9928665731c", SpecificationIv, SpecificationPlaintext, "",
        "3980ca0b3c00e841eb06fac4872a2757859e1ceaa6efd984628593b40ca1e19c7d773d00c144c525ac619d18c84a3f4718e2448b2fe324d9ccda2710acade256",
        "9924a7c8587336bfb118024db8674a14")]
    [DataRow(10, SpecificationKey + "feffe9928665731c", SpecificationIv, SpecificationPlaintext, SpecificationAssociatedData,
        "3980ca0b3c00e841eb06fac4872a2757859e1ceaa6efd984628593b40ca1e19c7d773d00c144c525ac619d18c84a3f4718e2448b2fe324d9ccda2710",
        "2519498e80f1478f37ba55bd6d27618c")]
    [DataRow(13, ZeroBlock + ZeroBlock, ZeroIv, "", "", "", "530f8afbc74536b9a963b4f1c4cb738b")]
    [DataRow(14, ZeroBlock + ZeroBlock, ZeroIv, ZeroBlock, "", "cea7403d4d606b6e074ec5d3baf39d18", "d0d1c8a799996bf0265b98b5d48ab919")]
    [DataRow(15, SpecificationKey + SpecificationKey, SpecificationIv, SpecificationPlaintext, "",
        "522dc1f099567d07f47f37a32a84427d643a8cdcbfe5c0c97598a2bd2555d1aa8cb08e48590dbb3da7b08b1056828838c5f61e6393ba7a0abcc9f662898015ad",
        "b094dac5d93471bdec1a502270e3cc6c")]
    [DataRow(16, SpecificationKey + SpecificationKey, SpecificationIv, SpecificationPlaintext, SpecificationAssociatedData,
        "522dc1f099567d07f47f37a32a84427d643a8cdcbfe5c0c97598a2bd2555d1aa8cb08e48590dbb3da7b08b1056828838c5f61e6393ba7a0abcc9f662898015ad",
        "76fc6ece0f4e1768cddf8853bb2d551b")]
    public void EncryptAndTryDecrypt_GcmSpecificationTestCase_GiveThePublishedCiphertextAndTag(
        int testCase, string key, string iv, string plaintext, string associatedData, string ciphertext, string tag)
    {
        // Cases 4, 10 and 16 cut the 64-byte plaintext, and so the ciphertext, to 60 bytes
        // whenever associated data is present.
        int length = associatedData.Length == 0 ? plaintext.Length / 2 : 60;
        byte[] plaintextBytes = Convert.FromHexString(plaintext)[..length];
        byte[] expectedCiphertext = Convert.FromHexString(ciphertext)[..length];
        byte[] nonce = Convert.FromHexString(iv);
        byte[] associatedDataBytes = Convert.FromHexString(associatedData);
        byte[] actualCiphertext = new byte[length];
        byte[] actualTag = new byte[GaloisCounterMode.TagSize];
        byte[] decrypted = new byte[length];
        using AesBlockCipher aes = new(Convert.FromHexString(key));
        using GaloisCounterMode mode = new(aes);
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", $"McGrew and Viega, \"The Galois/Counter Mode of Operation (GCM)\", appendix B, Test Case {testCase}");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("nonce", nonce);
        diagnostics.Bytes("plaintext", plaintextBytes);
        diagnostics.Bytes("associated data", associatedDataBytes);

        mode.Encrypt(nonce, plaintextBytes, actualCiphertext, actualTag, associatedDataBytes);
        bool succeeded = mode.TryDecrypt(nonce, expectedCiphertext, Convert.FromHexString(tag), decrypted, associatedDataBytes);
        diagnostics.Act("decrypted", succeeded);

        diagnostics.Diff("ciphertext", expectedCiphertext, actualCiphertext);
        diagnostics.Diff("tag", Convert.FromHexString(tag), actualTag);
        diagnostics.Assert("decrypted", true, succeeded);
        diagnostics.Diff("plaintext", plaintextBytes, decrypted);
        Assert.AreEqual(Convert.ToHexString(expectedCiphertext), Convert.ToHexString(actualCiphertext), $"ciphertext of Test Case {testCase}");
        Assert.AreEqual(tag, Convert.ToHexString(actualTag).ToLowerInvariant(), $"tag of Test Case {testCase}");
        Assert.IsTrue(succeeded, $"Test Case {testCase} decrypts");
        Assert.AreEqual(Convert.ToHexString(plaintextBytes), Convert.ToHexString(decrypted), $"plaintext of Test Case {testCase}");
    }

    [TestMethod]
    [DataRow("tag", 0)]
    [DataRow("tag", 127)]
    [DataRow("ciphertext", 0)]
    [DataRow("ciphertext", 479)]
    [DataRow("associatedData", 0)]
    [DataRow("associatedData", 159)]
    public void TryDecrypt_GcmSpecificationTestCase4WithOneBitFlipped_ReturnsFalse(string flippedInput, int bit)
    {
        byte[] ciphertext = Convert.FromHexString(TestCase4Ciphertext);
        byte[] tag = Convert.FromHexString(TestCase4Tag);
        byte[] associatedData = Convert.FromHexString(SpecificationAssociatedData);
        byte[] flipped = flippedInput switch
        {
            "tag" => tag,
            "ciphertext" => ciphertext,
            _ => associatedData,
        };
        flipped[bit / 8] ^= (byte)(0x80 >> (bit % 8));
        byte[] decrypted = new byte[ciphertext.Length];
        using AesBlockCipher aes = new(Convert.FromHexString(SpecificationKey));
        using GaloisCounterMode mode = new(aes);
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "McGrew and Viega, GCM appendix B, Test Case 4, one bit flipped");
        diagnostics.Arrange("flipped", $"{flippedInput} bit {bit}");
        diagnostics.Bytes("ciphertext", ciphertext);
        diagnostics.Bytes("tag", tag);
        diagnostics.Bytes("associated data", associatedData);

        bool succeeded = mode.TryDecrypt(Convert.FromHexString(SpecificationIv), ciphertext, tag, decrypted, associatedData);
        diagnostics.Act("decrypted", succeeded);

        diagnostics.Assert("decrypted", false, succeeded);
        Assert.IsFalse(succeeded, $"{flippedInput} with bit {bit} flipped");
    }

    [TestMethod]
    [DataRow(16, 1)]
    [DataRow(24, 2)]
    [DataRow(32, 3)]
    public void EncryptAndTryDecrypt_OverAes_MatchAesGcmOnRandomInputs(int keyLength, int seed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", $"the BCL's AesGcm on seeded random inputs (seed {seed}), plaintexts of 0 to 67 bytes");
        diagnostics.Arrange("key length", keyLength);
        using var phase = diagnostics.Phase("compare with AesGcm");
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
            if (!succeeded || !expectedCiphertext.AsSpan().SequenceEqual(ciphertext) || !expectedTag.AsSpan().SequenceEqual(tag) || !plaintext.AsSpan().SequenceEqual(decrypted))
            {
                diagnostics.Arrange("failing plaintext length", plaintextLength);
                diagnostics.Bytes("key", key);
                diagnostics.Bytes("nonce", nonce);
                diagnostics.Bytes("plaintext", plaintext);
                diagnostics.Bytes("associated data", associatedData);
                diagnostics.Diff("ciphertext", expectedCiphertext, ciphertext);
                diagnostics.Diff("tag", expectedTag, tag);
                diagnostics.Diff("plaintext", plaintext, decrypted);
            }

            Assert.AreEqual(Convert.ToHexString(expectedCiphertext), Convert.ToHexString(ciphertext), $"ciphertext of {plaintextLength} bytes");
            Assert.AreEqual(Convert.ToHexString(expectedTag), Convert.ToHexString(tag), $"tag of {plaintextLength} bytes");
            Assert.IsTrue(succeeded);
            Assert.AreEqual(Convert.ToHexString(plaintext), Convert.ToHexString(decrypted));
        }

        diagnostics.Act("plaintext lengths compared", 68);
        diagnostics.Assert("every ciphertext, tag and plaintext matches AesGcm", true, true);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "seeded random key, nonce and 50-byte plaintext (seed 4)");
        diagnostics.Bytes("key", key);
        diagnostics.Bytes("nonce", nonce);
        diagnostics.Bytes("plaintext", plaintext);

        mode.Encrypt(nonce, buffer, buffer, tag, []);
        diagnostics.Bytes("ciphertext", buffer);
        diagnostics.Bytes("tag", tag);
        bool succeeded = mode.TryDecrypt(nonce, buffer, tag, buffer, []);
        diagnostics.Act("decrypted", succeeded);

        diagnostics.Assert("decrypted", true, succeeded);
        diagnostics.Diff("plaintext", plaintext, buffer);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("one", one.ToString("X32", System.Globalization.CultureInfo.InvariantCulture));
        diagnostics.Arrange("value", value.ToString("X32", System.Globalization.CultureInfo.InvariantCulture));

        UInt128 right = GaloisCounterMode.Multiply(value, one);
        UInt128 left = GaloisCounterMode.Multiply(one, value);
        UInt128 zero = GaloisCounterMode.Multiply(value, UInt128.Zero);
        diagnostics.Act("value * one", right.ToString("X32", System.Globalization.CultureInfo.InvariantCulture));
        diagnostics.Act("one * value", left.ToString("X32", System.Globalization.CultureInfo.InvariantCulture));
        diagnostics.Act("value * zero", zero.ToString("X32", System.Globalization.CultureInfo.InvariantCulture));

        diagnostics.Assert("value * one", value, right);
        diagnostics.Assert("one * value", value, left);
        diagnostics.Assert("value * zero", UInt128.Zero, zero);
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
