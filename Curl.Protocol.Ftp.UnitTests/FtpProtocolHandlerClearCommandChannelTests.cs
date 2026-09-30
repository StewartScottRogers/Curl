using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>--ftp-ssl-ccc</c> and <c>--ftp-ssl-ccc-mode</c> (BL-636, ADR-0279): after
/// <c>PBSZ</c> and <c>PROT</c> over TLS the session sends <c>CCC</c>; a reply of 500 or more
/// leaves the control connection in TLS; any other asks the TLS connection to clear itself -
/// sending <c>close_notify</c> first only in active mode - and carries on from <c>PWD</c> over
/// the plaintext connection it hands back, or ends with exit 81 and no <c>QUIT</c> when it
/// hands back none, as curl 8.21.0's Schannel build always does. Measured on 2026-09-30 with
/// <c>Record-CurlExchange.ps1 -Ftp</c> against curl 8.21.0 (Schannel) and, through WSL, curl
/// 8.18.0 (OpenSSL). The fake TLS provider's secured connections are scripted, so no test
/// touches the network.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerClearCommandChannelTests
{
    private const string Greeting = "220 Recorder ready\r\n";

    private const string AuthAccepted = "234 AUTH accepted\r\n";

    private const string LoggedInAndProtected = "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n";

    private const string LogInAndProtectSent = "USER anonymous\r\nPASS ftp@example.com\r\nPBSZ 0\r\nPROT P\r\n";

    private const string CccAccepted = "200 CCC command successful\r\n";

    /// <summary>The replies from <c>PWD</c> through <c>QUIT</c> for the five-byte file.</summary>
    private const string Retrieved =
        "257 \"/\" is current directory\r\n229 Entering Extended Passive Mode (|||64396|)\r\n200 Type set\r\n213 5\r\n" +
        "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    /// <summary>What curl sent from <c>PWD</c> through <c>QUIT</c> for <c>a.txt</c>.</summary>
    private const string RetrieveSent = "PWD\r\nEPSV\r\nTYPE I\r\nSIZE a.txt\r\nRETR a.txt\r\nQUIT\r\n";

    [TestMethod]
    [DataRow(FtpCommandChannelClearing.Passive, false)]
    [DataRow(FtpCommandChannelClearing.Active, true)]
    public async Task ExecuteAsync_CccAccepted_SendsTheRestInPlainText(FtpCommandChannelClearing clearing, bool sendCloseNotifyFirst)
    {
        // curl -k --ftp-ssl-reqd --ftp-ssl-ccc [--ftp-ssl-ccc-mode active] ftp://.../a.txt,
        // OpenSSL build: PWD and everything after it in plain text, exit 0.
        var plaintext = Scripted(Retrieved);
        var securedControl = Scripted(LoggedInAndProtected + CccAccepted, clearedTo: plaintext);

        CccRun run = await RunAsync(securedControl, context => context.FtpCommandChannelClearing = clearing);

        Assert.AreEqual("AUTH SSL\r\n", run.PlainSent);
        Assert.AreEqual(LogInAndProtectSent + "CCC\r\n", Encoding.Latin1.GetString(securedControl.Sent));
        CollectionAssert.AreEqual(new[] { sendCloseNotifyFirst }, securedControl.ClearTlsRequests);
        Assert.AreEqual(RetrieveSent, Encoding.Latin1.GetString(plaintext.Sent));
        Assert.AreEqual("hello", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CccAnswered450_ClearsTlsAsBelow500()
    {
        // curl 8.18.0 (OpenSSL) with CCC answered 450: TLS is cleared all the same.
        var plaintext = Scripted(Retrieved);
        var securedControl = Scripted(LoggedInAndProtected + "450 later\r\n", clearedTo: plaintext);

        CccRun run = await RunAsync(securedControl, context => context.FtpCommandChannelClearing = FtpCommandChannelClearing.Passive);

        Assert.HasCount(1, securedControl.ClearTlsRequests);
        Assert.AreEqual(RetrieveSent, Encoding.Latin1.GetString(plaintext.Sent));
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    [DataRow("500 CCC refused")]
    [DataRow("533 CCC denied")]
    public async Task ExecuteAsync_CccRefused_CarriesOnOverTls(string refusal)
    {
        // curl -k --ftp-ssl-reqd --ftp-ssl-ccc, CCC answered 500 or 533: PWD follows over TLS, exit 0.
        var diagnostics = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        var securedControl = Scripted(LoggedInAndProtected + refusal + "\r\n" + Retrieved);

        CccRun run = await RunAsync(securedControl, context =>
        {
            context.FtpCommandChannelClearing = FtpCommandChannelClearing.Passive;
            context.DiagnosticLog = diagnostics;
        });

        Assert.AreEqual(LogInAndProtectSent + "CCC\r\n" + RetrieveSent, Encoding.Latin1.GetString(securedControl.Sent));
        Assert.IsEmpty(securedControl.ClearTlsRequests);
        CollectionAssert.Contains(
            diagnostics.At(DiagnosticLogLevel.Warning),
            $"CCC refused with {refusal[..3]}; the control connection stays TLS");
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    [DataRow(FtpCommandChannelClearing.Passive)]
    [DataRow(FtpCommandChannelClearing.Active)]
    public async Task ExecuteAsync_TlsNotCleared_EndsWithExit81WithoutQuit(FtpCommandChannelClearing clearing)
    {
        // curl 8.21.0 (Schannel) -v -k --ftp-ssl-reqd --ftp-ssl-ccc, CCC answered 200:
        // "* Failed to clear the command channel (CCC)", then exit 81 with no QUIT.
        var events = new RecordingTransferEvents();
        var securedControl = Scripted(LoggedInAndProtected + CccAccepted + Retrieved);

        CccRun run = await RunAsync(securedControl, context =>
        {
            context.FtpCommandChannelClearing = clearing;
            context.Events = events;
        });

        Assert.AreEqual(LogInAndProtectSent + "CCC\r\n", Encoding.Latin1.GetString(securedControl.Sent));
        Assert.AreEqual("Failed to clear the command channel (CCC)", events.Info[^1]);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Again, "Failed to clear the command channel (CCC)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsUrl_ClearsTheImplicitTlsControlConnection()
    {
        // curl -k --ftp-ssl-ccc ftps://.../a.txt: CCC follows PROT on implicit TLS too.
        var diagnostics = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var plaintext = Scripted(Retrieved);
        var control = Scripted(Greeting + LoggedInAndProtected + CccAccepted, clearedTo: plaintext);
        var data = new ScriptedConnection();
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(data));
        var tls = new QueuedTlsProvider(ConnectResult.Connected(Scripted("hello")));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftps://127.0.0.1:18636/a.txt"), Output = new MemoryStream() },
            adjust =>
            {
                adjust.FtpCommandChannelClearing = FtpCommandChannelClearing.Passive;
                adjust.DiagnosticLog = diagnostics;
            });

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        Assert.AreEqual(LogInAndProtectSent + "CCC\r\n", Encoding.Latin1.GetString(control.Sent));
        Assert.AreEqual(RetrieveSent, Encoding.Latin1.GetString(plaintext.Sent));
        CollectionAssert.Contains(diagnostics.At(DiagnosticLogLevel.Info), "CCC: the control connection is plaintext");
        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_CccWithoutTls_SendsNoCcc()
    {
        // --ftp-ssl-ccc without --ssl or --ssl-reqd: the control connection is never TLS, so no CCC.
        var control = Scripted(Greeting + "331 Password required\r\n230 Logged in\r\n" + Retrieved);
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(Scripted("hello")));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:18636/a.txt"), Output = new MemoryStream() },
            adjust => adjust.FtpCommandChannelClearing = FtpCommandChannelClearing.Active);

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), new QueuedTlsProvider()).ExecuteAsync(context);

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\n" + RetrieveSent, Encoding.Latin1.GetString(control.Sent));
        Assert.IsEmpty(control.ClearTlsRequests);
        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
    }

    private static ScriptedConnection Scripted(string text, IConnection? clearedTo = null) =>
        new(Encoding.Latin1.GetBytes(text)) { ClearedTo = clearedTo };

    // ftp:// under --ssl-reqd: the plain control connection answers AUTH SSL, the TLS provider
    // hands back securedControl for it and a scripted "hello" for the data connection.
    private static async Task<CccRun> RunAsync(ScriptedConnection securedControl, Action<MutableContext> adjust)
    {
        var control = Scripted(Greeting + AuthAccepted);
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(new ScriptedConnection()));
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl), ConnectResult.Connected(Scripted("hello")));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:18636/a.txt"), Output = new MemoryStream() },
            mutable =>
            {
                mutable.SslLevel = TransportSecurityLevel.Required;
                adjust(mutable);
            });

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);

        return new CccRun(result with { Report = null }, Encoding.Latin1.GetString(control.Sent), context.Output);
    }

    /// <summary>One transfer under <c>--ftp-ssl-ccc</c> and what it left behind.</summary>
    private sealed record CccRun(TransferResult Result, string PlainSent, Stream Output)
    {
        public string OutputText => Encoding.Latin1.GetString(((MemoryStream)Output).ToArray());
    }
}
