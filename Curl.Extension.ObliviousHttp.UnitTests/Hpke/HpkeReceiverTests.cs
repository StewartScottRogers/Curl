namespace Curl.Extension.ObliviousHttp.Hpke;

[TestClass]
public sealed class HpkeReceiverTests
{
    [TestMethod]
    [DynamicData(nameof(HpkeTestVectors.All), typeof(HpkeTestVectors))]
    public void Open_VectorCiphertextsInSequence_GivesThePlaintext(HpkeTestVector vector)
    {
        using HpkeReceiver receiver = HpkeTestVectors.SetUpReceiver(vector);

        byte[] plaintext0 = receiver.Open(HpkeTestVectors.AssociatedData0, Convert.FromHexString(vector.Ciphertext0));
        byte[] plaintext1 = receiver.Open(HpkeTestVectors.AssociatedData1, Convert.FromHexString(vector.Ciphertext1));

        CollectionAssert.AreEqual(HpkeTestVectors.Plaintext, plaintext0);
        CollectionAssert.AreEqual(HpkeTestVectors.Plaintext, plaintext1);
    }

    [TestMethod]
    [DynamicData(nameof(HpkeTestVectors.All), typeof(HpkeTestVectors))]
    public void Export_EmptyContext_GivesTheVectorsExportedValue(HpkeTestVector vector)
    {
        using HpkeReceiver receiver = HpkeTestVectors.SetUpReceiver(vector);

        byte[] exportedValue = receiver.Export([], 32);

        Assert.AreEqual(vector.ExportedValue, Convert.ToHexStringLower(exportedValue));
    }

    [TestMethod]
    [DynamicData(nameof(HpkeTestVectors.All), typeof(HpkeTestVectors))]
    public void Open_TamperedCiphertext_ThrowsHpkeOpenException(HpkeTestVector vector)
    {
        using HpkeReceiver receiver = HpkeTestVectors.SetUpReceiver(vector);
        byte[] ciphertext = Convert.FromHexString(vector.Ciphertext0);
        ciphertext[0] ^= 0x01;

        Assert.ThrowsExactly<HpkeOpenException>(() => receiver.Open(HpkeTestVectors.AssociatedData0, ciphertext));
    }

    [TestMethod]
    public void Open_TamperedCiphertext_LeavesTheSequenceNumberSoTheNextGenuineOneOpens()
    {
        HpkeTestVector vector = HpkeTestVectors.X25519Aes128Gcm;
        using HpkeReceiver receiver = HpkeTestVectors.SetUpReceiver(vector);
        byte[] tampered = Convert.FromHexString(vector.Ciphertext0);
        tampered[^1] ^= 0x80;
        Assert.ThrowsExactly<HpkeOpenException>(() => receiver.Open(HpkeTestVectors.AssociatedData0, tampered));

        byte[] plaintext = receiver.Open(HpkeTestVectors.AssociatedData0, Convert.FromHexString(vector.Ciphertext0));

        CollectionAssert.AreEqual(HpkeTestVectors.Plaintext, plaintext);
    }

    [TestMethod]
    [DynamicData(nameof(HpkeTestVectors.All), typeof(HpkeTestVectors))]
    public void Open_WrongRecipientPrivateKey_ThrowsHpkeOpenException(HpkeTestVector vector)
    {
        byte[] wrongPrivateKey = Convert.FromHexString(vector.RecipientPrivateKey);
        wrongPrivateKey[0] ^= 0x40;
        using HpkeReceiver receiver = HpkeReceiver.SetUp(vector.Suite, Convert.FromHexString(vector.EncapsulatedKey), wrongPrivateKey, HpkeTestVectors.Info);

        Assert.ThrowsExactly<HpkeOpenException>(() => receiver.Open(HpkeTestVectors.AssociatedData0, Convert.FromHexString(vector.Ciphertext0)));
    }

    [TestMethod]
    public void Open_WrongAssociatedData_ThrowsHpkeOpenException()
    {
        HpkeTestVector vector = HpkeTestVectors.X25519ChaCha20Poly1305;
        using HpkeReceiver receiver = HpkeTestVectors.SetUpReceiver(vector);

        Assert.ThrowsExactly<HpkeOpenException>(() => receiver.Open(HpkeTestVectors.AssociatedData1, Convert.FromHexString(vector.Ciphertext0)));
    }

    [TestMethod]
    public void Open_CiphertextShorterThanATag_ThrowsHpkeOpenException()
    {
        using HpkeReceiver receiver = HpkeTestVectors.SetUpReceiver(HpkeTestVectors.X25519Aes128Gcm);

        Assert.ThrowsExactly<HpkeOpenException>(() => receiver.Open([], new byte[15]));
    }

    [TestMethod]
    public void SetUp_ShortEncapsulatedKey_ThrowsHpkeSetupException()
    {
        HpkeTestVector vector = HpkeTestVectors.X25519Aes128Gcm;

        Assert.ThrowsExactly<HpkeSetupException>(() => HpkeReceiver.SetUp(vector.Suite, new byte[31], Convert.FromHexString(vector.RecipientPrivateKey), HpkeTestVectors.Info));
    }

    [TestMethod]
    public void Export_AfterDispose_ThrowsObjectDisposedException()
    {
        HpkeReceiver receiver = HpkeTestVectors.SetUpReceiver(HpkeTestVectors.X25519Aes128Gcm);

        receiver.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => receiver.Export([], 32));
    }
}
