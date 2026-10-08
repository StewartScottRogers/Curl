using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins where <c>--stderr</c> sends standard error, against curl 8.21.0 (mingw, Schannel) measured
/// on 2026-09-27 (BL-410 Notes): <c>--stderr se -v http://127.0.0.1:1/</c> wrote the <c>-v</c> lines
/// and the <c>curl: (7)</c> line to <c>se</c>, truncated, and nothing to standard error;
/// <c>--stderr -</c> wrote them to standard output; <c>--stderr ''</c> printed
/// <c>Warning: Warning: Failed to open </c> and carried on to standard error. curl opens the file
/// while it parses the option, so position decides where the parser's lines go (BL-476):
/// <c>--stderr se -H nocolon</c> put the warning in <c>se</c>, <c>-H nocolon --stderr se</c> on
/// standard error, <c>--stderr a -H nocolon --stderr b</c> in <c>a</c>, and <c>--stderr se -d @nosuch</c>
/// the three refusal lines in <c>se</c> (exit 26); <c>--stderr adir -s</c> warned, <c>-s --stderr adir</c> did not.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerStandardErrorFileTests
{
    private static readonly string FailureLine =
        "curl: (7) Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server" + Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    private static readonly string NoColonWarning =
        "Warning: The provided HTTP header 'nocolon' does not look like a header?" + Environment.NewLine;

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    private string FileText(string path) => Encoding.ASCII.GetString(files.Written[path].ToArray());

    [TestMethod]
    public async Task RunAsync_StandardErrorFileWithVerbose_WritesTheVerboseAndFailureLinesToTheFile()
    {
        int exitCode = await RunAsync(["--stderr", "se", "-v", "http://127.0.0.1:1/"]);

        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        Diagnostics.Diff("se", Lf("*   Trying 127.0.0.1:1...\r\n" + FailureLine), Lf(Encoding.ASCII.GetString(files.Written["se"].ToArray())));
        Assert.AreEqual(
            "*   Trying 127.0.0.1:1...\r\n" + FailureLine,
            Encoding.ASCII.GetString(files.Written["se"].ToArray()));
        Diagnostics.Assert("first write mode", FileWriteMode.Truncate, files.WriteModes[0]);
        Assert.AreEqual(FileWriteMode.Truncate, files.WriteModes[0]);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Diagnostics.Diff("stdout", string.Empty, Lf(StandardOutputText));
        Assert.AreEqual(string.Empty, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileWithWriteOutToStandardError_WritesTheTemplateToTheFile()
    {
        await RunAsync(["--stderr", "se", "-w", "%{stderr}hi\n", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("se", Lf(FailureLine + "hi\r\n"), Lf(Encoding.ASCII.GetString(files.Written["se"].ToArray())));
        Assert.AreEqual(FailureLine + "hi\r\n", Encoding.ASCII.GetString(files.Written["se"].ToArray()));
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileUnderSilent_CreatesTheFileEmpty()
    {
        await RunAsync(["-s", "--stderr", "se", "http://127.0.0.1:1/"]);

        Diagnostics.Assert("se length", 0, files.Written["se"].ToArray().Length);
        Assert.AreEqual(0, files.Written["se"].ToArray().Length);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorDash_WritesTheLinesToStandardOutput()
    {
        int exitCode = await RunAsync(["--stderr", "-", "-v", "http://127.0.0.1:1/"]);

        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        Diagnostics.Diff("stdout", Lf("*   Trying 127.0.0.1:1...\r\n" + FailureLine), Lf(StandardOutputText));
        Assert.AreEqual("*   Trying 127.0.0.1:1...\r\n" + FailureLine, StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Diagnostics.Assert("files written", 0, files.Written.Count);
        Assert.AreEqual(0, files.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileThatCannotBeOpened_WarnsTwiceOverAndCarriesOnToStandardError()
    {
        files.UnwritablePaths.Add(string.Empty);

        int exitCode = await RunAsync(["--stderr", string.Empty, "http://127.0.0.1:1/"]);

        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        Diagnostics.Diff("stderr", Lf("Warning: Warning: Failed to open " + Environment.NewLine + FailureLine), Lf(StandardErrorText));
        Assert.AreEqual("Warning: Warning: Failed to open " + Environment.NewLine + FailureLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileThatCannotBeOpenedUnderSilent_WritesNothing()
    {
        files.UnwritablePaths.Add("adir");

        await RunAsync(["-s", "--stderr", "adir", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningBeforeStandardErrorFile_KeepsTheWarningOnStandardError()
    {
        await RunAsync(["-H", "nocolon", "--stderr", "se", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("stderr", Lf(NoColonWarning), Lf(StandardErrorText));
        Assert.AreEqual(NoColonWarning, StandardErrorText);
        Diagnostics.Diff("se", Lf(FailureLine), Lf(FileText("se")));
        Assert.AreEqual(FailureLine, FileText("se"));
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningAfterStandardErrorFile_WritesTheWarningToTheFile()
    {
        await RunAsync(["--stderr", "se", "-H", "nocolon", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Diagnostics.Diff("se", Lf(NoColonWarning + FailureLine), Lf(FileText("se")));
        Assert.AreEqual(NoColonWarning + FailureLine, FileText("se"));
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningBetweenTwoStandardErrorFiles_WritesTheWarningToTheFirstAndTheRestToTheSecond()
    {
        await RunAsync(["--stderr", "a", "-H", "nocolon", "--stderr", "b", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Diagnostics.Diff("a", Lf(NoColonWarning), Lf(FileText("a")));
        Assert.AreEqual(NoColonWarning, FileText("a"));
        Diagnostics.Diff("b", Lf(FailureLine), Lf(FileText("b")));
        Assert.AreEqual(FailureLine, FileText("b"));
    }

    [TestMethod]
    public async Task RunAsync_SecondStandardErrorFileThatCannotBeOpened_WarnsInTheFirstAndCarriesOnThere()
    {
        files.UnwritablePaths.Add("adir");

        await RunAsync(["--stderr", "a", "--stderr", "adir", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Diagnostics.Diff("a", Lf("Warning: Warning: Failed to open adir" + Environment.NewLine + FailureLine), Lf(FileText("a")));
        Assert.AreEqual("Warning: Warning: Failed to open adir" + Environment.NewLine + FailureLine, FileText("a"));
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorDashAfterAFile_WritesTheRestToStandardOutput()
    {
        await RunAsync(["--stderr", "a", "-H", "nocolon", "--stderr", "-", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("a", Lf(NoColonWarning), Lf(FileText("a")));
        Assert.AreEqual(NoColonWarning, FileText("a"));
        Diagnostics.Diff("stdout", Lf(FailureLine), Lf(StandardOutputText));
        Assert.AreEqual(FailureLine, StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RefusalAfterStandardErrorFile_WritesTheRefusalLinesToTheFile()
    {
        int exitCode = await RunAsync(["--stderr", "se", "-d", "@nosuch", "http://127.0.0.1:1/"]);

        Diagnostics.Assert("exit code", 26, exitCode);
        Assert.AreEqual(26, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Diagnostics.Diff(
            "se",
            "curl: Failed to open nosuch\ncurl: option -d: error encountered when reading a file\n" + Lf(CommandLineRefusal.TryHelpLine) + "\n",
            Lf(FileText("se")));
        Assert.AreEqual(
            "curl: Failed to open nosuch" + Environment.NewLine
            + "curl: option -d: error encountered when reading a file" + Environment.NewLine
            + CommandLineRefusal.TryHelpLine + Environment.NewLine,
            FileText("se"));
    }

    [TestMethod]
    public async Task RunAsync_RefusalBeforeStandardErrorFile_WritesTheRefusalLinesToStandardErrorAndOpensNoFile()
    {
        int exitCode = await RunAsync(["-d", "@nosuch", "--stderr", "se", "http://127.0.0.1:1/"]);

        Diagnostics.Assert("exit code", 26, exitCode);
        Assert.AreEqual(26, exitCode);
        Diagnostics.Assert("stderr starts with the open failure", true, Lf(StandardErrorText).StartsWith("curl: Failed to open nosuch\n", StringComparison.Ordinal));
        StringAssert.StartsWith(StandardErrorText, "curl: Failed to open nosuch" + Environment.NewLine);
        Diagnostics.Assert("files written", 0, files.Written.Count);
        Assert.AreEqual(0, files.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_SilentAfterStandardErrorFileThatCannotBeOpened_StillWarns()
    {
        files.UnwritablePaths.Add("adir");

        await RunAsync(["--stderr", "adir", "-s", "http://127.0.0.1:1/"]);

        Diagnostics.Diff("stderr", "Warning: Warning: Failed to open adir\n", Lf(StandardErrorText));
        Assert.AreEqual("Warning: Warning: Failed to open adir" + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileWithNoRemoteFileName_WritesTheTransferWarningToTheFile()
    {
        await RunAsync(["--stderr", "se", "-O", "http://127.0.0.1:1/"]);

        Diagnostics.Assert(
            "se starts with the no remote filename warning",
            true,
            Lf(Encoding.ASCII.GetString(files.Written["se"].ToArray())).StartsWith("Warning: No remote filename, uses \"curl_response\"\n", StringComparison.Ordinal));
        StringAssert.StartsWith(
            Encoding.ASCII.GetString(files.Written["se"].ToArray()),
            "Warning: No remote filename, uses \"curl_response\"" + Environment.NewLine);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static RecordingProtocolHandler RefusedConnection() =>
        new("http", context =>
        {
            context.Events.ReportInfo("  Trying 127.0.0.1:1...");

            return ValueTask.FromResult(TransferResult.Failure(
                CurlExitCode.CouldntConnect,
                "Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server"));
        });

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("unwritable paths", string.Join(", ", files.UnwritablePaths));
        Diagnostics.Arrange("handler", "http, reports Trying 127.0.0.1:1... and fails to connect (exit 7)");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([RefusedConnection()])),
                    files,
                    files,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    configFileReader: new InMemoryDataFileReader())
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("files written", string.Join(", ", files.Written.Keys.Order(StringComparer.Ordinal)));
        return exitCode;
    }
}
