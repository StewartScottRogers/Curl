using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins what a <see cref="Pop3Session" /> keeps for authentication (BL-548): the greeting's
/// APOP timestamp and the answer to the last <c>CAPA</c>.
/// </summary>
[TestClass]
public sealed class Pop3SessionTests
{
    [TestMethod]
    public async Task RunAsync_DefaultSession_KeepsTheTimestampAndTheCapabilityLines()
    {
        Pop3Session session = await RunAsync(
            "+OK POP3 ready <1896.697170952@localhost>\r\n+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\n.\r\n+OK Bye\r\n");

        Assert.AreEqual("<1896.697170952@localhost>", session.ApopTimestamp);
        CollectionAssert.AreEqual(
            new[] { "+OK Capability list follows", "USER", "SASL PLAIN LOGIN" },
            session.Capabilities!.Lines.ToArray());
        Assert.IsFalse(session.Capabilities.AdvertisesStls);
    }

    [TestMethod]
    public async Task RunAsync_CapaRefused_KeepsNoCapabilities()
    {
        Pop3Session session = await RunAsync("+OK hi\r\n-ERR no\r\n+OK Bye\r\n");

        Assert.IsNull(session.ApopTimestamp);
        Assert.IsNull(session.Capabilities);
    }

    [TestMethod]
    [DataRow("+OK POP3 ready <1896.697170952@localhost>", "<1896.697170952@localhost>", DisplayName = "the recorder's greeting")]
    [DataRow("+OK a <b> c <d@e>", "<b> c <d@e>", DisplayName = "from the first <")]
    [DataRow("+OK ready <1896.697170952@localhost> now", null, DisplayName = "not at the end")]
    [DataRow("+OK ready <1896.697170952>", null, DisplayName = "no @")]
    [DataRow("+OK ready 1896@localhost>", null, DisplayName = "no <")]
    [DataRow("+OK ready", null, DisplayName = "none")]
    public void Read_Greeting_FindsTheApopTimestamp(string greeting, string? expected)
    {
        Assert.AreEqual(expected, Pop3ApopTimestamp.Read(greeting));
    }

    private static async Task<Pop3Session> RunAsync(string replies)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(replies));
        var context = new TransferContext { Url = CurlUrl.Parse("pop3://127.0.0.1/"), Output = Stream.Null };
        var session = new Pop3Session(
            new Pop3ControlChannel(connection, CancellationToken.None), new QueuedTlsProvider(), context, implicitTls: false);

        Assert.AreEqual(TransferResult.Success(0), await session.RunAsync());
        return session;
    }
}
