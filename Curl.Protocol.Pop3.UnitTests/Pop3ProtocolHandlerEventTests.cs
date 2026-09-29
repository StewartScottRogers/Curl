using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins what a POP3 transfer reports for <c>-v</c> and <c>--trace</c> against curl 8.21.0
/// (mingw, Schannel), recorded on 2026-09-28 with <c>Record-CurlExchange.ps1 -Pop3</c>
/// (BL-552 Notes): each command as a request header with its CRLF, each response line as a
/// response header with its line end, each piece of the body curl writes as its own data
/// event, <c>QUIT</c> not at all, and the closing lines.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerEventTests
{
    private const string Url = "pop3://127.0.0.1:18110/";

    private const string Greeting = "+OK POP3 ready\r\n";

    private const string CapaReply = "+OK\r\nUSER\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Message =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n.A line that starts with a dot.\r\n";

    private static readonly string[] OpenedSession = ["< +OK POP3 ready\r\n", "> CAPA\r\n", "< +OK\r\n", "< USER\r\n", "< .\r\n"];

    [TestMethod]
    public async Task ExecuteAsync_Retr_ReportsEachLineAndEachBodyPieceAndLeavesTheConnectionIntact()
    {
        const string retrReply =
            "+OK 133 octets\r\nFrom: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
            + "Hello from the recorder.\r\n..A line that starts with a dot.\r\n.\r\n";

        (TransferResult result, RecordingTransferEvents events, string output) = await RunAsync("1", null, Greeting, CapaReply, retrReply, Bye);

        Assert.AreEqual(TransferResult.Success(133), result);
        Assert.AreEqual(Message, output);
        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                "> RETR 1\r\n", "< +OK 133 octets\r\n",
                "{ 24", "{ 2", "{ 25", "{ 2", "{ 17", "{ 2", "{ 2", "{ 24", "{ 2", "{ 31", "{ 2",
                "* Connection #0 to host 127.0.0.1:18110 left intact",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserAndPass_ReportsThePasswordUnmasked()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync(
            string.Empty, new NetworkCredential("user", "secret"), Greeting, CapaReply, "+OK User accepted\r\n", "+OK Logged in\r\n", "+OK 0 messages\r\n.\r\n", Bye);

        Assert.AreEqual(TransferResult.Success(2), result);
        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                "> USER user\r\n", "< +OK User accepted\r\n",
                "> PASS secret\r\n", "< +OK Logged in\r\n",
                "> LIST\r\n", "< +OK 0 messages\r\n", "{ 2",
                "* Connection #0 to host 127.0.0.1:18110 left intact",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodySplitAcrossReadsBeforeItsCrlf_ReportsThePiecesOfEachRead()
    {
        (_, RecordingTransferEvents events, string output) = await RunAsync("1", null, Greeting, CapaReply, "+OK\r\nabc", "\r\n.\r\n", Bye);

        Assert.AreEqual("abc\r\n", output);
        CollectionAssert.AreEqual((string[])["{ 3", "{ 2"], events.Transcript.Where(line => line.StartsWith('{')).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyCutOff_ReportsWhatWasWrittenAndLeavesTheConnectionIntact()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, Greeting, CapaReply, "+OK\r\nabc\r\n");

        Assert.AreEqual(TransferResult.Success(3), result);
        CollectionAssert.AreEqual(
            (string[])["{ 3", "* Connection #0 to host 127.0.0.1:18110 left intact"], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrRefused_WritesNoMessageAndShutsTheConnectionDown()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, Greeting, CapaReply, "-ERR no such message\r\n", Bye);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])[.. OpenedSession, "> RETR 1\r\n", "< -ERR no such message\r\n", "* shutting down connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAfterRetr_WritesTheMessageAndShutsTheConnectionDown()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, Greeting, CapaReply);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["> RETR 1\r\n", "* response reading failed (errno: 0)", "* shutting down connection #0"],
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingRefused_WritesTheMessageAndClosesTheConnection()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, "-ERR go away\r\n");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["< -ERR go away\r\n", "* Got unexpected pop3-server response", "* closing connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassRefused_WritesTheMessageAndClosesTheConnection()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync(
            "1", new NetworkCredential("user", "secret"), Greeting, CapaReply, "+OK User accepted\r\n", "-ERR denied\r\n");

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["< -ERR denied\r\n", "* Access denied. -", "* closing connection #0"],
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_BadLoginOptions_WritesOnlyTheClosingLine()
    {
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url + "1"),
            Output = Stream.Null,
            Events = events,
            Mail = new MailRequestOptions { LoginOptions = "BAD" },
        };

        TransferResult result = await ExecuteAsync(context, Greeting);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        CollectionAssert.AreEqual((string[])["* closing connection #0"], events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFails_ReportsNoCommand()
    {
        var events = new RecordingTransferEvents();
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { WritesBeforeFailure = 0 };
        var context = new TransferContext { Url = CurlUrl.Parse(Url + "1"), Output = Stream.Null, Events = events };

        await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider()).ExecuteAsync(context);

        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith('>')));
    }

    private static async Task<(TransferResult Result, RecordingTransferEvents Events, string Output)> RunAsync(
        string path, NetworkCredential? credentials, params string[] reads)
    {
        var events = new RecordingTransferEvents();
        using var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse(Url + path), Output = output, Events = events, Credentials = credentials };

        TransferResult result = await ExecuteAsync(context, reads);

        return (result, events, Encoding.Latin1.GetString(output.ToArray()));
    }

    private static ValueTask<TransferResult> ExecuteAsync(TransferContext context, params string[] reads)
    {
        var connection = new ScriptedConnection([.. reads.Select(Encoding.Latin1.GetBytes)]);
        return new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider()).ExecuteAsync(context);
    }
}
