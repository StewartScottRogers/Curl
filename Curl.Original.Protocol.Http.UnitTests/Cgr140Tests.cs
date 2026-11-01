using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-140 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />:
/// after the server resets one stream with RST_STREAM INTERNAL_ERROR, the transfer fails with
/// exit 92 but its connection is left intact, and the next URL goes out on stream 3 of the same
/// connection, as curl 8.18.0 with nghttp2 does (measured, BL-3669 Notes).
/// </summary>
[TestClass]
public sealed class Cgr140Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2StreamResetByTheServer_LeavesTheConnectionIntactForTheNextUrl()
    {
        HpackEncoder server = new();
        byte[] response = Response(
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.InternalError),
            Http2FrameFactory.CreateHeaders(3, server.Encode([new(":status", "200"), new("content-length", "9")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(3, "hello h2\n"u8.ToArray(), isEndStream: true));
        SessionHoldingConnection connection = new(new ScriptedConnection(response, 65536));
        HttpProtocolHandler handler = new(
            new QueueConnector(ConnectResult.Connected(connection, null), ConnectResult.Connected(connection, null, isReused: true)),
            new SilentAuthenticator());
        RecordingTransferEvents firstEvents = new();
        MemoryStream secondOutput = new();
        Diagnostics.Arrange("urls, frames", "http://127.0.0.1:21612/1, /2; RST_STREAM INTERNAL_ERROR on 1, 200 hello h2 on 3");

        TransferResult first = await handler.ExecuteAsync(Context("http://127.0.0.1:21612/1", new MemoryStream(), firstEvents));
        TransferResult second = await handler.ExecuteAsync(Context("http://127.0.0.1:21612/2", secondOutput, new RecordingTransferEvents()));
        await connection.CloseAsync();

        Diagnostics.Act("exit codes, second body, returned reusable", string.Join(", ", first.ExitCode, second.ExitCode, Encoding.Latin1.GetString(secondOutput.ToArray()), connection.ReturnedReusableCount));
        Diagnostics.Assert("first exit code", CurlExitCode.Http2Stream, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, first.ExitCode);
        CollectionAssert.Contains(firstEvents.Info, "Connection #0 to host 127.0.0.1:21612 left intact");
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual("hello h2\n", Encoding.Latin1.GetString(secondOutput.ToArray()));
        Assert.AreEqual(2, connection.ReturnedReusableCount);
        Assert.AreEqual(0, second.Report!.ConnectionCount, "the second URL reused the connection");
    }

    private static TransferContext Context(string url, Stream output, ITransferEvents events) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        Events = events,
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
    };

    private static byte[] Response(params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];
}
