using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-142 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />,
/// as curl 8.22.0 with nghttp2 answers <c>curl -sS --http2-prior-knowledge -i</c> (measured,
/// BL-3912 Notes): a PUSH_PROMISE with push disabled fails with exit 92 naming the frame and
/// sends GOAWAY <c>PUSH_PROMISE: push disabled</c>; a <c>:status</c> that is not three digits
/// fails with exit 92 naming the field, and a head with no <c>:status</c> with exit 8 naming
/// the HEADERS frame, each after RST_STREAM PROTOCOL_ERROR. Nothing reaches the output.
/// </summary>
[TestClass]
public sealed class Cgr142Tests
{
    private const string ResetProtocolError = "00000403000000000100000001";

    private const string ClosingGoAway = "000011070000000000" + "00000000" + "00000000" + "73687574646F776E00";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2PushPromiseWithPushDisabled_FailsWithExit92NamingTheFrame()
    {
        HpackEncoder server = new();
        byte[] response = Response(
            Http2FrameFactory.CreatePushPromise(1, 2, new byte[] { 0x82, 0x86, 0x84 }, isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "0")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        MemoryStream output = new();
        Diagnostics.Arrange("response frames", "PUSH_PROMISE on 1 promising 2 (len 7), HEADERS 200 END_STREAM");

        TransferResult result = await Execute(connection, output);

        Diagnostics.Act("exit code, message", string.Join(", ", result.ExitCode, result.ErrorMessage));
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual("[HTTP2] [1] received invalid frame: FRAME[PUSH_PROMISE, len=7, hend=1], error -505: Protocol error", result.ErrorMessage);
        StringAssert.EndsWith(Convert.ToHexString(connection.Written), "000023070000000000" + "00000000" + "00000001" + Convert.ToHexString("PUSH_PROMISE: push disabled"u8), "GOAWAY PROTOCOL_ERROR saying push is disabled, and no closing GOAWAY after it");
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2PushPromiseWithoutEndHeaders_NamesTheFrameWithHend0()
    {
        byte[] response = Response(
            Http2FrameFactory.CreatePushPromise(1, 2, new byte[] { 0x82 }, isEndHeaders: false),
            Http2FrameFactory.CreateContinuation(1, new byte[] { 0x86, 0x84 }, isEndHeaders: true));
        Diagnostics.Arrange("response frames", "PUSH_PROMISE on 1 promising 2 without END_HEADERS (len 5), CONTINUATION");

        TransferResult result = await Execute(new ScriptedConnection(response, 65536), new MemoryStream());

        Diagnostics.Act("exit code, message", string.Join(", ", result.ExitCode, result.ErrorMessage));
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual("[HTTP2] [1] received invalid frame: FRAME[PUSH_PROMISE, len=5, hend=0], error -505: Protocol error", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("2x0")]
    [DataRow("20")]
    [DataRow("200 ")]
    public async Task ExecuteAsync_Http2StatusNotThreeDigits_ResetsTheStreamAndFailsWithExit92NamingTheField(string status)
    {
        HpackEncoder server = new();
        byte[] response = Response(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", status), new("content-length", "0")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        MemoryStream output = new();
        Diagnostics.Arrange(":status", status);

        TransferResult result = await Execute(connection, output);

        Diagnostics.Act("exit code, message", string.Join(", ", result.ExitCode, result.ErrorMessage));
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual($"Invalid HTTP header field was received: frame type: 1, stream: 1, name: [:status], value: [{status}]", result.ErrorMessage);
        StringAssert.EndsWith(Convert.ToHexString(connection.Written), ResetProtocolError + ClosingGoAway, "RST_STREAM PROTOCOL_ERROR, then the closing GOAWAY");
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    [DataRow(true, 1)]
    [DataRow(false, 0)]
    public async Task ExecuteAsync_Http2HeadWithoutStatus_ResetsTheStreamAndFailsWithExit8NamingTheFrame(bool isEndStream, int eos)
    {
        byte[] contentLengthZero = [0x0f, 0x0d, 0x01, 0x30];
        byte[] response = Response(Http2FrameFactory.CreateHeaders(1, contentLengthZero, isEndStream, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        MemoryStream output = new();
        Diagnostics.Arrange("response frames", $"HEADERS content-length 0 (len 4), no :status, END_STREAM {isEndStream}");

        TransferResult result = await Execute(connection, output);

        Diagnostics.Act("exit code, message", string.Join(", ", result.ExitCode, result.ErrorMessage));
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual($"[HTTP2] [1] received invalid frame: FRAME[HEADERS, len=4, hend=1, eos={eos}], error -532: Violation in HTTP messaging rule", result.ErrorMessage);
        StringAssert.EndsWith(Convert.ToHexString(connection.Written), ResetProtocolError + ClosingGoAway, "RST_STREAM PROTOCOL_ERROR, then the closing GOAWAY");
        Assert.AreEqual(0, output.Length);
    }

    private static async Task<TransferResult> Execute(ScriptedConnection connection, Stream output)
    {
        HttpProtocolHandler handler = new(QueueConnector.For(connection), new SilentAuthenticator());
        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
            Output = output,
            Events = new RecordingTransferEvents(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.22.0" },
        });
    }

    private static byte[] Response(params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];
}
