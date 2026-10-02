using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins what a POP3 response line holding a NUL byte does against curl 8.21.0 (Schannel),
/// measured on 2026-10-01 with <c>Record-CurlExchange.ps1 -CurlArgs "-v,pop3://127.0.0.1:&lt;port&gt;/"</c>
/// (BL-1120): exit 8 <c>Nul byte in server response line</c>, the line itself not reported for
/// <c>-v</c>, and no <c>QUIT</c>, as <c>Curl_pp_readresp</c> checks every response line. A
/// message body is not a response line and is written as it came.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerNulByteTests
{
    private const string Url = "pop3://127.0.0.1:18110/";

    private const string Greeting = "+OK hi\r\n";

    private const string CapaReply = "+OK\r\nUSER\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string NulByteInLine = "Nul byte in server response line";

    private static readonly TransferResult NulByteFailure = TransferResult.Failure(CurlExitCode.WeirdServerReply, NulByteInLine);

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheGreeting_FailsWithExit8AndReportsNoLine()
    {
        NulByteRun run = await RunAsync(string.Empty, "+OK hel\0lo\r\n");

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
        CollectionAssert.AreEqual(
            (string[])["* " + NulByteInLine, "* closing connection #0"],
            run.Events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheCapaReply_FailsWithExit8AndSendsNoQuit()
    {
        NulByteRun run = await RunAsync(string.Empty, Greeting, "+OK ca\0pa\r\n");

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual(Capa, run.Sent);
        CollectionAssert.AreEqual(
            (string[])["< +OK hi\r\n", "> CAPA\r\n", "* " + NulByteInLine, "* closing connection #0"],
            run.Events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInACapaListLine_FailsWithExit8AndSendsNoQuit()
    {
        NulByteRun run = await RunAsync(string.Empty, Greeting, "+OK\r\nUS\0ER\r\n.\r\n");

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual(Capa, run.Sent);
        CollectionAssert.AreEqual(
            (string[])["< +OK hi\r\n", "> CAPA\r\n", "< +OK\r\n", "* " + NulByteInLine, "* closing connection #0"],
            run.Events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInARetrBody_WritesItUnchangedAndSucceeds()
    {
        NulByteRun run = await RunAsync("1", Greeting, CapaReply, "+OK 6 octets\r\nab\0c\r\n.\r\n", Bye);

        Assert.AreEqual(TransferResult.Success(6), run.Result);
        Assert.AreEqual("ab\0c\r\n", run.Output);
        Assert.AreEqual(Capa + "RETR 1\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheQuitReply_IsIgnoredAndSucceeds()
    {
        NulByteRun run = await RunAsync("1", Greeting, CapaReply, "+OK 3 octets\r\nabc\r\n.\r\n", "+OK B\0ye\r\n");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(Capa + "RETR 1\r\nQUIT\r\n", run.Sent);
    }

    private static async Task<NulByteRun> RunAsync(string path, params string[] reads)
    {
        var events = new RecordingTransferEvents();
        using var output = new MemoryStream();
        var connection = new ScriptedConnection([.. reads.Select(Encoding.Latin1.GetBytes)]);
        var context = new TransferContext { Url = CurlUrl.Parse(Url + path), Output = output, Events = events };

        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider())
            .ExecuteAsync(context);

        return new NulByteRun(
            result, events, Encoding.Latin1.GetString(connection.Sent), Encoding.Latin1.GetString(output.ToArray()));
    }

    private sealed record NulByteRun(TransferResult Result, RecordingTransferEvents Events, string Sent, string Output);
}
