using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-100 (BL-3629): curl 8.21.0 reads a <c>-P</c> value that starts with a colon, such as
/// <c>::1</c>, as no address, so it announces the control connection's own address, measured
/// against Record-CurlExchange.ps1 -Ftp: <c>-P ::1</c> and <c>-P ::1:5000</c> on a
/// 127.0.0.1 control connection sent <c>EPRT |1|127.0.0.1|&lt;any port&gt;|</c>, while
/// <c>-P 0::1</c> and <c>-P [::1]</c> sent <c>EPRT |2|::1|&lt;any port&gt;|</c>.
/// </summary>
[TestClass]
public sealed class Cgr100Tests
{
    private const string Url = "ftp://127.0.0.1:20964/file.txt";

    private const string Replies = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "200 EPRT command successful\r\n200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    [DataRow("::1", DisplayName = "::1")]
    [DataRow("::1:5000", DisplayName = "::1:5000")]
    public async Task ExecuteAsync_PortValueStartsWithColon_AnnouncesTheControlConnectionsAddress(string port)
    {
        (TransferResult result, string sent, ListenTarget target) = await RunAsync(port, IPAddress.Loopback);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.Contains(sent, "EPRT |1|127.0.0.1|52273|\r\n");
        Assert.AreEqual(new ListenTarget(IPAddress.Loopback, 0, 0), target);
    }

    [TestMethod]
    [DataRow("0::1", DisplayName = "0::1")]
    [DataRow("[::1]", DisplayName = "[::1]")]
    public async Task ExecuteAsync_IPv6LiteralNotStartingWithColon_StillAnnouncesIt(string port)
    {
        (TransferResult result, string sent, ListenTarget target) = await RunAsync(port, IPAddress.IPv6Loopback);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.Contains(sent, "EPRT |2|::1|52273|\r\n");
        Assert.AreEqual(IPAddress.IPv6Loopback, target.Address);
    }

    [TestMethod]
    [DataRow("::1", 0, 0, DisplayName = "::1 is any port")]
    [DataRow(":40000", 40000, 40000, DisplayName = ":40000 is that port")]
    [DataRow(":40000-40010", 40000, 40010, DisplayName = ":40000-40010 is that range")]
    public void Parse_ValueStartsWithColon_HasNoAddress(string value, int low, int high) =>
        Assert.AreEqual(new FtpPortArgument(string.Empty, low, high), FtpPortArgument.Parse(value));

    private static async Task<(TransferResult Result, string Sent, ListenTarget Target)> RunAsync(string port, IPAddress listeningAddress)
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Replies)) { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55129) };
        var data = new ScriptedConnection(Encoding.Latin1.GetBytes("hello"));
        var pending = new ScriptedPendingConnection(new IPEndPoint(listeningAddress, 52273), ConnectResult.Connected(data));
        var listener = new QueuedListener(ListenResult.Listening(pending));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() },
            mutable => mutable.FtpPort = port);
        var handler = new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control)), listener, new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, Encoding.Latin1.GetString(control.Sent), listener.Targets[0]);
    }
}
