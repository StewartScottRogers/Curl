namespace Curl.HttpDatagram.UnitTests;

[TestClass]
public sealed class CapsuleCodecTests
{
    [TestMethod]
    public void Encode_DatagramCapsule_WritesTypeLengthAndValue()
    {
        var capsule = new Capsule(CapsuleType.Datagram, new byte[] { 0xAA, 0xBB, 0xCC });

        var bytes = CapsuleCodec.Encode(capsule);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x03, 0xAA, 0xBB, 0xCC }, bytes);
    }

    [TestMethod]
    public void Encode_TwoByteTypeAndTwoByteLength_WritesVariableLengthIntegers()
    {
        var value = new byte[100];
        var capsule = new Capsule(0x1234, value);

        var bytes = CapsuleCodec.Encode(capsule);

        CollectionAssert.AreEqual(new byte[] { 0x52, 0x34, 0x40, 0x64 }, bytes[..4]);
        Assert.HasCount(104, bytes);
    }

    [TestMethod]
    public void Encode_NullCapsule_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CapsuleCodec.Encode(null!));
    }

    [TestMethod]
    public void TryDecode_WholeCapsuleFollowedByMore_ReturnsCapsuleAndItsLength()
    {
        byte[] source = [0x00, 0x02, 0x01, 0x02, 0x00];

        var decoded = CapsuleCodec.TryDecode(source, out var capsule, out var consumed);

        Assert.IsTrue(decoded);
        Assert.AreEqual(CapsuleType.Datagram, capsule!.Type);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, capsule.Value.ToArray());
        Assert.AreEqual(4, consumed);
    }

    [TestMethod]
    public void TryDecode_EncodedCapsule_RoundTrips()
    {
        var original = new Capsule(0x2A3B4C5D, new byte[] { 9, 8, 7 });

        var decoded = CapsuleCodec.TryDecode(CapsuleCodec.Encode(original), out var capsule, out _);

        Assert.IsTrue(decoded);
        Assert.AreEqual(original.Type, capsule!.Type);
        CollectionAssert.AreEqual(original.Value.ToArray(), capsule.Value.ToArray());
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x00 })]
    [DataRow(new byte[] { 0x00, 0x40 })]
    [DataRow(new byte[] { 0x00, 0x03, 0x01, 0x02 })]
    [DataRow(new byte[] { 0x00, 0xC0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })]
    public void TryDecode_IncompleteCapsule_ReturnsFalse(byte[] source)
    {
        var decoded = CapsuleCodec.TryDecode(source, out var capsule, out var consumed);

        Assert.IsFalse(decoded);
        Assert.IsNull(capsule);
        Assert.AreEqual(0, consumed);
    }
}
