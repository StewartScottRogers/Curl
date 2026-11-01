using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-272 on <see cref="HttpProtocolHandler" />: curl with nghttp2 words an HTTP/2
/// connection error that its callback fails on as <c>nghttp2 recv error -902: The user callback
/// function failed</c> (exit 16), and a GOAWAY with an error mid-body as
/// <c>Recv failure: Connection reset by peer</c> (exit 56) (measured on curl 8.18.0, CGR-272).
/// </summary>
[TestClass]
public sealed class Cgr272Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2WindowUpdateOverflowsTheConnectionWindow_FailsWithTheCallbackFailedMessage()
    {
        TransferResult result = await RunAsync(Http2FrameFactory.CreateWindowUpdate(0, int.MaxValue));

        Diagnostics.Assert("exit code, message", $"{CurlExitCode.Http2}, nghttp2 recv error -902: The user callback function failed", $"{result.ExitCode}, {result.ErrorMessage}");
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("nghttp2 recv error -902: The user callback function failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2SettingsEnablePushOfTwo_FailsWithTheCallbackFailedMessage()
    {
        TransferResult result = await RunAsync(Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.EnablePush, 2)]));

        Diagnostics.Assert("exit code, message", $"{CurlExitCode.Http2}, nghttp2 recv error -902: The user callback function failed", $"{result.ExitCode}, {result.ErrorMessage}");
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("nghttp2 recv error -902: The user callback function failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2SettingsWithTooManyEntries_KeepsTheShutdownMessage()
    {
        Http2Setting[] settings = [.. Enumerable.Range(0, Http2Connection.MaximumSettingsEntries + 1).Select(_ => new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 10))];

        TransferResult result = await RunAsync(Http2FrameFactory.CreateSettings(settings));

        Diagnostics.Assert("exit code, message", $"{CurlExitCode.Http2}, nghttp2 shuts down connection with error 11: ENHANCE_YOUR_CALM", $"{result.ExitCode}, {result.ErrorMessage}");
        Assert.AreEqual("nghttp2 shuts down connection with error 11: ENHANCE_YOUR_CALM", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GoAwayWithAnErrorBeforeAnyHead_FailsWithConnectionReset()
    {
        TransferResult result = await RunAsync(Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty));

        Diagnostics.Assert("exit code, message", $"{CurlExitCode.RecvError}, Recv failure: Connection reset by peer", $"{result.ExitCode}, {result.ErrorMessage}");
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: Connection reset by peer", result.ErrorMessage);
    }

    private static async Task<TransferResult> RunAsync(Http2Frame frame)
    {
        byte[] response =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. Http2FrameCodec.Serialize(frame),
        ];
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/x"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge },
        };

        return await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator()).ExecuteAsync(context);
    }
}
