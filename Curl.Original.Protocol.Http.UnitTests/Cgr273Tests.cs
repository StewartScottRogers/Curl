using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-273 (BL-3802) on <see cref="HttpProtocolHandler" />: the trailers of an HTTP/2
/// response count in the report's response headers (<c>%{num_headers}</c>) and reach the
/// transfer events as received header lines, which <c>-v</c> shows as <c>&lt; name: value</c>.
/// </summary>
[TestClass]
public sealed class Cgr273Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2Trailers_CountAsResponseHeadersAndAreReportedAsReceivedLines()
    {
        HpackEncoder server = new();
        byte[] response =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "hi"u8.ToArray(), isEndStream: false)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-trailer", "yes"), new("grpc-status", "0")]), isEndStream: true, isEndHeaders: true)),
        ];
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("response frames", "HEADERS 200, DATA hi, HEADERS x-trailer grpc-status (end stream)");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
                Output = new MemoryStream(),
                HeaderOutput = new MemoryStream(),
                Events = events,
                Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
            });

        Diagnostics.Act("exit code, headers, events", $"{result.ExitCode}, {result.Report?.ResponseHeaders.Count}, {string.Join('|', events.Events)}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, result.Report!.ResponseHeaders.Count);
        CollectionAssert.Contains(events.Events, "< x-trailer: yes\r\n");
        CollectionAssert.Contains(events.Events, "< grpc-status: 0\r\n");
    }
}
