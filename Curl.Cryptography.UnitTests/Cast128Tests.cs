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

    // RFC 2144 Appendix B.1: key, plaintext 0123456789ABCDEF, ciphertext.
    [TestMethod]
    [DataRow(Key128, "238B4FE5847E44B2")]
    [DataRow("01234567123456782345", "EB6A711A2C02271B")]
    [DataRow("0123456712", "7AC816D16E9B302E")]
    public void EncryptBlockAndDecryptBlock_Rfc2144AppendixB1Vector_GiveThePublishedCiphertextAndPlaintext(string key, string ciphertext)
    {
        using Cast128 cast = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[Cast128.BlockSize];
        byte[] decrypted = new byte[Cast128.BlockSize];

        cast.EncryptBlock(Convert.FromHexString(Plaintext), encrypted);
        cast.DecryptBlock(Convert.FromHexString(ciphertext), decrypted);

        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(Plaintext, Convert.ToHexString(decrypted));
    }

    // RFC 2144 Appendix B.1: the 80- and 40-bit keys are also given zero-padded to 128 bits,
    // but a key of more than 80 bits runs 16 rounds, so the padded forms are a different cipher.
    [TestMethod]
    public void EncryptBlock_EightyBitKeyZeroPaddedTo128Bits_RunsSixteenRoundsAndDiffers()
    {
        using Cast128 cast = new(Convert.FromHexString("01234567123456782345000000000000"));
        byte[] encrypted = new byte[Cast128.BlockSize];

        cast.EncryptBlock(Convert.FromHexString(Plaintext), encrypted);

        Assert.AreNotEqual("EB6A711A2C02271B", Convert.ToHexString(encrypted));
    }

    // RFC 2144 Appendix B.2: a million iterations of four encryptions.
    [TestMethod]
    [TestCategory("Integration")]
    public void EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB()
    {
        byte[] a = Convert.FromHexString(Key128);
        byte[] b = Convert.FromHexString(Key128);

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

        Assert.AreEqual("EEA9D0A249FD3BA6B3436FB89D6DCA92", Convert.ToHexString(a));
        Assert.AreEqual("B2C95EB00C31AD7180AC05B8E83D696E", Convert.ToHexString(b));
    }

    // CBC of one block under an all-zero vector is ECB, so RFC 2144 B.1 pins it.
    [TestMethod]
    public void EncryptCbcAndDecryptCbc_OneBlockUnderZeroVector_GiveTheRfc2144AppendixB1Vector()
    {
        using Cast128 cast = new(Convert.FromHexString(Key128));
        byte[] encrypted = new byte[Cast128.BlockSize];
        byte[] decrypted = new byte[Cast128.BlockSize];

        cast.EncryptCbc(new byte[Cast128.BlockSize], Convert.FromHexString(Plaintext), encrypted);
        cast.DecryptCbc(new byte[Cast128.BlockSize], Convert.FromHexString("238B4FE5847E44B2"), decrypted);

        Assert.AreEqual("238B4FE5847E44B2", Convert.ToHexString(encrypted));
        Assert.AreEqual(Plaintext, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    public void EncryptCbc_TwoBlocks_ChainsEachBlockIntoTheNext()
    {
        using Cast128 cast = new(Convert.FromHexString(Key128));
        byte[] source = Convert.FromHexString(ChainPlaintext)[..16];
        byte[] chained = new byte[16];
        byte[] expected = new byte[16];
        byte[] block = new byte[Cast128.BlockSize];
        byte[] vector = Convert.FromHexString(Vector);

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

        CollectionAssert.AreEqual(expected, chained);
    }

    [TestMethod]
    public void EncryptCbcThenDecryptCbc_InPlaceAndChainedAcrossTwoCalls_RoundTrips()
    {
        using Cast128 cast = new(Convert.FromHexString(Key128));
        byte[] buffer = Convert.FromHexString(ChainPlaintext);
        byte[] vector = Convert.FromHexString(Vector);
        byte[] whole = new byte[buffer.Length];
        cast.EncryptCbc(vector, buffer, whole);

        cast.EncryptCbc(vector, buffer.AsSpan(0, 16), buffer.AsSpan(0, 16));
        cast.EncryptCbc(buffer.AsSpan(8, 8), buffer.AsSpan(16), buffer.AsSpan(16));
        CollectionAssert.AreEqual(whole, buffer);

        byte[] secondVector = buffer[8..16];
        cast.DecryptCbc(vector, buffer.AsSpan(0, 16), buffer.AsSpan(0, 16));
        cast.DecryptCbc(secondVector, buffer.AsSpan(16), buffer.AsSpan(16));
        Assert.AreEqual(ChainPlaintext, Convert.ToHexString(buffer));
    }

    [TestMethod]
    public void EncryptCbc_EmptySource_WritesNothing()
    {
        using Cast128 cast = new(Convert.FromHexString(Key128));

        cast.EncryptCbc(Convert.FromHexString(Vector), [], []);
        cast.DecryptCbc(Convert.FromHexString(Vector), [], []);
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(17)]
    public void Constructor_KeyOutsideFiveTo16Bytes_ThrowsArgumentException(int length)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Cast128(new byte[length]));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(16)]
    public void Constructor_FiveOr16ByteKey_IsAccepted(int length)
    {
        using Cast128 cast = new(new byte[length]);
    }

    [TestMethod]
    [DataRow(7, 8, "source")]
    [DataRow(8, 9, "destination")]
    public void EncryptBlockAndDecryptBlock_WrongLength_ThrowArgumentException(int sourceLength, int destinationLength, string parameterName)
    {
        using Cast128 cast = new(new byte[16]);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.DecryptBlock(new byte[sourceLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    [DataRow(8, 12, 12, "source")]
    [DataRow(8, 16, 8, "destination")]
    [DataRow(7, 16, 16, "initializationVector")]
    public void EncryptCbcAndDecryptCbc_WrongLength_ThrowArgumentException(int vectorLength, int sourceLength, int destinationLength, string parameterName)
    {
        using Cast128 cast = new(new byte[16]);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.EncryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => cast.DecryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    public void EveryOperation_AfterDispose_ThrowsObjectDisposedException()
    {
        Cast128 cast = new(new byte[16]);
        cast.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => cast.EncryptBlock(new byte[8], new byte[8]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => cast.DecryptBlock(new byte[8], new byte[8]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => cast.EncryptCbc(new byte[8], new byte[8], new byte[8]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => cast.DecryptCbc(new byte[8], new byte[8], new byte[8]));
    }
}
