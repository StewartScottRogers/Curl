using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.UnitTests.Wire;

[TestClass]
public sealed class Amqp0FrameTests
{
    private const string TuneOkFrame = "01" + "0000" + "0000000C" + "000A001F07FF00020000003C" + "CE";

    [TestMethod]
    public void ProtocolHeader_IsAmqpZeroThenVersion091()
    {
        Assert.AreEqual("414D515000000901", Convert.ToHexString(Amqp0ProtocolHeader.Bytes));
        Assert.AreEqual(Amqp0ProtocolHeader.Length, Amqp0ProtocolHeader.Bytes.Length);
        Amqp0ProtocolHeader.Verify(Convert.FromHexString("414D515000000901"));
    }

    [TestMethod]
    public void ProtocolHeader_OtherVersion_IsFrameError()
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0ProtocolHeader.Verify(Convert.FromHexString("414D515001010009")));

        Assert.AreEqual(Amqp0ReplyCode.FrameError, exception.ReplyCode);
    }

    [TestMethod]
    public void ToBytes_WritesTypeChannelSizePayloadAndFrameEnd()
    {
        var frame = new Amqp0Frame(Amqp0FrameType.Method, 0, Convert.FromHexString("000A001F07FF00020000003C"));

        Assert.AreEqual(TuneOkFrame, Convert.ToHexString(frame.ToBytes()));
        Assert.AreEqual("08000000000000CE", Convert.ToHexString(Amqp0Frame.Heartbeat.ToBytes()));
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(4096u)]
    public void TryRead_WholeFrame_ReadsItAndConsumesIt(uint frameMax)
    {
        byte[] bytes = Convert.FromHexString(TuneOkFrame + "08");

        Assert.IsTrue(Amqp0Frame.TryRead(bytes, frameMax, out Amqp0Frame? frame, out int consumed));

        Assert.AreEqual(20, consumed);
        Assert.AreEqual(Amqp0FrameType.Method, frame!.Type);
        Assert.AreEqual((ushort)0, frame.Channel);
        Assert.AreEqual("000A001F07FF00020000003C", Convert.ToHexString(frame.Payload));
    }

    [TestMethod]
    [DataRow("010000")]
    [DataRow("01000000000003AABB")]
    public void TryRead_PartialFrame_ReturnsFalse(string hex)
    {
        Assert.IsFalse(Amqp0Frame.TryRead(Convert.FromHexString(hex), 0, out Amqp0Frame? frame, out int consumed));

        Assert.IsNull(frame);
        Assert.AreEqual(0, consumed);
    }

    [TestMethod]
    [DataRow("0300010000000001AA00", 0u)]
    [DataRow("0900010000000000CE", 0u)]
    [DataRow("03000100000FF9", 4096u)]
    [DataRow("0300010000000001", 4u)]
    [DataRow("0800010000000000CE", 0u)]
    public void TryRead_MalformedFrame_IsFrameError(string hex, uint frameMax)
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0Frame.TryRead(Convert.FromHexString(hex), frameMax, out _, out _));

        Assert.AreEqual(Amqp0ReplyCode.FrameError, exception.ReplyCode);
    }
}
