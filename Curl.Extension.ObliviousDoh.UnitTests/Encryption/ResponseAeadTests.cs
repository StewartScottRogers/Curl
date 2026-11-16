using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Extension.ObliviousDoh.Encryption;

[TestClass]
public sealed class ResponseAeadTests
{
    private static readonly byte[] Key = new byte[16];
    private static readonly byte[] Nonce = new byte[ResponseAead.NonceSize];
    private static readonly byte[] AssociatedData = [1, 2, 3];
    private static readonly byte[] Text = [10, 20, 30, 40];

    private static byte[] SealWithAes128Gcm()
    {
        byte[] sealedText = new byte[Text.Length];
        byte[] tag = new byte[ResponseAead.TagSize];
        using AesGcm aesGcm = new(Key, ResponseAead.TagSize);
        aesGcm.Encrypt(Nonce, Text, sealedText, tag, AssociatedData);
        return [.. sealedText, .. tag];
    }

    [TestMethod]
    public void TryOpen_AesGcmWithTheRightTag_OpensTheText()
    {
        byte[] opened = new byte[Text.Length];

        bool result = ResponseAead.TryOpen(HpkeAead.Aes128Gcm, Key, Nonce, AssociatedData, SealWithAes128Gcm(), opened);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(Text, opened);
    }

    [TestMethod]
    public void TryOpen_AesGcmWithATamperedTag_RefusesToOpen()
    {
        byte[] ciphertext = SealWithAes128Gcm();
        ciphertext[^1] ^= 1;

        bool result = ResponseAead.TryOpen(HpkeAead.Aes128Gcm, Key, Nonce, AssociatedData, ciphertext, new byte[Text.Length]);

        Assert.IsFalse(result);
    }
}
