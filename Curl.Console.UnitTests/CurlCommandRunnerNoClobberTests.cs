using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--no-clobber</c> and <c>--clobber</c> end to end through the runner and
/// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />, with the output
/// files in an <see cref="InMemoryFileSystem" />. Every expectation was measured on 2026-09-28
/// with curl 8.21.0 (mingw, Schannel) against a loopback server answering with a 5-byte
/// <c>hello</c> body (BL-492 Notes): a taken name is kept and the body goes to the first free
/// <c>.1</c> ... <c>.99</c>, and when none is free the transfer fails with exit 23 while
/// <c>%{filename_effective}</c> names <c>.99</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerNoClobberTests
{
    private const string Url = "http://127.0.0.1:18492/";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string Disposition =
        "HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\"cd.txt\"\r\nContent-Length: 5\r\n\r\nhello";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_NoClobberTargetAbsent_WritesTheTargetWithoutOverwriting()
    {
        int exitCode = await RunAsync(Ok, "-sS", "-o", "out.txt", "--no-clobber", "-w", "%{filename_effective}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "out.txt", StandardOutputText);
        Diagnostics.Diff("file out.txt", "hello", WrittenText("out.txt"));
        Diagnostics.Assert("write modes", FileWriteMode.CreateNew, WriteModesText());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("out.txt", StandardOutputText);
        Assert.AreEqual("hello", WrittenText("out.txt"));
        CollectionAssert.AreEqual(new[] { FileWriteMode.CreateNew }, outputFiles.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_NoClobberTargetPresent_WritesTheFirstNumberedName()
    {
        outputFiles.ExistingPaths.Add("out.txt");
        Diagnostics.Arrange("existing files", "out.txt");

        int exitCode = await RunAsync(Ok, "-sS", "-o", "out.txt", "--no-clobber", "-w", "%{filename_effective}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "out.txt.1", StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Diagnostics.Assert("files written", "out.txt.1", WrittenPathsText());
        Diagnostics.Diff("file out.txt.1", "hello", WrittenText("out.txt.1"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("out.txt.1", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
        CollectionAssert.AreEquivalent(new[] { "out.txt.1" }, outputFiles.Written.Keys);
        Assert.AreEqual("hello", WrittenText("out.txt.1"));
    }

    [TestMethod]
    public async Task RunAsync_NoClobberTargetAndFirstNumberPresent_WritesTheSecond()
    {
        outputFiles.ExistingPaths.UnionWith(["out.txt", "out.txt.1"]);
        Diagnostics.Arrange("existing files", "out.txt, out.txt.1");

        int exitCode = await RunAsync(Ok, "-sS", "-o", "out.txt", "--no-clobber", "-w", "%{filename_effective}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "out.txt.2", StandardOutputText);
        Diagnostics.Assert("files written", "out.txt.2", WrittenPathsText());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("out.txt.2", StandardOutputText);
        CollectionAssert.AreEquivalent(new[] { "out.txt.2" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_NoClobberOnlyNinetyNineFree_WritesNinetyNine()
    {
        TakeTargetAndNumbers(98);

        int exitCode = await RunAsync(Ok, "-sS", "-o", "out.txt", "--no-clobber", "-w", "%{filename_effective}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "out.txt.99", StandardOutputText);
        Diagnostics.Diff("file out.txt.99", "hello", WrittenText("out.txt.99"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("out.txt.99", StandardOutputText);
        Assert.AreEqual("hello", WrittenText("out.txt.99"));
    }

    [TestMethod]
    [DataRow(99)]
    [DataRow(100)]
    public async Task RunAsync_NoClobberNoNameUpToNinetyNineFree_Exits23NamingNinetyNine(int lastTaken)
    {
        TakeTargetAndNumbers(lastTaken);

        int exitCode = await RunAsync(Ok, "-sS", "-o", "out.txt", "--no-clobber", "-w", "%{filename_effective}", Url);

        int createNewAttempts = outputFiles.WriteModes.Count(mode => mode == FileWriteMode.CreateNew);
        Diagnostics.Act("create-new attempts", createNewAttempts);
        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff("stdout", "out.txt.99", StandardOutputText);
        Diagnostics.Diff("stderr", "curl: (23) client returned ERROR on write of 5 bytes\n", Lf(StandardErrorText));
        Diagnostics.Assert("files written count", 0, outputFiles.Written.Count);
        Diagnostics.Assert("create-new attempts", 100, createNewAttempts);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual("out.txt.99", StandardOutputText);
        Assert.AreEqual("curl: (23) client returned ERROR on write of 5 bytes" + NewLine, StandardErrorText);
        Assert.AreEqual(0, outputFiles.Written.Count);
        Assert.AreEqual(100, outputFiles.WriteModes.Count(mode => mode == FileWriteMode.CreateNew));
    }

    [TestMethod]
    public async Task RunAsync_NoClobberNoNameFreeWithoutSilent_WarnsNamingTheTarget()
    {
        TakeTargetAndNumbers(100);

        int exitCode = await RunAsync(Ok, "--no-progress-meter", "-o", "out.txt", "--no-clobber", Url);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file out.txt: File exists\ncurl: (23) client returned ERROR on write of 5 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "Warning: Failed to open the file out.txt: File exists" + NewLine
            + "curl: (23) client returned ERROR on write of 5 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoClobberTargetUnopenable_DoesNotNumberAndWarnsWithTheFailure()
    {
        outputFiles.UnwritablePaths.Add("out.txt");
        Diagnostics.Arrange("unwritable files", "out.txt");

        int exitCode = await RunAsync(Ok, "--no-progress-meter", "-o", "out.txt", "--no-clobber", "-w", "%{filename_effective}", Url);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff("stdout", "out.txt", StandardOutputText);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file out.txt: No such file or directory\ncurl: (23) client returned ERROR on write of 5 bytes\n",
            Lf(StandardErrorText));
        Diagnostics.Assert("write attempts", 1, outputFiles.WriteModes.Count);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual("out.txt", StandardOutputText);
        Assert.AreEqual(
            "Warning: Failed to open the file out.txt: No such file or directory" + NewLine
            + "curl: (23) client returned ERROR on write of 5 bytes" + NewLine,
            StandardErrorText);
        Assert.AreEqual(1, outputFiles.WriteModes.Count);
    }

    [TestMethod]
    public async Task RunAsync_NoClobberEmptyBody_CreatesAnEmptyNumberedFile()
    {
        outputFiles.ExistingPaths.Add("out.txt");
        Diagnostics.Arrange("existing files", "out.txt");

        int exitCode = await RunAsync(
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", "-sS", "-o", "out.txt", "--no-clobber", "-w", "%{filename_effective}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "out.txt.1", StandardOutputText);
        Diagnostics.Diff("file out.txt.1", string.Empty, WrittenText("out.txt.1"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("out.txt.1", StandardOutputText);
        Assert.AreEqual(string.Empty, WrittenText("out.txt.1"));
    }

    [TestMethod]
    public async Task RunAsync_ClobberAfterNoClobber_Overwrites()
    {
        outputFiles.ExistingPaths.Add("out.txt");
        Diagnostics.Arrange("existing files", "out.txt");

        int exitCode = await RunAsync(Ok, "-sS", "-o", "out.txt", "--no-clobber", "--clobber", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("write modes", FileWriteMode.Truncate, WriteModesText());
        Diagnostics.Diff("file out.txt", "hello", WrittenText("out.txt"));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate }, outputFiles.WriteModes);
        Assert.AreEqual("hello", WrittenText("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_NoClobberWithResume_IsRefusedWithoutWriting()
    {
        outputFiles.ExistingPaths.Add("out.txt");
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("abc");
        Diagnostics.Arrange("existing out.txt", "abc");

        int exitCode = await RunAsync(
            "HTTP/1.1 206 Partial Content\r\nContent-Range: bytes 3-7/8\r\nContent-Length: 5\r\n\r\nhello",
            "-sS", "-C", "3", "-o", "out.txt", "--no-clobber", Url);

        // curl 8.21.0 refuses -C with --no-clobber before any transfer (BL-1223).
        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert("write attempts", 0, outputFiles.WriteModes.Count);
        Diagnostics.Diff(
            "stderr",
            "curl: --continue-at is mutually exclusive with --no-clobber\n"
            + "curl: option --no-clobber: is badly used here\n"
            + "curl: try 'curl --help' or 'curl --manual' for more information\n",
            Lf(StandardErrorText));
        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(outputFiles.WriteModes);
        Assert.AreEqual(
            "curl: --continue-at is mutually exclusive with --no-clobber" + NewLine
            + "curl: option --no-clobber: is badly used here" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoClobberRemoteHeaderNameTaken_WritesTheFirstNumberedName()
    {
        outputFiles.ExistingPaths.Add("cd.txt");
        Diagnostics.Arrange("existing files", "cd.txt");

        int exitCode = await RunAsync(Disposition, "-sS", "-OJ", "--no-clobber", "-w", "%{filename_effective}", Url + "x.bin");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "cd.txt.1", StandardOutputText);
        Diagnostics.Diff("file cd.txt.1", "hello", WrittenText("cd.txt.1"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("cd.txt.1", StandardOutputText);
        Assert.AreEqual("hello", WrittenText("cd.txt.1"));
    }

    [TestMethod]
    public async Task RunAsync_ClobberRemoteHeaderNameTaken_Overwrites()
    {
        outputFiles.ExistingPaths.Add("cd.txt");
        Diagnostics.Arrange("existing files", "cd.txt");

        int exitCode = await RunAsync(Disposition, "-sS", "-OJ", "--clobber", "-w", "%{filename_effective}", Url + "x.bin");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "cd.txt", StandardOutputText);
        Diagnostics.Diff("file cd.txt", "hello", WrittenText("cd.txt"));
        Diagnostics.Assert("write modes", FileWriteMode.Truncate, WriteModesText());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("cd.txt", StandardOutputText);
        Assert.AreEqual("hello", WrittenText("cd.txt"));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate }, outputFiles.WriteModes);
    }

    private void TakeTargetAndNumbers(int lastNumber)
    {
        outputFiles.ExistingPaths.Add("out.txt");
        for (int number = 1; number <= lastNumber; number++)
        {
            outputFiles.ExistingPaths.Add("out.txt." + number);
        }

        Diagnostics.Arrange("existing files", "out.txt, out.txt.1 ... out.txt." + lastNumber);
    }

    private string WrittenText(string path) => Encoding.Latin1.GetString(outputFiles.Written[path].ToArray());

    private string WrittenPathsText() => string.Join(", ", outputFiles.Written.Keys.Order(StringComparer.Ordinal));

    private string WriteModesText() => string.Join(", ", outputFiles.WriteModes);

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(string response, params string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(' ', arguments));
        Diagnostics.Arrange("scripted response", Lf(response));
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(response)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

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
                    runsOnWindows: false,
                    outputPaths: outputFiles)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("files written", WrittenPathsText());
        Diagnostics.Act("write modes", WriteModesText());
        return exitCode;
    }
}
