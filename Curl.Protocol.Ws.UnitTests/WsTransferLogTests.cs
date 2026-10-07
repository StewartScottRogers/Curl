using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the lines a <c>ws://</c> transfer writes to Curl's diagnostic log under the <c>ws</c>
/// component (ADR-0222, BL-929): the upgrade request, the upgrade accepted and the transfer's
/// end at <c>info</c>, a close frame with an unexpected code at <c>warning</c>, the failure
/// that ends the transfer at <c>error</c>, and each frame at <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class WsTransferLogTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n";

    private const string HelloAndClose = "\x81\x05hello\x88\x02\x03\xe8";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_Upgrade_LogsTheRequestTheUpgradeAndTheEndAtInfo()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        diagnostics.Arrange("url", "ws://h/chat?x=1");
        diagnostics.Bytes("scripted reply", Bytes(Head101 + HelloAndClose));

        using (diagnostics.Phase("transfer"))
        {
            await Handler(Head101 + HelloAndClose).ExecuteAsync(Context("ws://h/chat?x=1", log));
        }

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        diagnostics.Act("info lines", string.Join(" | ", info));
        diagnostics.Assert("info line count", 3, info.Length);
        Assert.HasCount(3, info);
        diagnostics.Assert("info line 0", "upgrade request sent: GET /chat?x=1", info[0]);
        Assert.AreEqual("upgrade request sent: GET /chat?x=1", info[0]);
        diagnostics.Assert("info line 1", "upgrade accepted: 101, switched to WebSocket", info[1]);
        Assert.AreEqual("upgrade accepted: 101, switched to WebSocket", info[1]);
        diagnostics.Assert("info line 2 starts with", "transfer done: 11 bytes in ", info[2]);
        Assert.StartsWith("transfer done: 11 bytes in ", info[2]);
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Ws));
    }

    [TestMethod]
    public async Task ExecuteAsync_Frames_LogsEachFramesOpcodeFinAndLengthAtVerbose()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted reply", Bytes(Head101 + "\x01\x03hel\x80\x02lo\x88\x00"));

        await Handler(Head101 + "\x01\x03hel\x80\x02lo\x88\x00").ExecuteAsync(Context("ws://h/", log));

        string[] verbose = log.MessagesAt(DiagnosticLogLevel.Verbose);
        diagnostics.Act("verbose lines", string.Join(" | ", verbose));
        string[] expected = ["received TEXT frame, no FIN, 3 bytes", "received CONTINUATION frame, FIN, 2 bytes", "received CLOSE frame, FIN, 0 bytes"];
        diagnostics.Assert("verbose lines", string.Join(" | ", expected), string.Join(" | ", verbose));
        CollectionAssert.AreEqual(
            new[] { "received TEXT frame, no FIN, 3 bytes", "received CONTINUATION frame, FIN, 2 bytes", "received CLOSE frame, FIN, 0 bytes" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_LogsTheFrameSentAtVerbose()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://h/"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes("hi")),
            DiagnosticLog = log,
        };
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("upload", "hi");
        diagnostics.Bytes("scripted reply", Bytes(Head101 + HelloAndClose));

        await Handler(Head101 + HelloAndClose).ExecuteAsync(context);

        string[] verbose = log.MessagesAt(DiagnosticLogLevel.Verbose);
        diagnostics.Act("verbose lines", string.Join(" | ", verbose));
        diagnostics.Assert("verbose lines contain", "sent BINARY frame, FIN, 2 bytes", string.Join(" | ", verbose));
        Assert.Contains("sent BINARY frame, FIN, 2 bytes", log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_CloseWithUnexpectedCode_LogsAWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted reply", Bytes(Head101 + "\x88\x05\x03\xf3" + "bye"));

        await Handler(Head101 + "\x88\x05\x03\xf3" + "bye").ExecuteAsync(Context("ws://h/", log));

        string[] warnings = log.MessagesAt(DiagnosticLogLevel.Warning);
        diagnostics.Act("warning lines", string.Join(" | ", warnings));
        diagnostics.Assert("warning lines", "server closed with unexpected code 1011", string.Join(" | ", warnings));
        CollectionAssert.AreEqual(new[] { "server closed with unexpected code 1011" }, log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_CloseGoingAway_LogsNoWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted reply", Bytes(Head101 + "\x88\x02\x03\xe9"));

        await Handler(Head101 + "\x88\x02\x03\xe9").ExecuteAsync(Context("ws://h/", log));

        string[] warnings = log.MessagesAt(DiagnosticLogLevel.Warning);
        diagnostics.Act("warning lines", string.Join(" | ", warnings));
        diagnostics.Assert("warning line count", 0, warnings.Length);
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedUpgrade_LogsTheExitCodeAtError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted reply", Bytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n"));

        TransferResult result = await Handler("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n").ExecuteAsync(Context("ws://h/", log));

        diagnostics.Act("exit code", $"{result.ExitCode} ({result.ErrorMessage})");
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        string[] errors = log.MessagesAt(DiagnosticLogLevel.Error);
        diagnostics.Act("error lines", string.Join(" | ", errors));
        diagnostics.Assert("error lines", "transfer failed with HttpReturnedError (exit 22): Refused WebSocket upgrade: 403", string.Join(" | ", errors));
        CollectionAssert.AreEqual(
            new[] { "transfer failed with HttpReturnedError (exit 22): Refused WebSocket upgrade: 403" },
            log.MessagesAt(DiagnosticLogLevel.Error));
        Assert.DoesNotContain("upgrade accepted: 101, switched to WebSocket", log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_LogsNoInfoOrVerboseLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Error);
        diagnostics.Bytes("scripted reply", Bytes(Head101 + "\x81\x05hello\x88\x02\x03\xf3"));

        await Handler(Head101 + "\x81\x05hello\x88\x02\x03\xf3").ExecuteAsync(Context("ws://h/", log));

        diagnostics.Act("logged line count", log.Lines.Count);
        diagnostics.Assert("logged line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtNone_LogsNothingOnFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);
        diagnostics.Arrange("log level", DiagnosticLogLevel.None);
        diagnostics.Bytes("scripted reply", Bytes("HTTP/1.1 403 Forbidden\r\n\r\n"));

        await Handler("HTTP/1.1 403 Forbidden\r\n\r\n").ExecuteAsync(Context("ws://h/", log));

        diagnostics.Act("logged line count", log.Lines.Count);
        diagnostics.Assert("logged line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheDiagnosticLog()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(Head101))));
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted reply", Bytes(Head101));

        await new WsProtocolHandler(connector, new RecordingAuthenticator(), new FixedRandomSource()).ExecuteAsync(Context("ws://h/", log));

        var target = connector.Targets.Single();
        diagnostics.Act("connect target carries the log", ReferenceEquals(log, target.DiagnosticLog));
        diagnostics.Assert("connect target carries the log", true, ReferenceEquals(log, target.DiagnosticLog));
        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsInTheUrl_LogNoSecret()
    {
        const string Authorization = "Basic dXNlcjpzM2NyZXQ=";
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(Head101 + HelloAndClose))));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://user:s3cret@h/p"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("user", "s3cret"),
            DiagnosticLog = log,
        };
        diagnostics.Arrange("url", "ws://user:<password>@h/p");
        diagnostics.Arrange("authorization", "Basic <credentials>");
        diagnostics.Bytes("scripted reply", Bytes(Head101 + HelloAndClose));

        await new WsProtocolHandler(connector, new RecordingAuthenticator(Authorization), new FixedRandomSource()).ExecuteAsync(context);

        diagnostics.Act("logged line count", log.Lines.Count);
        diagnostics.Assert("logged line count is positive", true, log.Lines.Count > 0);
        Assert.IsNotEmpty(log.Lines);
        bool leaked = log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal) || line.Message.Contains("dXNlcjpzM2NyZXQ", StringComparison.Ordinal));
        diagnostics.Assert("a line leaks the secret", false, leaked);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal) || line.Message.Contains("dXNlcjpzM2NyZXQ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Decode_WithoutALog_WritesNothingAndStillDecodes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("close frame", Bytes("\x88\x02\x03\xf3"));
        diagnostics.Arrange("transfer log", "none");

        WsDecodedBytes decoded = new WsFrameDecoder(null).Decode(Bytes("\x88\x02\x03\xf3"));

        string payload = Encoding.Latin1.GetString(decoded.Payload);
        diagnostics.Act("payload", payload);
        diagnostics.Assert("payload", "\x03\xf3", payload);
        Assert.AreEqual("\x03\xf3", Encoding.Latin1.GetString(decoded.Payload));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static WsProtocolHandler Handler(string reply) =>
        new(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(reply)))), new RecordingAuthenticator(), new FixedRandomSource());

    private static TransferContext Context(string url, IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), DiagnosticLog = log };
}
