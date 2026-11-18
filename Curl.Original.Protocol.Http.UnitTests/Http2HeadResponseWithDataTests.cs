using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>Pins BL-5391: an HTTP/2 HEAD response that carries DATA ends at its final head with -I, and still fails without it.</summary>
[TestClass]
public sealed class Http2HeadResponseWithDataTests
{
    private const string HeadRequest = "HEAD /headbody HTTP/1.1\r\nHost: h\r\n\r\n";

    [TestMethod]
    public async Task ReadToEndAsync_HeadResponseWithDataAndIgnoresBody_EndsAfterTheHeadWithoutFailing()
    {
        (string head, string tail) = await ExchangeAsync(ignoresBody: true, endsOnHeaders: false);

        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 5\r\n\r\n", head);
        Assert.AreEqual(string.Empty, tail);
    }

    [TestMethod]
    public async Task ReadToEndAsync_HeadResponseWithDataWithoutIgnoresBody_FailsWithExit16()
    {
        HttpTransferException failure = await Assert.ThrowsAsync<HttpTransferException>(async () => await ExchangeAsync(ignoresBody: false, endsOnHeaders: false));

        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReadToEndAsync_HeadResponseEndedOnItsHeadAndIgnoresBody_IsUnchanged()
    {
        (string head, string tail) = await ExchangeAsync(ignoresBody: true, endsOnHeaders: true);

        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 5\r\n\r\n", head);
        Assert.AreEqual(string.Empty, tail);
    }

    [TestMethod]
    public async Task ReadToEndAsync_GetStreamEndingAtFinalHeadWithContentLength_EndsWithoutGoAway()
    {
        HpackEncoder server = new();
        List<byte> scripted = [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([]))];
        scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: false, isEndHeaders: true)));
        ScriptedConnection connection = new([.. scripted], 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, true);
        await stream.WriteAsync(Encoding.Latin1.GetBytes("GET /g HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);
        byte[] buffer = new byte[256];

        int read = await stream.ReadAsync(buffer, CancellationToken.None);
        await stream.ReadToEndAsync(CancellationToken.None);
        int tail = await stream.ReadAsync(buffer, CancellationToken.None);

        Assert.AreEqual(0, tail);
        Assert.IsTrue(read > 0);
    }

    private static async Task<(string Head, string Tail)> ExchangeAsync(bool ignoresBody, bool endsOnHeaders)
    {
        HpackEncoder server = new();
        List<byte> scripted = [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([]))];
        scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: endsOnHeaders, isEndHeaders: true)));
        if (!endsOnHeaders)
        {
            scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true)));
        }

        ScriptedConnection connection = new([.. scripted], 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody);
        await stream.WriteAsync(Encoding.Latin1.GetBytes(HeadRequest), CancellationToken.None);
        byte[] buffer = new byte[256];
        int read = await stream.ReadAsync(buffer, CancellationToken.None);
        await stream.ReadToEndAsync(CancellationToken.None);
        int tail = await stream.ReadAsync(buffer, CancellationToken.None);
        return (Encoding.Latin1.GetString(buffer, 0, read), Encoding.Latin1.GetString(buffer, 0, tail));
    }
}
