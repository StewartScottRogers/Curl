using Curl.Cryptography;

namespace Curl.Extension.ObliviousDoh.Wire;

[TestClass]
public sealed class ObliviousDohWireTests
{
    private static readonly byte[] P256Key = [0x04, .. new byte[64]];

    [TestMethod]
    public void Message_Encode_LaysOutTypeKeyIdAndEncryptedMessageAsRfc9230Section6_1() =>
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x02, 0xAA, 0xBB, 0x00, 0x01, 0xCC }, new ObliviousDohMessage(ObliviousDohMessageType.Query, [0xAA, 0xBB], [0xCC]).Encode());

    [TestMethod]
    public void Message_Decode_ReadsEachField()
    {
        ObliviousDohMessage message = ObliviousDohMessage.Decode([0x02, 0x00, 0x01, 0xAA, 0x00, 0x02, 0xCC, 0xDD])!;

        Assert.AreEqual(ObliviousDohMessageType.Response, message.Type);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, message.KeyId);
        CollectionAssert.AreEqual(new byte[] { 0xCC, 0xDD }, message.EncryptedMessage);
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x02 })]
    [DataRow(new byte[] { 0x02, 0x00 })]
    [DataRow(new byte[] { 0x02, 0x00, 0x02, 0xAA })]
    [DataRow(new byte[] { 0x02, 0x00, 0x00 })]
    [DataRow(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x01, 0xCC, 0xFF })]
    public void Message_Decode_ShortOrOverlong_GivesNull(byte[] bytes) => Assert.IsNull(ObliviousDohMessage.Decode(bytes));

    [TestMethod]
    public void Plaintext_Encode_PadsWithZerosToA128OctetBlockAsRfc8467Section4_1()
    {
        byte[] plaintext = ObliviousDohPlaintext.Encode([1, 2, 3]);

        Assert.HasCount(128, plaintext);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x03, 1, 2, 3, 0x00, 0x79 }, plaintext[..7]);
        Assert.IsFalse(plaintext.AsSpan(7).ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Plaintext_Encode_AlreadyAWholeBlock_AddsNoPadding() =>
        Assert.HasCount(128, ObliviousDohPlaintext.Encode(new byte[124]));

    [TestMethod]
    public void Plaintext_DecodeDnsMessage_ReadsTheMessageBeforeThePadding() =>
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, ObliviousDohPlaintext.DecodeDnsMessage(ObliviousDohPlaintext.Encode([1, 2, 3])));

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x01, 0x01, 0x00, 0x01, 0x01 })]
    [DataRow(new byte[] { 0x00, 0x01, 0x01, 0x00 })]
    [DataRow(new byte[] { 0x00, 0x05, 0x01 })]
    [DataRow(new byte[] { 0x00, 0x01, 0x01, 0x00, 0x00, 0x00 })]
    public void Plaintext_DecodeDnsMessage_NonZeroPaddingOrMalformed_GivesNull(byte[] plaintext) =>
        Assert.IsNull(ObliviousDohPlaintext.DecodeDnsMessage(plaintext));

    [TestMethod]
    public void Config_EncodeContents_LaysOutTheSuiteAndKeyAsRfc9230Section5()
    {
        byte[] contents = new ObliviousDohConfig(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, [0xAB]).EncodeContents();

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x20, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0xAB }, contents);
    }

    [TestMethod]
    public void Config_KeyId_IsNhOctetsAndFollowsTheKey()
    {
        byte[] keyId = new ObliviousDohConfig(HpkeKem.DhkemP256HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, P256Key).KeyId();

        Assert.HasCount(32, keyId);
        CollectionAssert.AreNotEqual(keyId, new ObliviousDohConfig(HpkeKem.DhkemP256HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes256Gcm, P256Key).KeyId());
    }

    [TestMethod]
    public void Config_SelectFrom_SkipsAnUnknownVersionAndTakesTheFirstUsableEntry()
    {
        byte[] unknownVersion = [0x00, 0x02, .. TestTarget.Vector([0xFF])];
        byte[] usable = [0x00, 0x01, .. TestTarget.Vector(Contents(0x0010, 0x0001, 0x0002, P256Key))];
        byte[] later = [0x00, 0x01, .. TestTarget.Vector(Contents(0x0010, 0x0001, 0x0001, P256Key))];

        ObliviousDohConfig config = ObliviousDohConfig.SelectFrom(TestTarget.Vector([.. unknownVersion, .. usable, .. later]))!;

        Assert.AreEqual(HpkeAead.Aes256Gcm, config.Aead);
        CollectionAssert.AreEqual(P256Key, config.PublicKey);
    }

    [TestMethod]
    [DataRow((ushort)0x0099, (ushort)0x0001, (ushort)0x0001, 65)]
    [DataRow((ushort)0x0010, (ushort)0x0002, (ushort)0x0001, 65)]
    [DataRow((ushort)0x0010, (ushort)0x0001, (ushort)0x0099, 65)]
    [DataRow((ushort)0x0010, (ushort)0x0001, (ushort)0x0001, 32)]
    public void Config_SelectFrom_OnlyUnusableSuites_GivesNull(ushort kem, ushort kdf, ushort aead, int publicKeyLength)
    {
        byte[] entry = [0x00, 0x01, .. TestTarget.Vector(Contents(kem, kdf, aead, new byte[publicKeyLength]))];

        Assert.IsNull(ObliviousDohConfig.SelectFrom(TestTarget.Vector(entry)));
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x00, 0x00, 0xFF })]
    [DataRow(new byte[] { 0x00, 0x01, 0x00 })]
    [DataRow(new byte[] { 0x00, 0x04, 0x00, 0x01, 0x00, 0x05 })]
    [DataRow(new byte[] { 0x00, 0x05, 0x00, 0x01, 0x00, 0x01, 0x00 })]
    public void Config_SelectFrom_MalformedList_GivesNull(byte[] configs) => Assert.IsNull(ObliviousDohConfig.SelectFrom(configs));

    [TestMethod]
    public void Config_SelectFrom_ContentsWithATrailingByte_GivesNull()
    {
        byte[] entry = [0x00, 0x01, .. TestTarget.Vector([.. Contents(0x0010, 0x0001, 0x0001, P256Key), 0x00])];

        Assert.IsNull(ObliviousDohConfig.SelectFrom(TestTarget.Vector(entry)));
    }

    [TestMethod]
    public void LengthPrefix_Prepend_OverTheVectorLimit_Throws() =>
        Assert.ThrowsExactly<OverflowException>(() => LengthPrefix.Prepend(new byte[65536]));

    private static byte[] Contents(ushort kem, ushort kdf, ushort aead, byte[] publicKey) =>
        [(byte)(kem >> 8), (byte)kem, (byte)(kdf >> 8), (byte)kdf, (byte)(aead >> 8), (byte)aead, .. TestTarget.Vector(publicKey)];
}
