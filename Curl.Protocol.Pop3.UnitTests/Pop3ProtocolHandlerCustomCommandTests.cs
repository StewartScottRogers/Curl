using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins <c>-X</c>, <c>-l</c> and <c>-I</c> on POP3 against curl 8.21.0: the command each sends,
/// whether its answer is written, and each outcome. Every case was recorded from real curl (the
/// Schannel build) on 2026-09-28 with <c>Record-CurlExchange.ps1 -Pop3</c>, curl running
/// <c>-sS -u u:p</c> (BL-550 Notes); the login measured before each command is left out here,
/// as no credential means no login.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerCustomCommandTests
{
    private const string Opening =
        "+OK POP3 ready <1896.697170952@localhost>\r\n"
        + "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string RetrReply =
        "+OK 133 octets\r\nFrom: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
        + "Hello from the recorder.\r\n..A line that starts with a dot.\r\n.\r\n";

    private const string Message =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
        + "Hello from the recorder.\r\n.A line that starts with a dot.\r\n";

    private const string TopReply =
        "+OK Top of message follows\r\nFrom: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n.\r\n";

    private const string Headers = "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string Quit = "QUIT\r\n";

    private const string Url = "pop3://127.0.0.1:18110/";

    [TestMethod]
    [DataRow("DELE", "1", "DELE 1", "+OK Message deleted", DisplayName = "-X DELE /1")]
    [DataRow("dele", "1", "dele 1", "+OK Message deleted", DisplayName = "-X dele /1")]
    [DataRow("DELE ", "1", "DELE  1", "+OK Message deleted", DisplayName = "-X 'DELE ' /1")]
    [DataRow("DELE", "", "DELE", "+OK Message deleted", DisplayName = "-X DELE /")]
    [DataRow("NOOP", "", "NOOP", "+OK", DisplayName = "-X NOOP")]
    [DataRow("RSET", "", "RSET", "+OK", DisplayName = "-X RSET")]
    [DataRow("STAT", "", "STAT", "+OK 2 266", DisplayName = "-X STAT")]
    [DataRow("USER x", "", "USER x", "+OK User accepted", DisplayName = "-X 'USER x'")]
    [DataRow("APOP", "", "APOP", "+OK Logged in", DisplayName = "-X APOP")]
    [DataRow("PASS", "", "PASS", "+OK Logged in", DisplayName = "-X PASS")]
    [DataRow("UTF8", "", "UTF8", "+OK", DisplayName = "-X UTF8")]
    [DataRow("LIST 1", "", "LIST 1", "+OK 1 133", DisplayName = "-X 'LIST 1'")]
    [DataRow("uidl 1", "", "uidl 1", "+OK 1 uid-1", DisplayName = "-X 'uidl 1'")]
    public async Task ExecuteAsync_SingleLineCustomCommand_ReadsOnlyTheStatusLineWritesNothingAndQuits(
        string custom, string id, string line, string reply)
    {
        Pop3Run run = await RunAsync(Url + id, Mail(custom), Opening, reply + "\r\n", Bye);

        Assert.AreEqual(Capa + line + "\r\n" + Quit, run.Sent);
        Assert.IsEmpty(run.Output);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("UIDL", "", "UIDL", "+OK Unique-ID listing follows\r\n1 uid-1\r\n2 uid-2\r\n.\r\n", "1 uid-1\r\n2 uid-2\r\n", DisplayName = "-X UIDL")]
    [DataRow("TOP 1 0", "", "TOP 1 0", TopReply, Headers, DisplayName = "-X 'TOP 1 0'")]
    [DataRow("TOP%201 0", "", "TOP 1 0", TopReply, Headers, DisplayName = "-X 'TOP%201 0' is percent-decoded")]
    [DataRow("FOO", "", "FOO", "+OK\r\nbar\r\n.\r\n", "bar\r\n", DisplayName = "-X FOO: unknown, so a body")]
    [DataRow("capa", "", "capa", "+OK\r\nX\r\n.\r\n", "X\r\n", DisplayName = "-X capa")]
    public async Task ExecuteAsync_MultiLineCustomCommand_WritesTheBodyAndQuits(
        string custom, string id, string line, string reply, string written)
    {
        Pop3Run run = await RunAsync(Url + id, Mail(custom), Opening, reply, Bye);

        Assert.AreEqual(Capa + line + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(written, Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual(TransferResult.Success(written.Length), run.Result);
    }

    [TestMethod]
    [DataRow("UIDL", "UIDL 1", "+OK 1 uid-1", DisplayName = "-X UIDL /1")]
    [DataRow("LIST", "LIST 1", "+OK 1 133", DisplayName = "-X LIST /1")]
    [DataRow("DELEX", "DELEX 1", "+OK", DisplayName = "-X DELEX /1")]
    [DataRow("MSG", "MSG 1", "+OK", DisplayName = "-X MSG /1")]
    [DataRow("XTND", "XTND 1", "+OK", DisplayName = "-X XTND /1")]
    [DataRow("TOP", "TOP 1", "+OK", DisplayName = "-X TOP /1")]
    public async Task ExecuteAsync_BodyExpectedButOnlyAStatusLineComes_WaitsForTheBodyUntilTheServerHangsUp(
        string custom, string line, string reply)
    {
        // Measured: curl waited until the recorder hung up, then exit 0 with nothing written and no QUIT.
        Pop3Run run = await RunAsync(Url + "1", Mail(custom), Opening, reply + "\r\n");

        Assert.AreEqual(Capa + line + "\r\n", run.Sent);
        Assert.IsEmpty(run.Output);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("DELE", "9", "DELE 9", "-ERR no such message", DisplayName = "-X DELE /9 answered -ERR")]
    [DataRow("UIDL", "", "UIDL", "-ERR nope", DisplayName = "-X UIDL answered -ERR")]
    [DataRow("DELEX", "1", "DELEX 1", "-ERR Command not recognized", DisplayName = "-X DELEX /1 answered -ERR")]
    public async Task ExecuteAsync_CustomCommandNotAnsweredOk_FailsWithExit8AndStillQuits(
        string custom, string id, string line, string reply)
    {
        Pop3Run run = await RunAsync(Url + id, Mail(custom), Opening, reply + "\r\n", Bye);

        Assert.AreEqual(Capa + line + "\r\n" + Quit, run.Sent);
        Assert.IsEmpty(run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomCommandWithAControlCharacter_FailsWithExit3AndStillQuits()
    {
        Pop3Run run = await RunAsync(Url, Mail("DELE%0A1"), Opening, Bye);

        Assert.AreEqual(Capa + Quit, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyCustomCommand_SendsTheDefaultCommand()
    {
        Pop3Run run = await RunAsync(Url + "1", Mail(string.Empty), Opening, RetrReply, Bye);

        Assert.AreEqual(Capa + "RETR 1\r\n" + Quit, run.Sent);
        Assert.AreEqual(Message, Encoding.Latin1.GetString(run.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyOnAMessage_SendsListWithTheIdAndWritesNothing()
    {
        Pop3Run run = await RunAsync(Url + "1", new Flags(ListOnly: true), Opening, "+OK 1 133\r\n", Bye);

        Assert.AreEqual(Capa + "LIST 1\r\n" + Quit, run.Sent);
        Assert.IsEmpty(run.Output);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyAnsweredErr_FailsWithExit8AndStillQuits()
    {
        Pop3Run run = await RunAsync(Url + "1", new Flags(ListOnly: true), Opening, "-ERR nope\r\n", Bye);

        Assert.AreEqual(Capa + "LIST 1\r\n" + Quit, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyOnTheMaildrop_SendsListAndWritesTheListing()
    {
        Pop3Run run = await RunAsync(
            Url, new Flags(ListOnly: true), Opening, "+OK 2 messages (266 octets)\r\n1 133\r\n2 133\r\n.\r\n", Bye);

        Assert.AreEqual(Capa + "LIST\r\n" + Quit, run.Sent);
        Assert.AreEqual("1 133\r\n2 133\r\n", Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual(TransferResult.Success(14), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyWithACustomCommandOnTheMaildrop_WritesItsBody()
    {
        Pop3Run run = await RunAsync(Url, Mail("TOP 1 0", listOnly: true), Opening, TopReply, Bye);

        Assert.AreEqual(Capa + "TOP 1 0\r\n" + Quit, run.Sent);
        Assert.AreEqual(Headers, Encoding.Latin1.GetString(run.Output));
    }

    [TestMethod]
    [DataRow("DELE", "DELE 1", "+OK Message deleted\r\n", DisplayName = "-l -X DELE /1")]
    [DataRow("UIDL", "UIDL 1", "+OK 1 uid-1\r\n", DisplayName = "-l -X UIDL /1")]
    [DataRow("RETR", "RETR 1", RetrReply, DisplayName = "-l -X RETR /1: the body is left unread")]
    public async Task ExecuteAsync_ListOnlyWithACustomCommandOnAMessage_WritesNothingAndQuits(string custom, string line, string reply)
    {
        Pop3Run run = await RunAsync(Url + "1", Mail(custom, listOnly: true), Opening, reply, Bye);

        Assert.AreEqual(Capa + line + "\r\n" + Quit, run.Sent);
        Assert.IsEmpty(run.Output);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("1", "RETR 1", RetrReply, Message, DisplayName = "-I /1")]
    [DataRow("", "LIST", "+OK 2 messages (266 octets)\r\n1 133\r\n2 133\r\n.\r\n", "1 133\r\n2 133\r\n", DisplayName = "-I /")]
    public async Task ExecuteAsync_NoBody_ChangesNothing(string id, string line, string reply, string written)
    {
        Pop3Run run = await RunAsync(Url + id, new Flags(NoBody: true), Opening, reply, Bye);

        Assert.AreEqual(Capa + line + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(written, Encoding.Latin1.GetString(run.Output));
    }

    private static Flags Mail(string custom, bool listOnly = false) =>
        new(new MailRequestOptions { CustomCommand = custom }, listOnly);

    /// <summary>
    /// Runs <paramref name="url" /> with <paramref name="flags" />'s <c>-X</c>, <c>-l</c> and
    /// <c>-I</c> against a server that sends each of <paramref name="reads" /> as one read.
    /// </summary>
    private static async Task<Pop3Run> RunAsync(string url, Flags flags, params string[] reads)
    {
        var connection = new ScriptedConnection([.. reads.Select(Encoding.Latin1.GetBytes)]);
        using var output = new MemoryStream();
        var progress = new RecordingProgress();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Progress = progress,
            Mail = flags.Mail,
            ListOnly = flags.ListOnly,
            NoBody = flags.NoBody,
        };
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider();

        TransferResult result = await new Pop3ProtocolHandler(connector, tls).ExecuteAsync(context);

        return new Pop3Run(result, connection, connector, tls, output.ToArray(), progress);
    }

    /// <summary>The <c>-X</c> command, <c>-l</c> and <c>-I</c> a run is given.</summary>
    private sealed record Flags(MailRequestOptions? Mail = null, bool ListOnly = false, bool NoBody = false);
}
