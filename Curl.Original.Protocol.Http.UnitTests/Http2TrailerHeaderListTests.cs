using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5392: HTTP/2 trailer fields stay out of <see cref="TransferReport.ResponseHeaders" />, which
/// <c>%header{}</c> and <c>%{header_json}</c> read, while the header output (<c>-D</c>) still shows them after
/// the head's empty line, and HTTP/1.1 chunked trailers behave as before.
/// </summary>
[TestClass]
public sealed class Http2TrailerHeaderListTests
{
    private static async Task<(TransferResult Result, string HeaderText)> RunHttp2Async()
    {
        HpackEncoder server = new();
        byte[] response =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("x-h", "head")]), isEndStream: false, isEndHeaders: true)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-t", "trailerval")]), isEndStream: true, isEndHeaders: true)),
        ];
        MemoryStream headerOutput = new();
        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:44101/trail"),
                Output = new MemoryStream(),
                HeaderOutput = headerOutput,
                Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.22.0" },
            });
        return (result, Encoding.Latin1.GetString(headerOutput.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2Trailer_StaysOutOfTheResponseHeaders()
    {
        (TransferResult result, _) = await RunHttp2Async();

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(result.Report!.ResponseHeaders.Any(header => header.Key == "x-t"));
        Assert.IsTrue(result.Report.ResponseHeaders.Any(header => header.Key == "x-h"));
        Assert.AreEqual(1, result.Report.TrailerCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2Trailer_ReachesTheHeaderOutputAfterTheHeadsEmptyLine()
    {
        (_, string headerText) = await RunHttp2Async();

        Assert.IsTrue(headerText.Contains("x-h: head\r\n\r\nx-t: trailerval\r\n", StringComparison.Ordinal), headerText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedTrailer_StaysOutOfTheResponseHeadersAndIsInTheHeaderOutput()
    {
        MemoryStream headerOutput = new();
        string response = "HTTP/1.1 200 OK\r\nX-A: 1\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n0\r\nX-T: 1\r\n\r\n";

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:41000/"),
                Output = new MemoryStream(),
                HeaderOutput = headerOutput,
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0" },
            });

        Assert.IsFalse(result.Report!.ResponseHeaders.Any(header => header.Key == "X-T"));
        Assert.IsTrue(Encoding.Latin1.GetString(headerOutput.ToArray()).Contains("X-A: 1\r\nTransfer-Encoding: chunked\r\n\r\nX-T: 1\r\n", StringComparison.Ordinal));
    }
}
