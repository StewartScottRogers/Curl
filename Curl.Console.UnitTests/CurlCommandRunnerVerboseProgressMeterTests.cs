using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins standard error under <c>-v</c> without <c>-s</c>, where the progress meter and the
/// <c>-v</c> lines interleave, against curl 8.21.0 measured on Windows on 2026-09-27 (task
/// BL-411): <c>curl -v http://127.0.0.1:18421/f.txt -o o1</c> over a six-byte response writes
/// the connect lines, the meter's header lines and zero status line with no line end, the
/// request, the response and its data line, the three done status lines and a newline, and
/// only then <c>* Connection #0 to host 127.0.0.1:18421 left intact</c>. The runner here writes
/// line feeds as they are, not in Windows' text mode, and the speed column is the manual
/// clock's, not the measured one.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerVerboseProgressMeterTests
{
    private const string SourceUrl = "http://127.0.0.1:18421/f.txt";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string HeaderLines =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current" + NewLine
        + "                                 Dload  Upload  Total   Spent   Left   Speed" + NewLine;

    private static readonly string ZeroStatusLine =
        "\r  0      0   0      0   0      0      0      0                              0";

    /// <summary>Six of six bytes in 40 ms of the manual clock: 6 * 1000000 / 40000 = 150 bytes per second.</summary>
    private static readonly string SixOfSixStatusLine =
        "\r100      6 100      6   0      0    150      0                              0";

    private static readonly string ConnectLines =
        "*   Trying 127.0.0.1:18421...\n"
        + "* Established connection to 127.0.0.1 (127.0.0.1 port 18421) from 127.0.0.1 port 55348 \n";

    private static readonly string ExchangeLines =
        "* using HTTP/1.x\n"
        + "> GET /f.txt HTTP/1.1\r\n"
        + "> Host: 127.0.0.1:18421\r\n"
        + "> User-Agent: curl/8.21.0\r\n"
        + "> Accept: */*\r\n"
        + "> \r\n"
        + "* Request completely sent off\n"
        + "< HTTP/1.1 200 OK\r\n"
        + "< Content-Type: text/plain\r\n"
        + "< Content-Length: 6\r\n"
        + "< \r\n"
        + "{ [6 bytes data]\n";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly ManualTimeProvider clock = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_VerboseWithoutSilent_InterleavesTheMeterWithTheVerboseLinesAsMeasured()
    {
        int exitCode = await RunAsync(["-v", SourceUrl, "-o", "o1"], ExchangingSixBytes(TransferResult.Success(6)));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            ConnectLines
            + HeaderLines + ZeroStatusLine
            + ExchangeLines
            + SixOfSixStatusLine + SixOfSixStatusLine + SixOfSixStatusLine + NewLine
            + "* Connection #0 to host 127.0.0.1:18421 left intact\n",
            StandardErrorText);
    }

    /// <summary>
    /// Measured the same way with <c>Content-Length: 10</c> over the six bytes: after the data
    /// line, <c>* end of response with 4 bytes missing</c>, the meter's newline (no status line
    /// within the second), <c>* closing connection #0</c>, and then curl's failure line.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_VerboseTransferFailsAfterDone_WritesTheMeterEndBeforeTheConnectionEndAndTheFailureAfter()
    {
        int exitCode = await RunAsync(
            ["-v", SourceUrl, "-o", "o1"],
            ExchangingSixBytes(
                TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 4 bytes missing"),
                ["end of response with 4 bytes missing"],
                "closing connection #0"));

        Assert.AreEqual(18, exitCode);
        Assert.AreEqual(
            ConnectLines
            + HeaderLines + ZeroStatusLine
            + ExchangeLines
            + "* end of response with 4 bytes missing\n"
            + NewLine
            + "* closing connection #0\n"
            + "curl: (18) end of response with 4 bytes missing" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseAndSilent_WritesTheVerboseLinesInTheOrderReported()
    {
        int exitCode = await RunAsync(["-s", "-v", SourceUrl, "-o", "o1"], ExchangingSixBytes(TransferResult.Success(6)));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            ConnectLines + ExchangeLines + "* Connection #0 to host 127.0.0.1:18421 left intact\n",
            StandardErrorText);
    }

    private RecordingProtocolHandler ExchangingSixBytes(TransferResult result) =>
        ExchangingSixBytes(result, [], "Connection #0 to host 127.0.0.1:18421 left intact");

    private RecordingProtocolHandler ExchangingSixBytes(TransferResult result, IReadOnlyList<string> linesBeforeDone, string connectionEndLine) =>
        new("http", context =>
        {
            ITransferEvents events = context.Events;
            events.ReportInfo("  Trying 127.0.0.1:18421...");
            events.ReportInfo("Established connection to 127.0.0.1 (127.0.0.1 port 18421) from 127.0.0.1 port 55348 ");
            context.Progress.ReportTransferStarted();
            events.ReportInfo("using HTTP/1.x");
            events.ReportRequestHeader("GET /f.txt HTTP/1.1\r\nHost: 127.0.0.1:18421\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8);
            events.ReportInfo("Request completely sent off");
            events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
            events.ReportResponseHeader("Content-Type: text/plain\r\n"u8);
            events.ReportResponseHeader("Content-Length: 6\r\n"u8);
            events.ReportResponseHeader("\r\n"u8);
            events.ReportDataReceived("hello\n"u8);
            clock.Advance(40);
            context.Progress.ReportDownloaded(6, 6);
            foreach (string line in linesBeforeDone)
            {
                events.ReportInfo(line);
            }

            context.Progress.ReportTransferDone();
            events.ReportInfo(connectionEndLine);

            return ValueTask.FromResult(result);
        });

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: true,
                timeProvider: clock)
            .RunAsync(arguments);
}
