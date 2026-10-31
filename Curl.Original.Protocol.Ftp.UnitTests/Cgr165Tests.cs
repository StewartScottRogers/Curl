using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins CGR-165 (BL-3694): under <c>--ssl-reqd</c> curl 8.22.0 keeps a control connection
/// <c>AUTH</c> secured, in TLS, for the next URL, which sends no second <c>AUTH</c> or login,
/// and never keeps one <c>CCC</c> cleared, which it ends with <c>QUIT</c> so the next URL
/// connects anew. The fake TLS provider's secured connections are scripted, so no test
/// touches the network.
/// </summary>
[TestClass]
public sealed class Cgr165Tests
{
    private const string Url = "ftp://127.0.0.1:21900/";

    private const string Greeting = "220 ready\r\n234 AUTH accepted\r\n";

    private const string LoggedInAndProtected = "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n";

    private const string LogInAndProtectSent = "USER anonymous\r\nPASS ftp@example.com\r\nPBSZ 0\r\nPROT P\r\n";

    private const string EntryPath = "257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||64396|)\r\n";

    private const string Sent = "150 Opening\r\n226 Transfer complete\r\n";

    [TestMethod]
    public async Task ExecuteAsync_SecondUrlAfterAuthTls_ReusesTheSecuredControlConnectionAndQuitsOnItAtClose()
    {
        var plain = new KeepingConnection(Scripted(Greeting));
        var secured = Scripted(LoggedInAndProtected + EntryPath + Epsv + "200 Type set\r\n213 5\r\n" + Sent + Epsv + "213 5\r\n" + Sent + "221 Bye\r\n");
        var firstTls = new QueuedTlsProvider(ConnectResult.Connected(secured), ConnectResult.Connected(Scripted("hello")));
        var secondTls = new QueuedTlsProvider(ConnectResult.Connected(Scripted("world")));

        TransferResult first = await RunAsync(plain, reused: false, firstTls, "a.txt");
        TransferResult second = await RunAsync(plain, reused: true, secondTls, "b.txt");
        string sentBeforeClose = Encoding.Latin1.GetString(secured.Sent);
        await plain.CloseAsync();

        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual("AUTH SSL\r\n", Encoding.Latin1.GetString(plain.Script.Sent));
        Assert.AreEqual(
            LogInAndProtectSent + "PWD\r\nEPSV\r\nTYPE I\r\nSIZE a.txt\r\nRETR a.txt\r\nEPSV\r\nSIZE b.txt\r\nRETR b.txt\r\n",
            sentBeforeClose);
        Assert.AreEqual(sentBeforeClose + "QUIT\r\n", Encoding.Latin1.GetString(secured.Sent));
        Assert.HasCount(1, secondTls.Handshakes);
        Assert.AreEqual(2, plain.ReusableMarks);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnectionCccCleared_IsNotKeptAndEndsWithQuit()
    {
        var plain = new KeepingConnection(new ScriptedConnection(
            Encoding.Latin1.GetBytes(Greeting),
            Encoding.Latin1.GetBytes(EntryPath + Epsv + "200 Type set\r\n213 5\r\n" + Sent + "221 Bye\r\n")));
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedInAndProtected + "200 CCC command successful\r\n")) { ClearedTo = plain };
        var tls = new QueuedTlsProvider(ConnectResult.Connected(secured), ConnectResult.Connected(Scripted("hello")));

        TransferResult result = await RunAsync(plain, reused: false, tls, "file.txt", FtpCommandChannelClearing.Passive);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LogInAndProtectSent + "CCC\r\n", Encoding.Latin1.GetString(secured.Sent));
        Assert.AreEqual("AUTH SSL\r\nPWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n", Encoding.Latin1.GetString(plain.Script.Sent));
        Assert.AreEqual(0, plain.ReusableMarks);
        Assert.IsNull(plain.Session);
    }

    private static ScriptedConnection Scripted(string text) => new(Encoding.Latin1.GetBytes(text));

    // ftp:// under --ssl-reqd: the pooled control connection answers AUTH SSL, and the TLS
    // provider hands back the secured control connection and each data connection's bytes.
    private static async Task<TransferResult> RunAsync(KeepingConnection control, bool reused, QueuedTlsProvider tls, string file, FtpCommandChannelClearing clearing = FtpCommandChannelClearing.Off)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(control, null, isReused: reused), ConnectResult.Connected(new ScriptedConnection()));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url + file), Output = new MemoryStream() },
            mutable =>
            {
                mutable.SslLevel = TransportSecurityLevel.Required;
                mutable.FtpCommandChannelClearing = clearing;
            });

        return await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);
    }
}
