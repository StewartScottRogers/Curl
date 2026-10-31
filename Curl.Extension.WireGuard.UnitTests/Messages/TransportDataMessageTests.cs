using Curl.Extension.WireGuard.Messages;
using static Curl.Extension.WireGuard.UnitTests.Messages.HandshakeInitiationMessageTests;

namespace Curl.Extension.WireGuard.UnitTests.Messages;

[TestClass]
public sealed class TransportDataMessageTests
{
    private static readonly TransportDataMessage Sample = new(0x04030201, 0x0C0B0A0908070605, Filled(32, 0x20));

    [TestMethod]
    public void Encode_Sample_LaysOutTheFieldsOfSection546()
    {
        var bytes = Sample.Encode();

        Assert.HasCount(48, bytes);
        CollectionAssert.AreEqual(new byte[] { 4, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, bytes[..16]);
        CollectionAssert.AreEqual(Filled(32, 0x20), bytes[16..]);
    }

    [TestMethod]
    public void TryDecode_EncodedSample_GivesEveryFieldBack()
    {
        Assert.IsTrue(TransportDataMessage.TryDecode(Sample.Encode(), out var decoded));

        Assert.AreEqual(Sample.ReceiverIndex, decoded.ReceiverIndex);
        Assert.AreEqual(Sample.Counter, decoded.Counter);
        CollectionAssert.AreEqual(Sample.EncryptedPacket, decoded.EncryptedPacket);
    }

    [TestMethod]
    public void TryDecode_ShorterThanAKeepaliveOrBelowTheHeader_IsFalse()
    {
        var bytes = Sample.Encode();

        Assert.IsFalse(TransportDataMessage.TryDecode(bytes[..31], out var shorter));
        Assert.IsNull(shorter);
        Assert.IsFalse(TransportDataMessage.TryDecode(bytes[..3], out _));
        Assert.IsTrue(TransportDataMessage.TryDecode(bytes[..32], out _));
    }
}
