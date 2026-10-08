using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

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
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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

        Diagnostics.AssertValues("result", TransferResult.Success(133), result);
        Assert.AreEqual(TransferResult.Success(133), result);
        Diagnostics.AssertValues("output", Message, output);
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

        Diagnostics.AssertValues("result", TransferResult.Success(2), result);
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

        Diagnostics.AssertValues("output", "abc\r\n", output);
        Assert.AreEqual("abc\r\n", output);
        CollectionAssert.AreEqual((string[])["{ 3", "{ 2"], events.Transcript.Where(line => line.StartsWith('{')).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyCutOff_ReportsWhatWasWrittenAndLeavesTheConnectionIntact()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, Greeting, CapaReply, "+OK\r\nabc\r\n");

        Diagnostics.AssertValues("result", TransferResult.Success(3), result);
        Assert.AreEqual(TransferResult.Success(3), result);
        CollectionAssert.AreEqual(
            (string[])["{ 3", "* Connection #0 to host 127.0.0.1:18110 left intact"], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrRefused_WritesNoMessageAndShutsTheConnectionDown()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, Greeting, CapaReply, "-ERR no such message\r\n", Bye);

        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])[.. OpenedSession, "> RETR 1\r\n", "< -ERR no such message\r\n", "* shutting down connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAfterRetr_WritesTheMessageAndShutsTheConnectionDown()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, Greeting, CapaReply);

        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["> RETR 1\r\n", "* response reading failed (errno: 0)", "* shutting down connection #0"],
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingRefused_WritesTheMessageAndClosesTheConnection()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync("1", null, "-ERR go away\r\n");

        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.WeirdServerReply, result.ExitCode);
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

        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.LoginDenied, result.ExitCode);
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

        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        CollectionAssert.AreEqual((string[])["* closing connection #0"], events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFails_ReportsNoCommand()
    {
        var events = new RecordingTransferEvents();
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { WritesBeforeFailure = 0 };
        var context = new TransferContext { Url = CurlUrl.Parse(Url + "1"), Output = Stream.Null, Events = events };

        Diagnostics.ArrangeRun(Url + "1", connection.Script);
        Diagnostics.Arrange("writes before failure", connection.WritesBeforeFailure);
        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider()).ExecuteAsync(context);
        Diagnostics.ActTransfer(result, events, connection.Sent);

        Diagnostics.AssertValues("events.Transcript.Any(line => line.StartsWith('>'))", false, events.Transcript.Any(line => line.StartsWith('>')));
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith('>')));
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsAccepted_ReportsTheHandshakeAndTheConnectionOpenedAgainBeforeTheSecondCapa()
    {
        // curl 8.21.0 -v -k --ssl-reqd: between "< +OK Begin TLS negotiation" and the second
        // CAPA it writes the TLS lines and the connect's "Established connection" line again
        // (measured, BL-1084).
        var events = new RecordingTransferEvents();
        var tls = new QueuedTlsProvider(ConnectResult.Connected(SecuredSession()));
        var connector = new OpenedReportingConnector(ConnectResult.Connected(StlsAcceptingSession()));

        Diagnostics.Arrange("sessions", $"plaintext {Pop3Diagnostics.Show(StlsAcceptingSession().Script)}, secured {Pop3Diagnostics.Show(SecuredSession().Script)}");
        Diagnostics.Arrange("connector", "reports connection #3 opened");
        TransferResult result = await new Pop3ProtocolHandler(connector, tls).ExecuteAsync(TlsRequiredContext(events));
        Diagnostics.ActTransfer(result, events, []);

        Diagnostics.AssertValues("tls.HandshakeEvents.Single() is events", true, ReferenceEquals(events, tls.HandshakeEvents.Single()));
        Assert.AreSame(events, tls.HandshakeEvents.Single());
        CollectionAssert.AreEqual(
            (string[])[
                "+ opened #3 to 127.0.0.1",
                "< +OK POP3 ready\r\n", "> CAPA\r\n", "< +OK\r\n", "< STLS\r\n", "< .\r\n",
                "> STLS\r\n", "< +OK Begin TLS negotiation\r\n",
                "+ opened #3 to 127.0.0.1",
                "> CAPA\r\n",
            ],
            events.Transcript.Take(10).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsAcceptedAfterAConnectThatReportedNothing_ReportsNoConnectionOpened()
    {
        var events = new RecordingTransferEvents();
        var tls = new QueuedTlsProvider(ConnectResult.Connected(SecuredSession()));

        Diagnostics.Arrange("sessions", $"plaintext {Pop3Diagnostics.Show(StlsAcceptingSession().Script)}, secured {Pop3Diagnostics.Show(SecuredSession().Script)}");
        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(StlsAcceptingSession())), tls).ExecuteAsync(TlsRequiredContext(events));
        Diagnostics.ActTransfer(result, events, []);

        Assert.Contains("< +OK Begin TLS negotiation\r\n", events.Transcript);
        Diagnostics.AssertValues("events.Transcript.Any(line => line.StartsWith('+'))", false, events.Transcript.Any(line => line.StartsWith('+')));
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith('+')));
    }

    private static ScriptedConnection StlsAcceptingSession() =>
        new([.. new[] { Greeting, "+OK\r\nSTLS\r\n.\r\n", "+OK Begin TLS negotiation\r\n" }.Select(Encoding.Latin1.GetBytes)]);

    private static ScriptedConnection SecuredSession() =>
        new([.. new[] { CapaReply, "+OK 0 messages\r\n.\r\n", Bye }.Select(Encoding.Latin1.GetBytes)]);

    private static TransferContext TlsRequiredContext(RecordingTransferEvents events) =>
        new() { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), Events = events, SslLevel = TransportSecurityLevel.Required };

    private async Task<(TransferResult Result, RecordingTransferEvents Events, string Output)> RunAsync(
        string path, NetworkCredential? credentials, params string[] reads)
    {
        var events = new RecordingTransferEvents();
        using var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse(Url + path), Output = output, Events = events, Credentials = credentials };

        TransferResult result = await ExecuteAsync(context, reads);

        return (result, events, Encoding.Latin1.GetString(output.ToArray()));
    }

    private async ValueTask<TransferResult> ExecuteAsync(TransferContext context, params string[] reads)
    {
        var connection = new ScriptedConnection([.. reads.Select(Encoding.Latin1.GetBytes)]);
        Diagnostics.ArrangeRun(context.Url.ToString(), connection.Script, context.SslLevel);
        Diagnostics.Arrange("credentials", context.Credentials is null ? "(none)" : context.Credentials.UserName);
        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider()).ExecuteAsync(context);
        if (context.Events is RecordingTransferEvents events)
        {
            Diagnostics.ActTransfer(result, events, connection.Sent);
        }
        else
        {
            Diagnostics.ActResult(result);
        }

        return result;
    }
}
