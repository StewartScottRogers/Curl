using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins <see cref="FtpProtocolHandler.ConnectOnlyAsync" />: only FTP's connect phase goes on the
/// wire and the control connection is handed back open, as libcurl 8.22.0's
/// <c>CURLOPT_CONNECT_ONLY</c> 1 does for <c>ftp://</c> in upstream case 597 (BL-2705).
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerConnectOnlyTests
{
    private const string Url = "ftp://127.0.0.1:18597/597";

    private const string Greeting = "220 ready\r\n";

    private const string LoggedIn = "331 Password required\r\n230 Logged in\r\n";

    private const string Pwd = "257 \"/\" is current directory\r\n";

    private const string LoginSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private static TransferContext Context() => new() { Url = CurlUrl.Parse(Url), Output = new MemoryStream() };

    [TestMethod]
    public async Task ConnectOnlyAsync_AnonymousLogin_SendsOnlyUserPassPwdAndLeavesTheConnectionOpen()
    {
        var control = ScriptedConnection.FromReplies(Greeting + LoggedIn + Pwd + "221 Bye\r\n");

        FtpConnectOnlyOutcome outcome = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control))).ConnectOnlyAsync(Context());

        Assert.AreEqual(LoginSent, Encoding.Latin1.GetString(control.Sent));
        Assert.AreSame(control, outcome.Connection);
        Assert.IsFalse(control.IsDisposed);
        Assert.IsTrue(outcome.Result.IsSuccess);
        Assert.AreEqual(257, outcome.Result.Report!.ResponseCode);
        Assert.AreEqual("/", outcome.Result.Report.FtpEntryPath);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_User530_FailsAsTheTransferDoesAndClosesTheConnection()
    {
        string replies = Greeting + "530 nope\r\n";
        var control = ScriptedConnection.FromReplies(replies);
        var transferControl = ScriptedConnection.FromReplies(replies);

        FtpConnectOnlyOutcome outcome = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control))).ConnectOnlyAsync(Context());
        TransferResult transfer = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(transferControl))).ExecuteAsync(Context());

        Assert.IsNull(outcome.Connection);
        Assert.IsTrue(control.IsDisposed);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 530"), outcome.Result with { Report = null });
        Assert.AreEqual(transfer with { Report = transfer.Report! with { Timings = null } }, outcome.Result);
        Assert.AreEqual("USER anonymous\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_ControlClosesBeforePwdReply_FailsWithExit56AndClosesTheConnection()
    {
        var control = ScriptedConnection.FromReplies(Greeting + LoggedIn + "257 partial");

        FtpConnectOnlyOutcome outcome = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control))).ConnectOnlyAsync(Context());

        Assert.IsNull(outcome.Connection);
        Assert.IsTrue(control.IsDisposed);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), outcome.Result with { Report = null });
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var connector = new QueuedConnector(ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Failed to connect"));

        FtpConnectOnlyOutcome outcome = await new FtpProtocolHandler(connector).ConnectOnlyAsync(Context());

        Assert.IsNull(outcome.Connection);
        Assert.AreEqual(new TransferResult(CurlExitCode.OperationTimedOut, 0, "Failed to connect"), outcome.Result);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_SslReqd_HandsBackTheSecuredControlConnectionOpen()
    {
        var plain = ScriptedConnection.FromReplies(Greeting + "234 AUTH accepted\r\n");
        var secured = ScriptedConnection.FromReplies(LoggedIn + "200 PBSZ=0\r\n200 Protection level set to P\r\n" + Pwd);
        var connector = new QueuedConnector(ConnectResult.Connected(plain));
        TransferContext context = MutableContext.Build(Context(), m => m.SslLevel = TransportSecurityLevel.Required);

        FtpConnectOnlyOutcome outcome = await new FtpProtocolHandler(connector, new QueuedListener(), new QueuedTlsProvider(ConnectResult.Connected(secured))).ConnectOnlyAsync(context);

        Assert.AreSame(secured, outcome.Connection);
        Assert.IsFalse(secured.IsDisposed);
        Assert.IsFalse(plain.IsDisposed);
        Assert.AreEqual("AUTH SSL\r\n", Encoding.Latin1.GetString(plain.Sent));
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPBSZ 0\r\nPROT P\r\nPWD\r\n", Encoding.Latin1.GetString(secured.Sent));
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_KeptConnection_SendsNothingAndHandsItBack()
    {
        var kept = new KeepingConnection(ScriptedConnection.FromReplies(Greeting + LoggedIn + Pwd + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 1\r\n150 Opening\r\n226 Transfer complete\r\n"));
        var first = new QueuedConnector(ConnectResult.Connected(kept), ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("x"))));
        await new FtpProtocolHandler(first).ExecuteAsync(Context());
        int sentBefore = kept.Script.Sent.Length;

        FtpConnectOnlyOutcome outcome = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(kept, null, isReused: true))).ConnectOnlyAsync(Context());

        Assert.AreSame(kept, outcome.Connection);
        Assert.AreEqual(sentBefore, kept.Script.Sent.Length);
        Assert.AreEqual("/", outcome.Result.Report!.FtpEntryPath);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new FtpProtocolHandler(new QueuedConnector()).ConnectOnlyAsync(null!));
    }
}
