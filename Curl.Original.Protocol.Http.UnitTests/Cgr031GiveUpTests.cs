using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins that <see cref="HttpProtocolHandler" /> closes the HTTP/2 connection it gives up on after
/// five REFUSED_STREAM retries without the closing GOAWAY, as curl 8.18.0 does, while the five
/// connections it retries from still send one (BL-3838).
/// </summary>
[TestClass]
public sealed class Cgr031GiveUpTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2StreamRefusedOnSixConnections_SendsTheClosingGoAwayOnTheFirstFiveOnly()
    {
        byte[] refusal =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.RefusedStream)),
        ];
        byte[] goAway = Http2FrameCodec.Serialize(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.NoError, new byte[] { 0x73, 0x68, 0x75, 0x74, 0x64, 0x6f, 0x77, 0x6e, 0 }));
        ScriptedConnection[] connections = [.. Enumerable.Range(0, 6).Select(_ => new ScriptedConnection(refusal, 65536))];
        QueueConnector connector = QueueConnector.For(connections);
        Diagnostics.Arrange("six connections", "RST_STREAM REFUSED_STREAM on stream 1");

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
            Output = new MemoryStream(),
            Events = new RecordingTransferEvents(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
        });

        bool[] sentGoAway = [.. connections.Select(c => c.Written.AsSpan().EndsWith(goAway))];
        Diagnostics.Act("exit code, message, GOAWAY per connection", string.Join(", ", result.ExitCode, result.ErrorMessage, string.Join("/", sentGoAway)));
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Connection died, tried 5 times before giving up", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { true, true, true, true, true, false }, sentGoAway);
    }
}
