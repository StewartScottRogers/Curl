using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins the two <c>-v</c> lines curl 8.21.0 (the Schannel build) writes about the entry path,
/// in order among the command and reply lines: <c>Entry path is '&lt;dir&gt;'</c> for the
/// reply to <c>PWD</c>, and <c>Request has same path as previous transfer</c> before the first
/// command after login when the URL path needs no <c>CWD</c>. Measured on 2026-09-29 with
/// <c>Record-CurlExchange.ps1 -Ftp -FtpData 'hello ftp\r\n' -CurlArgs -v,ftp://127.0.0.1:port/...</c>
/// (BL-945, whose Notes hold the recordings).
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerEntryPathEventTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string File = "hello ftp\r\n";

    private const string LoggingIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n";

    private const string RootPwd = "257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||55801|)\r\n";

    private const string Retrieved = "200 Type set\r\n213 11\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string Listed = "200 Type set\r\n150 Opening ASCII mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string EntryPathIsRoot = "* Entry path is '/'";

    private const string SamePath = "* Request has same path as previous transfer";

    [TestMethod]
    [DataRow("/file.txt", FtpFileMethod.MultiCwd, "> EPSV\r\n", DisplayName = "a file at the root")]
    [DataRow("/", FtpFileMethod.MultiCwd, "> EPSV\r\n", DisplayName = "a root listing")]
    [DataRow("/a/b/file.txt", FtpFileMethod.NoCwd, "> EPSV\r\n", DisplayName = "--ftp-method nocwd")]
    [DataRow("/dir/", FtpFileMethod.NoCwd, "> EPSV\r\n", DisplayName = "a nocwd listing")]
    public async Task ExecuteAsync_PathNeedingNoCwd_ReportsTheSamePathLineAfterTheEntryPath(string path, FtpFileMethod method, string next)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ftp method", method);
        diagnostics.Arrange("expected next command", FtpDiagnostics.Escape(next));
        bool listing = path.EndsWith('/');
        EventRun run = await RunAsync(diagnostics, path, RootPwd + Epsv + (listing ? Listed : Retrieved), context => context.FtpFileMethod = method);

        string[] expected = ["> PWD\r\n", "< " + RootPwd, EntryPathIsRoot, SamePath, next];
        string[] actual = run.Events.Transcript.Skip(5).Take(5).ToArray();
        DiffLines(diagnostics, "transcript lines 5 to 9", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
        diagnostics.Assert("is success", true, run.Result.IsSuccess);
        Assert.IsTrue(run.Result.IsSuccess);
    }

    [TestMethod]
    [DataRow("/dir/file.txt", FtpFileMethod.MultiCwd, "> CWD dir\r\n", DisplayName = "CWD dir")]
    [DataRow("/a/b/file.txt", FtpFileMethod.SingleCwd, "> CWD a/b\r\n", DisplayName = "--ftp-method singlecwd, CWD a/b")]
    [DataRow("//file.txt", FtpFileMethod.MultiCwd, "> CWD /\r\n", DisplayName = "a // path, CWD /")]
    [DataRow("//a/file.txt", FtpFileMethod.NoCwd, "> EPSV\r\n", DisplayName = "a // path under nocwd")]
    public async Task ExecuteAsync_PathLeavingTheEntryDirectory_ReportsNoSamePathLine(string path, FtpFileMethod method, string next)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ftp method", method);
        diagnostics.Arrange("expected next command", FtpDiagnostics.Escape(next));
        EventRun run = await RunAsync(diagnostics, path, RootPwd + "250 OK\r\n250 OK\r\n", context => context.FtpFileMethod = method);

        string[] expected = ["> PWD\r\n", "< " + RootPwd, EntryPathIsRoot, next];
        string[] actual = run.Events.Transcript.Skip(5).Take(4).ToArray();
        DiffLines(diagnostics, "transcript lines 5 to 8", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
        diagnostics.Assert("same path line reported", false, run.Events.Transcript.Contains(SamePath));
        Assert.DoesNotContain(SamePath, run.Events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuoteAfterLogin_ReportsTheSamePathLineBeforeTheQuote()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v -Q NOOP ftp://127.0.0.1:47962/file.txt, NOOP answered 200 ok.
        diagnostics.Arrange("option", "-Q NOOP");
        EventRun run = await RunAsync(diagnostics, "/file.txt", RootPwd + "200 ok\r\n" + Epsv + Retrieved, context => context.QuoteCommands.Add("NOOP"));

        string[] expected = [EntryPathIsRoot, SamePath, "> NOOP\r\n", "< 200 ok\r\n", "> EPSV\r\n"];
        string[] actual = run.Events.Transcript.Skip(7).Take(5).ToArray();
        DiffLines(diagnostics, "transcript lines 7 to 11", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuoteAfterLoginWithACwd_ReportsNoSamePathLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v -Q NOOP ftp://127.0.0.1:47961/dir/file.txt
        diagnostics.Arrange("option", "-Q NOOP");
        EventRun run = await RunAsync(diagnostics, "/dir/file.txt", RootPwd + "200 ok\r\n250 OK\r\n" + Epsv + Retrieved, context => context.QuoteCommands.Add("NOOP"));

        string[] expected = [EntryPathIsRoot, "> NOOP\r\n", "< 200 ok\r\n", "> CWD dir\r\n"];
        string[] actual = run.Events.Transcript.Skip(7).Take(4).ToArray();
        DiffLines(diagnostics, "transcript lines 7 to 10", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_RelativeEntryPath_ReportsTheEntryPathOnceSystIsSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v ftp://127.0.0.1:47962/file.txt, PWD answered 257 "home" is current.
        diagnostics.Arrange("PWD answer", "257 \"home\" is current");
        EventRun run = await RunAsync(diagnostics, "/file.txt", "257 \"home\" is current\r\n502 Command not implemented\r\n" + Epsv + Retrieved, _ => { });

        string[] expected = ["< 257 \"home\" is current\r\n", "> SYST\r\n", "* Entry path is 'home'", "< 502 Command not implemented\r\n", SamePath, "> EPSV\r\n"];
        string[] actual = run.Events.Transcript.Skip(6).Take(6).ToArray();
        DiffLines(diagnostics, "transcript lines 6 to 11", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_Os400SecondPwd_ReportsBothEntryPaths()
    {
        // curl -v ftp://127.0.0.1:47962/file.txt, PWD answered "QSYS.LIB" then "/QSYS.LIB",
        // SYST 215 OS/400 is the remote, SITE 250 ok.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("system", "OS/400");
        EventRun run = await RunAsync(
            diagnostics,
            "/file.txt",
            "257 \"QSYS.LIB\" is current\r\n215 OS/400 is the remote\r\n250 ok\r\n257 \"/QSYS.LIB\" is current\r\n" + Epsv + Retrieved,
            _ => { });

        string[] expected =
        [
            "> PWD\r\n",
            "< 257 \"QSYS.LIB\" is current\r\n",
            "> SYST\r\n",
            "* Entry path is 'QSYS.LIB'",
            "< 215 OS/400 is the remote\r\n",
            "> SITE NAMEFMT 1\r\n",
            "< 250 ok\r\n",
            "> PWD\r\n",
            "< 257 \"/QSYS.LIB\" is current\r\n",
            "* Entry path is '/QSYS.LIB'",
            SamePath,
            "> EPSV\r\n",
        ];
        string[] actual = run.Events.Transcript.Skip(5).Take(12).ToArray();
        DiffLines(diagnostics, "transcript lines 5 to 16", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("257 slash is current", "* Failed to figure out path", DisplayName = "257 with no quote")]
    [DataRow("257 \"\"", "* Failed to figure out path", DisplayName = "257 with an empty name")]
    [DataRow("500 no", null, DisplayName = "PWD refused")]
    public async Task ExecuteAsync_PwdNamingNoDirectory_ReportsCurlsLineForTheReply(string pwdReply, string? line)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("PWD reply", pwdReply);
        diagnostics.Arrange("expected line", line ?? "(none)");
        EventRun run = await RunAsync(diagnostics, "/file.txt", pwdReply + "\r\n" + Epsv + Retrieved, _ => { });

        string[] expected = line is null
            ? ["< " + pwdReply + "\r\n", SamePath, "> EPSV\r\n"]
            : ["< " + pwdReply + "\r\n", line, SamePath, "> EPSV\r\n"];
        string[] actual = run.Events.Transcript.Skip(6).Take(expected.Length).ToArray();
        DiffLines(diagnostics, "transcript from line 6", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    private static void DiffLines(TestDiagnostics diagnostics, string label, IEnumerable<string> expected, IEnumerable<string> actual) =>
        diagnostics.Diff(label, FtpDiagnostics.Escape(string.Join(" | ", expected)), FtpDiagnostics.Escape(string.Join(" | ", actual)));

    private static async Task<EventRun> RunAsync(TestDiagnostics diagnostics, string path, string repliesAfterLogin, Action<MutableContext> adjust)
    {
        diagnostics.ArrangeFtp("ftp://127.0.0.1:47961" + path, LoggingIn + repliesAfterLogin);
        var events = new RecordingTransferEvents();
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggingIn + repliesAfterLogin))
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 47961),
        };
        var connector = new QueuedConnector(
            ConnectResult.Connected(control),
            ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(File))));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:47961" + path), Output = new MemoryStream() },
            mutable =>
            {
                mutable.Events = events;
                adjust(mutable);
            });

        TransferResult result;
        using (diagnostics.Phase("transfer"))
        {
            result = await new FtpProtocolHandler(connector, new QueuedListener(), new QueuedTlsProvider()).ExecuteAsync(context);
        }

        diagnostics.ActResult(result);
        return new EventRun(result, events);
    }

    /// <summary>One transfer and the events it reported.</summary>
    private sealed record EventRun(TransferResult Result, RecordingTransferEvents Events);
}
