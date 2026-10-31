using System.Text;

namespace Curl.Extension.ConnectRpc;

/// <summary>Connect protocol reference, "Streaming-Response", "End-Stream Message" and "Error Codes".</summary>
[TestClass]
public sealed class ConnectStreamReplyTests
{
    [TestMethod]
    public void Read_MessagesThenAnEndStreamWithNoError_GivesEachMessageOnItsOwnLine()
    {
        ConnectStreamReply reply = ConnectStreamReply.Read(Framed((0, "{\"n\":1}"), (0, "{\"n\":2}"), (2, "{\"metadata\":{\"k\":[\"v\"]}}")));

        Assert.AreEqual("{\"n\":1}\n{\"n\":2}\n", Encoding.UTF8.GetString(reply.Messages));
        Assert.IsNull(reply.Error);
    }

    [TestMethod]
    public void Read_AnEndStreamThatIsNotAnObject_HasNoError()
    {
        Assert.IsNull(ConnectStreamReply.Read(Framed((2, "[]"))).Error);
    }

    [TestMethod]
    public void Read_AnEndStreamWithAnError_GivesItsCodeAndMessage()
    {
        Assert.AreEqual(
            "Connect error not_found: no such user",
            ConnectStreamReply.Read(Framed((2, "{\"error\":{\"code\":\"not_found\",\"message\":\"no such user\"}}"))).Error);
    }

    [TestMethod]
    public void Read_AnErrorWithNoMessage_GivesItsCodeAlone()
    {
        Assert.AreEqual("Connect error internal", ConnectStreamReply.Read(Framed((2, "{\"error\":{\"code\":\"internal\",\"message\":5}}"))).Error);
    }

    [TestMethod]
    public void Read_AnErrorWithNoCode_IsUnknown()
    {
        Assert.AreEqual("Connect error unknown", ConnectStreamReply.Read(Framed((2, "{\"error\":{}}"))).Error);
        Assert.AreEqual("Connect error unknown", ConnectStreamReply.Read(Framed((2, "{\"error\":\"x\"}"))).Error);
    }

    [TestMethod]
    public void Read_AnEndStreamThatIsNotJson_IsRefused()
    {
        Assert.ThrowsExactly<FormatException>(() => ConnectStreamReply.Read(Framed((2, "{"))));
    }

    [TestMethod]
    public void Read_ACompressedMessage_IsRefused()
    {
        FormatException refused = Assert.ThrowsExactly<FormatException>(() => ConnectStreamReply.Read(Framed((1, "x"), (2, "{}"))));

        StringAssert.Contains(refused.Message, "compressed");
    }

    [TestMethod]
    public void Read_NoEndStreamMessage_IsRefused()
    {
        FormatException refused = Assert.ThrowsExactly<FormatException>(() => ConnectStreamReply.Read(Framed((0, "{}"))));

        StringAssert.Contains(refused.Message, "end-of-stream");
    }

    internal static byte[] Framed(params (byte Flags, string Message)[] messages)
    {
        List<byte> framed = [];
        foreach ((byte flags, string message) in messages)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            framed.Add(flags);
            framed.AddRange([(byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length]);
            framed.AddRange(bytes);
        }

        return [.. framed];
    }
}
