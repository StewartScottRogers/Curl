using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>ftp://</c> downloads against curl 8.21.0: the commands each URL sends on the
/// control connection, the targets it connects to, the body written to the output, and
/// the exit code and message of every way a transfer ends. Each command sequence and
/// message was recorded from real curl with <c>Record-CurlExchange.ps1 -Ftp</c> (BL-431)
/// and is replayed here through <see cref="ScriptedConnection" />, so nothing touches a
/// network.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerTests
{
    private const string Url = "ftp://127.0.0.1:18321/file.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    /// <summary>The recording server's replies from the greeting through <c>PWD</c>.</summary>
    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||61744|)\r\n";

    private const string TypeSet = "200 Type set\r\n";

    private const string Opening = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>What curl 8.21.0 sent for <c>curl ftp://127.0.0.1:18321/file.txt</c>, up to <c>PWD</c>.</summary>
    private const string LoginSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    /// <summary>What curl 8.21.0 sent for <c>curl ftp://127.0.0.1:18321/file.txt</c>.</summary>
    private const string DownloadSent = LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";

    [TestMethod]
    public void SupportedSchemes_IsExactlyFtp()
    {
        FtpProtocolHandler handler = new(new QueuedConnector());

        CollectionAssert.AreEqual(new[] { "ftp" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpProtocolHandler(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        FtpProtocolHandler handler = new(new QueuedConnector());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_File_SendsCurlsCommandsAndWritesTheBody()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Epsv + TypeSet + "213 10\r\n" + Opening + Complete + Bye,
            "hello ftp\n");

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual("hello ftp\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_File_ConnectsControlToTheUrlAndDataToTheEpsvPort()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye, "x");

        Assert.HasCount(2, run.Connector.Targets);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18321, false), run.Connector.Targets[0]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 61744, false), run.Connector.Targets[1]);
        Assert.IsTrue(run.Control.IsDisposed);
        Assert.IsTrue(run.Data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoPortInUrl_ConnectsToPort21()
    {
        FtpRun run = await FtpRun.ExecuteAsync("ftp://h/file.txt", LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye, "x");

        Assert.AreEqual(new ConnectTarget("h", 21, false), run.Connector.Targets[0]);
        Assert.AreEqual(new ConnectTarget("h", 61744, false), run.Connector.Targets[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Proxy_TunnelsBothConnections()
    {
        var proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy", 1080, null);

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x",
            context => new TransferContext { Url = context.Url, Output = context.Output, Proxy = proxy });

        Assert.AreEqual(proxy, run.Connector.Targets[0].Proxy);
        Assert.AreEqual(proxy, run.Connector.Targets[1].Proxy);
    }

    [TestMethod]
    public async Task ExecuteAsync_File_ReportsProgress()
    {
        var progress = new RecordingProgress();

        await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye,
            "abc",
            context => new TransferContext { Url = context.Url, Output = context.Output, Progress = progress });

        Assert.IsTrue(progress.Started);
        CollectionAssert.AreEqual(new[] { (3L, (long?)3) }, progress.Downloaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnectRefused_ReturnsTheConnectorsFailure()
    {
        var connector = new QueuedConnector(ConnectResult.Refused("Failed to connect to h port 21"));
        FtpProtocolHandler handler = new(connector);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ftp://h/f"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to h port 21", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultiLineGreeting_SkipsToTheLastLine()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            "220-Welcome\r\nno code here\r\n22\r\n220-second\n220 ready\r\n331 Password required\r\n230 Logged in\r\n257\r\n257x\r\n257 done\r\n"
                + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x");

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RepliesSplitAcrossReads_AreReassembled()
    {
        byte[] replies = Encoding.Latin1.GetBytes(LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye);
        var control = new ScriptedConnection(replies[..5], replies[5..30], replies[30..]);

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection("x"u8.ToArray()));

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadGreeting_FailsWithExit8AndSendsNothing()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, "500 go away\r\n");

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Got a 500 ftp-server response when 220 was expected"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BareCodeLine_DoesNotEndTheReply()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331\r\n230 ok\r\n257 \"/\" is current directory\r\n" + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x");

        Assert.AreEqual("USER anonymous\r\nPWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Greeting230_SkipsTheLogin()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            "230 welcome back\r\n257 \"/\" is current directory\r\n" + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye,
            "abc");

        Assert.AreEqual("PWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Greeting421_FailsWithExit28AndSendsNothing()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, "421 busy\r\n");

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pass421_FailsWithExit28WithoutQuit()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "331 Password required\r\n421 bye\r\n" + Bye);

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferEndsWith421_FailsWithExit28ControlConnectionLooksDead()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening + "421 bye\r\n" + Bye, "abc");

        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "control connection looks dead", 3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pass430_FailsWithExit67AccessDenied430AndNoQuit()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "331 Password required\r\n430 Access denied\r\n");

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 430"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_User530_FailsWithExit67WithoutSendingPass()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "530 nope\r\n");

        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 530"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pass332_FailsWithExit67AcctRequested()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "331 Password required\r\n332 need account\r\n");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "ACCT requested but none available"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_User230_SkipsPass()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "230 ok\r\n257 \"/\" is current directory\r\n" + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x");

        Assert.AreEqual("USER anonymous\r\nPWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n", run.Sent);
        Assert.IsTrue(run.Result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_Credentials_LogInAsThatUser()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x",
            context => new TransferContext { Url = context.Url, Output = context.Output, Credentials = new NetworkCredential("bob", "s3cret") });

        StringAssert.StartsWith(run.Sent, "USER bob\r\nPASS s3cret\r\nPWD\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsWithCrLf_AreSentAsGivenAsCurlDoes()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331 Password required\r\n502 Command not implemented\r\n",
            adjust: context => new TransferContext { Url = context.Url, Output = context.Output, Credentials = new NetworkCredential("a\r\nDELE x", "pw") });

        Assert.AreEqual("USER a\r\nDELE x\r\nPASS pw\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 502"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "430 no\r\n",
            adjust: context => new TransferContext { Url = context.Url, Output = context.Output, Credentials = new NetworkCredential("jörg", "pw") });

        CollectionAssert.AreEqual(new byte[] { 0x55, 0x53, 0x45, 0x52, 0x20, 0x6A, 0xF6, 0x72, 0x67, 0x0D, 0x0A }, run.SentBytes);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65535BytesWithItsCrLf_IsRead()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 " + new string('x', 65529) + "\r\n530 no\r\n");

        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65536Bytes_FailsWithExit100()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 " + new string('x', 65530) + "\r\n");

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OversizedReplyToQuit_StillSucceeds()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + "221 " + new string('x', 70000),
            "x");

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PwdRefused_CarriesOn()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "331 Password required\r\n230 Logged in\r\n500 no\r\n" + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye,
            "abc");

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Directories_SendOneCwdEachSkippingEmptySegments()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/dir//sub/file.txt",
            LoggedIn + "250 OK\r\n250 OK\r\n" + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye,
            "abc");

        Assert.AreEqual(LoginSent + "CWD dir\r\nCWD sub\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("abc", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentEncodedPath_SendsTheDecodedBytes()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/caf%C3%A9/a%20b%2x%/%E9.txt",
            LoggedIn + "250 OK\r\n250 OK\r\n" + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x");

        CollectionAssert.AreEqual(
            Encoding.Latin1.GetBytes(LoginSent + "CWD cafÃ©\r\nCWD a b%2x%\r\nEPSV\r\nTYPE I\r\nSIZE é.txt\r\nRETR é.txt\r\nQUIT\r\n"),
            run.SentBytes);
    }

    [TestMethod]
    public async Task ExecuteAsync_CwdRefused_QuitsAndFailsWithExit9()
    {
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/dir/file.txt", LoggedIn + "550 nope\r\n" + Bye);

        Assert.AreEqual(LoginSent + "CWD dir\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Server denied you to change to the given directory"), run.Result);
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1:18321/a%0Ab/f")]
    [DataRow("ftp://127.0.0.1:18321/f%00x")]
    public async Task ExecuteAsync_ControlCharacterInPath_FailsWithExit3AfterPwd(string url)
    {
        FtpRun run = await FtpRun.ExecuteAsync(url, LoggedIn);

        Assert.AreEqual(LoginSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, "path contains control characters"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvRefused_FallsBackToPasvAndItsPort()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + "500 no\r\n227 Entering Passive Mode (127,0,0,1,245,222)\r\n" + TypeSet + "213 3\r\n" + Opening + Complete + Bye,
            "abc");

        Assert.AreEqual(LoginSent + "EPSV\r\nPASV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 62942, false), run.Connector.Targets[1]);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasvNumbersWithoutParentheses_AreRead()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + "500 no\r\n227 127,0,0,1,1,2\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x");

        Assert.AreEqual(new ConnectTarget("127.0.0.1", 258, false), run.Connector.Targets[1]);
        Assert.IsTrue(run.Result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_ThrowsAndDisposesBothConnections()
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening));
        var data = new ScriptedConnection("abc"u8.ToArray());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await FtpRun.ExecuteAsync(
            Url,
            control,
            data,
            context => new TransferContext { Url = context.Url, Output = new WriteRefusingStream(new OperationCanceledException()) }));

        Assert.IsTrue(control.IsDisposed);
        Assert.IsTrue(data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledBeforeTheDataConnection_ThrowsAndDisposesTheControlConnection()
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting))
        {
            WritesBeforeFailure = 0,
            WriteFailure = new OperationCanceledException(),
        };
        var data = new ScriptedConnection();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await FtpRun.ExecuteAsync(Url, control, data));

        Assert.IsTrue(control.IsDisposed);
        Assert.IsFalse(data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasvAddress_IsIgnoredForTheControlHost()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + "500 no\r\n227 Entering Passive Mode (10,1,2,3,0,21)\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye,
            "x");

        Assert.AreEqual(new ConnectTarget("127.0.0.1", 21, false), run.Connector.Targets[1]);
    }

    [TestMethod]
    [DataRow("229 garbage")]
    [DataRow("229 Entering Extended Passive Mode (|||0|)")]
    [DataRow("229 Entering Extended Passive Mode (|||70000|)")]
    [DataRow("229 Entering Extended Passive Mode (|!|40000|)")]
    [DataRow("229 Entering Extended Passive Mode (||!40000|)")]
    [DataRow("229 Entering Extended Passive Mode (||")]
    [DataRow("229 Entering Extended Passive Mode (|||40000")]
    [DataRow("229 Entering Extended Passive Mode (|||40000|")]
    [DataRow("229 Entering Extended Passive Mode (|||40000|x")]
    [DataRow("229 Entering Extended Passive Mode (||||)")]
    [DataRow("229 Entering Extended Passive Mode (|||4a000|)")]
    public async Task ExecuteAsync_UnreadableEpsvReply_QuitsAndFailsWithExit13(string reply)
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + reply + "\r\n" + Bye);

        Assert.AreEqual(LoginSent + "EPSV\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpWeirdPasvReply, "Weirdly formatted EPSV reply"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvAndPasvRefused_QuitsAndFailsWithExit13()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + "500 no\r\n500 no\r\n" + Bye);

        Assert.AreEqual(LoginSent + "EPSV\r\nPASV\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpWeirdPasvReply, "Bad PASV/EPSV response: 500"), run.Result);
    }

    [TestMethod]
    [DataRow("227 garbage")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0,0)")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,256,1)")]
    [DataRow("227 Entering Passive Mode (300,0,0,1,0,21)")]
    [DataRow("227 Entering Passive Mode (1234,0,0,1,0,21)")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0)")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0,)")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0;21)")]
    public async Task ExecuteAsync_UnreadablePasvReply_FailsWithExit14WithoutQuit(string reply)
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + "500 no\r\n" + reply + "\r\n");

        Assert.AreEqual(LoginSent + "EPSV\r\nPASV\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpWeird227Format, "Could not interpret the 227-response"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnectRefused_ReturnsTheConnectorsFailure()
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Epsv));
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Refused("Failed to connect to 127.0.0.1 port 61744"));

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 61744", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.IsTrue(control.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_TypeRefused_QuitsAndFailsWithExit17()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + "500 no\r\n" + Bye);

        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE I\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpCouldntSetType, "Could not set desired mode"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Size550_QuitsAndFailsWithExit78()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "550 no\r\n" + Bye);

        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "The file does not exist"), run.Result);
    }

    [TestMethod]
    [DataRow("500 not supported")]
    [DataRow("213 ")]
    [DataRow("213 many")]
    [DataRow("213 -5")]
    public async Task ExecuteAsync_SizeUnknown_DownloadsWhateverArrives(string reply)
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + reply + "\r\n" + Opening + Complete + Bye, "abc");

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr550_QuitsAndFailsWithExit78()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "213 0\r\n550 No such file\r\n" + Bye);

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "RETR response: 550"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr500_QuitsAndFailsWithExit19()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "213 0\r\n500 x\r\n" + Bye);

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpCouldntRetrFile, "RETR response: 500"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr125_Downloads()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "213 1\r\n125 go\r\n250 ok\r\n" + Bye, "x");

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathEndingInSlash_ListsTheDirectory()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/dir/",
            LoggedIn + "250 OK\r\n" + Epsv + TypeSet + Opening + Complete + Bye,
            "drw a\r\n");

        Assert.AreEqual(LoginSent + "CWD dir\r\nEPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("drw a\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(7), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_List450_QuitsAndSucceedsWithNothing()
    {
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/", LoggedIn + Epsv + TypeSet + "450 x\r\n" + Bye);

        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_List550_QuitsAndFailsWithExit19()
    {
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/", LoggedIn + Epsv + TypeSet + "550 x\r\n" + Bye);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpCouldntRetrFile, "RETR response: 550"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferEndsWith451_QuitsAndFailsWithExit18()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening + "451 aborted\r\n" + Bye, "abc");

        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "server did not report OK, got 451", 3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataShorterThanSize_FailsWithExit18WithoutQuit()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + Epsv + TypeSet + "213 10\r\n" + Opening + Complete, "abc");

        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed with 7 bytes remaining to read", 3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataReadFails_FailsWithExit56()
    {
        var data = new ScriptedConnection("ab"u8.ToArray()) { FailReadsWhenExhausted = true };

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening)),
            data);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "Failure when receiving data from the peer", 2), run.Result);
        Assert.IsTrue(data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesWithCount_FailsWithExit23NamingIt()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening,
            "abc",
            context => new TransferContext { Url = context.Url, Output = new WriteRefusingStream(new OutputWriteFailedException(1, "full")) });

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 3 returned 1"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefuses_FailsWithExit23Returned0()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening,
            "abc",
            context => new TransferContext { Url = context.Url, Output = new WriteRefusingStream(new IOException("full")) });

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 3 returned 0"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlClosesMidConversation_FailsWithExit56()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "331 Password required\r\n230 Logged in\r\n257 partial");

        Assert.AreEqual(LoginSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlReadFails_FailsWithExit56()
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { FailReadsWhenExhausted = true };

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection());

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandCannotBeSent_FailsWithExit55()
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn)) { WritesBeforeFailure = 1 };

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection());

        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Failure when sending data to the peer"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitCannotBeSent_StillSucceeds()
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete)) { WritesBeforeFailure = 7 };

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection("x"u8.ToArray()));

        Assert.AreEqual(DownloadSent[..^"QUIT\r\n".Length], run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    /// <summary>
    /// One transfer run against a scripted control and data connection, and what it left
    /// behind.
    /// </summary>
    private sealed record FtpRun(
        TransferResult Result,
        ScriptedConnection Control,
        ScriptedConnection Data,
        QueuedConnector Connector,
        Stream Output)
    {
        public byte[] SentBytes => Control.Sent;

        public string Sent => Encoding.Latin1.GetString(Control.Sent);

        public string OutputText => Encoding.Latin1.GetString(((MemoryStream)Output).ToArray());

        public static Task<FtpRun> ExecuteAsync(
            string url,
            string replies,
            string data = "",
            Func<TransferContext, TransferContext>? adjust = null)
        {
            byte[] dataBytes = Encoding.Latin1.GetBytes(data);
            return ExecuteAsync(
                url,
                new ScriptedConnection(Encoding.Latin1.GetBytes(replies)),
                dataBytes.Length == 0 ? new ScriptedConnection() : new ScriptedConnection(dataBytes),
                adjust);
        }

        public static async Task<FtpRun> ExecuteAsync(
            string url,
            ScriptedConnection control,
            ScriptedConnection data,
            Func<TransferContext, TransferContext>? adjust = null)
        {
            var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(data));
            var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() };
            context = adjust?.Invoke(context) ?? context;

            TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);

            return new FtpRun(result, control, data, connector, context.Output);
        }
    }
}
