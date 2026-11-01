using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.UnitTests.Wire;

[TestClass]
public sealed class Amqp0ContentTests
{
    private const string TextPlainPersistentHeader = "003C" + "0000" + "0000000000000005" + "9000" + "0A746578742F706C61696E" + "02";

    private static readonly Amqp0BasicProperties EveryProperty = new()
    {
        ContentType = "text/plain",
        ContentEncoding = "gzip",
        Headers = new Amqp0FieldTable([new("h", Amqp0FieldValue.Void())]),
        DeliveryMode = 2,
        Priority = 9,
        CorrelationId = "c",
        ReplyTo = "r",
        Expiration = "60000",
        MessageId = "m",
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(1700000000),
        Type = "t",
        UserId = "guest",
        AppId = "curl",
        ClusterId = "x",
    };

    [TestMethod]
    public void ContentHeader_ToPayload_WritesClassWeightSizeFlagsAndProperties()
    {
        var header = new Amqp0ContentHeader(60, 5, new Amqp0BasicProperties { ContentType = "text/plain", DeliveryMode = 2 });

        Assert.AreEqual(TextPlainPersistentHeader, Convert.ToHexString(header.ToPayload()));
    }

    [TestMethod]
    public void ContentHeader_Read_ReadsThePresentProperties()
    {
        Amqp0ContentHeader header = Amqp0ContentHeader.Read(Convert.FromHexString(TextPlainPersistentHeader));

        Assert.AreEqual(new Amqp0ContentHeader(60, 5, new Amqp0BasicProperties { ContentType = "text/plain", DeliveryMode = 2 }), header);
    }

    [TestMethod]
    public void ContentHeader_EveryProperty_SetsEveryFlagAndReadsBack()
    {
        byte[] payload = new Amqp0ContentHeader(60, 0, EveryProperty).ToPayload();

        Assert.AreEqual("FFFC", Convert.ToHexString(payload, 12, 2));
        Amqp0ContentHeader read = Amqp0ContentHeader.Read(payload);
        Assert.AreEqual(EveryProperty with { Headers = read.Properties.Headers }, read.Properties);
        Assert.AreEqual(Convert.ToHexString(payload), Convert.ToHexString(read.ToPayload()));
    }

    [TestMethod]
    [DataRow("003C000000000000000000000001")]
    [DataRow("003C00000000000000000000000000")]
    [DataRow("003C0000000000000000000080")]
    public void ContentHeader_Malformed_IsSyntaxError(string hex)
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0ContentHeader.Read(Convert.FromHexString(hex)));

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    public void ToFrames_SplitsTheBodyAtFrameMax()
    {
        byte[] body = new byte[10000];
        body[^1] = 0x7F;

        IReadOnlyList<Amqp0Frame> frames = Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, body, 4096);

        Assert.HasCount(4, frames);
        Assert.AreEqual(Amqp0FrameType.Header, frames[0].Type);
        Assert.AreEqual("003C0000000000000000271000" + "00", Convert.ToHexString(frames[0].Payload));
        CollectionAssert.AreEqual(new[] { 4088, 4088, 1824 }, frames.Skip(1).Select(frame => frame.Payload.Length).ToArray());
        Assert.IsTrue(frames.Skip(1).All(frame => frame.Type == Amqp0FrameType.Body && frame.Channel == 1));
        Assert.AreEqual((byte)0x7F, frames[3].Payload[^1]);
    }

    [TestMethod]
    [DataRow(0u, 2)]
    [DataRow(4096u, 2)]
    public void ToFrames_BodyWithinOneFrame_IsOneBodyFrame(uint frameMax, int frameCount)
    {
        Assert.HasCount(frameCount, Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, "hello"u8.ToArray(), frameMax));
    }

    [TestMethod]
    public void ToFrames_EmptyBody_IsTheHeaderAlone()
    {
        Assert.HasCount(1, Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, Array.Empty<byte>(), 0));
    }

    [TestMethod]
    public void ToFrames_FrameMaxBelowMinimum_IsSyntaxError()
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, Array.Empty<byte>(), 4095));

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    public void Assembler_HeaderThenBodies_CollectsTheMessage()
    {
        var assembler = new Amqp0ContentAssembler(1);
        IReadOnlyList<Amqp0Frame> frames = Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, new byte[5000], 4096);

        Assert.IsFalse(assembler.Accept(frames[0]));
        Assert.IsFalse(assembler.Accept(frames[1]));
        Assert.IsTrue(assembler.Accept(frames[2]));

        Assert.IsTrue(assembler.IsComplete);
        Assert.HasCount(5000, assembler.Body);
        Assert.AreEqual(5000ul, assembler.Header!.BodySize);
    }

    [TestMethod]
    public void Assembler_EmptyBody_IsCompleteAtTheHeader()
    {
        var assembler = new Amqp0ContentAssembler(1);

        Assert.IsFalse(assembler.IsComplete);
        Assert.IsTrue(assembler.Accept(Amqp0Content.ToFrames(1, 60, Amqp0BasicProperties.None, Array.Empty<byte>(), 0)[0]));
    }

    [TestMethod]
    public void Assembler_FrameOutOfPlace_IsUnexpectedFrame()
    {
        Amqp0Frame header = new(Amqp0FrameType.Header, 1, Convert.FromHexString(TextPlainPersistentHeader));
        Amqp0Frame body = new(Amqp0FrameType.Body, 1, new byte[5]);

        AssertUnexpected(new Amqp0ContentAssembler(2), header);
        AssertUnexpected(new Amqp0ContentAssembler(1), body);
        var complete = new Amqp0ContentAssembler(1);
        complete.Accept(header);
        complete.Accept(body);
        AssertUnexpected(complete, body);
    }

    [TestMethod]
    public void Assembler_BodyLongerThanHeaderSaid_IsFrameError()
    {
        var assembler = new Amqp0ContentAssembler(1);
        assembler.Accept(new Amqp0Frame(Amqp0FrameType.Header, 1, Convert.FromHexString(TextPlainPersistentHeader)));

        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => assembler.Accept(new Amqp0Frame(Amqp0FrameType.Body, 1, new byte[6])));

        Assert.AreEqual(Amqp0ReplyCode.FrameError, exception.ReplyCode);
    }

    private static void AssertUnexpected(Amqp0ContentAssembler assembler, Amqp0Frame frame)
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => assembler.Accept(frame));

        Assert.AreEqual(Amqp0ReplyCode.UnexpectedFrame, exception.ReplyCode);
    }
}
