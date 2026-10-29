namespace Curl.HttpDatagram.UnitTests;

[TestClass]
public sealed class DatagramCapsuleReaderTests
{
    [TestMethod]
    public void TryReadDatagram_UnknownCapsuleBeforeDatagram_SkipsItSilently()
    {
        var reader = new DatagramCapsuleReader();
        reader.Append([0x52, 0x34, 0x02, 0xEE, 0xEE, 0x00, 0x01, 0x07]);

        var read = reader.TryReadDatagram(out var payload);

        Assert.IsTrue(read);
        CollectionAssert.AreEqual(new byte[] { 0x07 }, payload.ToArray());
        Assert.IsFalse(reader.TryReadDatagram(out _));
        reader.Complete();
    }

    [TestMethod]
    public void TryReadDatagram_OnlyUnknownCapsules_ReturnsFalseAndConsumesThem()
    {
        var reader = new DatagramCapsuleReader();
        reader.Append([0x17, 0x00, 0x29, 0x01, 0xFF]);

        var read = reader.TryReadDatagram(out var payload);

        Assert.IsFalse(read);
        Assert.IsTrue(payload.IsEmpty);
        reader.Complete();
    }

    [TestMethod]
    public void TryReadDatagram_CapsuleSplitAcrossAppends_ReturnsItOnceWhole()
    {
        var reader = new DatagramCapsuleReader();
        reader.Append([0x00, 0x03, 0x01]);
        var readEarly = reader.TryReadDatagram(out _);
        reader.Append([0x02, 0x03]);

        var read = reader.TryReadDatagram(out var payload);

        Assert.IsFalse(readEarly);
        Assert.IsTrue(read);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, payload.ToArray());
    }

    [TestMethod]
    public void Complete_StreamEndsInsideCapsule_ThrowsMalformedCapsule()
    {
        var reader = new DatagramCapsuleReader();
        reader.Append([0x00, 0x05, 0x01]);
        reader.TryReadDatagram(out _);

        var exception = Assert.ThrowsExactly<HttpDatagramException>(reader.Complete);

        Assert.AreEqual(HttpDatagramErrorCode.MalformedCapsule, exception.ErrorCode);
    }
}
