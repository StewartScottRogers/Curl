using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap.Fakes;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// CGR-239: a URL that needs another login leaves the first URL's kept connection open, as curl
/// 8.22.0 does (upstream test 836), and it sends <c>LOGOUT</c> only when the run's connection
/// cache closes it, after the second URL's session.
/// </summary>
[TestClass]
public sealed class Cgr239Tests
{
    private const string Greeting = "* OK ready\r\n";

    private const string Capability = "* CAPABILITY IMAP4rev1\r\nA001 OK CAPABILITY completed\r\n";

    [TestMethod]
    public async Task ExecuteAsync_SecondUserOnTheSameHost_LogsTheFirstOutOnlyWhenTheRunEnds()
    {
        var first = new PoolingScriptedConnection(
            Read(Greeting), Read(Capability), Read("A002 OK LOGIN completed\r\n"), Read("A003 OK SELECT completed\r\n"),
            Read("* 1 FETCH (BODY[] {1}\r\na)\r\nA004 OK FETCH completed\r\n"), Read("* BYE\r\nA005 OK LOGOUT completed\r\n"));
        var second = new PoolingScriptedConnection(
            Read(Greeting), Read(Capability.Replace("A001", "B001", StringComparison.Ordinal)), Read("B002 OK LOGIN completed\r\n"),
            Read("B003 OK SELECT completed\r\n"), Read("* 2 FETCH (BODY[] {1}\r\nb)\r\nB004 OK FETCH completed\r\n"));
        var connector = new QueuedConnector(
            ConnectResult.Connected(first, null), ConnectResult.Connected(first, null, isReused: true), ConnectResult.Connected(second, null, connectionNumber: 1));
        var handler = new ImapProtocolHandler(connector, new QueuedTlsProvider());

        await handler.ExecuteAsync(Context("1", "user.one"));
        TransferResult result = await handler.ExecuteAsync(Context("2", "user.two"));

        Assert.AreEqual(TransferResult.Success(1), result);
        Assert.IsFalse(first.IsClosed);
        Assert.IsFalse(Encoding.Latin1.GetString(first.Sent).Contains("LOGOUT", StringComparison.Ordinal));
        Assert.AreEqual(
            "B001 CAPABILITY\r\nB002 LOGIN user.two secret\r\nB003 SELECT 836\r\nB004 FETCH 2 BODY[]\r\n",
            Encoding.Latin1.GetString(second.Sent));

        await first.CloseAsync();

        StringAssert.EndsWith(Encoding.Latin1.GetString(first.Sent), "A004 FETCH 1 BODY[]\r\nA005 LOGOUT\r\n");
    }

    private static TransferContext Context(string index, string user) => new()
    {
        Url = CurlUrl.Parse("imap://127.0.0.1/836/;MAILINDEX=" + index),
        Output = Stream.Null,
        Credentials = new NetworkCredential(user, "secret"),
    };

    private static byte[] Read(string text) => Encoding.Latin1.GetBytes(text);
}
