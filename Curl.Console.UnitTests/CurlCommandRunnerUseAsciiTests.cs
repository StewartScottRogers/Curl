using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-B</c> / <c>--use-ascii</c> on standard output end to end through the runner: on
/// Windows curl leaves standard output in text mode for a <c>-B</c> transfer, so each line feed
/// of its body is written as CR LF, a carriage return before it included; off Windows, to a
/// <c>-o</c> file, or once an earlier transfer switched standard output to binary mode, the body
/// is written unchanged. Every expectation was measured on 2026-10-01 with curl 8.21.0 (mingw,
/// Schannel) through <c>Record-CurlExchange.ps1</c> (BL-961 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUseAsciiTests
{
    private const string Url = "lines://h/f.txt";

    private readonly MemoryStream standardOutput = new();
    private readonly InMemoryFileSystem outputFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_UseAsciiToStandardOutputOnWindows_WritesEachLineFeedAsCrLf()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput("l1\r\nl2\r\r\n");
        Assert.AreEqual("l1\r\nl2\r\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiToStandardOutputOffWindows_WritesTheBodyUnchanged()
    {
        int exitCode = await RunAsync(runsOnWindows: false, "-s", "-B", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput("l1\nl2\r\n");
        Assert.AreEqual("l1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NoUseAsciiOnWindows_WritesTheBodyUnchanged()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput("l1\nl2\r\n");
        Assert.AreEqual("l1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_UseAsciiToAnOutputFile_WritesTheFileUnchanged(bool runsOnWindows)
    {
        int exitCode = await RunAsync(runsOnWindows, "-s", "-B", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput(string.Empty);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Diagnostics.Bytes("out.txt", outputFiles.Written["out.txt"].ToArray());
        Diagnostics.Diff("out.txt", "l1\nl2\r\n"u8, outputFiles.Written["out.txt"].ToArray());
        Assert.AreEqual("l1\nl2\r\n", Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiWithWriteOutOnWindows_WritesTheBodyAndTheWriteOutLineFeedsAsCrLf()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", "-w", "w\\n", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput("l1\r\nl2\r\r\nw\r\n");
        Assert.AreEqual("l1\r\nl2\r\r\nw\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiAfterATransferThatSwitchedToBinaryOnWindows_WritesBothBodiesUnchanged()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", Url, "--next", "-B", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput("l1\nl2\r\nl1\nl2\r\n");
        Assert.AreEqual("l1\nl2\r\nl1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiBeforeATransferWithoutItOnWindows_WritesOnlyTheFirstBodyInTextMode()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", Url, "--next", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput("l1\r\nl2\r\r\nl1\nl2\r\n");
        Assert.AreEqual("l1\r\nl2\r\r\nl1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiThenALaterUseAsciiUrlOnWindows_KeepsTheWriteOutInTextMode()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", "-w", "w\\n", Url, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        DiffStandardOutput("l1\r\nl2\r\r\nw\r\nl1\r\nl2\r\r\nw\r\n");
        Assert.AreEqual("l1\r\nl2\r\r\nw\r\nl1\r\nl2\r\r\nw\r\n", StandardOutputText);
    }

    /// <summary>
    /// Writes a byte DIFF of standard output against <paramref name="expected" />; the bytes are
    /// compared as bytes because the carriage returns are what these tests are about.
    /// </summary>
    private void DiffStandardOutput(string expected) =>
        Diagnostics.Diff("stdout", Encoding.Latin1.GetBytes(expected), standardOutput.ToArray());

    private async Task<int> RunAsync(bool runsOnWindows, params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        Diagnostics.Arrange("handler behaviour", "lines writes the body l1 LF l2 CR LF (7 bytes) and succeeds");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([WritingLines()])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    new MemoryStream(),
                    new MemoryStream(),
                    runsOnWindows,
                    timeProvider: TimeProvider.System)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        return exitCode;
    }

    /// <summary>A <c>lines</c> handler that writes FTP's measured <c>l1\nl2\r\n</c> body and succeeds.</summary>
    private static RecordingProtocolHandler WritingLines() =>
        new("lines", async context =>
        {
            byte[] body = "l1\nl2\r\n"u8.ToArray();
            await context.Output.WriteAsync(body, context.CancellationToken);

            return TransferResult.Success(body.Length);
        });
}
