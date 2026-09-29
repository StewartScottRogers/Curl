using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Rtsp.Fakes;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Pins the lines an <c>rtsp://</c> transfer writes to Curl's diagnostic log under the
/// <c>rtsp</c> component (ADR-0222, BL-929): the request's method and <c>CSeq</c>, the reply's
/// status, <c>CSeq</c> and session and the transfer's end at <c>info</c>, a reply head cut
/// short at <c>warning</c>, the failure that ends the transfer at <c>error</c>, and each reply
/// header's name at <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class RtspTransferLogTests
{
    private const string Ok = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: abc;timeout=60\r\nContent-Length: 2\r\n\r\nok";

    [TestMethod]
    public async Task ExecuteAsync_Options_LogsTheRequestTheReplyAndTheEndAtInfo()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(Ok).ExecuteAsync(Context(log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Assert.HasCount(3, info);
        Assert.AreEqual("OPTIONS request sent with CSeq 1", info[0]);
        Assert.AreEqual("reply 200 with CSeq 1, session abc", info[1]);
        Assert.StartsWith("transfer done: 2 bytes in ", info[2]);
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Rtsp));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyWithoutSession_LogsNoSession()
    {
        var log = new RecordingDiagnosticLog();

        await Handler("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n").ExecuteAsync(Context(log));

        Assert.AreEqual("reply 200 with CSeq 1, no session", log.MessagesAt(DiagnosticLogLevel.Info)[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Reply_LogsEachHeaderNameAtVerbose()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(Ok).ExecuteAsync(Context(log));

        CollectionAssert.AreEqual(
            new[] { "reply header CSeq", "reply header Session", "reply header Content-Length" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadCutShort_LogsAWarning()
    {
        var log = new RecordingDiagnosticLog();

        await Handler("RTSP/1.0 200 OK\r\nCSeq: 1\r\nPubl").ExecuteAsync(Context(log));

        CollectionAssert.AreEqual(
            new[] { "server closed before the reply head ended: its last header lines were not read" },
            log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_CSeqMismatch_LogsTheExitCodeAtError()
    {
        var log = new RecordingDiagnosticLog();

        TransferResult result = await Handler("RTSP/1.0 200 OK\r\nCSeq: 7\r\n\r\n").ExecuteAsync(Context(log));

        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "transfer failed with RtspCseqError (exit 85): The CSeq of this request 1 did not match the response 7" },
            log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_LogsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await Handler("RTSP/1.0 200 OK\r\nCSeq: 1\r\nPubl").ExecuteAsync(Context(log));

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtNone_LogsNothingOnFailure()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);

        await Handler("RTSP/1.0 200 OK\r\nCSeq: 7\r\n\r\n").ExecuteAsync(Context(log));

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheDiagnosticLog()
    {
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(Ok))));

        await new RtspProtocolHandler(connector, new RecordingAuthenticator()).ExecuteAsync(Context(log));

        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsInTheUrl_LogNoSecret()
    {
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(Ok))));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("rtsp://user:s3cret@127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("user", "s3cret"),
            DiagnosticLog = log,
        };

        await new RtspProtocolHandler(connector, new RecordingAuthenticator("Basic dXNlcjpzM2NyZXQ=")).ExecuteAsync(context);

        Assert.IsNotEmpty(log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal) || line.Message.Contains("dXNlcjpzM2NyZXQ", StringComparison.Ordinal)));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static RtspProtocolHandler Handler(string reply) =>
        new(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Bytes(reply)))), new RecordingAuthenticator());

    private static TransferContext Context(IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"), Output = new MemoryStream(), DiagnosticLog = log };
}
