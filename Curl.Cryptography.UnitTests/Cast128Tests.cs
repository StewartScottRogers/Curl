using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Cast128" /> to RFC 2144 Appendix B (the B.1 single plaintext-key-ciphertext
/// sets for 128-, 80- and 40-bit keys, and the B.2 full maintenance test), checks its CBC
/// mode, and checks its argument and disposal rules (ADR-0118).
/// </summary>
[TestClass]
public sealed class Cast128Tests
{
    private const string Plaintext = "0123456789ABCDEF";

    private const string Key128 = "0123456712345678234567893456789A";

    private const string Vector = "FEDCBA9876543210";

    // "Four blocks of CAST-128 in CBC." padded with a NUL to 32 bytes.
    private const string ChainPlaintext = "466F757220626C6F636B73206F6620434153542D31323820696E204342432E00";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 2144 Appendix B.1: key, plaintext 0123456789ABCDEF, ciphertext.
    [TestMethod]
    [DataRow(Key128, "238B4FE5847E44B2")]
    [DataRow("01234567123456782345", "EB6A711A2C02271B")]
    [DataRow("0123456712", "7AC816D16E9B302E")]
    public void EncryptBlockAndDecryptBlock_Rfc2144AppendixB1Vector_GiveThePublishedCiphertextAndPlaintext(string key, string ciphertext)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[Cast128.BlockSize];
        byte[] decrypted = new byte[Cast128.BlockSize];
        diagnostics.Arrange("vector source", "RFC 2144 Appendix B.1");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("plaintext", Convert.FromHexString(Plaintext));
        diagnostics.Bytes("ciphertext", Convert.FromHexString(ciphertext));

        cast.EncryptBlock(Convert.FromHexString(Plaintext), encrypted);
        cast.DecryptBlock(Convert.FromHexString(ciphertext), decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("encrypted", Convert.FromHexString(ciphertext), encrypted);
        diagnostics.Diff("decrypted", Convert.FromHexString(Plaintext), decrypted);
        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(Plaintext, Convert.ToHexString(decrypted));
    }

    // RFC 2144 Appendix B.1: the 80- and 40-bit keys are also given zero-padded to 128 bits,
    // but a key of more than 80 bits runs 16 rounds, so the padded forms are a different cipher.
    [TestMethod]
    public void EncryptBlock_EightyBitKeyZeroPaddedTo128Bits_RunsSixteenRoundsAndDiffers()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(Convert.FromHexString("01234567123456782345000000000000"));
        byte[] encrypted = new byte[Cast128.BlockSize];
        diagnostics.Arrange("vector source", "RFC 2144 Appendix B.1, 80-bit key zero-padded to 128 bits");
        diagnostics.Bytes("key", Convert.FromHexString("01234567123456782345000000000000"));
        diagnostics.Bytes("plaintext", Convert.FromHexString(Plaintext));

        cast.EncryptBlock(Convert.FromHexString(Plaintext), encrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));

        diagnostics.Diff("encrypted against the 12-round 80-bit ciphertext", Convert.FromHexString("EB6A711A2C02271B"), encrypted);
        Assert.AreNotEqual("EB6A711A2C02271B", Convert.ToHexString(encrypted));
    }

    // RFC 2144 Appendix B.2: a million iterations of four encryptions.
    [TestMethod]
    [TestCategory("LongRunning")]
    [RunsOnlyWhenLongRunningTestsAreEnabled]
    public void EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] a = Convert.FromHexString(Key128);
        byte[] b = Convert.FromHexString(Key128);
        diagnostics.Arrange("vector source", "RFC 2144 Appendix B.2 full maintenance test");
        diagnostics.Bytes("initial a", a);
        diagnostics.Bytes("initial b", b);
        diagnostics.Arrange("iterations", 1_000_000);

        using (diagnostics.Phase("million iterations"))
        {
            for (int iteration = 0; iteration < 1_000_000; iteration++)
            {
                using (Cast128 underB = new(b))
                {
                    underB.EncryptBlock(a.AsSpan(0, 8), a.AsSpan(0, 8));
                    underB.EncryptBlock(a.AsSpan(8), a.AsSpan(8));
                }

                using Cast128 underA = new(a);
                underA.EncryptBlock(b.AsSpan(0, 8), b.AsSpan(0, 8));
                underA.EncryptBlock(b.AsSpan(8), b.AsSpan(8));
            }
        }

        diagnostics.Act("a", Convert.ToHexString(a));
        diagnostics.Act("b", Convert.ToHexString(b));

        diagnostics.Diff("a", Convert.FromHexString("EEA9D0A249FD3BA6B3436FB89D6DCA92"), a);
        diagnostics.Diff("b", Convert.FromHexString("B2C95EB00C31AD7180AC05B8E83D696E"), b);
        Assert.AreEqual("EEA9D0A249FD3BA6B3436FB89D6DCA92", Convert.ToHexString(a));
        Assert.AreEqual("B2C95EB00C31AD7180AC05B8E83D696E", Convert.ToHexString(b));
    }

    // CBC of one block under an all-zero vector is ECB, so RFC 2144 B.1 pins it.
    [TestMethod]
    public void EncryptCbcAndDecryptCbc_OneBlockUnderZeroVector_GiveTheRfc2144AppendixB1Vector()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(Convert.FromHexString(Key128));
        byte[] encrypted = new byte[Cast128.BlockSize];
        byte[] decrypted = new byte[Cast128.BlockSize];
        diagnostics.Arrange("vector source", "RFC 2144 Appendix B.1, CBC under an all-zero vector");
        diagnostics.Bytes("key", Convert.FromHexString(Key128));
        diagnostics.Bytes("initialization vector", new byte[Cast128.BlockSize]);
        diagnostics.Bytes("plaintext", Convert.FromHexString(Plaintext));

        cast.EncryptCbc(new byte[Cast128.BlockSize], Convert.FromHexString(Plaintext), encrypted);
        cast.DecryptCbc(new byte[Cast128.BlockSize], Convert.FromHexString("238B4FE5847E44B2"), decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("encrypted", Convert.FromHexString("238B4FE5847E44B2"), encrypted);
        diagnostics.Diff("decrypted", Convert.FromHexString(Plaintext), decrypted);
        Assert.AreEqual("238B4FE5847E44B2", Convert.ToHexString(encrypted));
        Assert.AreEqual(Plaintext, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    public void EncryptCbc_TwoBlocks_ChainsEachBlockIntoTheNext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(Convert.FromHexString(Key128));
        byte[] source = Convert.FromHexString(ChainPlaintext)[..16];
        byte[] chained = new byte[16];
        byte[] expected = new byte[16];
        byte[] block = new byte[Cast128.BlockSize];
        byte[] vector = Convert.FromHexString(Vector);
        diagnostics.Bytes("key", Convert.FromHexString(Key128));
        diagnostics.Bytes("initialization vector", vector);
        diagnostics.Bytes("plaintext", source);
        diagnostics.Arrange("expected", "two ECB encryptions, each of the block XOR the previous ciphertext");

        cast.EncryptCbc(vector, source, chained);
        for (int index = 0; index < Cast128.BlockSize; index++)
        {
            block[index] = (byte)(source[index] ^ vector[index]);
        }

        cast.EncryptBlock(block, expected.AsSpan(0, 8));
        for (int index = 0; index < Cast128.BlockSize; index++)
        {
            block[index] = (byte)(source[8 + index] ^ expected[index]);
        }

        cast.EncryptBlock(block, expected.AsSpan(8));
        diagnostics.Act("chained", Convert.ToHexString(chained));

        diagnostics.Diff("chained", expected, chained);
        CollectionAssert.AreEqual(expected, chained);
    }

    [TestMethod]
    public void EncryptCbcThenDecryptCbc_InPlaceAndChainedAcrossTwoCalls_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(Convert.FromHexString(Key128));
        byte[] buffer = Convert.FromHexString(ChainPlaintext);
        byte[] vector = Convert.FromHexString(Vector);
        byte[] whole = new byte[buffer.Length];
        cast.EncryptCbc(vector, buffer, whole);
        diagnostics.Bytes("key", Convert.FromHexString(Key128));
        diagnostics.Bytes("initialization vector", vector);
        diagnostics.Bytes("plaintext", buffer);
        diagnostics.Arrange("whole-buffer ciphertext", Convert.ToHexString(whole));

        cast.EncryptCbc(vector, buffer.AsSpan(0, 16), buffer.AsSpan(0, 16));
        cast.EncryptCbc(buffer.AsSpan(8, 8), buffer.AsSpan(16), buffer.AsSpan(16));
        diagnostics.Act("encrypted in two calls", Convert.ToHexString(buffer));
        diagnostics.Diff("encrypted", whole, buffer);
        CollectionAssert.AreEqual(whole, buffer);

        byte[] secondVector = buffer[8..16];
        cast.DecryptCbc(vector, buffer.AsSpan(0, 16), buffer.AsSpan(0, 16));
        cast.DecryptCbc(secondVector, buffer.AsSpan(16), buffer.AsSpan(16));
        diagnostics.Act("decrypted in two calls", Convert.ToHexString(buffer));
        diagnostics.Diff("decrypted", Convert.FromHexString(ChainPlaintext), buffer);
        Assert.AreEqual(ChainPlaintext, Convert.ToHexString(buffer));
    }

    [TestMethod]
    public void EncryptCbc_EmptySource_WritesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(Convert.FromHexString(Key128));
        byte[] buffer = [0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5];
        diagnostics.Arrange("source length", 0);
        diagnostics.Bytes("buffer before", buffer);

        cast.EncryptCbc(Convert.FromHexString(Vector), [], buffer.AsSpan(0, 0));
        cast.DecryptCbc(Convert.FromHexString(Vector), [], buffer.AsSpan(0, 0));
        diagnostics.Act("buffer after", Convert.ToHexString(buffer));

        diagnostics.Diff("buffer", Convert.FromHexString("A5A5A5A5A5A5A5A5"), buffer);
        Assert.AreEqual("A5A5A5A5A5A5A5A5", Convert.ToHexString(buffer));
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(17)]
    public void Constructor_KeyOutsideFiveTo16Bytes_ThrowsArgumentException(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Cast128(new byte[length]));
        diagnostics.Act("parameter name", exception.ParamName);

        diagnostics.Assert("parameter name", "key", exception.ParamName);
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(16)]
    public void Constructor_FiveOr16ByteKey_IsAccepted(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(new byte[length]);
        byte[] plaintext = new byte[Cast128.BlockSize];
        byte[] ciphertext = new byte[Cast128.BlockSize];
        byte[] decrypted = new byte[Cast128.BlockSize];
        diagnostics.Arrange("key length", length);
        diagnostics.Bytes("plaintext", plaintext);

        cast.EncryptBlock(plaintext, ciphertext);
        cast.DecryptBlock(ciphertext, decrypted);
        diagnostics.Act("ciphertext", Convert.ToHexString(ciphertext));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("ciphertext against plaintext", plaintext, ciphertext);
        diagnostics.Diff("decrypted", plaintext, decrypted);
        CollectionAssert.AreNotEqual(plaintext, ciphertext);
        CollectionAssert.AreEqual(plaintext, decrypted);
    }

    [TestMethod]
    [DataRow(7, 8, "source")]
    [DataRow(8, 9, "destination")]
    public void EncryptBlockAndDecryptBlock_WrongLength_ThrowArgumentException(int sourceLength, int destinationLength, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(new byte[16]);
        diagnostics.Arrange("source length", sourceLength);
        diagnostics.Arrange("destination length", destinationLength);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.DecryptBlock(new byte[sourceLength], new byte[destinationLength]));
        diagnostics.Act("encrypt parameter name", encrypting.ParamName);
        diagnostics.Act("decrypt parameter name", decrypting.ParamName);

        diagnostics.Assert("encrypt parameter name", parameterName, encrypting.ParamName);
        diagnostics.Assert("decrypt parameter name", parameterName, decrypting.ParamName);
        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    [DataRow(8, 12, 12, "source")]
    [DataRow(8, 16, 8, "destination")]
    [DataRow(7, 16, 16, "initializationVector")]
    public void EncryptCbcAndDecryptCbc_WrongLength_ThrowArgumentException(int vectorLength, int sourceLength, int destinationLength, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Cast128 cast = new(new byte[16]);
        diagnostics.Arrange("initialization vector length", vectorLength);
        diagnostics.Arrange("source length", sourceLength);
        diagnostics.Arrange("destination length", destinationLength);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.EncryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.DecryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        diagnostics.Act("encrypt parameter name", encrypting.ParamName);
        diagnostics.Act("decrypt parameter name", decrypting.ParamName);

        diagnostics.Assert("encrypt parameter name", parameterName, encrypting.ParamName);
        diagnostics.Assert("decrypt parameter name", parameterName, decrypting.ParamName);
        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    public void EveryOperation_AfterDispose_ThrowsObjectDisposedException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Cast128 cast = new(new byte[16]);
        cast.Dispose();
        diagnostics.Arrange("state", "disposed");

        ObjectDisposedException encryptBlock = Assert.ThrowsExactly<ObjectDisposedException>(() => cast.EncryptBlock(new byte[8], new byte[8]));
        ObjectDisposedException decryptBlock = Assert.ThrowsExactly<ObjectDisposedException>(() => cast.DecryptBlock(new byte[8], new byte[8]));
        ObjectDisposedException encryptCbc = Assert.ThrowsExactly<ObjectDisposedException>(() => cast.EncryptCbc(new byte[8], new byte[8], new byte[8]));
        ObjectDisposedException decryptCbc = Assert.ThrowsExactly<ObjectDisposedException>(() => cast.DecryptCbc(new byte[8], new byte[8], new byte[8]));
        string thrown = string.Join(", ", encryptBlock.GetType().Name, decryptBlock.GetType().Name, encryptCbc.GetType().Name, decryptCbc.GetType().Name);
        diagnostics.Act("exceptions (EncryptBlock, DecryptBlock, EncryptCbc, DecryptCbc)", thrown);

        diagnostics.Assert("exceptions", string.Join(", ", Enumerable.Repeat(nameof(ObjectDisposedException), 4)), thrown);
    }
}
