using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>Pins BL-5312: a request with a -H Content-Length and no body ends its HTTP/2 stream when the response is read.</summary>
[TestClass]
public sealed class Http2StreamConnectionContentLengthTests
{
    private const int PrefaceLength = 24;

    private const int FrameHeaderLength = 9;

    private const byte HeadersType = 1;

    private const byte DataType = 0;

    [TestMethod]
    public async Task ReadAsync_NoBodyWithContentLengthZeroField_EndsTheRequestAndReadsTheResponse()
    {
        (List<(byte Type, byte Flags, int Length)> frames, string body) = await ExchangeAsync("POST /echo HTTP/1.1\r\nHost: h\r\nContent-Length: 0\r\n\r\n", bodyLength: 0);

        Assert.AreEqual(0x4, frames.Single(frame => frame.Type == HeadersType).Flags, "HEADERS: END_HEADERS only");
        (byte Type, byte Flags, int Length) data = frames.Single(frame => frame.Type == DataType);
        Assert.AreEqual(0x1, data.Flags, "DATA: END_STREAM");
        Assert.AreEqual(0, data.Length);
        Assert.AreEqual("HTTP/2 200 \r\n", body[..13]);
    }

    [TestMethod]
    public async Task ReadAsync_NoBodyWithContentLengthFiveField_EndsTheRequestPromptlyAndReadsTheReply()
    {
        (List<(byte Type, byte Flags, int Length)> frames, string body) = await ExchangeAsync("POST /echo HTTP/1.1\r\nHost: h\r\nContent-Length: 5\r\n\r\n", bodyLength: 0);

        Assert.AreEqual(0x1, frames.Single(frame => frame.Type == DataType).Flags, "DATA: END_STREAM");
        Assert.StartsWith("HTTP/2 200", body);
    }

    [TestMethod]
    public async Task WriteAsync_BodyWithContentLength_SendsTheBodyAndEndsTheStreamOnTheLastDataFrame()
    {
        (List<(byte Type, byte Flags, int Length)> frames, string _) = await ExchangeAsync("POST /echo HTTP/1.1\r\nHost: h\r\nContent-Length: 3\r\n\r\n", bodyLength: 3, "abc"u8.ToArray());

        (byte Type, byte Flags, int Length) data = frames.Single(frame => frame.Type == DataType);
        Assert.AreEqual(3, data.Length);
        Assert.AreEqual(0x1, data.Flags, "DATA: END_STREAM on the last frame");
    }

    private static async Task<(List<(byte Type, byte Flags, int Length)> Frames, string Response)> ExchangeAsync(string head, long bodyLength, byte[]? body = null)
    {
        HpackEncoder server = new();
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: true, isEndHeaders: true)),
        ];
        ScriptedConnection connection = new(scripted, 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", bodyLength, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes(head), CancellationToken.None);
        if (body is not null)
        {
            await stream.WriteAsync(body, CancellationToken.None);
        }

        byte[] buffer = new byte[256];
        int read = await stream.ReadAsync(buffer, CancellationToken.None);

        byte[] written = connection.Written;
        List<(byte Type, byte Flags, int Length)> frames = [];
        int offset = PrefaceLength;
        while (offset + FrameHeaderLength <= written.Length)
        {
            int length = (written[offset] << 16) | (written[offset + 1] << 8) | written[offset + 2];
            frames.Add((written[offset + 3], written[offset + 4], length));
            offset += FrameHeaderLength + length;
        }

        return (frames, Encoding.Latin1.GetString(buffer, 0, read));
    }
}
