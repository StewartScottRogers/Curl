using Curl.Extension.WireGuard.Messages;

namespace Curl.Extension.WireGuard.UnitTests.Messages;

[TestClass]
public sealed class HandshakeInitiationMessageTests
{
    private static readonly HandshakeInitiationMessage Sample = new(
        0x04030201,
        Filled(32, 0x10),
        Filled(48, 0x20),
        Filled(28, 0x30),
        Filled(16, 0x40),
        Filled(16, 0x50));

    [TestMethod]
    public void Encode_Sample_LaysOutTheFieldsOfSection542()
    {
        var bytes = Sample.Encode();

        Assert.HasCount(148, bytes);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0, 1, 2, 3, 4 }, bytes[..8]);
        CollectionAssert.AreEqual(Filled(32, 0x10), bytes[8..40]);
        CollectionAssert.AreEqual(Filled(48, 0x20), bytes[40..88]);
        CollectionAssert.AreEqual(Filled(28, 0x30), bytes[88..116]);
        CollectionAssert.AreEqual(Filled(16, 0x40), bytes[116..132]);
        CollectionAssert.AreEqual(Filled(16, 0x50), bytes[132..148]);
    }

    [TestMethod]
    public void TryDecode_EncodedSample_GivesEveryFieldBack()
    {
        Assert.IsTrue(HandshakeInitiationMessage.TryDecode(Sample.Encode(), out var decoded));

        Assert.AreEqual(Sample.SenderIndex, decoded.SenderIndex);
        CollectionAssert.AreEqual(Sample.UnencryptedEphemeral, decoded.UnencryptedEphemeral);
        CollectionAssert.AreEqual(Sample.EncryptedStatic, decoded.EncryptedStatic);
        CollectionAssert.AreEqual(Sample.EncryptedTimestamp, decoded.EncryptedTimestamp);
        CollectionAssert.AreEqual(Sample.Mac1, decoded.Mac1);
        CollectionAssert.AreEqual(Sample.Mac2, decoded.Mac2);
    }

    [TestMethod]
    public void TryDecode_WrongLengthOrType_IsFalse()
    {
        var bytes = Sample.Encode();

        Assert.IsFalse(HandshakeInitiationMessage.TryDecode(bytes[..147], out var shorter));
        Assert.IsNull(shorter);
        bytes[0] = 2;
        Assert.IsFalse(HandshakeInitiationMessage.TryDecode(bytes, out _));
    }

    [TestMethod]
    public void Encode_FieldOfTheWrongLength_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => (Sample with { EncryptedStatic = new byte[32] }).Encode());

        Assert.AreEqual("EncryptedStatic", exception.ParamName);
    }

    internal static byte[] Filled(int length, byte first) =>
        [.. Enumerable.Range(0, length).Select(offset => (byte)(first + offset))];
}
