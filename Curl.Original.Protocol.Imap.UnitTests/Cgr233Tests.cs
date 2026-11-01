using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap.Fakes;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins CGR-233: <c>-I</c> on a <c>UID FETCH</c> is exit 8 <c>Weird server reply</c> at the
/// literal, and curl 8.22.0 (Schannel) then sends <c>LOGOUT</c> when the tagged completion has
/// not yet arrived with the literal. Measured on 2026-10-28 with
/// <c>Record-CurlExchange.ps1 -Script</c>, <c>-s -S -u u:p -I imap://127.0.0.1/INBOX;UID=1</c>:
/// the literal split from the rest of the response, in the middle or after its last byte,
/// sent <c>A005 LOGOUT</c>; the whole response in one segment (the recorder's <c>-Imap</c>
/// mode) sent none, which <c>ImapProtocolHandlerBodyLimitTests</c> keeps pinned.
/// </summary>
[TestClass]
public sealed class Cgr233Tests
{
    private const string Opening = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n"
        + "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n";

    private const string SelectReply = "* 2 EXISTS\r\n* OK [UIDVALIDITY 1] UIDs valid\r\nA002 OK [READ-WRITE] SELECT completed\r\n";

    private const string FetchHeader = "* 1 FETCH (UID 1 BODY[] {10}\r\n";

    private const string Completion = ")\r\nA003 OK FETCH completed\r\n";

    private const string LogoutReply = "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n";

    private const string Weird = "Weird server reply";

    [TestMethod]
    [DataRow("01234", "56789", DisplayName = "literal split in the middle (measured)")]
    [DataRow("0123456789", "", DisplayName = "literal ends its segment (measured)")]
    public async Task ExecuteAsync_NoBodyAndCompletionNotYetArrived_LogsOutAfterWeirdServerReply(string firstPart, string secondPart)
    {
        ImapRun run = await RunAsync(Encoding.Latin1.GetBytes(Opening + SelectReply), Encoding.Latin1.GetBytes(FetchHeader + firstPart),
            Encoding.Latin1.GetBytes(secondPart + Completion + LogoutReply));

        Assert.AreEqual("A001 CAPABILITY\r\nA002 SELECT INBOX\r\nA003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, Weird), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyAndCompletionArrivedWithTheLiteral_SendsNoLogout()
    {
        ImapRun run = await RunAsync(Encoding.Latin1.GetBytes(Opening + SelectReply), Encoding.Latin1.GetBytes(FetchHeader + "0123456789" + Completion + LogoutReply));

        Assert.AreEqual("A001 CAPABILITY\r\nA002 SELECT INBOX\r\nA003 UID FETCH 1 BODY[]\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, Weird), run.Result);
    }

    private static Task<ImapRun> RunAsync(params byte[][] reads) =>
        ImapRun.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("imap://127.0.0.1:18143/INBOX;UID=1"), Output = new MemoryStream(), NoBody = true },
            new ScriptedConnection(reads));
}
