using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--remove-on-error</c> end to end through the runner and <see cref="HttpProtocolHandler" />
/// over a <see cref="ScriptedConnector" />, with the output files behind the runner's
/// <see cref="IOutputPaths" />, an <see cref="InMemoryFileSystem" />. Every expectation was measured
/// on 2026-09-28 with curl 8.21.0 (mingw, Schannel) against a loopback server (BL-494 Notes): a
/// failed transfer removes the file it opened and keeps its own exit code and message, a file it
/// never opened is left alone, and standard output is unaffected.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRemoveOnErrorTests
{
    private const string Url = "http://127.0.0.1:18494/a";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string CutShort = "HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhello";

    private const string NotFound = "HTTP/1.1 404 Not Found\r\nContent-Length: 5\r\n\r\nnope!";

    private const string MeasuredPath = @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl494\out.txt";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string DeletedText => string.Join(", ", outputFiles.DeleteAttempts);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorSuccess_KeepsTheFile()
    {
        int exitCode = await RunAsync([Ok], "-s", "-S", "--remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("out.txt content", "hello", WrittenText("out.txt"));
        Assert.AreEqual("hello", WrittenText("out.txt"));
        Diagnostics.Assert("delete attempts", string.Empty, DeletedText);
        Assert.IsEmpty(outputFiles.DeleteAttempts);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorBodyCutShort_RemovesTheFileAndKeepsTheFailure()
    {
        int exitCode = await RunAsync([CutShort], "-s", "-S", "--remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Assert("delete attempts", "out.txt", DeletedText);
        CollectionAssert.AreEqual(new[] { "out.txt" }, outputFiles.DeleteAttempts);
        Diagnostics.Assert("out.txt written", false, outputFiles.Written.ContainsKey("out.txt"));
        Assert.IsFalse(outputFiles.Written.ContainsKey("out.txt"));
        Diagnostics.Diff("stderr", "curl: (18) end of response with 5 bytes missing\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (18) end of response with 5 bytes missing" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorBodyCutShortFileThereBefore_RemovesTheFile()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync([CutShort], "-s", "-S", "--remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Assert("out.txt written", false, outputFiles.Written.ContainsKey("out.txt"));
        Assert.IsFalse(outputFiles.Written.ContainsKey("out.txt"));
        Diagnostics.Assert("out.txt still exists", false, outputFiles.ExistingPaths.Contains("out.txt"));
        Assert.DoesNotContain("out.txt", outputFiles.ExistingPaths);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorFailWithNoBodyWritten_LeavesAFileThereBefore()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync([NotFound], "-s", "-S", "-f", "--remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("delete attempts", string.Empty, DeletedText);
        Assert.IsEmpty(outputFiles.DeleteAttempts);
        Diagnostics.Assert("out.txt still exists", true, outputFiles.ExistingPaths.Contains("out.txt"));
        Assert.Contains("out.txt", outputFiles.ExistingPaths);
        Diagnostics.Diff("stderr", "curl: (22) The requested URL returned error: 404\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (22) The requested URL returned error: 404" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorFailWithBody_RemovesTheWrittenFile()
    {
        int exitCode = await RunAsync([NotFound], "-s", "-S", "--fail-with-body", "--remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("delete attempts", "out.txt", DeletedText);
        CollectionAssert.AreEqual(new[] { "out.txt" }, outputFiles.DeleteAttempts);
        Diagnostics.Assert("out.txt written", false, outputFiles.Written.ContainsKey("out.txt"));
        Assert.IsFalse(outputFiles.Written.ContainsKey("out.txt"));
        Diagnostics.Diff("stderr", "curl: (22) The requested URL returned error: 404\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (22) The requested URL returned error: 404" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoRemoveOnErrorAfterRemoveOnError_KeepsThePartialFile()
    {
        int exitCode = await RunAsync([CutShort], "-s", "-S", "--remove-on-error", "--no-remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Assert("delete attempts", string.Empty, DeletedText);
        Assert.IsEmpty(outputFiles.DeleteAttempts);
        Diagnostics.Diff("out.txt content", "hello", WrittenText("out.txt"));
        Assert.AreEqual("hello", WrittenText("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorToStandardOutput_WritesTheBodyAndRemovesNothing()
    {
        int exitCode = await RunAsync([CutShort], "-s", "-S", "--remove-on-error", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Diff("stdout", "hello", StandardOutputText);
        Assert.AreEqual("hello", StandardOutputText);
        Diagnostics.Assert("delete attempts", string.Empty, DeletedText);
        Assert.IsEmpty(outputFiles.DeleteAttempts);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-sv")]
    public async Task RunAsync_RemoveOnErrorVerbose_PrintsTheWrappedNoteAfterTheFailureAndBeforeWriteOut(string verbose)
    {
        int exitCode = await RunAsync(
            [CutShort], verbose, "-S", "--no-progress-meter", "--remove-on-error", "-w", "%{stderr}WE:%{filename_effective}\n", "-o", MeasuredPath, Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Assert("stderr ends with the wrapped note", true, Lf(StandardErrorText).EndsWith("WE:" + MeasuredPath + "\n", StringComparison.Ordinal));
        StringAssert.EndsWith(
            StandardErrorText,
            "curl: (18) end of response with 5 bytes missing" + NewLine
            + @"Note: Removed output file: C:\Users\Stewart " + NewLine
            + @"Note: Rogers\AppData\Local\Temp\bl494\out.txt" + NewLine
            + "WE:" + MeasuredPath + "\n");
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorFileCannotBeDeleted_WarnsFailedRemoving()
    {
        outputFiles.UndeletablePaths.Add("NUL");

        int exitCode = await RunAsync([CutShort], "--no-progress-meter", "--remove-on-error", "-o", "NUL", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Diff("stderr", "curl: (18) end of response with 5 bytes missing\nWarning: Failed removing: NUL\n", Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (18) end of response with 5 bytes missing" + NewLine + "Warning: Failed removing: NUL" + NewLine,
            StandardErrorText);
    }

    // Measured on the OpenSSL curl 8.21.0 for Linux, 2026-10-01 (BL-752 Notes).
    [TestMethod]
    public async Task RunAsync_RemoveOnErrorOutputNotARegularFile_WarnsSkippingRemoval()
    {
        outputFiles.NonRegularPaths.Add("/dev/null");

        int exitCode = await RunAsync([CutShort], "--no-progress-meter", "--remove-on-error", "-o", "/dev/null", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Diff("stderr", "curl: (18) end of response with 5 bytes missing\nWarning: Skipping removal; not a regular file: /dev/null\n", Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (18) end of response with 5 bytes missing" + NewLine
            + "Warning: Skipping removal; not a regular file: /dev/null" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorOutputNotARegularFileSilent_PrintsNoWarning()
    {
        outputFiles.NonRegularPaths.Add("/dev/null");

        int exitCode = await RunAsync([CutShort], "-s", "-S", "--remove-on-error", "-o", "/dev/null", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Diff("stderr", "curl: (18) end of response with 5 bytes missing\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (18) end of response with 5 bytes missing" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorFileCannotBeDeletedSilent_PrintsNoWarning()
    {
        outputFiles.UndeletablePaths.Add("out.txt");

        int exitCode = await RunAsync([CutShort], "-s", "-S", "--remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 18, exitCode);
        Assert.AreEqual(18, exitCode);
        Diagnostics.Diff("stderr", "curl: (18) end of response with 5 bytes missing\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (18) end of response with 5 bytes missing" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorFirstUrlFailsSecondSucceeds_RemovesOnlyTheFirstFile()
    {
        int exitCode = await RunAsync(
            [CutShort, string.Empty, Ok], "-s", "--remove-on-error", "-o", "one.txt", "http://127.0.0.1:18494/1", "-o", "two.txt", "http://127.0.0.1:18494/2");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("delete attempts", "one.txt", DeletedText);
        CollectionAssert.AreEqual(new[] { "one.txt" }, outputFiles.DeleteAttempts);
        Diagnostics.Assert("files written", "two.txt", string.Join(", ", outputFiles.Written.Keys.Order(StringComparer.Ordinal)));
        CollectionAssert.AreEquivalent(new[] { "two.txt" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_RemoveOnErrorWithContinueAt_IsRefusedBeforeAnyTransfer()
    {
        int exitCode = await RunAsync([Ok], "-C", "5", "--remove-on-error", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Assert("connection targets", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
        Diagnostics.Assert("stderr starts with the refusal", true, Lf(StandardErrorText).StartsWith("curl: --continue-at is mutually exclusive with --remove-on-error\ncurl: option --remove-on-error: is badly used here\n", StringComparison.Ordinal));
        StringAssert.StartsWith(
            StandardErrorText,
            "curl: --continue-at is mutually exclusive with --remove-on-error" + NewLine
            + "curl: option --remove-on-error: is badly used here" + NewLine);
    }

    private string WrittenText(string path) => Encoding.Latin1.GetString(outputFiles.Written[path].ToArray());

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(string[] responses, params string[] arguments)
    {
        server = new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("scripted responses", Lf(string.Join("\n--\n", responses)));
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
        Diagnostics.Act("stdout", StandardOutputText);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("delete attempts", DeletedText);
        return exitCode;
    }
}
