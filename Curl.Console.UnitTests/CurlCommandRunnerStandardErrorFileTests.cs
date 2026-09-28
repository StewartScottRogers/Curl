using System.Text;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins where <c>--stderr</c> sends standard error, against curl 8.21.0 (mingw, Schannel) measured
/// on 2026-09-27 (BL-410 Notes): <c>--stderr se -v http://127.0.0.1:1/</c> wrote the <c>-v</c> lines
/// and the <c>curl: (7)</c> line to <c>se</c>, truncated, and nothing to standard error;
/// <c>--stderr -</c> wrote them to standard output; <c>--stderr ''</c> printed
/// <c>Warning: Warning: Failed to open </c> and carried on to standard error.
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

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

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
    public async Task RunAsync_StandardErrorFileWithAParserWarning_KeepsTheWarningOnStandardError()
    {
        // curl sends a warning to the file only when its option comes after --stderr; the parser
        // keeps no order, so every parser warning stays on standard error (BL-410 Notes).
        await RunAsync(["-H", "nocolon", "--stderr", "se", "http://127.0.0.1:1/"]);

        StringAssert.StartsWith(StandardErrorText, "Warning: The provided HTTP header 'nocolon' does not look like a header?");
        Assert.IsTrue(files.Written.ContainsKey("se"));
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
                runsOnWindows: true)
            .RunAsync(arguments);
}
