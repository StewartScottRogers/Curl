using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="AeadChaCha20Poly1305" /> to RFC 8439's published vectors (section 2.8.2
/// and Appendix A.5), and checks a wrong tag is the <c>false</c> of ADR-0118 with no
/// plaintext written.
/// </summary>
[TestClass]
public sealed class AeadChaCha20Poly1305Tests
{
    private const string SunscreenKey = "808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f";
    private const string SunscreenNonce = "070000004041424344454647";
    private const string SunscreenAssociatedData = "50515253c0c1c2c3c4c5c6c7";

    private const string SunscreenPlaintext =
        "4c616469657320616e642047656e746c656d656e206f662074686520636c617373206f66202739393a204966204920636f756c64206f6666657220796f75206f6e6c79206f6e652074697020666f7220746865206675747572652c2073756e73637265656e20776f756c642062652069742e";

    private const string SunscreenCiphertext =
        "d31a8d34648e60db7b86afbc53ef7ec2a4aded51296e08fea9e2b5a736ee62d63dbea45e8ca9671282fafb69da92728b1a71de0a9e060b2905d6a5b67ecd3b3692ddbd7f2d778b8c9803aee328091b58fab324e4fad675945585808b4831d7bc3ff4def08e4b7a9de576d26586cec64b6116";

    private const string SunscreenTag = "1ae10b594f09e26a7e902ecbd0600691";

    private const string DraftKey = "1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0";
    private const string DraftNonce = "000000000102030405060708";
    private const string DraftAssociatedData = "f33388860000000000004e91";
    private const string DraftTag = "eead9d67890cbb22392336fea1851f38";

    private const string DraftCiphertext =
        "64a0861575861af460f062c79be643bd5e805cfd345cf389f108670ac76c8cb24c6cfc18755d43eea09ee94e382d26b0bdb7b73c321b0100d4f03b7f355894cf332f830e710b97ce98c8a84abd0b948114ad176e008d33bd60f982b1ff37c8559797a06ef4f0ef61c186324e2b3506383606907b6a7c02b0f9f6157b53c867e4b9166c767b804d46a59b5216cde7a4e99040c5a40433225ee282a1b0a06c523eaf4534d7f83fa1155b0047718cbc546a0d072b04b3564eea1b422273f548271a0bb2316053fa76991955ebd63159434ecebb4e466dae5a1073a6727627097a1049e617d91d361094fa68f0ff77987130305beaba2eda04df997b714d6c6f2c29a6ad5cb4022b02709b";

    private const string DraftPlaintext =
        "496e7465726e65742d4472616674732061726520647261667420646f63756d656e74732076616c696420666f722061206d6178696d756d206f6620736978206d6f6e74687320616e64206d617920626520757064617465642c207265706c616365642c206f72206f62736f6c65746564206279206f7468657220646f63756d656e747320617420616e792074696d652e20497420697320696e617070726f70726961746520746f2075736520496e7465726e65742d447261667473206173207265666572656e6365206d6174657269616c206f7220746f2063697465207468656d206f74686572207468616e206173202fe2809c776f726b20696e2070726f67726573732e2fe2809d";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 8439 section 2.8.2.
    [TestMethod]
    public void Encrypt_Rfc8439Section282Vector_GivesThePublishedCiphertextAndTag()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AeadChaCha20Poly1305 aead = new(Convert.FromHexString(SunscreenKey));
        byte[] ciphertext = new byte[SunscreenPlaintext.Length / 2];
        byte[] tag = new byte[AeadChaCha20Poly1305.TagSize];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.8.2");
        diagnostics.Bytes("key", Convert.FromHexString(SunscreenKey));
        diagnostics.Bytes("nonce", Convert.FromHexString(SunscreenNonce));

        aead.Encrypt(
            Convert.FromHexString(SunscreenNonce),
            Convert.FromHexString(SunscreenPlaintext),
            ciphertext,
            tag,
            Convert.FromHexString(SunscreenAssociatedData));
        diagnostics.Act("ciphertext", Convert.ToHexStringLower(ciphertext));
        diagnostics.Act("tag", Convert.ToHexStringLower(tag));

        diagnostics.Diff("ciphertext", Convert.FromHexString(SunscreenCiphertext), ciphertext);
        diagnostics.Diff("tag", Convert.FromHexString(SunscreenTag), tag);
        Assert.AreEqual(SunscreenCiphertext, Convert.ToHexStringLower(ciphertext));
        Assert.AreEqual(SunscreenTag, Convert.ToHexStringLower(tag));
    }

    // RFC 8439 section 2.8.2, decrypted.
    [TestMethod]
    public void TryDecrypt_Rfc8439Section282Vector_GivesThePublishedPlaintext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AeadChaCha20Poly1305 aead = new(Convert.FromHexString(SunscreenKey));
        byte[] plaintext = new byte[SunscreenCiphertext.Length / 2];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.8.2");
        diagnostics.Bytes("key", Convert.FromHexString(SunscreenKey));
        diagnostics.Bytes("tag", Convert.FromHexString(SunscreenTag));

        bool succeeded = aead.TryDecrypt(
            Convert.FromHexString(SunscreenNonce),
            Convert.FromHexString(SunscreenCiphertext),
            Convert.FromHexString(SunscreenTag),
            plaintext,
            Convert.FromHexString(SunscreenAssociatedData));
        diagnostics.Act("succeeded", succeeded);
        diagnostics.Act("plaintext", Convert.ToHexStringLower(plaintext));

        diagnostics.Assert("succeeded", true, succeeded);
        diagnostics.Diff("plaintext", SunscreenPlaintext, Convert.ToHexStringLower(plaintext));
        Assert.IsTrue(succeeded);
        Assert.AreEqual(SunscreenPlaintext, Convert.ToHexStringLower(plaintext));
    }

    // RFC 8439 Appendix A.5.
    [TestMethod]
    public void TryDecrypt_Rfc8439AppendixA5Vector_GivesThePublishedPlaintext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] plaintext = new byte[DraftCiphertext.Length / 2];
        diagnostics.Arrange("vector source", "RFC 8439 Appendix A.5");
        diagnostics.Bytes("tag", Convert.FromHexString(DraftTag));

        bool succeeded = TryDecryptDraft(Convert.FromHexString(DraftCiphertext), Convert.FromHexString(DraftTag), Convert.FromHexString(DraftAssociatedData), plaintext);
        diagnostics.Act("succeeded", succeeded);
        diagnostics.Act("plaintext", Convert.ToHexStringLower(plaintext));

        diagnostics.Assert("succeeded", true, succeeded);
        diagnostics.Diff("plaintext", DraftPlaintext, Convert.ToHexStringLower(plaintext));
        Assert.IsTrue(succeeded);
        Assert.AreEqual(DraftPlaintext, Convert.ToHexStringLower(plaintext));
    }

    // RFC 8439 Appendix A.5 with one bit flipped in the tag, the ciphertext or the
    // associated data: the tag fails, and the plaintext buffer is left all zero.
    [TestMethod]
    [DataRow("tag", 0)]
    [DataRow("tag", 15)]
    [DataRow("ciphertext", 0)]
    [DataRow("ciphertext", 264)]
    [DataRow("associatedData", 11)]
    public void TryDecrypt_Rfc8439AppendixA5VectorWithAFlippedBit_ReturnsFalseAndWritesNoPlaintext(string flipped, int index)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, byte[]> inputs = new()
        {
            ["tag"] = Convert.FromHexString(DraftTag),
            ["ciphertext"] = Convert.FromHexString(DraftCiphertext),
            ["associatedData"] = Convert.FromHexString(DraftAssociatedData),
        };
        inputs[flipped][index] ^= 0x01;
        byte[] plaintext = new byte[DraftCiphertext.Length / 2];
        Array.Fill(plaintext, (byte)0xAA);
        diagnostics.Arrange("vector source", "RFC 8439 Appendix A.5 with one bit flipped");
        diagnostics.Arrange("flipped input and index", $"{flipped}, {index}");

        bool succeeded = TryDecryptDraft(inputs["ciphertext"], inputs["tag"], inputs["associatedData"], plaintext);
        diagnostics.Act("succeeded", succeeded);

        diagnostics.Assert("succeeded", false, succeeded);
        diagnostics.Diff("plaintext left all zero", new byte[plaintext.Length], plaintext);
        Assert.IsFalse(succeeded);
        Assert.IsTrue(plaintext.All(value => value == 0));
    }

    [TestMethod]
    public void EncryptThenTryDecrypt_EmptyPlaintextAndNoAssociatedData_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AeadChaCha20Poly1305 aead = new(Convert.FromHexString(SunscreenKey));
        byte[] nonce = Convert.FromHexString(SunscreenNonce);
        byte[] tag = new byte[AeadChaCha20Poly1305.TagSize];
        diagnostics.Arrange("input", "empty plaintext, no associated data, RFC 8439 key and nonce");
        diagnostics.Bytes("nonce", nonce);

        aead.Encrypt(nonce, [], [], tag);
        bool succeeded = aead.TryDecrypt(nonce, [], tag, []);
        diagnostics.Bytes("tag", tag);
        diagnostics.Act("succeeded", succeeded);

        diagnostics.Assert("succeeded", true, succeeded);
        diagnostics.Assert("tag is not all zero", true, !tag.All(value => value == 0));
        Assert.IsTrue(succeeded);
        Assert.IsFalse(tag.All(value => value == 0));
    }

    [TestMethod]
    public void Constructor_KeyIsNot32Bytes_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", AeadChaCha20Poly1305.KeySize - 1);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => new AeadChaCha20Poly1305(new byte[AeadChaCha20Poly1305.KeySize - 1]));
        diagnostics.Act("exception ParamName", exception.ParamName);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(8, 16, 16, 16)]
    [DataRow(12, 16, 15, 16)]
    [DataRow(12, 16, 16, 12)]
    public void Encrypt_WrongLength_Throws(int nonceLength, int plaintextLength, int ciphertextLength, int tagLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AeadChaCha20Poly1305 aead = new(new byte[AeadChaCha20Poly1305.KeySize]);
        diagnostics.Arrange("nonce, plaintext, ciphertext and tag lengths", $"{nonceLength}, {plaintextLength}, {ciphertextLength}, {tagLength}");

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => aead.Encrypt(new byte[nonceLength], new byte[plaintextLength], new byte[ciphertextLength], new byte[tagLength]));
        diagnostics.Act("exception ParamName", exception.ParamName);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(8, 16, 16, 16)]
    [DataRow(12, 16, 17, 16)]
    [DataRow(12, 16, 16, 17)]
    public void TryDecrypt_WrongLength_Throws(int nonceLength, int ciphertextLength, int plaintextLength, int tagLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AeadChaCha20Poly1305 aead = new(new byte[AeadChaCha20Poly1305.KeySize]);
        diagnostics.Arrange("nonce, ciphertext, plaintext and tag lengths", $"{nonceLength}, {ciphertextLength}, {plaintextLength}, {tagLength}");

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => aead.TryDecrypt(new byte[nonceLength], new byte[ciphertextLength], new byte[tagLength], new byte[plaintextLength]));
        diagnostics.Act("exception ParamName", exception.ParamName);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void Encrypt_AfterDispose_ThrowsObjectDisposedException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        AeadChaCha20Poly1305 aead = new(new byte[AeadChaCha20Poly1305.KeySize]);
        aead.Dispose();
        diagnostics.Arrange("state", "disposed instance");

        var exception = Assert.ThrowsExactly<ObjectDisposedException>(
            () => aead.Encrypt(new byte[AeadChaCha20Poly1305.NonceSize], [], [], new byte[AeadChaCha20Poly1305.TagSize]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception type", nameof(ObjectDisposedException), exception.GetType().Name);
    }

    [TestMethod]
    public void TryDecrypt_AfterDispose_ThrowsObjectDisposedException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        AeadChaCha20Poly1305 aead = new(new byte[AeadChaCha20Poly1305.KeySize]);
        aead.Dispose();
        diagnostics.Arrange("state", "disposed instance");

        var exception = Assert.ThrowsExactly<ObjectDisposedException>(
            () => aead.TryDecrypt(new byte[AeadChaCha20Poly1305.NonceSize], [], new byte[AeadChaCha20Poly1305.TagSize], []));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception type", nameof(ObjectDisposedException), exception.GetType().Name);
    }

    private static bool TryDecryptDraft(byte[] ciphertext, byte[] tag, byte[] associatedData, byte[] plaintext)
    {
        using AeadChaCha20Poly1305 aead = new(Convert.FromHexString(DraftKey));
        return aead.TryDecrypt(Convert.FromHexString(DraftNonce), ciphertext, tag, plaintext, associatedData);
    }
}
