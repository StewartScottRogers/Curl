using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-141 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />:
/// a RST_STREAM NO_ERROR after the final head ends the body cleanly, as curl 8.18.0 with nghttp2
/// does - exit 0 without a <c>content-length</c>, exit 18 when the body is shorter than it - and
/// one before the head fails with exit 92 saying the stream closed cleanly too early (measured,
/// BL-3670 Notes).
/// </summary>
[TestClass]
public sealed class Cgr141Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2ResetNoErrorAfterPartialBodyWithoutLength_EndsTheTransferWithExit0()
    {
        HpackEncoder server = new();
        MemoryStream output = new();
        byte[] response = Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "partial"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.NoError));
        Diagnostics.Arrange("response frames", "HEADERS 200, DATA partial, RST_STREAM NO_ERROR");

        TransferResult result = await Execute(response, output);

        Diagnostics.Act("exit code, message, body", string.Join(", ", result.ExitCode, result.ErrorMessage, Encoding.Latin1.GetString(output.ToArray())));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("partial", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ResetNoErrorBeforeTheWholeContentLength_FailsWithExit18()
    {
        HpackEncoder server = new();
        MemoryStream output = new();
        byte[] response = Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "9")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "partial"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.NoError));
        Diagnostics.Arrange("response frames", "HEADERS 200 content-length 9, DATA partial, RST_STREAM NO_ERROR");

        TransferResult result = await Execute(response, output);

        Diagnostics.Act("exit code, message, body", string.Join(", ", result.ExitCode, result.ErrorMessage, Encoding.Latin1.GetString(output.ToArray())));
        Diagnostics.Assert("exit code", CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual("end of response with 2 bytes missing", result.ErrorMessage);
        Assert.AreEqual("partial", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ResetNoErrorBeforeTheHead_FailsWithExit92ClosedCleanlyTooEarly()
    {
        MemoryStream output = new();
        byte[] response = Response(Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.NoError));
        Diagnostics.Arrange("response frames", "RST_STREAM NO_ERROR");

        TransferResult result = await Execute(response, output);

        Diagnostics.Act("exit code, message", string.Join(", ", result.ExitCode, result.ErrorMessage));
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual("HTTP/2 stream 1 was closed cleanly, but before getting  all response header fields, treated as error", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ReadAsync_Http2ResetNoErrorQueuedWhileAnotherStreamRead_EndsTheBodyCleanly()
    {
        HpackEncoder server = new();
        byte[] response = Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "partial"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.NoError),
            Http2FrameFactory.CreateHeaders(3, server.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        Http2Session session = new(new ScriptedConnection(response, 65536));
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");
        Diagnostics.Arrange("response frames", "HEADERS 200 on 1, DATA partial on 1, RST_STREAM NO_ERROR on 1, HEADERS 204 on 3; stream 3 read first");

        string secondResponse = await ReadToEndAsync(second);
        string firstResponse = await ReadToEndAsync(first);

        Diagnostics.Act("first response", firstResponse.Replace("\r\n", "\\r\\n", StringComparison.Ordinal));
        Assert.AreEqual("HTTP/2 204 \r\n\r\n", secondResponse);
        Assert.AreEqual("HTTP/2 200 \r\n\r\npartial", firstResponse);
    }

    private static async Task<Http2StreamConnection> StartAsync(Http2Session session, string path)
    {
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes($"GET {path} HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);
        return stream;
    }

    private static async Task<string> ReadToEndAsync(Http2StreamConnection stream)
    {
        MemoryStream response = new();
        byte[] buffer = new byte[256];
        int read;
        while ((read = await stream.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            response.Write(buffer, 0, read);
        }

        return Encoding.Latin1.GetString(response.ToArray());
    }

    private static async Task<TransferResult> Execute(byte[] response, Stream output)
    {
        HttpProtocolHandler handler = new(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator());
        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
            Output = output,
            Events = new RecordingTransferEvents(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
        });
    }

    private static byte[] Response(params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];
}
