using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Camellia" /> to RFC 3713 Appendix A's test vectors for 128-, 192- and
/// 256-bit keys, its CBC mode to vectors recorded from OpenSSL 3.5.7's
/// <c>openssl enc</c> (RFC 5932 publishes none), and checks its argument and disposal
/// rules (ADR-0118).
/// </summary>
[TestClass]
public sealed class CamelliaTests
{
    private const string AppendixPlaintext = "0123456789ABCDEFFEDCBA9876543210";

    // printf '00112233445566778899aabbccddeeff000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f' | xxd -r -p |
    //   openssl enc -camellia-128-cbc -K 2b7e151628aed2a6abf7158809cf4f3c -iv 000102030405060708090a0b0c0d0e0f -nopad | xxd -p -c 64
    private const string ChainKey128 = "2B7E151628AED2A6ABF7158809CF4F3C";
    private const string ChainVector = "000102030405060708090A0B0C0D0E0F";
    private const string ChainPlaintext = "00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";
    private const string ChainCiphertext128 = "850D996DCCA7D131D2A4A449540027CF6AD9FE0A0FDE5223DC687C85A906D79D7A21D87F251E17EF15DE2EFC8997956A";

    // printf '00112233445566778899aabbccddeeff000102030405060708090a0b0c0d0e0f' | xxd -r -p |
    //   openssl enc -camellia-256-cbc -K 603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4 -iv 000102030405060708090a0b0c0d0e0f -nopad | xxd -p -c 64
    private const string ChainKey256 = "603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4";
    private const string ChainPlaintext256 = "00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F";
    private const string ChainCiphertext256 = "EB2A7525AEBE5C7CC9A73679929F1676F1070AD67E17CF254B9697D6F95A813B";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 3713 Appendix A: key, then the ciphertext of 0123456789abcdeffedcba9876543210.
    [TestMethod]
    [DataRow("0123456789ABCDEFFEDCBA9876543210", "67673138549669730857065648EABE43")]
    [DataRow("0123456789ABCDEFFEDCBA98765432100011223344556677", "B4993401B3E996F84EE5CEE7D79B09B9")]
    [DataRow("0123456789ABCDEFFEDCBA987654321000112233445566778899AABBCCDDEEFF", "9ACC237DFF16D76C20EF7C919E3A7509")]
    public void EncryptBlockAndDecryptBlock_AppendixAVector_GiveThePublishedCiphertextAndPlaintext(string key, string ciphertext)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Camellia camellia = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[Camellia.BlockSize];
        byte[] decrypted = new byte[Camellia.BlockSize];
        diagnostics.Arrange("vector source", "RFC 3713 Appendix A");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("plaintext", Convert.FromHexString(AppendixPlaintext));
        diagnostics.Bytes("ciphertext", Convert.FromHexString(ciphertext));

        camellia.EncryptBlock(Convert.FromHexString(AppendixPlaintext), encrypted);
        camellia.DecryptBlock(Convert.FromHexString(ciphertext), decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("encrypted", Convert.FromHexString(ciphertext), encrypted);
        diagnostics.Diff("decrypted", Convert.FromHexString(AppendixPlaintext), decrypted);
        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(AppendixPlaintext, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    [DataRow(ChainKey128, ChainPlaintext, ChainCiphertext128)]
    [DataRow(ChainKey256, ChainPlaintext256, ChainCiphertext256)]
    public void EncryptCbcAndDecryptCbc_OpenSslVector_GiveTheRecordedCiphertextAndPlaintext(string key, string plaintext, string ciphertext)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Camellia camellia = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[plaintext.Length / 2];
        byte[] decrypted = new byte[ciphertext.Length / 2];
        diagnostics.Arrange("vector source", "recorded from OpenSSL 3.5.7 openssl enc -camellia-*-cbc -nopad");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("initialization vector", Convert.FromHexString(ChainVector));
        diagnostics.Bytes("plaintext", Convert.FromHexString(plaintext));
        diagnostics.Bytes("ciphertext", Convert.FromHexString(ciphertext));

        camellia.EncryptCbc(Convert.FromHexString(ChainVector), Convert.FromHexString(plaintext), encrypted);
        camellia.DecryptCbc(Convert.FromHexString(ChainVector), Convert.FromHexString(ciphertext), decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("encrypted", Convert.FromHexString(ciphertext), encrypted);
        diagnostics.Diff("decrypted", Convert.FromHexString(plaintext), decrypted);
        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(plaintext, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    public void EncryptCbcThenDecryptCbc_InPlaceAndChainedAcrossTwoCalls_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Camellia camellia = new(Convert.FromHexString(ChainKey128));
        byte[] buffer = Convert.FromHexString(ChainPlaintext);
        byte[] vector = Convert.FromHexString(ChainVector);
        diagnostics.Arrange("vector source", "recorded from OpenSSL 3.5.7 openssl enc -camellia-128-cbc -nopad");
        diagnostics.Bytes("key", Convert.FromHexString(ChainKey128));
        diagnostics.Bytes("initialization vector", vector);
        diagnostics.Bytes("plaintext", buffer);

        camellia.EncryptCbc(vector, buffer.AsSpan(0, 32), buffer.AsSpan(0, 32));
        camellia.EncryptCbc(buffer.AsSpan(16, 16), buffer.AsSpan(32), buffer.AsSpan(32));
        diagnostics.Act("encrypted", Convert.ToHexString(buffer));
        diagnostics.Diff("encrypted", Convert.FromHexString(ChainCiphertext128), buffer);
        Assert.AreEqual(ChainCiphertext128, Convert.ToHexString(buffer));

        byte[] secondVector = buffer[16..32];
        camellia.DecryptCbc(vector, buffer.AsSpan(0, 32), buffer.AsSpan(0, 32));
        camellia.DecryptCbc(secondVector, buffer.AsSpan(32), buffer.AsSpan(32));
        diagnostics.Act("decrypted", Convert.ToHexString(buffer));
        diagnostics.Diff("decrypted", Convert.FromHexString(ChainPlaintext), buffer);
        Assert.AreEqual(ChainPlaintext, Convert.ToHexString(buffer));
    }

    [TestMethod]
    public void EncryptCbc_EmptySource_WritesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Camellia camellia = new(Convert.FromHexString(ChainKey128));
        byte[] buffer = [0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5];
        diagnostics.Arrange("source length", 0);
        diagnostics.Bytes("buffer before", buffer);

        camellia.EncryptCbc(Convert.FromHexString(ChainVector), [], buffer.AsSpan(0, 0));
        camellia.DecryptCbc(Convert.FromHexString(ChainVector), [], buffer.AsSpan(0, 0));
        diagnostics.Act("buffer after", Convert.ToHexString(buffer));

        diagnostics.Diff("buffer", Convert.FromHexString("A5A5A5A5A5A5A5A5"), buffer);
        Assert.AreEqual("A5A5A5A5A5A5A5A5", Convert.ToHexString(buffer));
    }

    [TestMethod]
    public void FunctionLayerThenInverseFunctionLayer_AnyInput_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const ulong Input = 0x0123456789ABCDEFUL;
        const ulong Subkey = 0xF0E1D2C3B4A59687UL;
        diagnostics.Arrange("input", $"0x{Input:X16}");
        diagnostics.Arrange("subkey", $"0x{Subkey:X16}");

        ulong layered = Camellia.FunctionLayer(Input, Subkey);
        ulong restored = Camellia.InverseFunctionLayer(layered, Subkey);
        diagnostics.Act("FL output", $"0x{layered:X16}");
        diagnostics.Act("FL^-1 output", $"0x{restored:X16}");

        diagnostics.Assert("round trip", $"0x{Input:X16}", $"0x{restored:X16}");
        Assert.AreEqual(Input, restored);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(15)]
    [DataRow(17)]
    [DataRow(23)]
    [DataRow(25)]
    [DataRow(31)]
    [DataRow(33)]
    public void Constructor_KeyNot16Or24Or32Bytes_ThrowsArgumentException(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Camellia(new byte[length]));
        diagnostics.Act("parameter name", exception.ParamName);

        diagnostics.Assert("parameter name", "key", exception.ParamName);
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(7, 16, "source")]
    [DataRow(16, 17, "destination")]
    public void EncryptBlockAndDecryptBlock_WrongLength_ThrowArgumentException(int sourceLength, int destinationLength, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Camellia camellia = new(new byte[16]);
        diagnostics.Arrange("source length", sourceLength);
        diagnostics.Arrange("destination length", destinationLength);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => camellia.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => camellia.DecryptBlock(new byte[sourceLength], new byte[destinationLength]));
        diagnostics.Act("encrypt parameter name", encrypting.ParamName);
        diagnostics.Act("decrypt parameter name", decrypting.ParamName);

        diagnostics.Assert("encrypt parameter name", parameterName, encrypting.ParamName);
        diagnostics.Assert("decrypt parameter name", parameterName, decrypting.ParamName);
        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    [DataRow(16, 24, 24, "source")]
    [DataRow(16, 32, 16, "destination")]
    [DataRow(15, 32, 32, "initializationVector")]
    public void EncryptCbcAndDecryptCbc_WrongLength_ThrowArgumentException(int vectorLength, int sourceLength, int destinationLength, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Camellia camellia = new(new byte[16]);
        diagnostics.Arrange("initialization vector length", vectorLength);
        diagnostics.Arrange("source length", sourceLength);
        diagnostics.Arrange("destination length", destinationLength);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => camellia.EncryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => camellia.DecryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        diagnostics.Act("encrypt parameter name", encrypting.ParamName);
        diagnostics.Act("decrypt parameter name", decrypting.ParamName);

        diagnostics.Assert("encrypt parameter name", parameterName, encrypting.ParamName);
        diagnostics.Assert("decrypt parameter name", parameterName, decrypting.ParamName);
        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    public void Dispose_ZeroesTheKeySchedule()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Camellia camellia = new(Convert.FromHexString(ChainKey256));
        diagnostics.Bytes("key", Convert.FromHexString(ChainKey256));
        diagnostics.Arrange("encryption subkeys non-zero", camellia.EncryptionSubkeys.ContainsAnyExcept(0UL));
        diagnostics.Arrange("decryption subkeys non-zero", camellia.DecryptionSubkeys.ContainsAnyExcept(0UL));
        Assert.IsTrue(camellia.EncryptionSubkeys.ContainsAnyExcept(0UL));
        Assert.IsTrue(camellia.DecryptionSubkeys.ContainsAnyExcept(0UL));

        camellia.Dispose();
        bool encryptionNonZero = camellia.EncryptionSubkeys.ContainsAnyExcept(0UL);
        bool decryptionNonZero = camellia.DecryptionSubkeys.ContainsAnyExcept(0UL);
        diagnostics.Act("encryption subkeys non-zero after dispose", encryptionNonZero);
        diagnostics.Act("decryption subkeys non-zero after dispose", decryptionNonZero);

        diagnostics.Assert("encryption subkeys non-zero after dispose", false, encryptionNonZero);
        diagnostics.Assert("decryption subkeys non-zero after dispose", false, decryptionNonZero);
        Assert.IsFalse(encryptionNonZero);
        Assert.IsFalse(decryptionNonZero);
    }

    [TestMethod]
    public void EveryOperation_AfterDispose_ThrowsObjectDisposedException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Camellia camellia = new(new byte[16]);
        camellia.Dispose();
        byte[] block = new byte[Camellia.BlockSize];
        diagnostics.Arrange("state", "disposed");
        diagnostics.Arrange("block length", block.Length);

        ObjectDisposedException encryptBlock = Assert.ThrowsExactly<ObjectDisposedException>(() => camellia.EncryptBlock(block, block));
        ObjectDisposedException decryptBlock = Assert.ThrowsExactly<ObjectDisposedException>(() => camellia.DecryptBlock(block, block));
        ObjectDisposedException encryptCbc = Assert.ThrowsExactly<ObjectDisposedException>(() => camellia.EncryptCbc(block, block, block));
        ObjectDisposedException decryptCbc = Assert.ThrowsExactly<ObjectDisposedException>(() => camellia.DecryptCbc(block, block, block));
        string thrown = string.Join(", ", encryptBlock.GetType().Name, decryptBlock.GetType().Name, encryptCbc.GetType().Name, decryptCbc.GetType().Name);
        diagnostics.Act("exceptions (EncryptBlock, DecryptBlock, EncryptCbc, DecryptCbc)", thrown);

        diagnostics.Assert("exceptions", string.Join(", ", Enumerable.Repeat(nameof(ObjectDisposedException), 4)), thrown);
    }
}
