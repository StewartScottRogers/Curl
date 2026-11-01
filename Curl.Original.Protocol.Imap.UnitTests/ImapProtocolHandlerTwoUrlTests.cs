using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap.Fakes;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins the command log of two fetches from one IMAP mailbox against curl 8.22.0 (BL-3167),
/// measured 2026-10-22 with <c>Record-CurlExchange.ps1 -Imap</c>, curl (Schannel) running
/// <c>-sS -u u:p imap://127.0.0.1:18117/INBOX/;MAILINDEX=1 imap://127.0.0.1:18117/INBOX/;MAILINDEX=2</c>:
/// <c>A001 CAPABILITY</c>, the login as <c>A002</c>, <c>A003 SELECT INBOX</c>,
/// <c>A004 FETCH 1 BODY[]</c>, <c>A005 FETCH 2 BODY[]</c> and <c>A006 LOGOUT</c> on one
/// connection. The recorder offered <c>AUTH=PLAIN</c>, so curl logged in with
/// <c>AUTHENTICATE PLAIN</c>; this server offers no mechanism, so the login is <c>LOGIN</c>, as
/// curl sends then, and every other command is the measured one.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerTwoUrlTests
{
    [TestMethod]
    public async Task ExecuteAsync_TwoMessagesFromTheSameMailbox_LogInSelectAndLogOutOnceAsCurlDoes()
    {
        var connection = new PoolingScriptedConnection(
            Read("* OK ready\r\n"), Read("* CAPABILITY IMAP4rev1\r\nA001 OK CAPABILITY completed\r\n"), Read("A002 OK LOGIN completed\r\n"),
            Read("A003 OK [READ-WRITE] SELECT completed\r\n"), Read("* 1 FETCH (BODY[] {1}\r\na)\r\nA004 OK FETCH completed\r\n"),
            Read("* 2 FETCH (BODY[] {1}\r\nb)\r\nA005 OK FETCH completed\r\n"), Read("* BYE\r\nA006 OK LOGOUT completed\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null), ConnectResult.Connected(connection, null, isReused: true));
        var handler = new ImapProtocolHandler(connector, new QueuedTlsProvider());

        TransferResult first = await handler.ExecuteAsync(Context("1"));
        TransferResult second = await handler.ExecuteAsync(Context("2"));
        await connection.CloseAsync();

        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(
            "A001 CAPABILITY\r\nA002 LOGIN u p\r\nA003 SELECT INBOX\r\nA004 FETCH 1 BODY[]\r\nA005 FETCH 2 BODY[]\r\nA006 LOGOUT\r\n",
            Encoding.Latin1.GetString(connection.Sent));
    }

    private static TransferContext Context(string index) => new()
    {
        Url = CurlUrl.Parse("imap://127.0.0.1:18117/INBOX/;MAILINDEX=" + index),
        Output = Stream.Null,
        Credentials = new NetworkCredential("u", "p"),
    };

    private static byte[] Read(string text) => Encoding.Latin1.GetBytes(text);
}
