using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins CGR-166 (BL-3695): curl 8.22.0 sends <c>EPRT</c> despite <c>--disable-eprt</c>
/// when the control connection is IPv6, even with an IPv4 <c>-P</c> address, and falls back
/// to <c>PORT</c> only once that <c>EPRT</c> is refused, as <c>curl -P 127.0.0.1
/// --disable-eprt ftp://[::1]:21917/file.txt</c> was measured to.
/// </summary>
[TestClass]
public sealed class Cgr166Tests
{
    private const string Url = "ftp://[::1]:21917/file.txt";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private const string Retrieved = "200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string RetrieveSent = "TYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";

    private static readonly IPEndPoint Ipv6ControlLocal = new(IPAddress.IPv6Loopback, 55392);

    [TestMethod]
    public async Task ExecuteAsync_DisableEprtOnIpv6ControlConnection_SendsEprt()
    {
        (TransferResult result, string sent) = await RunAsync(LoggedIn + "200 EPRT command successful\r\n" + Retrieved, Ipv6ControlLocal, Pending(55393));

        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|55393|\r\n" + RetrieveSent, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisableEprtWithControlAddressUnknown_SendsPort()
    {
        (TransferResult result, string sent) = await RunAsync(LoggedIn + "200 PORT command successful\r\n" + Retrieved, null, Pending(55393));

        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
        Assert.AreEqual(LogInSent + "PORT 127,0,0,1,216,97\r\n" + RetrieveSent, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisableEprtOnIpv6ControlConnectionEprtRefused_FallsBackToPort()
    {
        (TransferResult result, string sent) = await RunAsync(
            LoggedIn + "500 no\r\n200 PORT command successful\r\n" + Retrieved,
            Ipv6ControlLocal,
            Pending(55397),
            Pending(55398));

        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
        Assert.AreEqual(LogInSent + "EPRT |1|127.0.0.1|55397|\r\nPORT 127,0,0,1,216,102\r\n" + RetrieveSent, sent);
    }

    private static ScriptedPendingConnection Pending(int port) =>
        new(new IPEndPoint(IPAddress.Loopback, port), ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("hello"))));

    // curl -P 127.0.0.1 --disable-eprt ftp://[::1]:21917/file.txt: the -P address is IPv4, and
    // the control connection's own address controlLocal (null when it is unknown).
    private static async Task<(TransferResult Result, string Sent)> RunAsync(string replies, IPEndPoint? controlLocal, params ScriptedPendingConnection[] pending)
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies)) { LocalEndPoint = controlLocal };
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() },
            mutable =>
            {
                mutable.FtpPort = "127.0.0.1";
                mutable.FtpUseEprt = false;
            });
        var handler = new FtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(control)),
            new QueuedListener([.. pending.Select(ListenResult.Listening)]),
            new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(context);
        return (result, Encoding.Latin1.GetString(control.Sent));
    }
}
