using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="AesCtr" /> to NIST SP 800-38A appendix F.5 (CTR-AES128, CTR-AES192 and
/// CTR-AES256, encrypt and decrypt), checks that the keystream carries across calls and
/// that the counter wraps, and checks its argument and disposal rules (ADR-0118).
/// </summary>
[TestClass]
public sealed class AesCtrTests
{
    // SP 800-38A F.5: every CTR example shares the initial counter block and plaintext.
    private const string InitialCounter = "F0F1F2F3F4F5F6F7F8F9FAFBFCFDFEFF";

    private const string Plaintext =
        "6BC1BEE22E409F96E93D7E117393172A" +
        "AE2D8A571E03AC9C9EB76FAC45AF8E51" +
        "30C81C46A35CE411E5FBC1191A0A52EF" +
        "F69F2445DF4F9B17AD2B417BE66C3710";

    private const string Aes128Key = "2B7E151628AED2A6ABF7158809CF4F3C";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // F.5.1 CTR-AES128.Encrypt, whose output F.5.2 CTR-AES128.Decrypt takes back.
    private const string Aes128Ciphertext =
        "874D6191B620E3261BEF6864990DB6CE" +
        "9806F66B7970FDFF8617187BB9FFFDFF" +
        "5AE4DF3EDBD5D35E5B4F09020DB03EAB" +
        "1E031DDA2FBE03D1792170A0F3009CEE";

    // SP 800-38A F.5.1 to F.5.6: key, ciphertext; F.5.1, F.5.3 and F.5.5 encrypt the
    // plaintext to the ciphertext, and F.5.2, F.5.4 and F.5.6 decrypt it back.
    [TestMethod]
    [DataRow(Aes128Key, Aes128Ciphertext)]
    [DataRow(
        "8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B",
        "1ABC932417521CA24F2B0459FE7E6E0B090339EC0AA6FAEFD5CCC2C6F4CE8E941E36B26BD1EBC670D1BD1D665620ABF74F78A7F6D29809585A97DAEC58C6B050")]
    [DataRow(
        "603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4",
        "601EC313775789A5B7A7F504BBF3D228F443E3CA4D62B59ACA84E990CACAF5C52B0930DAA23DE94CE87017BA2D84988DDFC9C58DB67AADA613C2DD08457941A6")]
    public void ApplyKeyStream_Sp80038aF5Vector_EncryptsAndDecryptsToThePublishedBytes(string key, string ciphertext)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AesCtr encryptor = new(Convert.FromHexString(key), Convert.FromHexString(InitialCounter));
        using AesCtr decryptor = new(Convert.FromHexString(key), Convert.FromHexString(InitialCounter));
        byte[] encrypted = new byte[Plaintext.Length / 2];
        byte[] decrypted = new byte[Plaintext.Length / 2];
        diagnostics.Arrange("source", "NIST SP 800-38A appendix F.5 vector");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("initial counter", Convert.FromHexString(InitialCounter));
        diagnostics.Bytes("plaintext", Convert.FromHexString(Plaintext));
        diagnostics.Bytes("ciphertext", Convert.FromHexString(ciphertext));

        encryptor.ApplyKeyStream(Convert.FromHexString(Plaintext), encrypted);
        decryptor.ApplyKeyStream(Convert.FromHexString(ciphertext), decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("ciphertext", ciphertext, Convert.ToHexString(encrypted));
        diagnostics.Diff("plaintext", Plaintext, Convert.ToHexString(decrypted));
        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(Plaintext, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    public void ApplyKeyStream_F51MessageSplitAcrossCalls_GivesTheOneCallBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AesCtr ctr = new(Convert.FromHexString(Aes128Key), Convert.FromHexString(InitialCounter));
        byte[] message = Convert.FromHexString(Plaintext);
        diagnostics.Arrange("source", "NIST SP 800-38A F.5.1 CTR-AES128.Encrypt vector");
        diagnostics.Bytes("key", Convert.FromHexString(Aes128Key));
        diagnostics.Bytes("plaintext", message);

        // Pieces that start and end inside blocks, one empty, the last in place.
        foreach ((int start, int length) in new[] { (0, 5), (5, 0), (5, 20), (25, 16), (41, 23) })
        {
            ctr.ApplyKeyStream(message.AsSpan(start, length), message.AsSpan(start, length));
        }

        diagnostics.Act("ciphertext", Convert.ToHexString(message));

        diagnostics.Diff("ciphertext", Aes128Ciphertext, Convert.ToHexString(message));
        Assert.AreEqual(Aes128Ciphertext, Convert.ToHexString(message));
    }

    [TestMethod]
    public void ApplyKeyStream_BeyondTheKeyStreamBuffer_KeepsCountingBlocks()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] key = Convert.FromHexString(Aes128Key);
        byte[] message = new byte[1000];
        using AesCtr ctr = new(key, Convert.FromHexString(InitialCounter));
        diagnostics.Arrange("source", "AES-ECB keystream computed block by block");
        diagnostics.Bytes("key", key);
        diagnostics.Arrange("message length", message.Length);

        ctr.ApplyKeyStream(message, message);
        byte[] expected = ExpectedKeyStream(key, Convert.FromHexString(InitialCounter), message.Length);
        diagnostics.Act("keystream length", message.Length);

        diagnostics.Diff("keystream", expected, message);
        Assert.AreEqual(Convert.ToHexString(expected), Convert.ToHexString(message));
    }

    [TestMethod]
    public void ApplyKeyStream_CounterAtAllOnes_WrapsToZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] key = Convert.FromHexString(Aes128Key);
        byte[] allOnes = Convert.FromHexString("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
        byte[] zero = new byte[AesCtr.BlockSize];
        byte[] keyStream = new byte[2 * AesCtr.BlockSize];
        using AesCtr ctr = new(key, allOnes);
        diagnostics.Arrange("source", "BCL AES-ECB of the all-ones and zero counter blocks");
        diagnostics.Bytes("key", key);
        diagnostics.Bytes("initial counter", allOnes);

        ctr.ApplyKeyStream(new byte[keyStream.Length], keyStream);
        using Aes aes = Aes.Create();
        aes.Key = key;
        byte[] expectedFirst = aes.EncryptEcb(allOnes, PaddingMode.None);
        byte[] expectedSecond = aes.EncryptEcb(zero, PaddingMode.None);
        diagnostics.Bytes("keystream", keyStream);
        diagnostics.Act("keystream", Convert.ToHexString(keyStream));

        diagnostics.Diff("first keystream block", expectedFirst, keyStream[..AesCtr.BlockSize]);
        diagnostics.Diff("second keystream block", expectedSecond, keyStream[AesCtr.BlockSize..]);
        Assert.AreEqual(Convert.ToHexString(aes.EncryptEcb(allOnes, PaddingMode.None)), Convert.ToHexString(keyStream[..AesCtr.BlockSize]));
        Assert.AreEqual(Convert.ToHexString(aes.EncryptEcb(zero, PaddingMode.None)), Convert.ToHexString(keyStream[AesCtr.BlockSize..]));
    }

    [TestMethod]
    [DataRow("000000000000000000000000000000FF", "00000000000000000000000000000100")]
    [DataRow("00FFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "01000000000000000000000000000000")]
    [DataRow("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "00000000000000000000000000000000")]
    [DataRow("F0F1F2F3F4F5F6F7F8F9FAFBFCFDFEFF", "F0F1F2F3F4F5F6F7F8F9FAFBFCFDFF00")]
    public void Increment_CounterBlock_AddsOneBigEndianWithCarry(string before, string after)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] counter = Convert.FromHexString(before);
        diagnostics.Arrange("counter before", before);

        AesCtr.Increment(counter);
        diagnostics.Act("counter after", Convert.ToHexString(counter));

        diagnostics.Diff("counter after", after, Convert.ToHexString(counter));
        Assert.AreEqual(after, Convert.ToHexString(counter));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(15)]
    [DataRow(17)]
    [DataRow(31)]
    [DataRow(33)]
    public void Constructor_KeyNot16Or24Or32Bytes_ThrowsArgumentException(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new AesCtr(new byte[length], new byte[AesCtr.BlockSize]));
        diagnostics.Act("ParamName", exception.ParamName);

        diagnostics.Assert("ParamName", "key", exception.ParamName);
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(15)]
    [DataRow(17)]
    public void Constructor_InitialCounterNotOneBlock_ThrowsArgumentException(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("initial counter length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new AesCtr(new byte[16], new byte[length]));
        diagnostics.Act("ParamName", exception.ParamName);

        diagnostics.Assert("ParamName", "initialCounter", exception.ParamName);
        Assert.AreEqual("initialCounter", exception.ParamName);
    }

    [TestMethod]
    public void ApplyKeyStream_DestinationNotAsLongAsSource_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AesCtr ctr = new(new byte[16], new byte[AesCtr.BlockSize]);
        diagnostics.Arrange("source and destination lengths", "5 and 4");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => ctr.ApplyKeyStream(new byte[5], new byte[4]));
        diagnostics.Act("ParamName", exception.ParamName);

        diagnostics.Assert("ParamName", "destination", exception.ParamName);
        Assert.AreEqual("destination", exception.ParamName);
    }

    [TestMethod]
    public void ApplyKeyStream_AfterDispose_ThrowsObjectDisposedException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        AesCtr ctr = new(new byte[32], new byte[AesCtr.BlockSize]);
        ctr.Dispose();
        diagnostics.Arrange("state", "disposed AesCtr with a 32-byte key");

        ObjectDisposedException exception = Assert.ThrowsExactly<ObjectDisposedException>(() => ctr.ApplyKeyStream(new byte[1], new byte[1]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ObjectDisposedException), exception.GetType().Name);
    }

    /// <summary>The CTR keystream computed block by block with the BCL's AES-ECB.</summary>
    private static byte[] ExpectedKeyStream(byte[] key, byte[] initialCounter, int length)
    {
        using Aes aes = Aes.Create();
        aes.Key = key;
        byte[] counter = (byte[])initialCounter.Clone();
        byte[] keyStream = new byte[(length + AesCtr.BlockSize - 1) / AesCtr.BlockSize * AesCtr.BlockSize];
        for (int offset = 0; offset < keyStream.Length; offset += AesCtr.BlockSize)
        {
            aes.EncryptEcb(counter, PaddingMode.None).CopyTo(keyStream, offset);
            AesCtr.Increment(counter);
        }

        return keyStream[..length];
    }
}
