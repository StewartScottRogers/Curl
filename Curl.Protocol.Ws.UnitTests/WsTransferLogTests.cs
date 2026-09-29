using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;

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

    [TestMethod]
    public async Task ExecuteAsync_Upgrade_LogsTheRequestTheUpgradeAndTheEndAtInfo()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(Head101 + HelloAndClose).ExecuteAsync(Context("ws://h/chat?x=1", log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Assert.HasCount(3, info);
        Assert.AreEqual("upgrade request sent: GET /chat?x=1", info[0]);
        Assert.AreEqual("upgrade accepted: 101, switched to WebSocket", info[1]);
        Assert.StartsWith("transfer done: 11 bytes in ", info[2]);
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Ws));
    }

    [TestMethod]
    public async Task ExecuteAsync_Frames_LogsEachFramesOpcodeFinAndLengthAtVerbose()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(Head101 + "\x01\x03hel\x80\x02lo\x88\x00").ExecuteAsync(Context("ws://h/", log));

        CollectionAssert.AreEqual(
            new[] { "received TEXT frame, no FIN, 3 bytes", "received CONTINUATION frame, FIN, 2 bytes", "received CLOSE frame, FIN, 0 bytes" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_LogsTheFrameSentAtVerbose()
    {
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://h/"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes("hi")),
            DiagnosticLog = log,
        };

        await Handler(Head101 + HelloAndClose).ExecuteAsync(context);

        Assert.Contains("sent BINARY frame, FIN, 2 bytes", log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_CloseWithUnexpectedCode_LogsAWarning()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(Head101 + "\x88\x05\x03\xf3" + "bye").ExecuteAsync(Context("ws://h/", log));

        CollectionAssert.AreEqual(new[] { "server closed with unexpected code 1011" }, log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_CloseGoingAway_LogsNoWarning()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(Head101 + "\x88\x02\x03\xe9").ExecuteAsync(Context("ws://h/", log));

        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedUpgrade_LogsTheExitCodeAtError()
    {
        var log = new RecordingDiagnosticLog();

        TransferResult result = await Handler("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n").ExecuteAsync(Context("ws://h/", log));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "transfer failed with HttpReturnedError (exit 22): Refused WebSocket upgrade: 403" },
            log.MessagesAt(DiagnosticLogLevel.Error));
        Assert.DoesNotContain("upgrade accepted: 101, switched to WebSocket", log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_LogsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await Handler(Head101 + "\x81\x05hello\x88\x02\x03\xf3").ExecuteAsync(Context("ws://h/", log));

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtNone_LogsNothingOnFailure()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);

        await Handler("HTTP/1.1 403 Forbidden\r\n\r\n").ExecuteAsync(Context("ws://h/", log));

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheDiagnosticLog()
    {
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(Head101))));

        await new WsProtocolHandler(connector, new RecordingAuthenticator(), new FixedRandomSource()).ExecuteAsync(Context("ws://h/", log));

        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsInTheUrl_LogNoSecret()
    {
        const string Authorization = "Basic dXNlcjpzM2NyZXQ=";
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(Head101 + HelloAndClose))));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://user:s3cret@h/p"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("user", "s3cret"),
            DiagnosticLog = log,
        };

        await new WsProtocolHandler(connector, new RecordingAuthenticator(Authorization), new FixedRandomSource()).ExecuteAsync(context);

        Assert.IsNotEmpty(log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal) || line.Message.Contains("dXNlcjpzM2NyZXQ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Decode_WithoutALog_WritesNothingAndStillDecodes()
    {
        WsDecodedBytes decoded = new WsFrameDecoder(null).Decode(Bytes("\x88\x02\x03\xf3"));

        Assert.AreEqual("\x03\xf3", Encoding.Latin1.GetString(decoded.Payload));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static WsProtocolHandler Handler(string reply) =>
        new(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(reply)))), new RecordingAuthenticator(), new FixedRandomSource());

    private static TransferContext Context(string url, IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), DiagnosticLog = log };
}
