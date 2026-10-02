using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP session meets a NUL byte against curl 8.21.0 (Schannel), measured on
/// 2026-10-01 with <c>Record-CurlExchange.ps1</c> (BL-1119): a response line holding one ends
/// the transfer with exit 8 <c>Nul byte in server response line</c>, before <c>-v</c> reports
/// the line and without <c>LOGOUT</c>, as <c>lib/pingpong.c</c> refuses it; a <c>FETCH</c>
/// literal's bytes are body data and pass unchecked.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerNulByteTests
{
    private const string Host = "imap://127.0.0.1:18143/";

    private const string NulByteInLine = "Nul byte in server response line";

    private static readonly TransferResult NulByteFailure = TransferResult.Failure(CurlExitCode.WeirdServerReply, NulByteInLine);

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheGreeting_FailsWithExit8AndReportsNoResponseLine()
    {
        // Measured: GREETING=* OK hel\0lo, exit 8, no < line, nothing sent.
        var events = new RecordingTransferEvents();

        ImapRun run = await ImapRun.ExecuteAsync(Context(Host, events), Connection("* OK hel\0lo\r\n"));

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
        CollectionAssert.AreEqual((string[])["* " + NulByteInLine, "* closing connection #0"], events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheCapabilityCompletion_FailsWithExit8AndSendsNoLogout()
    {
        // Measured: greeting * OK hi, then A001 OK ca\0pa; exit 8, no LOGOUT.
        var events = new RecordingTransferEvents();

        ImapRun run = await ImapRun.ExecuteAsync(Context(Host, events), Connection("* OK hi\r\n", "A001 OK ca\0pa\r\n"));

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual("A001 CAPABILITY\r\n", run.Sent);
        CollectionAssert.AreEqual(
            (string[])["< * OK hi\r\n", "> A001 CAPABILITY\r\n", "* " + NulByteInLine, "* closing connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInAnUntaggedListLine_FailsWithExit8AndSendsNoLogout()
    {
        var events = new RecordingTransferEvents();

        ImapRun run = await ImapRun.ExecuteAsync(
            Context(Host, events),
            Connection("* OK hi\r\n", "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n", "* LIST () \"/\" IN\0BOX\r\nA002 OK LIST completed\r\n"));

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\n", run.Sent);
        Assert.DoesNotContain("< * LIST () \"/\" IN\0BOX\r\n", events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInAFetchLiteral_IsWrittenUnchangedAndSucceeds()
    {
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse(Host + "INBOX;UID=1"), Output = output };

        ImapRun run = await ImapRun.ExecuteAsync(
            context,
            Connection(
                "* OK hi\r\n",
                "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n",
                "A002 OK [READ-WRITE] SELECT completed\r\n",
                "* 1 FETCH (UID 1 BODY[] {5}\r\nhe\0lo)\r\nA003 OK FETCH completed\r\n",
                "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n"));

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual("he\0lo", Encoding.Latin1.GetString(output.ToArray()));
        Assert.EndsWith("A004 LOGOUT\r\n", run.Sent);
    }

    private static TransferContext Context(string url, ITransferEvents events) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Events = events };

    private static ScriptedConnection Connection(params string[] reads) =>
        new([.. reads.Select(Encoding.Latin1.GetBytes)]);
}
