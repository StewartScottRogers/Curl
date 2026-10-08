using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class CbcSshCipherTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Plaintext =
        "6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411E5FBC1191A0A52EFF69F2445DF4F9B17AD2B417BE66C3710";

    private static readonly byte[] SixteenByteIv = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");

    private static readonly byte[] EightByteIv = Convert.FromHexString("F0E1D2C3B4A59687");

    [TestMethod]
    [DataRow("2B7E151628AED2A6ABF7158809CF4F3C", "7649ABAC8119B246CEE98E9B12E9197D5086CB9B507219EE95DB113A917678B273BED6B8E3C1743B7116E69E222295163FF1CAA1681FAC09120ECA307586E1A7", DisplayName = "SP 800-38A F.2.1, aes128-cbc")]
    [DataRow("8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B", "4F021DB243BC633D7178183A9FA071E8B4D9ADA9AD7DEDF4E5E738763F69145A571B242012FB7AE07FA9BAAC3DF102E008B0E27988598881D920A9E64F5615CD", DisplayName = "SP 800-38A F.2.3, aes192-cbc")]
    [DataRow("603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4", "F58C4C04D6E5F1BA779EABFB5F7BFBD69CFC4E967EDB808D679F777BC6702C7D39F23369A9D9BACFA530E26304231461B2EB05E2C39BE9FCDA6C19078C6A9D1B", DisplayName = "SP 800-38A F.2.5, aes256-cbc")]
    public void ForAes_NistVector_ChainsAcrossCallsAndDecryptsInPlace(string key, string ciphertext)
    {
        using CbcSshCipher encryptor = CbcSshCipher.ForAes(Convert.FromHexString(key), SixteenByteIv);
        using CbcSshCipher decryptor = CbcSshCipher.ForAes(Convert.FromHexString(key), SixteenByteIv);
        byte[] bytes = Convert.FromHexString(Plaintext);
        Diagnostics.Arrange("key", key);
        Diagnostics.Arrange("iv", Convert.ToHexString(SixteenByteIv));
        Diagnostics.Bytes("plaintext", bytes);

        encryptor.Encrypt(bytes.AsSpan(0, 16), bytes.AsSpan(0, 16));
        encryptor.Encrypt(bytes.AsSpan(16), bytes.AsSpan(16));
        Diagnostics.ActBytes("encrypted in place, one block then the rest", bytes);
        Diagnostics.AssertHex("ciphertext", ciphertext, bytes);
        Assert.AreEqual(ciphertext, Convert.ToHexString(bytes));

        decryptor.Decrypt(bytes.AsSpan(0, 16), bytes.AsSpan(0, 16));
        decryptor.Decrypt(bytes.AsSpan(16, 32), bytes.AsSpan(16, 32));
        decryptor.Decrypt(bytes.AsSpan(48), bytes.AsSpan(48));
        Diagnostics.ActBytes("decrypted in place, 16, 32 then 16 bytes", bytes);
        Diagnostics.AssertHex("decrypted", Plaintext, bytes);
        Diagnostics.Assert("block size", 16, encryptor.BlockSize);
        Assert.AreEqual(Plaintext, Convert.ToHexString(bytes));
        Assert.AreEqual(16, encryptor.BlockSize);
    }

    [TestMethod]
    [DataRow("3des-cbc", 24, DisplayName = "3des-cbc")]
    [DataRow("blowfish-cbc", 16, DisplayName = "blowfish-cbc")]
    [DataRow("cast128-cbc", 16, DisplayName = "cast128-cbc")]
    public void EightByteBlockCipher_ChainsAcrossCallsAsOneCbcRunDoes(string cipher, int keyLength)
    {
        byte[] key = [.. Enumerable.Range(1, keyLength).Select(value => (byte)(value * 11))];
        byte[] plaintext = Convert.FromHexString(Plaintext);
        byte[] expected = OneCbcRun(cipher, key, plaintext);
        using CbcSshCipher encryptor = Create(cipher, key);
        using CbcSshCipher decryptor = Create(cipher, key);
        byte[] bytes = [.. plaintext];
        Diagnostics.Arrange("cipher", cipher);
        Diagnostics.Arrange("key", Convert.ToHexString(key));
        Diagnostics.Arrange("iv", Convert.ToHexString(EightByteIv));
        Diagnostics.Bytes("plaintext", plaintext);

        encryptor.Encrypt(bytes.AsSpan(0, 8), bytes.AsSpan(0, 8));
        encryptor.Encrypt(bytes.AsSpan(8), bytes.AsSpan(8));
        Diagnostics.ActBytes("encrypted in place, one block then the rest", bytes);
        Diagnostics.AssertBytes("ciphertext against one CBC run", expected, bytes);
        CollectionAssert.AreEqual(expected, bytes);

        decryptor.Decrypt(bytes.AsSpan(0, 24), bytes.AsSpan(0, 24));
        decryptor.Decrypt(bytes.AsSpan(24), bytes.AsSpan(24));
        Diagnostics.ActBytes("decrypted in place, 24 then 40 bytes", bytes);
        Diagnostics.AssertBytes("decrypted", plaintext, bytes);
        Diagnostics.Assert("block size", 8, encryptor.BlockSize);
        CollectionAssert.AreEqual(plaintext, bytes);
        Assert.AreEqual(8, encryptor.BlockSize);
    }

    [TestMethod]
    public void EncryptAndDecrypt_NoBytes_LeaveTheChainWhereItWas()
    {
        byte[] key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3C");
        using CbcSshCipher encryptor = CbcSshCipher.ForAes(key, SixteenByteIv);
        using CbcSshCipher decryptor = CbcSshCipher.ForAes(key, SixteenByteIv);
        byte[] bytes = Convert.FromHexString(Plaintext);
        Diagnostics.Arrange("key", Convert.ToHexString(key));
        Diagnostics.Arrange("calls", "an empty encrypt and an empty decrypt before each real one");

        encryptor.Encrypt([], []);
        encryptor.Encrypt(bytes, bytes);
        decryptor.Decrypt([], []);
        decryptor.Decrypt(bytes, bytes);

        Diagnostics.ActBytes("round trip", bytes);
        Diagnostics.AssertHex("round trip", Plaintext, bytes);
        Assert.AreEqual(Plaintext, Convert.ToHexString(bytes));
    }

    private static CbcSshCipher Create(string cipher, byte[] key) => cipher switch
    {
        "3des-cbc" => CbcSshCipher.ForTripleDes(key, EightByteIv),
        "blowfish-cbc" => CbcSshCipher.ForBlowfish(key, EightByteIv),
        _ => CbcSshCipher.ForCast128(key, EightByteIv),
    };

    private static byte[] OneCbcRun(string cipher, byte[] key, byte[] plaintext)
    {
        byte[] ciphertext = new byte[plaintext.Length];
        if (cipher == "3des-cbc")
        {
            using TripleDES tripleDes = TripleDES.Create();
            tripleDes.Key = key;
            return tripleDes.EncryptCbc(plaintext, EightByteIv, PaddingMode.None);
        }

        if (cipher == "blowfish-cbc")
        {
            using Blowfish blowfish = new(key);
            blowfish.EncryptCbc(EightByteIv, plaintext, ciphertext);
            return ciphertext;
        }

        using Cast128 cast128 = new(key);
        cast128.EncryptCbc(EightByteIv, plaintext, ciphertext);
        return ciphertext;
    }
}
