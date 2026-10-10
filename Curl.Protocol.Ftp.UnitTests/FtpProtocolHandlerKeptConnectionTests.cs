using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins how <see cref="FtpProtocolHandler" /> keeps the control connection for the next URL
/// and sends <c>QUIT</c> only when the run's connection cache closes it, as curl 8.21.0 does
/// in upstream tests 146, 215, 1010 and 1225 (BL-1981).
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerKeptConnectionTests
{
    private const string Host = "ftp://127.0.0.1:18321";

    private const string LoggedIn = "220 ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||61744|)\r\n";

    private const string Ok = "250 OK\r\n";

    private const string Retrieved = Epsv + "200 Type set\r\n213 1\r\n150 Opening\r\n226 Transfer complete\r\n";

    private const string RetrievedAgain = Epsv + "213 1\r\n150 Opening\r\n226 Transfer complete\r\n";

    private const string Listed = Epsv + "200 Type set\r\n150 Opening\r\n226 Transfer complete\r\n";

    private const string ListedAgain = Epsv + "150 Opening\r\n226 Transfer complete\r\n";

    private const string LoginSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    [TestMethod]
    public async Task ExecuteAsync_SecondUrlOnAnotherPath_ReusesTheConnectionWithCwdToTheEntryPathAndQuitsOnlyAtClose()
    {
        string replies = LoggedIn + Ok + Ok + Ok + Retrieved + Ok + RetrievedAgain + "221 Bye\r\n";
        var control = new KeepingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        var script = new Script(control);

        TransferResult first = await script.RunAsync(Host + "/first/dir/here/146", reused: false);
        TransferResult second = await script.RunAsync(Host + "/146", reused: true);
        string sentBeforeClose = script.Sent;
        await control.CloseAsync();

        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(
            LoginSent + "CWD first\r\nCWD dir\r\nCWD here\r\nEPSV\r\nTYPE I\r\nSIZE 146\r\nRETR 146\r\nCWD /\r\nEPSV\r\nSIZE 146\r\nRETR 146\r\n",
            sentBeforeClose);
        Assert.AreEqual(sentBeforeClose + "QUIT\r\n", script.Sent);
        Assert.AreEqual(2, control.ReusableMarks);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondUrlOnTheSamePath_SendsNoCwdAndNoType()
    {
        string replies = LoggedIn + Ok + Ok + Ok + Listed + ListedAgain;
        var control = new KeepingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        var script = new Script(control);

        await script.RunAsync(Host + "/a/path/215/", reused: false);
        TransferResult second = await script.RunAsync(Host + "/a/path/215/", reused: true);

        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(LoginSent + "CWD a\r\nCWD path\r\nCWD 215\r\nEPSV\r\nTYPE A\r\nLIST\r\nEPSV\r\nLIST\r\n", script.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondAbsoluteNoCwdUrl_SendsNoCwd()
    {
        string replies = LoggedIn + Listed + ListedAgain;
        var control = new KeepingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        var script = new Script(control) { FileMethod = FtpFileMethod.NoCwd };

        await script.RunAsync(Host + "//list/1010/", reused: false);
        TransferResult second = await script.RunAsync(Host + "//list/1010/", reused: true);

        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE A\r\nLIST /list/1010\r\nEPSV\r\nLIST /list/1010\r\n", script.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondAbsoluteUrlOnAnotherPath_ChangesFromTheRootWithoutTheEntryPath()
    {
        string replies = LoggedIn + Ok + Ok + Retrieved + Ok + Ok + Ok + RetrievedAgain;
        var control = new KeepingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        var script = new Script(control);

        await script.RunAsync(Host + "//foo/1225", reused: false);
        TransferResult second = await script.RunAsync(Host + "//foo/bar/1225", reused: true);

        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(
            LoginSent + "CWD /\r\nCWD foo\r\nEPSV\r\nTYPE I\r\nSIZE 1225\r\nRETR 1225\r\nCWD /\r\nCWD foo\r\nCWD bar\r\nEPSV\r\nSIZE 1225\r\nRETR 1225\r\n",
            script.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_EntryPathCwdRefused_FailsWithExit9AndKeepsTheConnection()
    {
        string replies = LoggedIn + Ok + Retrieved + "550 No\r\n";
        var control = new KeepingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        var script = new Script(control);

        await script.RunAsync(Host + "/d/f", reused: false);
        TransferResult second = await script.RunAsync(Host + "/f", reused: true);

        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, second.ExitCode);
        Assert.AreEqual(LoginSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSIZE f\r\nRETR f\r\nCWD /\r\n", script.Sent);
        Assert.AreEqual(2, control.ReusableMarks);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeptConnectionForAnotherLogin_ClosesItWithQuitAndConnectsAnew()
    {
        string replies = LoggedIn + Ok + Retrieved + "221 Bye\r\n";
        var kept = new KeepingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        var fresh = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Ok + Retrieved + "221 Bye\r\n"));
        var script = new Script(kept);
        await script.RunAsync(Host + "/d/f", reused: false);
        var connector = new QueuedConnector(
            ConnectResult.Connected(kept, null, isReused: true),
            ConnectResult.Connected(fresh),
            ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("x"))));
        var context = new TransferContext { Url = CurlUrl.Parse(Host + "/d/f"), Output = new MemoryStream(), Credentials = new NetworkCredential("bob", "pw") };

        TransferResult second = await new FtpProtocolHandler(connector).ExecuteAsync(context);
        await kept.Session!.ShutDownAsync(CancellationToken.None);

        Assert.IsTrue(second.IsSuccess);
        Assert.EndsWith("RETR f\r\nQUIT\r\n", script.Sent);
        Assert.StartsWith("USER bob\r\nPASS pw\r\n", Encoding.Latin1.GetString(fresh.Sent));
        Assert.HasCount(3, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionFailingWithoutQuit_SendsNoQuitAtClose()
    {
        string replies = LoggedIn + Ok + Retrieved + Ok + Ok + Epsv + "213 1\r\n150 Opening\r\n";
        var control = new KeepingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        var script = new Script(control);
        await script.RunAsync(Host + "/d/f", reused: false);

        TransferResult second = await script.RunAsync(Host + "/e/f", reused: true);
        string sentBeforeClose = script.Sent;
        await control.CloseAsync();

        Assert.IsFalse(second.IsSuccess);
        Assert.AreEqual(sentBeforeClose, script.Sent);
    }

    /// <summary>Runs transfers over one kept control connection, each with a fresh data connection.</summary>
    private sealed class Script(KeepingConnection control)
    {

        public FtpFileMethod FileMethod { get; init; }

        public string Sent => Encoding.Latin1.GetString(control.Script.Sent);

        public async Task<TransferResult> RunAsync(string url, bool reused)
        {
            var connector = new QueuedConnector(
                ConnectResult.Connected(control, null, isReused: reused),
                ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("x"))));
            var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream(), FtpFileMethod = FileMethod };
            return await new FtpProtocolHandler(connector).ExecuteAsync(context);
        }
    }
}
