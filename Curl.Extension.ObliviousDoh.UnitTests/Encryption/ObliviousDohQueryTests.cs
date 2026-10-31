using Curl.Cryptography;
using Curl.Extension.ObliviousDoh.Wire;

namespace Curl.Extension.ObliviousDoh.Encryption;

[TestClass]
public sealed class ObliviousDohQueryTests
{
    private static readonly byte[] DnsQuery = [0x00, 0x00, 0x01, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0, 0x01, (byte)'a', 0x00, 0x00, 0x01, 0x00, 0x01];

    [TestMethod]
    public void Seal_TheTargetOpensTheQueryWithItsKeyAndTheAssociatedDataOfRfc9230Section6_2()
    {
        using var target = new TestTarget();
        using ObliviousDohQuery query = ObliviousDohQuery.Seal(target.Config(), DnsQuery)!;

        target.Respond(query.Message, dns => dns);

        CollectionAssert.AreEqual(ObliviousDohPlaintext.Encode(DnsQuery), target.LastQueryPlaintext);
    }

    [TestMethod]
    public void OpenResponse_ATargetsResponseSealedAsRfc9230Section6_4_GivesTheDnsAnswer()
    {
        using var target = new TestTarget();
        using ObliviousDohQuery query = ObliviousDohQuery.Seal(target.Config(), DnsQuery)!;

        byte[] response = target.Respond(query.Message, TestTarget.AnswerTo);

        CollectionAssert.AreEqual(TestTarget.AnswerTo(DnsQuery), query.OpenResponse(response));
    }

    [TestMethod]
    public void Seal_AFixedEphemeralKey_GivesTheSameMessage()
    {
        using var target = new TestTarget();
        byte[] ephemeral = new byte[32];
        ephemeral[^1] = 7;
        using ObliviousDohQuery first = ObliviousDohQuery.Seal(target.Config(), DnsQuery, ephemeral)!;
        using ObliviousDohQuery second = ObliviousDohQuery.Seal(target.Config(), DnsQuery, ephemeral)!;

        CollectionAssert.AreEqual(first.Message, second.Message);
        CollectionAssert.AreEqual(target.Config().KeyId(), ObliviousDohMessage.Decode(first.Message)!.KeyId);
    }

    [TestMethod]
    public void Seal_APublicKeyOffTheCurve_GivesNull()
    {
        var config = new ObliviousDohConfig(HpkeKem.DhkemP256HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, [0x04, .. new byte[64]]);
        byte[] ephemeral = new byte[32];
        ephemeral[^1] = 7;

        Assert.IsNull(ObliviousDohQuery.Seal(config, DnsQuery));
        Assert.IsNull(ObliviousDohQuery.Seal(config, DnsQuery, ephemeral));
    }

    [TestMethod]
    public void Seal_NullConfig_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ObliviousDohQuery.Seal(null!, DnsQuery));

    [TestMethod]
    public void OpenResponse_ATamperedResponse_GivesNull()
    {
        using var target = new TestTarget();
        using ObliviousDohQuery query = ObliviousDohQuery.Seal(target.Config(), DnsQuery)!;
        byte[] response = target.Respond(query.Message, TestTarget.AnswerTo);
        response[^1] ^= 1;

        Assert.IsNull(query.OpenResponse(response));
    }

    [TestMethod]
    [DataRow(HpkeAead.Aes256Gcm)]
    [DataRow(HpkeAead.ChaCha20Poly1305)]
    public void OpenResponse_UnderAnotherAead_NotSealedWithIt_GivesNull(HpkeAead aead)
    {
        using var target = new TestTarget();
        using ObliviousDohQuery query = ObliviousDohQuery.Seal(target.Config(aead), DnsQuery)!;

        Assert.IsNull(query.OpenResponse(new ObliviousDohMessage(ObliviousDohMessageType.Response, new byte[32], new byte[40]).Encode()));
    }

    [TestMethod]
    public void OpenResponse_NotAResponseOrShorterThanATag_GivesNull()
    {
        using var target = new TestTarget();
        using ObliviousDohQuery query = ObliviousDohQuery.Seal(target.Config(), DnsQuery)!;

        Assert.IsNull(query.OpenResponse(new ObliviousDohMessage(ObliviousDohMessageType.Query, new byte[16], new byte[40]).Encode()));
        Assert.IsNull(query.OpenResponse(new ObliviousDohMessage(ObliviousDohMessageType.Response, new byte[16], new byte[15]).Encode()));
        Assert.IsNull(query.OpenResponse([0x02]));
    }
}
