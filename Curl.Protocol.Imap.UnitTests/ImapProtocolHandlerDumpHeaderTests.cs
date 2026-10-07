using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins what an IMAP transfer writes to the <c>-D</c> stream against curl 8.21.0 (BL-1132):
/// every response line read, tagged, untagged and continuation, byte for byte and in order,
/// up to the transfer's end, but not the <c>LOGOUT</c> answer and not a <c>FETCH</c> literal;
/// and nothing extra under <c>-i</c>. Every case marked measured was recorded from real curl
/// (the Schannel build) on 2026-10-01 with <c>Record-CurlExchange.ps1 -Imap</c>, curl running
/// <c>-s -u u:p -D &lt;file&gt;</c> against the recorder's default replies.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerDumpHeaderTests
{
    private const string Host = "imap://127.0.0.1:18143/";

    private const string Opening = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n"
        + "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n"
        + "+ \r\nA002 OK Authenticated\r\n";

    private const string SelectReply = "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n* 2 EXISTS\r\n* 0 RECENT\r\n"
        + "* OK [UIDVALIDITY 1] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\nA003 OK [READ-WRITE] SELECT completed\r\n";

    private const string ListLines = "* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIST (\\HasNoChildren) \"/\" Sent\r\n";

    private const string Message = "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n";

    private static readonly byte[] PlainMessage = Encoding.Latin1.GetBytes("\0u\0p");

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOnList_WritesEveryLineButLogoutsAndTheListingStillGoesToOutput()
    {
        // Measured: imap://127.0.0.1:18143/, exit 0.
        string dumped = Opening + ListLines + "A003 OK LIST completed\r\n";

        DumpRun run = await RunAsync(Host, dumped + Logout("A004"));

        Diagnostics.Diff("dump header", dumped, run.DumpHeader);
        Diagnostics.Diff("output", ListLines, run.Output);
        Assert.AreEqual(TransferResult.Success(ListLines.Length), run.Result);
        Assert.AreEqual(dumped, run.DumpHeader);
        Assert.AreEqual(ListLines, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOnFetch_WritesTheFetchLineAndTheLineClosingItButNotTheLiteral()
    {
        // Measured: INBOX;UID=1, the file holds "* 1 FETCH (UID 1 BODY[] {100}" and ")" but no message byte.
        string fetchLine = "* 1 FETCH (UID 1 BODY[] {100}\r\n";

        DumpRun run = await RunAsync(
            Host + "INBOX;UID=1", Opening + SelectReply + fetchLine + Message + ")\r\nA004 OK FETCH completed\r\n" + Logout("A005"));

        Diagnostics.Diff("dump header", Opening + SelectReply + fetchLine + ")\r\nA004 OK FETCH completed\r\n", run.DumpHeader);
        Diagnostics.Diff("output", Message, run.Output);
        Assert.AreEqual(TransferResult.Success(100), run.Result);
        Assert.AreEqual(Opening + SelectReply + fetchLine + ")\r\nA004 OK FETCH completed\r\n", run.DumpHeader);
        Assert.AreEqual(Message, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOnSearch_WritesEveryLineAndTheSearchLineStillGoesToOutput()
    {
        // Measured: INBOX?NEW, exit 0.
        string dumped = Opening + SelectReply + "* SEARCH 1 2\r\nA004 OK SEARCH completed\r\n";

        DumpRun run = await RunAsync(Host + "INBOX?NEW", dumped + Logout("A005"));

        Diagnostics.Diff("dump header", dumped, run.DumpHeader);
        Diagnostics.Diff("output", "* SEARCH 1 2\r\n", run.Output);
        Assert.AreEqual(TransferResult.Success(14), run.Result);
        Assert.AreEqual(dumped, run.DumpHeader);
        Assert.AreEqual("* SEARCH 1 2\r\n", run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderAndTheServerHangsUp_WritesTheLinesReadThenFailsWithExit56()
    {
        // Measured with -Response '* OK hi\r\nA001 BAD no\r\n' and no -u: exit 56, the file exactly those bytes.
        DumpRun run = await RunAsync(Host, "* OK hi\r\nA001 BAD no\r\n", withCredentials: false);

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, run.Result.ExitCode);
        Diagnostics.Diff("dump header", "* OK hi\r\nA001 BAD no\r\n", run.DumpHeader);
        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        Assert.AreEqual("* OK hi\r\nA001 BAD no\r\n", run.DumpHeader);
        Assert.AreEqual(string.Empty, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOnAListedLiteral_WritesTheListedLineButNotTheLiteral()
    {
        // A listed literal is body, written to the output only, and ends the listing.
        string listed = "* LIST () \"/\" {5}\r\n";

        DumpRun run = await RunAsync(Host, Opening + listed + "IN\nBX\r\nA003 OK LIST completed\r\n" + Logout("A004"));

        Diagnostics.Diff("dump header", Opening + listed, run.DumpHeader);
        Diagnostics.Diff("output", listed + "IN\nBX", run.Output);
        Assert.AreEqual(TransferResult.Success(24), run.Result);
        Assert.AreEqual(Opening + listed, run.DumpHeader);
        Assert.AreEqual(listed + "IN\nBX", run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOnALiteralReadAsLines_WritesItsBytesAsTheyArrived()
    {
        // A literal in a response the session keeps is read with the line, as curl's pingpong reader reads it.
        string dumped = "* OK ready\r\n* CAPABILITY IMAP4rev1 {4}\r\nab\ncd\r\nA001 OK done\r\nA002 OK LIST completed\r\n";

        DumpRun run = await RunAsync(Host, dumped + Logout("A003"), withCredentials: false);

        Diagnostics.Diff("dump header", dumped, run.DumpHeader);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.AreEqual(dumped, run.DumpHeader);
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludeAlone_OutputGainsNoResponseLine()
    {
        // Measured: curl -s -i against '* OK hi\r\nA001 BAD no\r\n' writes nothing to stdout.
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Host),
            Output = output,
            HeaderOutput = output,
            Credentials = new NetworkCredential("u", "p"),
        };

        Diagnostics.Arrange("url", Host);
        Diagnostics.Arrange("server", DiagnosticText.Escape(Opening + ListLines + "A003 OK LIST completed\r\n" + Logout("A004")));

        ImapRun run = await ImapRun.ExecuteAsync(
            context, new ScriptedConnection(Latin1(Opening + ListLines + "A003 OK LIST completed\r\n" + Logout("A004"))), new FakeSaslAuthenticator("PLAIN", PlainMessage));

        string written = Encoding.Latin1.GetString(output.ToArray());
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("output", DiagnosticText.Escape(written));
        Diagnostics.Diff("output", ListLines, written);
        Assert.AreEqual(TransferResult.Success(ListLines.Length), run.Result);
        Assert.AreEqual(ListLines, Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderRefusesTheGreeting_FailsWithExit23AndSendsNothing()
    {
        // Measured (BL-1138): -D - into a closed pipe, exit 23, "client returned ERROR on write of 66 bytes", nothing sent.
        RefusedRun run = await RunRefusedAsync(writesBeforeFailure: 0);

        Diagnostics.Assert("error message", "client returned ERROR on write of 66 bytes", run.Result.ErrorMessage);
        Diagnostics.Diff("sent", string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "client returned ERROR on write of 66 bytes"), run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderRefusesALaterLine_FailsWithExit23AndSendsNoLogout()
    {
        // Measured (BL-1138): the CAPABILITY line refused, exit 23, "... of 55 bytes", nothing sent after A001 CAPABILITY.
        RefusedRun run = await RunRefusedAsync(writesBeforeFailure: 1);

        Diagnostics.Assert("error message", "client returned ERROR on write of 55 bytes", run.Result.ErrorMessage);
        Diagnostics.Diff("sent", "A001 CAPABILITY\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "client returned ERROR on write of 55 bytes"), run.Result);
        Assert.AreEqual("A001 CAPABILITY\r\n", run.Sent);
    }

    private async Task<RefusedRun> RunRefusedAsync(int writesBeforeFailure)
    {
        Diagnostics.Arrange("url", Host);
        Diagnostics.Arrange("server", DiagnosticText.Escape(Opening + ListLines + "A003 OK LIST completed\r\n" + Logout("A004")));
        Diagnostics.Arrange("writes before the dump-header stream fails", writesBeforeFailure);
        var connection = new ScriptedConnection(Latin1(Opening + ListLines + "A003 OK LIST completed\r\n" + Logout("A004")));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Host),
            Output = new MemoryStream(),
            DumpHeaderOutput = new FailingOutputStream(new IOException("The pipe has been ended.")) { WritesBeforeFailure = writesBeforeFailure },
            Credentials = new NetworkCredential("u", "p"),
        };

        ImapRun run = await ImapRun.ExecuteAsync(context, connection, new FakeSaslAuthenticator("PLAIN", PlainMessage));

        string sent = Encoding.Latin1.GetString(connection.Sent);
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("sent", DiagnosticText.Escape(sent));
        return new RefusedRun(run.Result, sent);
    }

    private sealed record RefusedRun(TransferResult Result, string Sent);

    private static string Logout(string tag) => "* BYE Logging out\r\n" + tag + " OK LOGOUT completed\r\n";

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private async Task<DumpRun> RunAsync(string url, string replies, bool withCredentials = true)
    {
        Diagnostics.Arrange("url", url);
        Diagnostics.Arrange("server", DiagnosticText.Escape(replies));
        Diagnostics.Arrange("credentials", withCredentials);
        var output = new MemoryStream();
        var dumpHeader = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            HeaderOutput = dumpHeader,
            DumpHeaderOutput = dumpHeader,
            Credentials = withCredentials ? new NetworkCredential("u", "p") : null,
        };

        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Latin1(replies)), new FakeSaslAuthenticator("PLAIN", PlainMessage));

        string dumped = Encoding.Latin1.GetString(dumpHeader.ToArray());
        string written = Encoding.Latin1.GetString(output.ToArray());
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("dump header", DiagnosticText.Escape(dumped));
        Diagnostics.Act("output", DiagnosticText.Escape(written));
        return new DumpRun(run.Result, dumped, written);
    }

    private sealed record DumpRun(TransferResult Result, string DumpHeader, string Output);
}
