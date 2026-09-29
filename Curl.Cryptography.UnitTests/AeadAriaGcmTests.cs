namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="AeadAriaGcm" /> to RFC 8269 Appendix A.2's ARIA-GCM vectors (SRTP with
/// an all-zero salt, so the nonce is the published initialization vector), checks a
/// flipped bit is the <c>false</c> of ADR-0118 with no plaintext written, and checks its
/// argument and disposal rules.
/// </summary>
[TestClass]
public sealed class AeadAriaGcmTests
{
    // RFC 8269 Appendix A.2, the values common to both key sizes.
    private const string Nonce = "000020E8F5EB00000000315E";
    private const string AssociatedData = "8008315EBF2E6FE020E8F5EB";

    private const string Plaintext =
        "F57AF5FD4AE19562976EC57A5A7AD55A5AF5C5E5C5FDF5C55AD57A4A7272D57262E9729566ED66E97AC54A4A5A7AD5E1" +
        "5AE5FDD5FD5AC5D56AE56AD5C572D54AE54AC55A956AFD6AED5A4AC562957A9516991691D572FD14E97AE962ED7A9F4A" +
        "955AF572E162F57A956666E17AE1F54A95F566D54A66E16E4AFD6A9F7AE1C5C55AE5D56AFDE916C5E94A6EC56695E14A" +
        "FDE1148416E94AD57AC5146ED59D1CC5";

    // RFC 8269 Appendix A.2.1, SRTP_AEAD_ARIA_128_GCM: the encrypted payload is the
    // ciphertext, then the 16-byte tag.
    private const string Key128 = "E91E5E75DA65554A48181F3846349562";

    private const string Sealed128 =
        "4D8A9A0675550C704B17D8C9DDC81A5CD6F7DA34F2FE1B3DB7CB3DFB9697102EA0F3C1FC2DBC873D44BCEEAE8E444297" +
        "4BA21FF6789D3272613FB9631A7CF3F14BACBEB421633A90FFBE58C2FA6BDCA534F10D0DE0502CE1D531B6336E588782" +
        "78531E5C22BC6C85BBD784D78D9E680AA19031AAF89101D669D7A3965C1F7E16229D7463E0535F4E253F5D18187D40B8" +
        "AE0F564BD970B5E7E2ADFB211E89A9535ABACE3F37F5A736F4BE984BBFFBEDC1";

    // RFC 8269 Appendix A.2.2, SRTP_AEAD_ARIA_256_GCM.
    private const string Key256 = "0C5FFD37A11EDC42C325287FC0604F2E3E8CD5671A00FE3216AA5EB105783B54";

    private const string Sealed256 =
        "6F9E4BCBC8C85FC0128FB1E4A0A20CB9932FF74581F54FC013DD054B19F99371425B352D97D3F337B90B63D1B082ADEE" +
        "EA9D2D7391897D591B985E55FB50CB5350CF7D38DC27DDA127C078A149C8EB98083D66363A46E3726AF217D3A00275AD" +
        "5BF772C7610EA4C23006878F0EE69A8397703169A419303F40B72E4573714D19E2697DF61E7C7252E5ABC6BADE876AC4" +
        "961BFAC4D5E867AFCA351A48AED52822E210D6CED2CF430FF841472915E7EF48";

    [TestMethod]
    [DataRow(Key128, Sealed128)]
    [DataRow(Key256, Sealed256)]
    public void Encrypt_Rfc8269AppendixA2Vector_GivesThePublishedCiphertextAndTag(string key, string sealedPayload)
    {
        using AeadAriaGcm aead = new(Convert.FromHexString(key));
        byte[] ciphertext = new byte[Plaintext.Length / 2];
        byte[] tag = new byte[AeadAriaGcm.TagSize];

        aead.Encrypt(Convert.FromHexString(Nonce), Convert.FromHexString(Plaintext), ciphertext, tag, Convert.FromHexString(AssociatedData));

        Assert.AreEqual(sealedPayload, Convert.ToHexString(ciphertext) + Convert.ToHexString(tag));
    }

    [TestMethod]
    [DataRow(Key128, Sealed128)]
    [DataRow(Key256, Sealed256)]
    public void TryDecrypt_Rfc8269AppendixA2Vector_GivesThePublishedPlaintext(string key, string sealedPayload)
    {
        byte[] sealedBytes = Convert.FromHexString(sealedPayload);
        byte[] plaintext = new byte[sealedBytes.Length - AeadAriaGcm.TagSize];

        bool succeeded = TryDecrypt(key, sealedBytes, Convert.FromHexString(AssociatedData), plaintext);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(Plaintext, Convert.ToHexString(plaintext));
    }

    // RFC 8269 Appendix A.2.1 with one bit flipped in the tag, the ciphertext or the
    // associated data: the tag fails, and the plaintext buffer is left all zero.
    [TestMethod]
    [DataRow("sealed", 0)]
    [DataRow("sealed", 159)]
    [DataRow("sealed", 160)]
    [DataRow("sealed", 175)]
    [DataRow("associatedData", 11)]
    public void TryDecrypt_Rfc8269VectorWithAFlippedBit_ReturnsFalseAndWritesNoPlaintext(string flipped, int index)
    {
        Dictionary<string, byte[]> inputs = new()
        {
            ["sealed"] = Convert.FromHexString(Sealed128),
            ["associatedData"] = Convert.FromHexString(AssociatedData),
        };
        inputs[flipped][index] ^= 0x01;
        byte[] plaintext = new byte[(Sealed128.Length / 2) - AeadAriaGcm.TagSize];
        Array.Fill(plaintext, (byte)0xAA);

        bool succeeded = TryDecrypt(Key128, inputs["sealed"], inputs["associatedData"], plaintext);

        Assert.IsFalse(succeeded);
        Assert.IsTrue(plaintext.All(value => value == 0));
    }

    [TestMethod]
    public void EncryptThenTryDecrypt_EmptyPlaintextAndNoAssociatedData_RoundTrips()
    {
        using AeadAriaGcm aead = new(Convert.FromHexString(Key128));
        byte[] nonce = Convert.FromHexString(Nonce);
        byte[] tag = new byte[AeadAriaGcm.TagSize];

        aead.Encrypt(nonce, [], [], tag);
        bool succeeded = aead.TryDecrypt(nonce, [], tag, []);

        Assert.IsTrue(succeeded);
        Assert.IsFalse(tag.All(value => value == 0));
    }

    [TestMethod]
    [DataRow(15)]
    [DataRow(33)]
    public void Constructor_KeyNotSixteenTwentyFourOrThirtyTwoBytes_Throws(int keyLength)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new AeadAriaGcm(new byte[keyLength]));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(11, 4, 4, 16, "nonce")]
    [DataRow(12, 4, 5, 16, "destination")]
    [DataRow(12, 4, 4, 15, "tag")]
    public void EncryptAndTryDecrypt_WrongLength_Throws(int nonceLength, int sourceLength, int destinationLength, int tagLength, string parameterName)
    {
        using AeadAriaGcm aead = new(new byte[16]);

        ArgumentException encrypt = Assert.ThrowsExactly<ArgumentException>(
            () => aead.Encrypt(new byte[nonceLength], new byte[sourceLength], new byte[destinationLength], new byte[tagLength]));
        ArgumentException decrypt = Assert.ThrowsExactly<ArgumentException>(
            () => aead.TryDecrypt(new byte[nonceLength], new byte[sourceLength], new byte[tagLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, encrypt.ParamName);
        Assert.AreEqual(parameterName, decrypt.ParamName);
    }

    [TestMethod]
    public void Dispose_LaterCallsThrow()
    {
        AeadAriaGcm aead = new(new byte[32]);

        aead.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => aead.Encrypt(new byte[12], [], [], new byte[16]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => aead.TryDecrypt(new byte[12], [], new byte[16], []));
    }

    private static bool TryDecrypt(string key, byte[] sealedBytes, byte[] associatedData, byte[] plaintext)
    {
        using AeadAriaGcm aead = new(Convert.FromHexString(key));
        int ciphertextLength = sealedBytes.Length - AeadAriaGcm.TagSize;
        return aead.TryDecrypt(Convert.FromHexString(Nonce), sealedBytes.AsSpan(0, ciphertextLength), sealedBytes.AsSpan(ciphertextLength), plaintext, associatedData);
    }
}
