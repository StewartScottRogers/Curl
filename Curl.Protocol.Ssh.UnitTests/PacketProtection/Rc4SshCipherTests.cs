using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class Rc4SshCipherTests
{
    private static readonly byte[] Key = Convert.FromHexString("0102030405060708090A0B0C0D0E0F10");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // RFC 6229 section 2, 128-bit key 0x0102...10: offset 0 for arcfour, offset 1536 for
    // arcfour128, which discards the first 1536 keystream bytes (RFC 4345 section 4).
    [TestMethod]
    [DataRow(0, "9AC7CC9A609D1EF7B2932899CDE41B97", DisplayName = "arcfour, RFC 6229 offset 0")]
    [DataRow(1536, "FFA0B514647EC04F6306B892AE661181", DisplayName = "arcfour128, RFC 6229 offset 1536")]
    public void Encrypt_Rfc6229Vector_GivesTheKeyStreamAfterTheDiscard(int discardLength, string keyStream)
    {
        using Rc4SshCipher cipher = new(Key, discardLength);
        byte[] output = new byte[16];
        Diagnostics.Arrange("key", Convert.ToHexString(Key));
        Diagnostics.Arrange("discard length", discardLength);

        cipher.Encrypt(new byte[8], output.AsSpan(0, 8));
        cipher.Encrypt(new byte[8], output.AsSpan(8));

        Diagnostics.ActBytes("key stream, 8 zero bytes twice", output);
        Diagnostics.AssertHex("key stream", keyStream, output);
        Diagnostics.Assert("block size", 8, cipher.BlockSize);
        Assert.AreEqual(keyStream, Convert.ToHexString(output));
        Assert.AreEqual(8, cipher.BlockSize);
    }

    [TestMethod]
    public void Decrypt_Ciphertext_DecryptsInPlaceWithAFreshCipher()
    {
        using Rc4SshCipher encryptor = new(Key, 1536);
        using Rc4SshCipher decryptor = new(Key, 1536);
        byte[] plaintext = [.. Enumerable.Range(0, 40).Select(value => (byte)value)];
        byte[] bytes = [.. plaintext];
        Diagnostics.Arrange("key", Convert.ToHexString(Key));
        Diagnostics.Arrange("discard length", 1536);
        Diagnostics.Bytes("plaintext", plaintext);

        encryptor.Encrypt(bytes, bytes);
        Diagnostics.Bytes("encrypted in place", bytes);
        decryptor.Decrypt(bytes, bytes);

        Diagnostics.ActBytes("decrypted in place", bytes);
        Diagnostics.AssertBytes("decrypted", plaintext, bytes);
        CollectionAssert.AreEqual(plaintext, bytes);
    }
}
