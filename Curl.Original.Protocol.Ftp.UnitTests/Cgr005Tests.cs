using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins third-party gap report issue CGR-005 (BL-3534): over FTPS with <c>PROT P</c> in
/// passive mode the data connection's TLS handshake waits for the transfer command, as curl
/// 8.22.0's does, so a server that starts data-connection TLS only after <c>RETR</c> (vsftpd,
/// proftpd) is not left waiting for a <c>TYPE</c> that never comes. The fake TLS provider
/// records what the control connection had sent when the handshake started.
/// </summary>
[TestClass]
public sealed class Cgr005Tests
{
    public TestContext TestContext { get; set; } = null!;

    private const string LoggedIn = "220 ready\r\n331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n257 \"/\" is current directory\r\n";

    private const string Transfer = "200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_FtpsOverEpsv_RunsTheDataHandshakeAfterRetr()
    {
        string sentAtHandshake = await RunAsync(
            LoggedIn + "229 Entering Extended Passive Mode (|||64396|)\r\n" + Transfer,
            disableEpsv: false);

        Assert.EndsWith("PWD\r\nEPSV\r\nTYPE I\r\nSIZE a.txt\r\nRETR a.txt\r\n", sentAtHandshake);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsOverPasv_RunsTheDataHandshakeAfterRetr()
    {
        string sentAtHandshake = await RunAsync(
            LoggedIn + "227 Entering Passive Mode (127,0,0,1,251,140)\r\n" + Transfer,
            disableEpsv: true);

        Assert.EndsWith("PWD\r\nPASV\r\nTYPE I\r\nSIZE a.txt\r\nRETR a.txt\r\n", sentAtHandshake);
    }

    /// <summary>
    /// Downloads <c>ftps://127.0.0.1/a.txt</c> and returns what the control connection had
    /// sent when the data connection's handshake started.
    /// </summary>
    private async Task<string> RunAsync(string replies, bool disableEpsv)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string url = "ftps://127.0.0.1/a.txt";
        diagnostics.ArrangeFtp(url, replies, "hello");
        var control = ScriptedConnection.FromReplies(replies);
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(new ScriptedConnection()));
        var tls = new QueuedTlsProvider(ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("hello"))));
        string sentAtHandshake = string.Empty;
        tls.BeforeEachHandshake = () => sentAtHandshake = Encoding.Latin1.GetString(control.Sent);
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream(), FtpDisableEpsv = disableEpsv };

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        diagnostics.ActResult(result with { Report = null });
        diagnostics.Act("sent at handshake", FtpDiagnostics.Escape(sentAtHandshake));
        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
        Assert.AreEqual("hello", Encoding.Latin1.GetString(((MemoryStream)context.Output).ToArray()));
        return sentAtHandshake;
    }
}
