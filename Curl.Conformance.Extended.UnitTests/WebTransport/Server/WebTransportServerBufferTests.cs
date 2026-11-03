using Curl.Conformance.Extended.WebTransport.Server;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Server;

[TestClass]
public sealed class WebTransportServerBufferTests
{
    [TestMethod]
    [DataRow(5L, 1)]
    [DataRow(300L, 2)]
    [DataRow(70000L, 4)]
    [DataRow(1L << 40, 8)]
    public void WriteVariableLengthInteger_Value_RoundTripsInTheShortestEncoding(long value, int length)
    {
        List<byte> output = [];

        WebTransportServerBuffer.WriteVariableLengthInteger(output, value);
        var buffer = new WebTransportServerBuffer();
        buffer.Append([.. output]);

        Assert.HasCount(length, output);
        Assert.IsTrue(buffer.TryReadVariableLengthInteger(out long read));
        Assert.AreEqual(value, read);
    }

    [TestMethod]
    public void TryReadVariableLengthInteger_EmptyOrPartial_ReadsNothing()
    {
        var buffer = new WebTransportServerBuffer();

        Assert.IsFalse(buffer.TryReadVariableLengthInteger(out _));
        buffer.Append([0x40]);
        Assert.IsFalse(buffer.TryReadVariableLengthInteger(out _));
        buffer.Append([0x41]);

        Assert.IsTrue(buffer.TryReadVariableLengthInteger(out long value));
        Assert.AreEqual(0x41, value);
    }

    [TestMethod]
    public void TryReadTypeLengthValue_ValueNotYetComplete_RewindsUntilItIs()
    {
        var buffer = new WebTransportServerBuffer();
        buffer.Append([0x00, 0x03, 0x01]);

        Assert.IsFalse(buffer.TryReadTypeLengthValue(out _, out _));
        Assert.AreEqual(0, buffer.Position);
        buffer.Append([0x02, 0x03]);

        Assert.IsTrue(buffer.TryReadTypeLengthValue(out long type, out byte[] value));
        Assert.AreEqual(0, type);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, value);
    }

    [TestMethod]
    public void TryReadTypeLengthValue_LengthNotYetHere_ReadsNothing()
    {
        var buffer = new WebTransportServerBuffer();
        buffer.Append([0x01]);

        Assert.IsFalse(buffer.TryReadTypeLengthValue(out _, out _));
        CollectionAssert.AreEqual(new byte[] { 0x01 }, buffer.TakeRest());
    }
}
