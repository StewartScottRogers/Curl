using Curl.Extension.WireGuard.Messages;
using static Curl.Extension.WireGuard.UnitTests.Messages.HandshakeInitiationMessageTests;

namespace Curl.Extension.WireGuard.UnitTests.Messages;

[TestClass]
public sealed class HandshakeResponseMessageTests
{
    private static readonly HandshakeResponseMessage Sample = new(
        0x04030201,
        0x08070605,
        Filled(32, 0x10),
        Filled(16, 0x20),
        Filled(16, 0x40),
        Filled(16, 0x50));

    [TestMethod]
    public void Encode_Sample_LaysOutTheFieldsOfSection543()
    {
        var bytes = Sample.Encode();

        Assert.HasCount(92, bytes);
        CollectionAssert.AreEqual(new byte[] { 2, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 }, bytes[..12]);
        CollectionAssert.AreEqual(Filled(32, 0x10), bytes[12..44]);
        CollectionAssert.AreEqual(Filled(16, 0x20), bytes[44..60]);
        CollectionAssert.AreEqual(Filled(16, 0x40), bytes[60..76]);
        CollectionAssert.AreEqual(Filled(16, 0x50), bytes[76..92]);
    }

    [TestMethod]
    public void TryDecode_EncodedSample_GivesEveryFieldBack()
    {
        Assert.IsTrue(HandshakeResponseMessage.TryDecode(Sample.Encode(), out var decoded));

        Assert.AreEqual(Sample.SenderIndex, decoded.SenderIndex);
        Assert.AreEqual(Sample.ReceiverIndex, decoded.ReceiverIndex);
        CollectionAssert.AreEqual(Sample.UnencryptedEphemeral, decoded.UnencryptedEphemeral);
        CollectionAssert.AreEqual(Sample.EncryptedNothing, decoded.EncryptedNothing);
        CollectionAssert.AreEqual(Sample.Mac1, decoded.Mac1);
        CollectionAssert.AreEqual(Sample.Mac2, decoded.Mac2);
    }

    [TestMethod]
    public void TryDecode_WrongLengthOrReservedByteSet_IsFalse()
    {
        var bytes = Sample.Encode();

        Assert.IsFalse(HandshakeResponseMessage.TryDecode([.. bytes, 0], out var longer));
        Assert.IsNull(longer);
        bytes[3] = 1;
        Assert.IsFalse(HandshakeResponseMessage.TryDecode(bytes, out _));
    }
}
