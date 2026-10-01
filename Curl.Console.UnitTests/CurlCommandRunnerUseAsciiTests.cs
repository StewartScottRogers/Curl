using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_UseAsciiToStandardOutputOnWindows_WritesEachLineFeedAsCrLf()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("l1\r\nl2\r\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiToStandardOutputOffWindows_WritesTheBodyUnchanged()
    {
        int exitCode = await RunAsync(runsOnWindows: false, "-s", "-B", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("l1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NoUseAsciiOnWindows_WritesTheBodyUnchanged()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("l1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_UseAsciiToAnOutputFile_WritesTheFileUnchanged(bool runsOnWindows)
    {
        int exitCode = await RunAsync(runsOnWindows, "-s", "-B", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.AreEqual("l1\nl2\r\n", Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiWithWriteOutOnWindows_WritesTheBodyAndTheWriteOutLineFeedsAsCrLf()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", "-w", "w\\n", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("l1\r\nl2\r\r\nw\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiAfterATransferThatSwitchedToBinaryOnWindows_WritesBothBodiesUnchanged()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", Url, "--next", "-B", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("l1\nl2\r\nl1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiBeforeATransferWithoutItOnWindows_WritesOnlyTheFirstBodyInTextMode()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", Url, "--next", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("l1\r\nl2\r\r\nl1\nl2\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UseAsciiThenALaterUseAsciiUrlOnWindows_KeepsTheWriteOutInTextMode()
    {
        int exitCode = await RunAsync(runsOnWindows: true, "-s", "-B", "-w", "w\\n", Url, Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("l1\r\nl2\r\r\nw\r\nl1\r\nl2\r\r\nw\r\n", StandardOutputText);
    }

    private Task<int> RunAsync(bool runsOnWindows, params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([WritingLines()])),
                outputFiles,
                outputFiles,
                standardOutput,
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows,
                timeProvider: TimeProvider.System)
            .RunAsync(arguments);

    /// <summary>A <c>lines</c> handler that writes FTP's measured <c>l1\nl2\r\n</c> body and succeeds.</summary>
    private static RecordingProtocolHandler WritingLines() =>
        new("lines", async context =>
        {
            byte[] body = "l1\nl2\r\n"u8.ToArray();
            await context.Output.WriteAsync(body, context.CancellationToken);

            return TransferResult.Success(body.Length);
        });
}
