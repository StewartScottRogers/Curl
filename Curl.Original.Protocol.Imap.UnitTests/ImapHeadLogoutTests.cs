using System.Text;
using Curl.Original.Protocol.Imap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins BL-4935: <c>-I</c> on a <c>;UID=</c> fetch is exit 8 <c>Weird server reply</c> and curl 8.22.0
/// (Schannel) sends <c>LOGOUT</c> when the server is still open, even when the whole
/// <c>FETCH</c> response arrived in one segment (measured 2026-11-11 against a loopback mock that
/// logged the client's lines); a server that had hung up after its response got none.
/// </summary>
[TestClass]
public sealed class ImapHeadLogoutTests
{
    private const string Opening = "* OK ready\r\n* CAPABILITY IMAP4rev1\r\nA001 OK CAPABILITY completed\r\n";

    private const string SelectReply = "* 1 EXISTS\r\nA002 OK [READ-WRITE] SELECT completed\r\n";

    private const string FetchReply = "* 1 FETCH (UID 1 BODY[] {5}\r\nhello)\r\nA003 OK FETCH completed\r\n";

    private const string SentWithoutLogout = "A001 CAPABILITY\r\nA002 SELECT INBOX\r\nA003 UID FETCH 1 BODY[]\r\n";

    [TestMethod]
    public async Task ExecuteAsync_NoBodyAndFullyBufferedResponseWithServerStillOpen_SendsLogout()
    {
        ImapRun run = await RunAsync(peerStaysOpen: true);

        Assert.AreEqual(SentWithoutLogout + "A004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyAndFullyBufferedResponseWithServerHungUp_SendsNoLogout()
    {
        ImapRun run = await RunAsync(peerStaysOpen: false);

        Assert.AreEqual(SentWithoutLogout, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
    }

    private static Task<ImapRun> RunAsync(bool peerStaysOpen) =>
        ImapRun.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("imap://127.0.0.1:18143/INBOX;UID=1"), Output = new MemoryStream(), NoBody = true },
            new ScriptedConnection(Encoding.Latin1.GetBytes(Opening + SelectReply + FetchReply)) { PeerStaysOpen = peerStaysOpen });
}
