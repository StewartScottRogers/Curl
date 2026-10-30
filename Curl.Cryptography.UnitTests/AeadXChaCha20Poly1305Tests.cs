namespace Curl.Cryptography;

/// <summary>Pins <see cref="AeadXChaCha20Poly1305" /> to draft-irtf-cfrg-xchacha-03 Appendix A.3.1.</summary>
[TestClass]
public sealed class AeadXChaCha20Poly1305Tests
{
    private const string Plaintext =
        "4c616469657320616e642047656e746c656d656e206f662074686520636c617373206f66202739393a204966204920636f756c64206f6666657220796f75206f6e6c79206f6e652074697020666f7220746865206675747572652c2073756e73637265656e20776f756c642062652069742e";

    private const string AssociatedData = "50515253c0c1c2c3c4c5c6c7";

    private const string Key = "808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f";

    private const string Nonce = "404142434445464748494a4b4c4d4e4f5051525354555657";

    private const string Ciphertext =
        "bd6d179d3e83d43b9576579493c0e939572a1700252bfaccbed2902c21396cbb731c7f1b0b4aa6440bf3a82f4eda7e39ae64c6708c54c216cb96b72e1213b4522f8c9ba40db5d945b11b69b982c1bb9e3f3fac2bc369488f76b2383565d3fff921f9664c97637da9768812f615c68b13b52e";

    private const string Tag = "c0875924c1c7987947deafd8780acf49";

    [TestMethod]
    public void Encrypt_DraftAppendixA31_MatchesCiphertextAndTag()
    {
        using var aead = new AeadXChaCha20Poly1305(Convert.FromHexString(Key));
        byte[] plaintext = Convert.FromHexString(Plaintext);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AeadXChaCha20Poly1305.TagSize];

        aead.Encrypt(Convert.FromHexString(Nonce), plaintext, ciphertext, tag, Convert.FromHexString(AssociatedData));

        Assert.AreEqual(Ciphertext, Convert.ToHexStringLower(ciphertext));
        Assert.AreEqual(Tag, Convert.ToHexStringLower(tag));
    }

    [TestMethod]
    public void TryDecrypt_DraftAppendixA31_RecoversPlaintext()
    {
        using var aead = new AeadXChaCha20Poly1305(Convert.FromHexString(Key));
        byte[] plaintext = new byte[Plaintext.Length / 2];

        bool opened = aead.TryDecrypt(
            Convert.FromHexString(Nonce),
            Convert.FromHexString(Ciphertext),
            Convert.FromHexString(Tag),
            plaintext,
            Convert.FromHexString(AssociatedData));

        Assert.IsTrue(opened);
        Assert.AreEqual(Plaintext, Convert.ToHexStringLower(plaintext));
    }

    [TestMethod]
    public void TryDecrypt_TamperedTag_FailsWithPlaintextZeroed()
    {
        using var aead = new AeadXChaCha20Poly1305(Convert.FromHexString(Key));
        byte[] tag = Convert.FromHexString(Tag);
        tag[0] ^= 1;
        byte[] plaintext = new byte[Plaintext.Length / 2];
        Array.Fill(plaintext, (byte)0xAA);

        bool opened = aead.TryDecrypt(
            Convert.FromHexString(Nonce),
            Convert.FromHexString(Ciphertext),
            tag,
            plaintext,
            Convert.FromHexString(AssociatedData));

        Assert.IsFalse(opened);
        Assert.IsTrue(plaintext.All(b => b == 0));
    }

    [TestMethod]
    public void Constructor_WrongKeyLength_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new AeadXChaCha20Poly1305(new byte[16]));

    [TestMethod]
    public void Encrypt_WrongNonceLength_Throws()
    {
        using var aead = new AeadXChaCha20Poly1305(new byte[AeadXChaCha20Poly1305.KeySize]);

        Assert.ThrowsExactly<ArgumentException>(() => aead.Encrypt(new byte[12], [], [], new byte[16]));
    }

    [TestMethod]
    public void Encrypt_AfterDispose_ThrowsObjectDisposed()
    {
        var aead = new AeadXChaCha20Poly1305(new byte[AeadXChaCha20Poly1305.KeySize]);

        aead.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => aead.Encrypt(new byte[24], [], [], new byte[16]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => aead.TryDecrypt(new byte[24], [], new byte[16], []));
    }
}
