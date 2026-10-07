using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="AesCbcCts" /> to RFC 3962 appendix B, the six AES-128 ciphertext
/// stealing vectors, in both directions, and checks its one-block case and its argument
/// and disposal rules (ADR-0118).
/// </summary>
[TestClass]
public sealed class AesCbcCtsTests
{
    // RFC 3962 appendix B: the key is "chicken teriyaki" and the IV is all zeros.
    private const string Key = "636869636B656E207465726979616B69";

    private static readonly byte[] ZeroVector = new byte[AesCbcCts.BlockSize];

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 3962 appendix B: input, output, for inputs of 17, 31, 32, 47, 48 and 64 bytes
    // of "I would like the General Gau's Chicken, please, and wonton soup."
    [TestMethod]
    [DataRow(
        "4920776F756C64206C696B652074686520",
        "C6353568F2BF8CB4D8A580362DA7FF7F97")]
    [DataRow(
        "4920776F756C64206C696B65207468652047656E6572616C20476175277320",
        "FC00783E0EFDB2C1D445D4C8EFF7ED2297687268D6ECCCC0C07B25E25ECFE5")]
    [DataRow(
        "4920776F756C64206C696B65207468652047656E6572616C2047617527732043",
        "39312523A78662D5BE7FCBCC98EBF5A897687268D6ECCCC0C07B25E25ECFE584")]
    [DataRow(
        "4920776F756C64206C696B65207468652047656E6572616C20476175277320436869636B656E2C20706C656173652C",
        "97687268D6ECCCC0C07B25E25ECFE584B3FFFD940C16A18C1B5549D2F838029E39312523A78662D5BE7FCBCC98EBF5")]
    [DataRow(
        "4920776F756C64206C696B65207468652047656E6572616C20476175277320436869636B656E2C20706C656173652C20",
        "97687268D6ECCCC0C07B25E25ECFE5849DAD8BBB96C4CDC03BC103E1A194BBD839312523A78662D5BE7FCBCC98EBF5A8")]
    [DataRow(
        "4920776F756C64206C696B65207468652047656E6572616C20476175277320436869636B656E2C20706C656173652C20616E6420776F6E746F6E20736F75702E",
        "97687268D6ECCCC0C07B25E25ECFE58439312523A78662D5BE7FCBCC98EBF5A84807EFE836EE89A526730DBC2F7BC8409DAD8BBB96C4CDC03BC103E1A194BBD8")]
    public void EncryptAndDecrypt_Rfc3962AppendixBVector_GiveThePublishedBytes(string input, string output)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AesCbcCts cts = new(Convert.FromHexString(Key));
        byte[] encrypted = new byte[input.Length / 2];
        byte[] decrypted = new byte[input.Length / 2];
        diagnostics.Arrange("source", "RFC 3962 appendix B vector");
        diagnostics.Bytes("key", Convert.FromHexString(Key));
        diagnostics.Bytes("initialization vector", ZeroVector);
        diagnostics.Bytes("plaintext", Convert.FromHexString(input));
        diagnostics.Bytes("ciphertext", Convert.FromHexString(output));

        cts.Encrypt(ZeroVector, Convert.FromHexString(input), encrypted);
        cts.Decrypt(ZeroVector, Convert.FromHexString(output), decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("ciphertext", output, Convert.ToHexString(encrypted));
        diagnostics.Diff("plaintext", input, Convert.ToHexString(decrypted));
        Assert.AreEqual(output, Convert.ToHexString(encrypted));
        Assert.AreEqual(input, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    public void EncryptAndDecrypt_OneBlock_ArePlainCbc()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] key = Convert.FromHexString(Key);
        byte[] vector = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
        byte[] block = Convert.FromHexString("4920776F756C64206C696B6520746865");
        using Aes aes = Aes.Create();
        aes.Key = key;
        byte[] expected = aes.EncryptCbc(block, vector, PaddingMode.None);
        using AesCbcCts cts = new(key);
        byte[] encrypted = new byte[AesCbcCts.BlockSize];
        byte[] decrypted = new byte[AesCbcCts.BlockSize];
        diagnostics.Arrange("source", "BCL AES-CBC without padding over one block");
        diagnostics.Bytes("key", key);
        diagnostics.Bytes("initialization vector", vector);
        diagnostics.Bytes("block", block);

        cts.Encrypt(vector, block, encrypted);
        cts.Decrypt(vector, encrypted, decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("ciphertext", expected, encrypted);
        diagnostics.Diff("plaintext", block, decrypted);
        Assert.AreEqual(Convert.ToHexString(expected), Convert.ToHexString(encrypted));
        Assert.AreEqual(Convert.ToHexString(block), Convert.ToHexString(decrypted));
    }

    [TestMethod]
    [DataRow(33)]
    [DataRow(80)]
    [DataRow(1001)]
    public void Decrypt_OfEncrypt_WithAVectorAndHeadBlocks_GivesTheMessageBack(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = new byte[length];
        for (int index = 0; index < length; index++)
        {
            message[index] = (byte)(index * 7);
        }

        byte[] vector = Convert.FromHexString("0F0E0D0C0B0A09080706050403020100");
        using AesCbcCts cts = new(new byte[32]);
        byte[] encrypted = new byte[length];
        byte[] decrypted = new byte[length];
        diagnostics.Arrange("message length", length);
        diagnostics.Bytes("initialization vector", vector);

        cts.Encrypt(vector, message, encrypted);
        cts.Decrypt(vector, encrypted, decrypted);
        diagnostics.Act("decrypted length", decrypted.Length);

        diagnostics.Diff("round-tripped message", message, decrypted);
        Assert.AreEqual(Convert.ToHexString(message), Convert.ToHexString(decrypted));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(15)]
    public void EncryptAndDecrypt_SourceShorterThanOneBlock_ThrowArgumentException(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AesCbcCts cts = new(Convert.FromHexString(Key));
        diagnostics.Arrange("source length", length);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => cts.Encrypt(ZeroVector, new byte[length], new byte[length]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => cts.Decrypt(ZeroVector, new byte[length], new byte[length]));
        diagnostics.Act("encrypting ParamName", encrypting.ParamName);
        diagnostics.Act("decrypting ParamName", decrypting.ParamName);

        diagnostics.Assert("encrypting ParamName", "source", encrypting.ParamName);
        diagnostics.Assert("decrypting ParamName", "source", decrypting.ParamName);
        Assert.AreEqual("source", encrypting.ParamName);
        Assert.AreEqual("source", decrypting.ParamName);
    }

    [TestMethod]
    [DataRow(15, 17, 17, "initializationVector")]
    [DataRow(17, 17, 17, "initializationVector")]
    [DataRow(16, 17, 16, "destination")]
    [DataRow(16, 17, 18, "destination")]
    public void EncryptAndDecrypt_WrongLength_ThrowArgumentException(int vectorLength, int sourceLength, int destinationLength, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AesCbcCts cts = new(Convert.FromHexString(Key));
        diagnostics.Arrange("vector length", vectorLength);
        diagnostics.Arrange("source length", sourceLength);
        diagnostics.Arrange("destination length", destinationLength);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => cts.Encrypt(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => cts.Decrypt(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        diagnostics.Act("encrypting ParamName", encrypting.ParamName);
        diagnostics.Act("decrypting ParamName", decrypting.ParamName);

        diagnostics.Assert("encrypting ParamName", parameterName, encrypting.ParamName);
        diagnostics.Assert("decrypting ParamName", parameterName, decrypting.ParamName);
        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(8)]
    [DataRow(20)]
    public void Constructor_KeyNot16Or24Or32Bytes_ThrowsArgumentException(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new AesCbcCts(new byte[length]));
        diagnostics.Act("ParamName", exception.ParamName);

        diagnostics.Assert("ParamName", "key", exception.ParamName);
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void EncryptAndDecrypt_AfterDispose_ThrowObjectDisposedException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        AesCbcCts cts = new(new byte[24]);
        cts.Dispose();
        byte[] message = new byte[AesCbcCts.BlockSize];
        diagnostics.Arrange("state", "disposed AesCbcCts with a 24-byte key");

        ObjectDisposedException encrypting = Assert.ThrowsExactly<ObjectDisposedException>(() => cts.Encrypt(ZeroVector, message, message));
        ObjectDisposedException decrypting = Assert.ThrowsExactly<ObjectDisposedException>(() => cts.Decrypt(ZeroVector, message, message));
        diagnostics.Act("encrypting exception", encrypting.GetType().Name);
        diagnostics.Act("decrypting exception", decrypting.GetType().Name);

        diagnostics.Assert("encrypting exception", nameof(ObjectDisposedException), encrypting.GetType().Name);
        diagnostics.Assert("decrypting exception", nameof(ObjectDisposedException), decrypting.GetType().Name);
    }
}
