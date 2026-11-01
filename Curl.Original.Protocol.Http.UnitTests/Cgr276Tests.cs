using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>Pins CGR-276: an empty request body is an empty DATA frame, not END_STREAM on HEADERS.</summary>
[TestClass]
public sealed class Cgr276Tests
{
    private const int PrefaceLength = 24;

    private const int FrameHeaderLength = 9;

    private const byte HeadersType = 1;

    private const byte DataType = 0;

    [TestMethod]
    public async Task EndRequestAsync_EmptyBodyWithContentLength_SendsHeadersWithoutEndStreamThenEmptyDataWithEndStream()
    {
        List<(byte Type, byte Flags, int Length)> frames = await FramesWrittenAsync("POST /echo HTTP/1.1\r\nHost: h\r\nContent-Length: 0\r\n\r\n", sendsEndRequest: true);

        Assert.AreEqual(0x4, frames.Single(frame => frame.Type == HeadersType).Flags, "HEADERS: END_HEADERS only");
        (byte Type, byte Flags, int Length) data = frames.Single(frame => frame.Type == DataType);
        Assert.AreEqual(0x1, data.Flags, "DATA: END_STREAM");
        Assert.AreEqual(0, data.Length);
    }

    [TestMethod]
    public async Task EndRequestAsync_RequestWithoutBody_EndsTheStreamOnHeaders()
    {
        List<(byte Type, byte Flags, int Length)> frames = await FramesWrittenAsync("GET /echo HTTP/1.1\r\nHost: h\r\n\r\n", sendsEndRequest: true);

        Assert.AreEqual(0x5, frames.Single(frame => frame.Type == HeadersType).Flags, "HEADERS: END_STREAM and END_HEADERS");
        Assert.IsFalse(frames.Any(frame => frame.Type == DataType), "no DATA frame");
    }

    private static async Task<List<(byte Type, byte Flags, int Length)>> FramesWrittenAsync(string head, bool sendsEndRequest)
    {
        byte[] scripted = [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([]))];
        ScriptedConnection connection = new(scripted, 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes(head), CancellationToken.None);
        if (sendsEndRequest)
        {
            await stream.EndRequestAsync(CancellationToken.None);
        }

        byte[] written = connection.Written;
        List<(byte Type, byte Flags, int Length)> frames = [];
        int offset = PrefaceLength;
        while (offset + FrameHeaderLength <= written.Length)
        {
            int length = (written[offset] << 16) | (written[offset + 1] << 8) | written[offset + 2];
            frames.Add((written[offset + 3], written[offset + 4], length));
            offset += FrameHeaderLength + length;
        }

        return frames;
    }
}
