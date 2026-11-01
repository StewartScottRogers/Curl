using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-4025 on <see cref="HttpProtocolHandler" />: an HTTP/2 response that ends with trailers reports
/// their count in <see cref="TransferReport.TrailerCount" />, which <c>%{num_headers}</c> prints instead of
/// the head's header count, and keeps the head's headers in <see cref="TransferReport.ResponseHeaders" />.
/// Measured with curl 8.18.0 and nghttp2 1.68: head x-a, x-b + trailer x-t gave num_headers 1 and
/// <c>%header{x-a}</c> 1; head x-a + trailer x-t gave 1; a bare head + trailers x-t, x-u gave 2.
/// </summary>
[TestClass]
public sealed class Http2TrailerNumHeadersTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(2, 1, 3)]
    [DataRow(1, 1, 2)]
    [DataRow(0, 2, 2)]
    [DataRow(0, 1, 1)]
    public async Task ExecuteAsync_Http2Trailers_ReportTheirOwnCountAndKeepTheHead(int headFields, int trailerFields, int allHeaders)
    {
        HpackEncoder server = new();
        List<HeaderField> head = [new(":status", "200")];
        head.AddRange(Enumerable.Range(0, headFields).Select(index => new HeaderField($"x-h{index}", "1")));
        List<HeaderField> trailers = [.. Enumerable.Range(0, trailerFields).Select(index => new HeaderField($"x-t{index}", "1"))];
        byte[] response =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode(head), isEndStream: false, isEndHeaders: true)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "hello h2\n"u8.ToArray(), isEndStream: false)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode(trailers), isEndStream: true, isEndHeaders: true)),
        ];
        Diagnostics.Arrange("head fields, trailer fields", $"{headFields}, {trailerFields}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:21613/x"),
                Output = new MemoryStream(),
                HeaderOutput = new MemoryStream(),
                Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
            });

        Diagnostics.Act("exit code, trailer count, response headers", $"{result.ExitCode}, {result.Report?.TrailerCount}, {result.Report?.ResponseHeaders.Count}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(trailerFields, result.Report!.TrailerCount);
        Assert.AreEqual(allHeaders, result.Report.ResponseHeaders.Count);
    }
}
