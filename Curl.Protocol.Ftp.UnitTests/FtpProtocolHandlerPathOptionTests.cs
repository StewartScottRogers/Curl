using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>--disable-epsv</c>, <c>--no-ftp-skip-pasv-ip</c>, <c>--ftp-method</c> and
/// <c>--ftp-create-dirs</c> over <c>ftp://</c> against curl 8.21.0: the commands sent on the
/// control connection, the data connection's target, the output and the exit code. Every
/// case was recorded from real curl on 2026-09-27 with <c>Record-CurlExchange.ps1 -Ftp</c>
/// serving the three bytes <c>abc</c> and uploading <c>hello</c> (BL-436, ADR-0323's BL-436
/// addendum), and is replayed here with the recorder's replies.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerPathOptionTests
{
    private const string Host = "ftp://127.0.0.1:47361";

    /// <summary>The recording server's replies from the greeting through <c>PWD</c>.</summary>
    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Ok = "250 OK\r\n";

    private const string NoSuchDirectory = "550 No such directory\r\n";

    private const string Created = "257 created\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||49737|)\r\n";

    private const string TypeSet = "200 Type set\r\n";

    private const string Sized = "213 3\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    /// <summary>What curl sent from <c>EPSV</c> through <c>QUIT</c> for a file <c>f.txt</c> in the current directory.</summary>
    private const string RetrieveSent = "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";

    /// <summary>The replies curl read from <c>EPSV</c> through <c>QUIT</c> for a three-byte file.</summary>
    private const string Retrieved = Epsv + TypeSet + Sized + Opened + Complete + Bye;

    /// <summary>What curl sent from <c>EPSV</c> through <c>QUIT</c> to upload <c>f.txt</c>.</summary>
    private const string StoreSent = "EPSV\r\nTYPE I\r\nSTOR f.txt\r\nQUIT\r\n";

    /// <summary>The replies curl read from <c>EPSV</c> through <c>QUIT</c> for an upload.</summary>
    private const string Stored = Epsv + TypeSet + Opened + Complete + Bye;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_DisableEpsv_SendsPasvWithoutTryingEpsv()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--disable-epsv");

        // curl --disable-epsv ftp://127.0.0.1:47361/d/f.txt
        FtpRun run = await RunAsync(
            diagnostics,
            "/d/f.txt",
            LoggedIn + Ok + "227 Entering Passive Mode (127,0,0,1,194,61)\r\n" + TypeSet + Sized + Opened + Complete + Bye,
            context => context.FtpDisableEpsv = true);

        diagnostics.DiffSent(LogInSent + "CWD d\r\nPASV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD d\r\nPASV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        diagnostics.Assert("data connection target", "127.0.0.1:49725", run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 49725, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        diagnostics.Diff("output", "abc", run.OutputText);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisableEpsvAndPasvRefused_QuitsWithExit13()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--disable-epsv, PASV answered 500");

        // curl --disable-epsv ftp://127.0.0.1:47361/f.txt, PASV answered 500 no
        FtpRun run = await RunAsync(diagnostics, "/f.txt", LoggedIn + "500 no\r\n" + Bye, context => context.FtpDisableEpsv = true);

        diagnostics.DiffSent(LogInSent + "PASV\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "PASV\r\nQUIT\r\n", run.Sent);
        diagnostics.Assert("result", "FtpWeirdPasvReply", run.Result.ExitCode);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpWeirdPasvReply, "Bad PASV/EPSV response: 500"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIp_ConnectsTheDataConnectionToTheAddressThe227Names()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Host + "/f.txt", LoggedIn + "227 Entering Passive Mode (127,0,0,2,194,63)\r\n");
        diagnostics.Arrange("options", "--disable-epsv --no-ftp-skip-pasv-ip");

        // curl --disable-epsv --no-ftp-skip-pasv-ip ftp://127.0.0.1:47361/f.txt, PASV naming
        // 127.0.0.2: curl connects there, fails with exit 7 and sends nothing more.
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + "227 Entering Passive Mode (127,0,0,2,194,63)\r\n"));
        var connector = new QueuedConnector(
            ConnectResult.Connected(control),
            ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.2 port 49727"));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Host + "/f.txt"),
            Output = new MemoryStream(),
            FtpDisableEpsv = true,
            FtpSkipPasvIp = false,
        };

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);
        diagnostics.ActResult(result);

        diagnostics.Diff("control commands sent", FtpDiagnostics.Escape(LogInSent + "PASV\r\n"), FtpDiagnostics.Escape(Encoding.Latin1.GetString(control.Sent)));
        Assert.AreEqual(LogInSent + "PASV\r\n", Encoding.Latin1.GetString(control.Sent));
        diagnostics.Assert("data connection target", "127.0.0.2:49727", connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.2", 49727, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, connector.Targets[1]);
        diagnostics.Assert("result", "CouldntConnect", (result with { Report = null }).ExitCode);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.2 port 49727"), result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIpWithTheControlAddress_Downloads()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "--disable-epsv --no-ftp-skip-pasv-ip");

        // curl --disable-epsv --no-ftp-skip-pasv-ip ftp://127.0.0.1:47361/f.txt
        FtpRun run = await RunAsync(
            diagnostics,
            "/f.txt",
            LoggedIn + "227 Entering Passive Mode (127,0,0,1,194,67)\r\n" + TypeSet + Sized + Opened + Complete + Bye,
            context =>
            {
                context.FtpDisableEpsv = true;
                context.FtpSkipPasvIp = false;
            });

        diagnostics.DiffSent(LogInSent + "PASV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "PASV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        diagnostics.Assert("data connection target", "127.0.0.1:49731", run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 49731, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSkipPasvIpByDefault_IgnoresTheAddressThe227Names()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--disable-epsv");

        // curl --disable-epsv ftp://127.0.0.1:47361/f.txt, PASV naming 127.0.0.2
        FtpRun run = await RunAsync(
            diagnostics,
            "/f.txt",
            LoggedIn + "227 Entering Passive Mode (127,0,0,2,194,63)\r\n" + TypeSet + Sized + Opened + Complete + Bye,
            context => context.FtpDisableEpsv = true);

        diagnostics.Assert("data connection target", "127.0.0.1:49727", run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 49727, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIpWithEpsvAccepted_UsesTheControlHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--no-ftp-skip-pasv-ip");

        // curl --no-ftp-skip-pasv-ip ftp://127.0.0.1:47361/f.txt
        FtpRun run = await RunAsync(diagnostics, "/f.txt", LoggedIn + Retrieved, context => context.FtpSkipPasvIp = false);

        diagnostics.DiffSent(LogInSent + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + RetrieveSent, run.Sent);
        diagnostics.Assert("data connection target", "127.0.0.1:49737", run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 49737, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SingleCwd_ChangesToTheWholeDirectoryAtOnce()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-method singlecwd");

        // curl --ftp-method singlecwd ftp://127.0.0.1:47361/a/b/f.txt
        FtpRun run = await RunAsync(diagnostics, "/a/b/f.txt", LoggedIn + Ok + Retrieved, SingleCwd);

        diagnostics.DiffSent(LogInSent + "CWD a/b\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + "CWD a/b\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SingleCwdForAFileInTheCurrentDirectory_SendsNoCwd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-method singlecwd");

        // curl --ftp-method singlecwd ftp://127.0.0.1:47361/f.txt
        FtpRun run = await RunAsync(diagnostics, "/f.txt", LoggedIn + Retrieved, SingleCwd);

        diagnostics.DiffSent(LogInSent + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SingleCwdRefused_QuitsWithExit9()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-method singlecwd, CWD answered 550");

        // curl --ftp-method singlecwd ftp://127.0.0.1:47361/a/b/f.txt, CWD answered 550
        FtpRun run = await RunAsync(diagnostics, "/a/b/f.txt", LoggedIn + NoSuchDirectory + Bye, SingleCwd);

        diagnostics.DiffSent(LogInSent + "CWD a/b\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD a/b\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Server denied you to change to the given directory"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SingleCwdListing_ChangesToTheWholeDirectoryThenLists()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-method singlecwd");

        // curl --ftp-method singlecwd ftp://127.0.0.1:47361/a/b/
        FtpRun run = await RunAsync(diagnostics, "/a/b/", LoggedIn + Ok + Epsv + TypeSet + Opened + Complete + Bye, SingleCwd);

        diagnostics.DiffSent(LogInSent + "CWD a/b\r\nEPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD a/b\r\nEPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("abc", run.OutputText);
    }

    [TestMethod]
    [DataRow("//abs/f.txt", "CWD /abs", DisplayName = "an absolute directory keeps its slash")]
    [DataRow("//f.txt", "CWD /", DisplayName = "the root directory is /")]
    [DataRow("/a//b%20c/f.txt", "CWD a//b c", DisplayName = "empty segments and escapes are kept as decoded")]
    public async Task ExecuteAsync_SingleCwd_SendsTheDecodedDirectoryPartAsCurlDoes(string path, string cwd)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);
        diagnostics.Arrange("expected CWD", cwd);

        // curl --ftp-method singlecwd ftp://127.0.0.1:47361<path>
        FtpRun run = await RunAsync(diagnostics, path, LoggedIn + Ok + Retrieved, SingleCwd);

        diagnostics.DiffSent(LogInSent + cwd + "\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + cwd + "\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    [DataRow("//abs/f.txt", "CWD /\r\nCWD abs\r\n", DisplayName = "an absolute path changes to / first")]
    [DataRow("///abs/f.txt", "CWD /\r\nCWD abs\r\n", DisplayName = "one / however many slashes start it")]
    [DataRow("/%2Fabs/f.txt", "CWD /\r\nCWD abs\r\n", DisplayName = "an escaped slash that starts it is a /")]
    [DataRow("//f.txt", "CWD /\r\n", DisplayName = "a file in the root directory")]
    [DataRow("/a//b/f.txt", "CWD a\r\nCWD b\r\n", DisplayName = "an empty segment in the middle is skipped")]
    [DataRow("/a%2Fb/f.txt", "CWD a\r\nCWD b\r\n", DisplayName = "an escaped slash splits the segment")]
    [DataRow("/a/%2Fb/f.txt", "CWD a\r\nCWD b\r\n", DisplayName = "an escaped slash after a slash is an empty segment")]
    public async Task ExecuteAsync_MultiCwd_SplitsTheDecodedPathAsCurlDoes(string path, string cwds)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);
        diagnostics.Arrange("expected CWDs", FtpDiagnostics.Escape(cwds));

        // curl ftp://127.0.0.1:47361<path> (the default --ftp-method multicwd), BL-446
        string cwdReplies = string.Concat(Enumerable.Repeat(Ok, cwds.Split("CWD").Length - 1));
        FtpRun run = await RunAsync(diagnostics, path, LoggedIn + cwdReplies + Retrieved, _ => { });

        diagnostics.DiffSent(LogInSent + cwds + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + cwds + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultiCwdListingOfAnAbsoluteDirectory_ChangesToRootFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", "//abs/");

        // curl ftp://127.0.0.1:47361//abs/, BL-446
        FtpRun run = await RunAsync(diagnostics, "//abs/", LoggedIn + Ok + Ok + Epsv + TypeSet + Opened + Complete + Bye, _ => { });

        diagnostics.DiffSent(LogInSent + "CWD /\r\nCWD abs\r\nEPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD /\r\nCWD abs\r\nEPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("abc", run.OutputText);
    }

    [TestMethod]
    [DataRow("/a/b/f.txt", "a/b/f.txt", DisplayName = "a relative path")]
    [DataRow("//abs/x%20y/f.txt", "/abs/x y/f.txt", DisplayName = "an absolute path, decoded")]
    public async Task ExecuteAsync_NoCwd_NamesTheWholePathInEachCommand(string path, string name)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);
        diagnostics.Arrange("expected name", name);

        // curl --ftp-method nocwd ftp://127.0.0.1:47361<path>
        FtpRun run = await RunAsync(diagnostics, path, LoggedIn + Retrieved, NoCwd);

        diagnostics.DiffSent(LogInSent + $"EPSV\r\nTYPE I\r\nSIZE {name}\r\nRETR {name}\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + $"EPSV\r\nTYPE I\r\nSIZE {name}\r\nRETR {name}\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    [DataRow("/a/b/", "LIST a/b", DisplayName = "a directory is LIST's argument")]
    [DataRow("/", "LIST", DisplayName = "the current directory has no argument")]
    [DataRow("//", "LIST /", DisplayName = "the root directory is /")]
    public async Task ExecuteAsync_NoCwdListing_ListsTheDirectoryByName(string path, string list)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);
        diagnostics.Arrange("expected list command", list);

        // curl --ftp-method nocwd ftp://127.0.0.1:47361<path>
        FtpRun run = await RunAsync(diagnostics, path, LoggedIn + Epsv + TypeSet + Opened + Complete + Bye, NoCwd);

        diagnostics.DiffSent(LogInSent + $"EPSV\r\nTYPE A\r\n{list}\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + $"EPSV\r\nTYPE A\r\n{list}\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("abc", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCwdUpload_StoresUnderTheWholePath()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-method nocwd, upload hello");

        // curl --ftp-method nocwd -T up.txt ftp://127.0.0.1:47361/a/b/f.txt
        FtpRun run = await RunAsync(diagnostics, "/a/b/f.txt", LoggedIn + Stored, context =>
        {
            NoCwd(context);
            context.Upload = Hello();
        });

        diagnostics.DiffSent(LogInSent + "EPSV\r\nTYPE I\r\nSTOR a/b/f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "EPSV\r\nTYPE I\r\nSTOR a/b/f.txt\r\nQUIT\r\n", run.Sent);
        diagnostics.Diff("uploaded", "hello", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual("hello", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCwdHead_NamesTheWholePath()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "-I --ftp-method nocwd");

        // curl -I --ftp-method nocwd ftp://127.0.0.1:47361/a/b/f.txt
        FtpRun run = await RunAsync(
            diagnostics,
            "/a/b/f.txt",
            LoggedIn + "213 20260927123456\r\n" + TypeSet + Sized + "350 Restarting at 0\r\n" + Bye,
            context =>
            {
                NoCwd(context);
                context.NoBody = true;
                context.HeaderOutput = context.Output;
            });

        diagnostics.DiffSent(LogInSent + "MDTM a/b/f.txt\r\nTYPE I\r\nSIZE a/b/f.txt\r\nREST 0\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "MDTM a/b/f.txt\r\nTYPE I\r\nSIZE a/b/f.txt\r\nREST 0\r\nQUIT\r\n", run.Sent);
        diagnostics.Diff("output", FtpDiagnostics.Escape("Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\nContent-Length: 3\r\nAccept-ranges: bytes\r\n"), FtpDiagnostics.Escape(run.OutputText));
        Assert.AreEqual("Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\nContent-Length: 3\r\nAccept-ranges: bytes\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsWithAMissingDirectory_MakesItAndChangesIntoIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-create-dirs -T");

        // curl --ftp-create-dirs -T up.txt ftp://127.0.0.1:47361/a/b/f.txt, the first CWD
        // answered 550 and MKD answered 257
        FtpRun run = await RunAsync(
            diagnostics,
            "/a/b/f.txt",
            LoggedIn + NoSuchDirectory + "257 \"/a\" created\r\n" + Ok + Ok + Stored,
            CreateDirsUpload);

        diagnostics.DiffSent(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nCWD b\r\n" + StoreSent, run.Sent);
        Assert.AreEqual(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nCWD b\r\n" + StoreSent, run.Sent);
        diagnostics.Diff("uploaded", "hello", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual("hello", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsWithMkdRefused_TriesCwdAgainThenQuitsWithExit9()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-create-dirs -T, every CWD and MKD answered 550");

        // curl --ftp-create-dirs -T up.txt ftp://127.0.0.1:47361/a/b/f.txt, every CWD and MKD
        // answered 550
        FtpRun run = await RunAsync(
            diagnostics,
            "/a/b/f.txt",
            LoggedIn + NoSuchDirectory + "550 Permission denied\r\n" + NoSuchDirectory + Bye,
            CreateDirsUpload);

        diagnostics.DiffSent(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Server denied you to change to the given directory"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsWithMkdAcceptedButCwdStillRefused_QuitsWithExit9()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-create-dirs -T, MKD answered 257 and both CWDs 550");

        // curl --ftp-create-dirs -T up.txt ftp://127.0.0.1:47361/a/f.txt, MKD answered 257
        // and both CWDs 550
        FtpRun run = await RunAsync(diagnostics, "/a/f.txt", LoggedIn + NoSuchDirectory + Created + NoSuchDirectory + Bye, CreateDirsUpload);

        diagnostics.DiffSent(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nQUIT\r\n", run.Sent);
        diagnostics.Assert("exit code", CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsWithEveryMkdRefused_GivesEachDirectoryItsOwnMkd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-create-dirs -T, every MKD answered 550");

        // curl --ftp-create-dirs -T up.txt ftp://127.0.0.1:47361/a/b/f.txt: CWD a 550,
        // MKD a 550, CWD a 250, CWD b 550, MKD b 550, CWD b 550
        const string Refused = "550 Permission denied\r\n";
        FtpRun run = await RunAsync(
            diagnostics,
            "/a/b/f.txt",
            LoggedIn + NoSuchDirectory + Refused + Ok + NoSuchDirectory + Refused + NoSuchDirectory + Bye,
            CreateDirsUpload);

        diagnostics.DiffSent(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nCWD b\r\nMKD b\r\nCWD b\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nCWD b\r\nMKD b\r\nCWD b\r\nQUIT\r\n", run.Sent);
        diagnostics.Assert("exit code", CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsWithTwoMissingDirectories_MakesBoth()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-create-dirs -T");

        // curl --ftp-create-dirs -T up.txt ftp://127.0.0.1:47361/a/b/f.txt
        FtpRun run = await RunAsync(
            diagnostics,
            "/a/b/f.txt",
            LoggedIn + NoSuchDirectory + Created + Ok + NoSuchDirectory + Created + Ok + Stored,
            CreateDirsUpload);

        diagnostics.DiffSent(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nCWD b\r\nMKD b\r\nCWD b\r\n" + StoreSent, run.Sent);
        Assert.AreEqual(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\nCWD b\r\nMKD b\r\nCWD b\r\n" + StoreSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsOnADownload_MakesTheMissingDirectoryToo()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-create-dirs");

        // curl --ftp-create-dirs ftp://127.0.0.1:47361/a/f.txt
        FtpRun run = await RunAsync(
            diagnostics,
            "/a/f.txt",
            LoggedIn + NoSuchDirectory + Created + Ok + Retrieved,
            context => context.FtpCreateDirectories = true);

        diagnostics.DiffSent(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(LogInSent + "CWD a\r\nMKD a\r\nCWD a\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsWithSingleCwd_MakesTheWholeDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "--ftp-create-dirs --ftp-method singlecwd -T");

        // curl --ftp-create-dirs --ftp-method singlecwd -T up.txt ftp://127.0.0.1:47361/a/b/f.txt
        FtpRun run = await RunAsync(
            diagnostics,
            "/a/b/f.txt",
            LoggedIn + NoSuchDirectory + Created + Ok + Stored,
            context =>
            {
                CreateDirsUpload(context);
                SingleCwd(context);
            });

        diagnostics.DiffSent(LogInSent + "CWD a/b\r\nMKD a/b\r\nCWD a/b\r\n" + StoreSent, run.Sent);
        Assert.AreEqual(LogInSent + "CWD a/b\r\nMKD a/b\r\nCWD a/b\r\n" + StoreSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreateDirsWithMkdAnswered421_EndsWithExit28AndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("option", "--ftp-create-dirs, MKD answered 421");

        // curl --ftp-create-dirs ftp://127.0.0.1:47361/a/f, MKD answered 421 bye
        FtpRun run = await RunAsync(diagnostics, "/a/f", LoggedIn + "550 no\r\n421 bye\r\n", context => context.FtpCreateDirectories = true);

        diagnostics.DiffSent(LogInSent + "CWD a\r\nMKD a\r\n", run.Sent);
        Assert.AreEqual(LogInSent + "CWD a\r\nMKD a\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached"), run.Result);
    }

    private static void SingleCwd(MutableContext context) => context.FtpFileMethod = FtpFileMethod.SingleCwd;

    private static void NoCwd(MutableContext context) => context.FtpFileMethod = FtpFileMethod.NoCwd;

    private static void CreateDirsUpload(MutableContext context)
    {
        context.FtpCreateDirectories = true;
        context.Upload = Hello();
    }

    private static MemoryStream Hello() => new(Encoding.Latin1.GetBytes("hello"));

    private static async Task<FtpRun> RunAsync(TestDiagnostics diagnostics, string path, string replies, Action<MutableContext> adjust)
    {
        diagnostics.ArrangeFtp(Host + path, replies, "abc");
        FtpRun run = await FtpRun.ExecuteAsync(Host + path, replies, "abc", context => MutableContext.Build(context, adjust));
        diagnostics.ActRun(run);
        return run;
    }
}
