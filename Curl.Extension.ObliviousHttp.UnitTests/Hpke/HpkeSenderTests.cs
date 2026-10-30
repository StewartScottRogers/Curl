using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Hpke;

[TestClass]
public sealed class HpkeSenderTests
{
    private static readonly HpkeSuite X25519Aes128Gcm = HpkeTestVectors.X25519Aes128Gcm.Suite;

    [TestMethod]
    [DynamicData(nameof(HpkeTestVectors.All), typeof(HpkeTestVectors))]
    public void SetUp_VectorEphemeralKey_GivesTheVectorsEncapsulatedKey(HpkeTestVector vector)
    {
        using HpkeSender sender = HpkeTestVectors.SetUpSender(vector);

        Assert.AreEqual(vector.EncapsulatedKey, Convert.ToHexStringLower(sender.EncapsulatedKey));
    }

    [TestMethod]
    [DynamicData(nameof(HpkeTestVectors.All), typeof(HpkeTestVectors))]
    public void Seal_TwoMessages_GivesTheVectorsCiphertextsInSequence(HpkeTestVector vector)
    {
        using HpkeSender sender = HpkeTestVectors.SetUpSender(vector);

        byte[] ciphertext0 = sender.Seal(HpkeTestVectors.AssociatedData0, HpkeTestVectors.Plaintext);
        byte[] ciphertext1 = sender.Seal(HpkeTestVectors.AssociatedData1, HpkeTestVectors.Plaintext);

        Assert.AreEqual(vector.Ciphertext0, Convert.ToHexStringLower(ciphertext0));
        Assert.AreEqual(vector.Ciphertext1, Convert.ToHexStringLower(ciphertext1));
    }

    [TestMethod]
    [DynamicData(nameof(HpkeTestVectors.All), typeof(HpkeTestVectors))]
    public void Export_EmptyContext_GivesTheVectorsExportedValue(HpkeTestVector vector)
    {
        using HpkeSender sender = HpkeTestVectors.SetUpSender(vector);

        byte[] exportedValue = sender.Export([], 32);

        Assert.AreEqual(vector.ExportedValue, Convert.ToHexStringLower(exportedValue));
    }

    [TestMethod]
    public void Export_NegativeLength_ThrowsArgumentOutOfRangeException()
    {
        using HpkeSender sender = HpkeTestVectors.SetUpSender(HpkeTestVectors.X25519Aes128Gcm);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sender.Export([], -1));
    }

    [TestMethod]
    public void SetUp_FreshEphemeralKey_ReceiverOpensWhatItSeals()
    {
        HpkeTestVector vector = HpkeTestVectors.X25519Aes128Gcm;
        using HpkeSender sender = HpkeSender.SetUp(X25519Aes128Gcm, Convert.FromHexString(vector.RecipientPublicKey), HpkeTestVectors.Info);
        using HpkeReceiver receiver = HpkeReceiver.SetUp(X25519Aes128Gcm, sender.EncapsulatedKey, Convert.FromHexString(vector.RecipientPrivateKey), HpkeTestVectors.Info);

        byte[] plaintext = receiver.Open(HpkeTestVectors.AssociatedData0, sender.Seal(HpkeTestVectors.AssociatedData0, HpkeTestVectors.Plaintext));

        CollectionAssert.AreEqual(HpkeTestVectors.Plaintext, plaintext);
        Assert.AreNotEqual(vector.EncapsulatedKey, Convert.ToHexStringLower(sender.EncapsulatedKey));
    }

    [TestMethod]
    public void SetUp_FreshEphemeralKeyAndShortRecipientPublicKey_ThrowsHpkeSetupException()
    {
        Assert.ThrowsExactly<HpkeSetupException>(() => HpkeSender.SetUp(X25519Aes128Gcm, new byte[31], HpkeTestVectors.Info));
    }

    [TestMethod]
    public void SetUp_GivenEphemeralKeyAndShortRecipientPublicKey_ThrowsHpkeSetupException()
    {
        byte[] ephemeralPrivateKey = Convert.FromHexString(HpkeTestVectors.X25519Aes128Gcm.EphemeralPrivateKey);

        Assert.ThrowsExactly<HpkeSetupException>(() => HpkeSender.SetUp(X25519Aes128Gcm, new byte[31], ephemeralPrivateKey, HpkeTestVectors.Info));
    }

    [TestMethod]
    public void SetUp_UnsupportedAead_ThrowsArgumentException()
    {
        HpkeSuite suite = X25519Aes128Gcm with { Aead = (HpkeAead)0x0004 };

        Assert.ThrowsExactly<ArgumentException>(() => HpkeTestVectors.SetUpSender(HpkeTestVectors.X25519Aes128Gcm with { Suite = suite }));
    }

    [TestMethod]
    public void Seal_AfterDispose_ThrowsObjectDisposedException()
    {
        HpkeSender sender = HpkeTestVectors.SetUpSender(HpkeTestVectors.X25519Aes128Gcm);

        sender.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => sender.Seal([], HpkeTestVectors.Plaintext));
    }
}
