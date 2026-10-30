using Curl.Extension.WireGuard.Messages;
using static Curl.Extension.WireGuard.UnitTests.Messages.HandshakeInitiationMessageTests;

namespace Curl.Extension.WireGuard.UnitTests.Messages;

[TestClass]
public sealed class CookieReplyMessageTests
{
    private static readonly CookieReplyMessage Sample = new(0x04030201, Filled(24, 0x10), Filled(32, 0x30));

    [TestMethod]
    public void Encode_Sample_LaysOutTheFieldsOfSection547()
    {
        var bytes = Sample.Encode();

        Assert.HasCount(64, bytes);
        CollectionAssert.AreEqual(new byte[] { 3, 0, 0, 0, 1, 2, 3, 4 }, bytes[..8]);
        CollectionAssert.AreEqual(Filled(24, 0x10), bytes[8..32]);
        CollectionAssert.AreEqual(Filled(32, 0x30), bytes[32..64]);
    }

    [TestMethod]
    public void TryDecode_EncodedSample_GivesEveryFieldBack()
    {
        Assert.IsTrue(CookieReplyMessage.TryDecode(Sample.Encode(), out var decoded));

        Assert.AreEqual(Sample.ReceiverIndex, decoded.ReceiverIndex);
        CollectionAssert.AreEqual(Sample.Nonce, decoded.Nonce);
        CollectionAssert.AreEqual(Sample.EncryptedCookie, decoded.EncryptedCookie);
    }

    [TestMethod]
    public void TryDecode_WrongLengthOrType_IsFalse()
    {
        var bytes = Sample.Encode();

        Assert.IsFalse(CookieReplyMessage.TryDecode(bytes[..63], out var shorter));
        Assert.IsNull(shorter);
        bytes[0] = 4;
        Assert.IsFalse(CookieReplyMessage.TryDecode(bytes, out _));
    }
}
