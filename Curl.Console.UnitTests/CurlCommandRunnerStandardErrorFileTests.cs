using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

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

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    private static readonly string NoColonWarning =
        "Warning: The provided HTTP header 'nocolon' does not look like a header?" + Environment.NewLine;

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    private string FileText(string path) => Encoding.ASCII.GetString(files.Written[path].ToArray());

    [TestMethod]
    public async Task RunAsync_StandardErrorFileWithVerbose_WritesTheVerboseAndFailureLinesToTheFile()
    {
        int exitCode = await RunAsync(["--stderr", "se", "-v", "http://127.0.0.1:1/"]);

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(
            "*   Trying 127.0.0.1:1...\r\n" + FailureLine,
            Encoding.ASCII.GetString(files.Written["se"].ToArray()));
        Assert.AreEqual(FileWriteMode.Truncate, files.WriteModes[0]);
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileWithWriteOutToStandardError_WritesTheTemplateToTheFile()
    {
        await RunAsync(["--stderr", "se", "-w", "%{stderr}hi\n", "http://127.0.0.1:1/"]);

        Assert.AreEqual(FailureLine + "hi\r\n", Encoding.ASCII.GetString(files.Written["se"].ToArray()));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileUnderSilent_CreatesTheFileEmpty()
    {
        await RunAsync(["-s", "--stderr", "se", "http://127.0.0.1:1/"]);

        Assert.AreEqual(0, files.Written["se"].ToArray().Length);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorDash_WritesTheLinesToStandardOutput()
    {
        int exitCode = await RunAsync(["--stderr", "-", "-v", "http://127.0.0.1:1/"]);

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("*   Trying 127.0.0.1:1...\r\n" + FailureLine, StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.AreEqual(0, files.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileThatCannotBeOpened_WarnsTwiceOverAndCarriesOnToStandardError()
    {
        files.UnwritablePaths.Add(string.Empty);

        int exitCode = await RunAsync(["--stderr", string.Empty, "http://127.0.0.1:1/"]);

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("Warning: Warning: Failed to open " + Environment.NewLine + FailureLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileThatCannotBeOpenedUnderSilent_WritesNothing()
    {
        files.UnwritablePaths.Add("adir");

        await RunAsync(["-s", "--stderr", "adir", "http://127.0.0.1:1/"]);

        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningBeforeStandardErrorFile_KeepsTheWarningOnStandardError()
    {
        await RunAsync(["-H", "nocolon", "--stderr", "se", "http://127.0.0.1:1/"]);

        Assert.AreEqual(NoColonWarning, StandardErrorText);
        Assert.AreEqual(FailureLine, FileText("se"));
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningAfterStandardErrorFile_WritesTheWarningToTheFile()
    {
        await RunAsync(["--stderr", "se", "-H", "nocolon", "http://127.0.0.1:1/"]);

        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.AreEqual(NoColonWarning + FailureLine, FileText("se"));
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningBetweenTwoStandardErrorFiles_WritesTheWarningToTheFirstAndTheRestToTheSecond()
    {
        await RunAsync(["--stderr", "a", "-H", "nocolon", "--stderr", "b", "http://127.0.0.1:1/"]);

        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.AreEqual(NoColonWarning, FileText("a"));
        Assert.AreEqual(FailureLine, FileText("b"));
    }

    [TestMethod]
    public async Task RunAsync_SecondStandardErrorFileThatCannotBeOpened_WarnsInTheFirstAndCarriesOnThere()
    {
        files.UnwritablePaths.Add("adir");

        await RunAsync(["--stderr", "a", "--stderr", "adir", "http://127.0.0.1:1/"]);

        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.AreEqual("Warning: Warning: Failed to open adir" + Environment.NewLine + FailureLine, FileText("a"));
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorDashAfterAFile_WritesTheRestToStandardOutput()
    {
        await RunAsync(["--stderr", "a", "-H", "nocolon", "--stderr", "-", "http://127.0.0.1:1/"]);

        Assert.AreEqual(NoColonWarning, FileText("a"));
        Assert.AreEqual(FailureLine, StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RefusalAfterStandardErrorFile_WritesTheRefusalLinesToTheFile()
    {
        int exitCode = await RunAsync(["--stderr", "se", "-d", "@nosuch", "http://127.0.0.1:1/"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
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

        Assert.AreEqual(26, exitCode);
        StringAssert.StartsWith(StandardErrorText, "curl: Failed to open nosuch" + Environment.NewLine);
        Assert.AreEqual(0, files.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_SilentAfterStandardErrorFileThatCannotBeOpened_StillWarns()
    {
        files.UnwritablePaths.Add("adir");

        await RunAsync(["--stderr", "adir", "-s", "http://127.0.0.1:1/"]);

        Assert.AreEqual("Warning: Warning: Failed to open adir" + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileWithNoRemoteFileName_WritesTheTransferWarningToTheFile()
    {
        await RunAsync(["--stderr", "se", "-O", "http://127.0.0.1:1/"]);

        StringAssert.StartsWith(
            Encoding.ASCII.GetString(files.Written["se"].ToArray()),
            "Warning: No remote filename, uses \"curl_response\"" + Environment.NewLine);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private static RecordingProtocolHandler RefusedConnection() =>
        new("http", context =>
        {
            context.Events.ReportInfo("  Trying 127.0.0.1:1...");

            return ValueTask.FromResult(TransferResult.Failure(
                CurlExitCode.CouldntConnect,
                "Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server"));
        });

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
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
