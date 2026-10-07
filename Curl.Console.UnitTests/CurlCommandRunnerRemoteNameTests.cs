using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-O</c>, <c>-J</c>, <c>--output-dir</c> and <c>--create-dirs</c> end to end through the
/// runner and <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />. Every
/// expectation was measured on 2026-09-27 with curl 8.21.0 (mingw, Schannel) against a loopback
/// server on 127.0.0.1 answering with a 5-byte <c>hello</c> body (BL-239 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRemoteNameTests
{
    private const string Host = "http://127.0.0.1:18239";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string WrittenNames => string.Join(", ", outputFiles.Written.Keys.Order(StringComparer.Ordinal));

    [TestMethod]
    public async Task RunAsync_RemoteName_WritesTheLastPathSegmentWithoutQueryOrFragment()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-O", Host + "/dir/file.txt?q=1#f");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("file.txt content", "hello", WrittenText("file.txt"));
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("file.txt"));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameOfDirectoryPath_WritesTheLastSegmentThatIsNotEmpty()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-O", Host + "/a/b//");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("b content", "hello", WrittenText("b"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("b"));
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameOfRootPath_WarnsAndWritesCurlResponse()
    {
        int exitCode = await RunAsync([Ok], "-O", Host + "/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("curl_response content", "hello", WrittenText("curl_response"));
        Diagnostics.Diff("stderr", "Warning: No remote filename, uses \"curl_response\"\n", Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("curl_response"));
        Assert.AreEqual("Warning: No remote filename, uses \"curl_response\"" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentRemoteNameOfRootPath_DoesNotWarn()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-O", Host);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("curl_response content", "hello", WrittenText("curl_response"));
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("curl_response"));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameKeepsPercentEncoding()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-O", Host + "/a%20b%3F.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("a%20b%3F.txt content", "hello", WrittenText("a%20b%3F.txt"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("a%20b%3F.txt"));
    }

    [TestMethod]
    public async Task RunAsync_OnWindowsRemoteName_ReplacesColonAndRenamesReservedDeviceNames()
    {
        int exitCode = await RunAsync([Ok, Ok], true, "-sS", "--remote-name-all", Host + "/a:b.txt", Host + "/con");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "_con, a_b.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "a_b.txt", "_con" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_NotOnWindowsRemoteName_KeepsTheName()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-O", Host + "/a:b.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "a:b.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "a:b.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameThenStandardOutput_WritesEachWhereItsOptionSays()
    {
        int exitCode = await RunAsync([Ok, Ok], "-sS", "-O", Host + "/f.txt", Host + "/g.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("f.txt content", "hello", WrittenText("f.txt"));
        Diagnostics.Diff("stdout", "hello", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("f.txt"));
        Assert.AreEqual("hello", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirectory_PutsRemoteAndOutputNamesUnderIt()
    {
        int exitCode = await RunAsync([Ok, Ok], "-sS", "--output-dir", "od", "-O", "-o", "sub.txt", Host + "/f.txt", Host + "/g.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "od/f.txt, od/sub.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "od/f.txt", "od/sub.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_OnWindowsOutputDirectory_KeepsTheDirectoryAsTyped()
    {
        outputFiles.UnwritablePaths.Add("o?d/x");
        Diagnostics.Arrange("unwritable path", "o?d/x");

        int exitCode = await RunAsync([Ok], true, "-sS", "--output-dir", "o?d", "-o", "x", Host + "/f.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stderr", "curl: (23) client returned ERROR on write of 5 bytes\n", Lf(StandardErrorText));
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("curl: (23) client returned ERROR on write of 5 bytes" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirectoryMissing_WarnsWithTheJoinedName()
    {
        outputFiles.UnwritablePaths.Add("nod/f.txt");
        Diagnostics.Arrange("unwritable path", "nod/f.txt");

        int exitCode = await RunAsync([Ok], "-O", "--output-dir", "nod", Host + "/f.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file nod/f.txt: No such file or directory\ncurl: (23) client returned ERROR on write of 5 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "Warning: Failed to open the file nod/f.txt: No such file or directory" + NewLine
            + "curl: (23) client returned ERROR on write of 5 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CreateDirectories_CreatesEachDirectoryOfTheOutputPath()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-O", "--output-dir", "n1/n2", "--create-dirs", Host + "/f.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("directories created", "n1, n1/n2", string.Join(", ", outputFiles.CreatedDirectories));
        Diagnostics.Diff("n1/n2/f.txt content", "hello", WrittenText("n1/n2/f.txt"));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "n1", "n1/n2" }, outputFiles.CreatedDirectories);
        Assert.AreEqual("hello", WrittenText("n1/n2/f.txt"));
    }

    [TestMethod]
    public async Task RunAsync_CreateDirectoriesFails_ReportsTheDirectoryAndStopsTheRun()
    {
        outputFiles.UncreatableDirectories.Add("blk/b");
        Diagnostics.Arrange("uncreatable directory", "blk/b");

        int exitCode = await RunAsync([Ok, Ok], "--create-dirs", "-o", "blk/b/c.txt", "-o", "ok.txt", Host + "/f.txt", Host + "/g.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: Error creating directory blk/b\ncurl: (23) Failed writing received data to disk/application\n",
            Lf(StandardErrorText));
        Diagnostics.Assert("request bytes sent", 0, server.Written.Length);
        Diagnostics.Assert("files written", 0, outputFiles.Written.Count);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "curl: Error creating directory blk/b" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
        Assert.AreEqual(0, server.Written.Length);
        Assert.AreEqual(0, outputFiles.Written.Count);
    }

    /// <summary>
    /// A <c>mkdir</c> that fails with <c>EROFS</c> prints curl 8.21.0's own message for it,
    /// <c>tool_dirhie.c</c>'s <c>show_dir_errno</c>, and still exits 23 (BL-1433).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_CreateDirectoriesOnReadOnlyFileSystem_SaysSoAndStopsTheRun()
    {
        outputFiles.DirectoryErrorNumbers.Add("ro", 30);
        Diagnostics.Arrange("directory error", "ro fails with errno 30 (EROFS)");

        int exitCode = await RunAsync([Ok], "--create-dirs", "-o", "ro/c.txt", Host + "/f.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: ro resides on a read-only file system\ncurl: (23) Failed writing received data to disk/application\n",
            Lf(StandardErrorText));
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "curl: ro resides on a read-only file system" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentCreateDirectoriesFails_PrintsNothing()
    {
        outputFiles.UncreatableDirectories.Add("blk/b");
        Diagnostics.Arrange("uncreatable directory", "blk/b");

        int exitCode = await RunAsync([Ok], "-s", "--create-dirs", "-o", "blk/b/c.txt", Host + "/f.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderName_WritesTheContentDispositionName()
    {
        int exitCode = await RunAsync([Disposition("attachment; filename=\"x y.txt\"")], "-sS", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "x y.txt", WrittenNames);
        Diagnostics.Diff("x y.txt content", "hello", WrittenText("x y.txt"));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "x y.txt" }, outputFiles.Written.Keys);
        Assert.AreEqual("hello", WrittenText("x y.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameWithoutHeader_WritesTheUrlName()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "u.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "u.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameOfNotFound_WritesTheUrlName()
    {
        int exitCode = await RunAsync(
            [Disposition("attachment; filename=\"nf.txt\"", "404 Not Found")], "-sS", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "u.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "u.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameOfRedirect_WritesTheContentDispositionName()
    {
        int exitCode = await RunAsync(
            [Disposition("attachment; filename=\"r.txt\"", "302 Found")], "-sS", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "r.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "r.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameTaken_WarnsAndExits23KeepingTheFile()
    {
        outputFiles.ExistingPaths.Add("x.txt");
        Diagnostics.Arrange("existing path", "x.txt");

        int exitCode = await RunAsync([Disposition("attachment; filename=\"x.txt\"")], "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file x.txt: File exists\ncurl: (23) client returned ERROR on write of 51 bytes\n",
            Lf(StandardErrorText));
        Diagnostics.Assert("files written", 0, outputFiles.Written.Count);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "Warning: Failed to open the file x.txt: File exists" + NewLine
            + "curl: (23) client returned ERROR on write of 51 bytes" + NewLine,
            StandardErrorText);
        Assert.AreEqual(0, outputFiles.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameTakenJustBeforeTheOpen_WarnsAndExits23WithoutOverwriting()
    {
        outputFiles.BeforeCreateNew = path => outputFiles.ExistingPaths.Add(path);
        Diagnostics.Arrange("file system", "every path comes to exist just before it is created");

        int exitCode = await RunAsync([Disposition("attachment; filename=\"x.txt\"")], "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file x.txt: File exists\ncurl: (23) client returned ERROR on write of 51 bytes\n",
            Lf(StandardErrorText));
        Diagnostics.Assert("files written", 0, outputFiles.Written.Count);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "Warning: Failed to open the file x.txt: File exists" + NewLine
            + "curl: (23) client returned ERROR on write of 51 bytes" + NewLine,
            StandardErrorText);
        Assert.AreEqual(0, outputFiles.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameEmpty_WarnsAndExits23()
    {
        int exitCode = await RunAsync([Disposition("attachment; filename=\"\"")], "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file : No such file or directory\ncurl: (23) client returned ERROR on write of 46 bytes\n",
            Lf(StandardErrorText));
        Diagnostics.Assert("files written", 0, outputFiles.Written.Count);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "Warning: Failed to open the file : No such file or directory" + NewLine
            + "curl: (23) client returned ERROR on write of 46 bytes" + NewLine,
            StandardErrorText);
        Assert.AreEqual(0, outputFiles.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameUnopenable_WarnsWithTheOpenFailure()
    {
        outputFiles.UnwritablePaths.Add("x.txt");
        Diagnostics.Arrange("unwritable path", "x.txt");

        int exitCode = await RunAsync([Disposition("attachment; filename=\"x.txt\"")], "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file x.txt: No such file or directory\ncurl: (23) client returned ERROR on write of 51 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "Warning: Failed to open the file x.txt: No such file or directory" + NewLine
            + "curl: (23) client returned ERROR on write of 51 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameAfterResumeOpenedTheFile_Exits23KeepingIt()
    {
        outputFiles.ExistingContent["u.txt"] = Encoding.ASCII.GetBytes("abc");
        Diagnostics.Arrange("existing content", "u.txt: abc");

        int exitCode = await RunAsync([Disposition("attachment; filename=\"x.txt\"")], "-sS", "-C", "3", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stderr", "curl: (23) client returned ERROR on write of 51 bytes\n", Lf(StandardErrorText));
        Diagnostics.Diff("u.txt content", "abc", WrittenText("u.txt"));
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("curl: (23) client returned ERROR on write of 51 bytes" + NewLine, StandardErrorText);
        Assert.AreEqual("abc", WrittenText("u.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameWithoutRemoteName_WritesToStandardOutput()
    {
        int exitCode = await RunAsync([Disposition("attachment; filename=\"x.txt\"")], "-sS", "-J", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "hello", StandardOutputText);
        Diagnostics.Assert("files written", 0, outputFiles.Written.Count);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.AreEqual(0, outputFiles.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameWithOutputName_WritesTheOutputName()
    {
        int exitCode = await RunAsync([Disposition("attachment; filename=\"x.txt\"")], "-sS", "-J", "-o", "o.txt", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "o.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "o.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameWithInclude_WritesEveryHeaderLineToTheNamedFile()
    {
        string response = Disposition("attachment; filename=\"x.txt\"");

        int exitCode = await RunAsync([response], "-sS", "-i", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "x.txt", WrittenNames);
        Diagnostics.Diff("x.txt content", Lf(response), Lf(WrittenText("x.txt")));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "x.txt" }, outputFiles.Written.Keys);
        Assert.AreEqual(response, WrittenText("x.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameWithDumpHeader_StillWritesTheHeaderLines()
    {
        string response = Disposition("attachment; filename=\"x.txt\"");

        int exitCode = await RunAsync([response], "-sS", "-D", "-", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", Lf(response[..^"hello".Length]), Lf(StandardOutputText));
        Diagnostics.Diff("x.txt content", "hello", WrittenText("x.txt"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(response[..^"hello".Length], StandardOutputText);
        Assert.AreEqual("hello", WrittenText("x.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameWithOutputDirectory_PutsTheNameUnderIt()
    {
        int exitCode = await RunAsync([Disposition("attachment; filename=\"x.txt\"")], "-sS", "-OJ", "--output-dir", "od", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "od/x.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "od/x.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_OnWindowsRemoteHeaderName_RewritesTheName()
    {
        int exitCode = await RunAsync([Disposition("attachment; filename=\"a*b?.txt\"")], true, "-sS", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written (sorted)", "a_b_.txt", WrittenNames);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "a_b_.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameWithRemoteTime_StampsTheNamedFile()
    {
        string response = "HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\"x.txt\"\r\n"
            + "Last-Modified: Sat, 01 Jan 2022 00:00:00 GMT\r\nContent-Length: 5\r\n\r\nhello";

        int exitCode = await RunAsync([response], "-sS", "-R", "-OJ", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stamped paths", "x.txt", string.Join(", ", outputFiles.LastWriteTimesSet.Select(set => set.Path)));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("x.txt", outputFiles.LastWriteTimesSet.Single().Path);
    }

    /// <summary>
    /// curl 8.21.0 (mingw, Schannel), measured 2026-10-03: <c>--no-progress-meter -R -o</c>
    /// against <c>Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT</c> prints only the capping
    /// warning, stamps 30827-12-31T23:59:59Z and exits 0 (BL-1425).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_OnWindowsRemoteTimePast30827_CapsTheStampToTheMaximumWithCurlsWarning()
    {
        string response = "HTTP/1.1 200 OK\r\nLast-Modified: Mon, 01 Jan 40000 00:00:00 GMT\r\nContent-Length: 5\r\n\r\nhello";

        int exitCode = await RunAsync([response], true, "--no-progress-meter", "-R", "-o", "out.txt", Host + "/u.txt");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("out.txt content", "hello", WrittenText("out.txt"));
        Diagnostics.Assert("stamps set", ("out.txt", 910670515199L, false), outputFiles.LastWriteTimesSet.SingleOrDefault());
        Diagnostics.Diff("stderr", "Warning: Capping set filetime to max to avoid overflow\n", Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("out.txt"));
        Assert.AreEqual(("out.txt", 910670515199L, false), outputFiles.LastWriteTimesSet.Single());
        Assert.AreEqual(
            "Warning: Capping set filetime to max to avoid overflow" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_CreateDirectoriesWithoutGivenOutputPaths_UsesTheDiskAndCreatesNothingForABareName()
    {
        server = new ScriptedConnector([Encoding.Latin1.GetBytes(Ok)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        string[] arguments = ["-sS", "--create-dirs", "-o", "c.txt", Host + "/f.txt"];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("output paths", "none given to the runner");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("c.txt content", "hello", WrittenText("c.txt"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("c.txt"));
    }

    private static string Disposition(string value, string status = "200 OK") =>
        $"HTTP/1.1 {status}\r\nContent-Disposition: {value}\r\nContent-Length: 5\r\n\r\nhello";

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private string WrittenText(string path) => Encoding.Latin1.GetString(outputFiles.Written[path].ToArray());

    private Task<int> RunAsync(string[] responses, params string[] arguments) => RunAsync(responses, false, arguments);

    private async Task<int> RunAsync(string[] responses, bool runsOnWindows, params string[] arguments)
    {
        server = new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("scripted responses", Lf(string.Join(" | ", responses)));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows,
                    outputPaths: outputFiles)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("files written (sorted)", WrittenNames);
        return exitCode;
    }
}
