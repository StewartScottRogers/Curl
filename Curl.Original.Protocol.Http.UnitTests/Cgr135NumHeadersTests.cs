using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-135 (BL-3979) on <see cref="HttpProtocolHandler" />: when an HTTP/2 head fails part way
/// (a second <c>content-length</c> after a valid one), <c>%{num_headers}</c> counts the header lines
/// curl wrote before the failure, as curl 8.18.0 with nghttp2 1.68 does (measured, BL-3664 Notes).
/// </summary>
[TestClass]
public sealed class Cgr135NumHeadersTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2HeadWithContentLengthTwice_CountsTheHeaderLineWrittenBeforeTheFailure()
    {
        HeaderField[] fields = [new(":status", "200"), new("content-length", "9"), new("content-length", "9")];
        ScriptedConnection connection = new(Response(fields), 65536);
        MemoryStream headers = new();
        Diagnostics.Arrange("fields", "200, content-length: 9 twice");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
                Output = new MemoryStream(),
                HeaderOutput = headers,
                Events = new RecordingTransferEvents(),
                Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
            });

        Diagnostics.Act("exit code, head, headers stored", $"{result.ExitCode}, {Encoding.Latin1.GetString(headers.ToArray())}, {result.Report?.ResponseHeadersStored}");
        Diagnostics.Assert("headers stored", 1, result.Report?.ResponseHeadersStored);
        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 9\r\n", Encoding.Latin1.GetString(headers.ToArray()));
        Assert.AreEqual(1, result.Report!.ResponseHeadersStored);
    }

    private static byte[] Response(HeaderField[] fields) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(fields), isEndStream: true, isEndHeaders: true)),
    ];
}
