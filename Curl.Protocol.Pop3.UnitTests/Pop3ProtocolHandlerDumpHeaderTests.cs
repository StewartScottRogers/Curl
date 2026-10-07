using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins what a POP3 transfer writes to the <c>-D</c> stream against curl 8.21.0 (BL-1133):
/// every response line read, byte for byte with its line end, up to the transfer's end, and
/// never the <c>LIST</c> or <c>RETR</c> body nor <c>QUIT</c>'s answer. Recorded from real curl
/// (the Schannel build) on 2026-10-01 with <c>Record-CurlExchange.ps1 -Pop3</c>, curl running
/// <c>-s -u u:p -D &lt;file&gt;</c>; under <c>-i</c> alone curl wrote nothing beyond the body.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerDumpHeaderTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "pop3://127.0.0.1:18110/";

    private const string Greeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    private const string CapaReply =
        "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string AuthReplies = "+ \r\n+OK Authenticated\r\n";

    /// <summary>Every line curl wrote to <c>-D</c> before the command that fetches.</summary>
    private const string Opening = Greeting + CapaReply + AuthReplies;

    private const string ListStatus = "+OK 2 messages (266 octets)\r\n";

    private const string Listing = "1 133\r\n2 133\r\n";

    private const string RetrStatus = "+OK 133 octets\r\n";

    /// <summary>The recorder's default message, as curl wrote it to standard output.</summary>
    private const string Message =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
        + "Hello from the recorder.\r\n.A line that starts with a dot.\r\n";

    /// <summary>The recorder's <c>RETR 1</c> body: <see cref="Message" /> dot-stuffed, then the terminator.</summary>
    private const string StuffedMessage =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
        + "Hello from the recorder.\r\n..A line that starts with a dot.\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputAndCapaRefused_WritesBothLinesAndFailsWithExit56()
    {
        // Recording: -Response '+OK hi\r\n-ERR no\r\n', curl -s -D <file> pop3://...: exit 56.
        DumpRun run = await RunAsync(Url, credential: null, "+OK hi\r\n-ERR no\r\n");

        Diagnostics.AssertValues("run.Dumped", "+OK hi\r\n-ERR no\r\n", run.Dumped);
        Assert.AreEqual("+OK hi\r\n-ERR no\r\n", run.Dumped);
        Diagnostics.AssertValues("run.Result.ExitCode", CurlExitCode.RecvError, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        Diagnostics.AssertValues("run.Output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputAndList_WritesEveryResponseLineButNotTheListingOrBye()
    {
        DumpRun run = await RunAsync(Url, new NetworkCredential("u", "p"), Opening, ListStatus + Listing + ".\r\n", Bye);

        Diagnostics.AssertValues("run.Dumped", Opening + ListStatus, run.Dumped);
        Assert.AreEqual(Opening + ListStatus, run.Dumped);
        Diagnostics.AssertValues("run.Output", Listing, run.Output);
        Assert.AreEqual(Listing, run.Output);
        Diagnostics.AssertResult(TransferResult.Success(Listing.Length), run.Result);
        Assert.AreEqual(TransferResult.Success(Listing.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputAndRetr_WritesEveryResponseLineButNotTheMessageOrBye()
    {
        DumpRun run = await RunAsync(Url + "1", new NetworkCredential("u", "p"), Opening, RetrStatus + StuffedMessage, Bye);

        Diagnostics.AssertValues("run.Dumped", Opening + RetrStatus, run.Dumped);
        Assert.AreEqual(Opening + RetrStatus, run.Dumped);
        Diagnostics.AssertValues("run.Output", Message, run.Output);
        Assert.AreEqual(Message, run.Output);
        Diagnostics.AssertResult(TransferResult.Success(Message.Length), run.Result);
        Assert.AreEqual(TransferResult.Success(Message.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludeWithoutDumpHeader_WritesOnlyTheMessageToOutput()
    {
        using var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url + "1"),
            Output = output,
            HeaderOutput = output,
            Credentials = new NetworkCredential("u", "p"),
        };

        TransferResult result = await ExecuteAsync(context, Opening, RetrStatus + StuffedMessage, Bye);

        Assert.AreEqual(Message, Encoding.Latin1.GetString(output.ToArray()));
        Diagnostics.AssertValues("result", TransferResult.Success(Message.Length), result);
        Assert.AreEqual(TransferResult.Success(Message.Length), result);
    }

    private async Task<DumpRun> RunAsync(string url, NetworkCredential? credential, params string[] reads)
    {
        using var output = new MemoryStream();
        using var dumped = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            DumpHeaderOutput = dumped,
            Credentials = credential,
        };

        Diagnostics.Arrange("credentials", credential is null ? "(none)" : credential.UserName);

        TransferResult result = await ExecuteAsync(context, reads);

        var run = new DumpRun(result, Encoding.Latin1.GetString(output.ToArray()), Encoding.Latin1.GetString(dumped.ToArray()));
        Diagnostics.Act("output", Pop3Diagnostics.Show(run.Output));
        Diagnostics.Act("dumped", Pop3Diagnostics.Show(run.Dumped));
        return run;
    }

    private async ValueTask<TransferResult> ExecuteAsync(TransferContext context, params string[] reads)
    {
        var connection = new ScriptedConnection([.. reads.Select(Encoding.Latin1.GetBytes)]);
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var sasl = new ScriptedSaslAuthenticator(("PLAIN", ["\0u\0p"]), ("LOGIN", ["u", "p"]));
        Diagnostics.ArrangeRun(context.Url.ToString(), connection.Script, context.SslLevel);
        TransferResult result = await new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), sasl).ExecuteAsync(context);
        Diagnostics.ActResult(result);
        Diagnostics.Act("sent", Pop3Diagnostics.Show(Encoding.Latin1.GetString(connection.Sent)));
        return result;
    }

    private sealed record DumpRun(TransferResult Result, string Output, string Dumped);
}
