using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins where the runner sends <c>-v</c>, <c>--trace</c> and <c>--trace-ascii</c> output, against
/// curl 8.21.0 (mingw, Schannel) measured on 2026-09-27 with <c>Record-CurlExchange.ps1</c> serving
/// <c>HTTP/1.1 200 OK</c>, <c>Content-Type: text/plain</c>, <c>Content-Length: 6</c> and <c>hello\n</c>;
/// the commands are in BL-242's Notes. The handler here reports the events that exchange produces,
/// so these tests pin the routing and the bytes, not the HTTP handler's reporting.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTransferEventTests
{
    private const string InfoEnd = "\r\n";
    private const string HeaderEnd = "\r\r\n";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem files = new();
    private readonly SettableClock clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_VerboseToOutputFile_WritesTheMeasuredLinesToStandardError()
    {
        int exitCode = await RunAsync(["-s", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expectedStandardError =
            "*   Trying 127.0.0.1:18441..." + InfoEnd
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116 " + InfoEnd
            + "* using HTTP/1.x" + InfoEnd
            + "> GET /f.txt HTTP/1.1" + HeaderEnd
            + "> Host: 127.0.0.1:18441" + HeaderEnd
            + "> User-Agent: curl/8.21.0" + HeaderEnd
            + "> Accept: */*" + HeaderEnd
            + "> " + HeaderEnd
            + "* Request completely sent off" + InfoEnd
            + "< HTTP/1.1 200 OK" + HeaderEnd
            + "< Content-Type: text/plain" + HeaderEnd
            + "< Content-Length: 6" + HeaderEnd
            + "< " + HeaderEnd
            + "{ [6 bytes data]" + InfoEnd
            + "* Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd;
        Diagnostics.Diff("stderr", expectedStandardError, StandardErrorText);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
        Diagnostics.Diff("file o", "hello\n", Encoding.ASCII.GetString(files.Written["o"].ToArray()));
        Assert.AreEqual("hello\n", Encoding.ASCII.GetString(files.Written["o"].ToArray()));
        Diagnostics.Diff("stdout", string.Empty, StandardOutputText);
        Assert.AreEqual(string.Empty, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithBodyOnStandardOutputThatIsNoTerminal_WritesTheDataLine()
    {
        await RunAsync(["-s", "-v", "http://127.0.0.1:18423/f.txt"], MeasuredExchange(18423, 51406));

        Diagnostics.Assert("stderr contains the data line before the connection line", true, StandardErrorText.Contains("< " + HeaderEnd + "{ [6 bytes data]" + InfoEnd + "* Connection #0", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "< " + HeaderEnd + "{ [6 bytes data]" + InfoEnd + "* Connection #0");
        Diagnostics.Diff("stdout", "hello\n", StandardOutputText);
        Assert.AreEqual("hello\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithStandardOutputATerminal_WritesNoDataLine()
    {
        await RunAsync(
            ["-s", "-v", "http://127.0.0.1:18423/f.txt", "-o", "o"],
            MeasuredExchange(18423, 51406),
            standardOutputIsTerminal: true);

        Diagnostics.Assert("stderr goes from the headers to the connection line", true, StandardErrorText.Contains("< " + HeaderEnd + "* Connection #0", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "< " + HeaderEnd + "* Connection #0");
        Diagnostics.Assert("stderr contains a data line", false, StandardErrorText.Contains("bytes data]", StringComparison.Ordinal));
        Assert.IsFalse(StandardErrorText.Contains("bytes data]", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_VerboseOffWindows_EndsEachLineWithTheBytesAlone()
    {
        await RunAsync(["-s", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116), runsOnWindows: false);

        Diagnostics.Assert("stderr starts with the Trying line ended by LF", true, StandardErrorText.StartsWith("*   Trying 127.0.0.1:18441...\n* Established", StringComparison.Ordinal));
        StringAssert.StartsWith(StandardErrorText, "*   Trying 127.0.0.1:18441...\n* Established");
        Diagnostics.Assert("stderr contains the request line ended by CR LF", true, StandardErrorText.Contains("> GET /f.txt HTTP/1.1\r\n> Host", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "> GET /f.txt HTTP/1.1\r\n> Host");
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithTraceTime_StampsEachLineStartFromTheRunnersClock()
    {
        // The stamp is HH:MM:SS.uuuuuu and a space in front of each line an event starts, as
        // curl 8.21.0 wrote it for -s -v --trace-time (measured 2026-09-27, BL-358).
        int exitCode = await RunAsync(
            ["-s", "-v", "--trace-time", "http://127.0.0.1:18441/f.txt", "-o", "o"],
            MeasuredExchange(18441, 55116, clock));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expectedStandardError =
            "11:54:11.571000 *   Trying 127.0.0.1:18441..." + InfoEnd
            + "11:54:11.572000 * Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116 " + InfoEnd
            + "11:54:11.572000 * using HTTP/1.x" + InfoEnd
            + "11:54:11.572000 > GET /f.txt HTTP/1.1" + HeaderEnd
            + "11:54:11.572000 > Host: 127.0.0.1:18441" + HeaderEnd
            + "11:54:11.572000 > User-Agent: curl/8.21.0" + HeaderEnd
            + "11:54:11.572000 > Accept: */*" + HeaderEnd
            + "11:54:11.572000 > " + HeaderEnd
            + "11:54:11.572000 * Request completely sent off" + InfoEnd
            + "11:54:11.574000 < HTTP/1.1 200 OK" + HeaderEnd
            + "11:54:11.574000 < Content-Type: text/plain" + HeaderEnd
            + "11:54:11.574000 < Content-Length: 6" + HeaderEnd
            + "11:54:11.574000 < " + HeaderEnd
            + "11:54:11.574000 { [6 bytes data]" + InfoEnd
            + "11:54:11.574000 * Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd;
        Diagnostics.Diff("stderr", expectedStandardError, StandardErrorText);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DoubleVerbose_StampsAndMarksTheVerboseLines()
    {
        // curl 8.21.0's -vv writes the stamp and then the IDs (measured 2026-09-29, BL-648 Notes).
        await RunAsync(["-s", "-vv", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116, clock));

        Diagnostics.Assert("stderr starts with the stamped, marked Trying line", true, StandardErrorText.StartsWith("11:54:11.571000 [0-0] *   Trying 127.0.0.1:18441..." + InfoEnd, StringComparison.Ordinal));
        StringAssert.StartsWith(StandardErrorText, "11:54:11.571000 [0-0] *   Trying 127.0.0.1:18441..." + InfoEnd);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithTraceIdsForTwoUrls_MarksEachTransfersLinesWithItsIds()
    {
        // As curl 8.21.0 marked -v --trace-ids for two URLs, each on its own connection:
        // [0-0] on the first transfer's lines, [1-1] on the second's (measured 2026-09-29, BL-648 Notes).
        int exitCode = await RunAsync(
            ["-s", "-v", "--trace-ids", "http://127.0.0.1:18441/f.txt", "http://127.0.0.1:18441/f.txt", "-o", "o", "-o", "p"],
            MeasuredExchange(18441, 55116));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", MeasuredVerboseLines("[0-0] ") + MeasuredVerboseLines("[1-1] "), StandardErrorText);
        Assert.AreEqual(MeasuredVerboseLines("[0-0] ") + MeasuredVerboseLines("[1-1] "), StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigIdsBeforeV_MarksEachTransfersLinesWithItsIds()
    {
        // curl 8.21.0 kept --trace-config ids through the first -v that clears --trace-ids (measured 2026-10-01, BL-649 Notes).
        int exitCode = await RunAsync(
            ["-s", "--trace-config", "ids", "-v", "http://127.0.0.1:18441/f.txt", "http://127.0.0.1:18441/f.txt", "-o", "o", "-o", "p"],
            MeasuredExchange(18441, 55116));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", MeasuredVerboseLines("[0-0] ") + MeasuredVerboseLines("[1-1] "), StandardErrorText);
        Assert.AreEqual(MeasuredVerboseLines("[0-0] ") + MeasuredVerboseLines("[1-1] "), StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigTlsHttp1AndUnknownName_WritesNoLinesBeyondV()
    {
        // curl 8.21.0's Schannel build wrote nothing more for --trace-config tls, http/1 or bogus
        // than for -v alone, over HTTP and over HTTPS (measured 2026-10-01, BL-649 Notes, ADR-0318).
        int exitCode = await RunAsync(
            ["-s", "-v", "--trace-config", "tls,http/1,bogus", "http://127.0.0.1:18441/f.txt", "http://127.0.0.1:18441/f.txt", "-o", "o", "-o", "p"],
            MeasuredExchange(18441, 55116));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", MeasuredVerboseLines(string.Empty) + MeasuredVerboseLines(string.Empty), StandardErrorText);
        Assert.AreEqual(MeasuredVerboseLines(string.Empty) + MeasuredVerboseLines(string.Empty), StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceIdsAndConnId_PrintTheSameConnectionNumbers()
    {
        await RunAsync(
            ["-s", "-v", "--trace-ids", "-w", "%{xfer_id}-%{conn_id} ", "http://127.0.0.1:18441/f.txt", "http://127.0.0.1:18441/f.txt", "-o", "o", "-o", "p"],
            MeasuredExchange(18441, 55116));

        Diagnostics.Diff("stdout", "0-0 1-1 ", StandardOutputText);
        Assert.AreEqual("0-0 1-1 ", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigRead_WritesTheClientResetLineAsTheTransferStartsAndBeforeTheConnectionIsLeftIntact()
    {
        // As curl 8.21.0 wrote -s --trace-config read -v for a 200 (measured 2026-10-02, BL-1159 Notes).
        int exitCode = await RunAsync(["-s", "--trace-config", "read", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expectedStandardError =
            ReadResetLine
            + MeasuredVerboseLines(string.Empty).Replace("* Connection #0", ReadResetLine + "* Connection #0", StringComparison.Ordinal);
        Diagnostics.Diff("stderr", expectedStandardError, StandardErrorText);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    [DataRow("dns")]
    [DataRow("setup")]
    [DataRow("-read")]
    public async Task RunAsync_TraceConfigWithoutRead_WritesNoReadLine(string components)
    {
        await RunAsync(["-s", "--trace-config", components, "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Diff("stderr", MeasuredVerboseLines(string.Empty), StandardErrorText);
        Assert.AreEqual(MeasuredVerboseLines(string.Empty), StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigReadWithoutVerbose_WritesNothing()
    {
        await RunAsync(["-s", "--trace-config", "read", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-vvv")]
    [DataRow("-vvvv")]
    public async Task RunAsync_ThreeOrMoreVs_WriteBothClientResetLines(string verbosity)
    {
        await RunAsync(["-s", verbosity, "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("client reset lines", 2, StandardErrorText.Split("* [READ] client_reset, clear readers" + InfoEnd).Length - 1);
        Assert.AreEqual(2, StandardErrorText.Split("* [READ] client_reset, clear readers" + InfoEnd).Length - 1);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigReadWithTraceIds_MarksTheFirstClientResetLineWithAnX()
    {
        await RunAsync(["-s", "--trace-config", "read", "-v", "--trace-ids", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("stderr starts with the x-marked client reset line", true, StandardErrorText.StartsWith("[0-x] " + ReadResetLine + "[0-0] *   Trying 127.0.0.1:18441..." + InfoEnd, StringComparison.Ordinal));
        StringAssert.StartsWith(StandardErrorText, "[0-x] " + ReadResetLine + "[0-0] *   Trying 127.0.0.1:18441..." + InfoEnd);
        Diagnostics.Assert("stderr ends with the marked client reset and connection lines", true, StandardErrorText.EndsWith("[0-0] " + ReadResetLine + "[0-0] * Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd, StringComparison.Ordinal));
        StringAssert.EndsWith(StandardErrorText, "[0-0] " + ReadResetLine + "[0-0] * Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd);
    }

    private const string ReadResetLine = "* [READ] client_reset, clear readers" + InfoEnd;

    [TestMethod]
    public async Task RunAsync_VerboseRewindWithoutTraceConfigRead_WritesThePlainLineAndNoReadLine()
    {
        // curl -v -d ab -L on a 302: "Need to rewind upload for next request" and no [READ] line (BL-1213 Notes).
        await RunAsync(["-s", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], RewindingExchange());

        Diagnostics.Assert("stderr contains the rewind line", true, StandardErrorText.Contains("* Need to rewind upload for next request" + InfoEnd, StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "* Need to rewind upload for next request" + InfoEnd);
        Diagnostics.Assert("stderr contains a [READ] line", false, StandardErrorText.Contains("[READ]", StringComparison.Ordinal));
        Assert.IsFalse(StandardErrorText.Contains("[READ]", StringComparison.Ordinal), StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigReadRewind_WritesTheRewindLinesAroundTheHopsEnd()
    {
        // curl -v --trace-config read -d ab -L on a 302 kept alive (BL-1213 Notes).
        await RunAsync(["-s", "-v", "--trace-config", "read", "http://127.0.0.1:18441/f.txt", "-o", "o"], RewindingExchange());
        string expectedRewindLines =
            "* [READ] client reader needs rewind before next request" + InfoEnd
            + "* Need to rewind upload for next request" + InfoEnd
            + "* [READ] client_reset, will rewind reader" + InfoEnd
            + "* Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd;

        Diagnostics.Assert("stderr contains the rewind lines", true, StandardErrorText.Contains(expectedRewindLines, StringComparison.Ordinal));
        StringAssert.Contains(
            StandardErrorText,
            expectedRewindLines);
    }

    private static RecordingProtocolHandler RewindingExchange() =>
        new("http", context =>
        {
            context.Events.ReportInfo("Need to rewind upload for next request");
            context.Events.ReportInfo("Connection #0 to host 127.0.0.1:18441 left intact");
            return ValueTask.FromResult(TransferResult.Success(0));
        });

    [TestMethod]
    public async Task RunAsync_TraceConfigWrite_WritesTheClientWriterLinesAfterEachHeaderAndTheBody()
    {
        // The lines curl 8.21.0 wrote under -s -v --trace-config write (measured 2026-10-02, BL-1187 Notes).
        int exitCode = await RunAsync(["-s", "--trace-config", "write", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expectedHeaderLines =
            "< HTTP/1.1 200 OK" + HeaderEnd
            + "* [WRITE] [OUT] wrote 17 header bytes -> 17" + InfoEnd
            + "* [WRITE] [PAUSE] writing 17/17 bytes of type c -> 0" + InfoEnd
            + "* [WRITE] download_write header(type=c, blen=17) -> 0" + InfoEnd
            + "* [WRITE] client_write(type=c, len=17) -> 0" + InfoEnd
            + "< Content-Type: text/plain" + HeaderEnd
            + "* [WRITE] header_collect pushed(type=1, len=26) -> 0" + InfoEnd
            + "* [WRITE] [OUT] wrote 26 header bytes -> 26" + InfoEnd
            + "* [WRITE] [PAUSE] writing 26/26 bytes of type 4 -> 0" + InfoEnd
            + "* [WRITE] download_write header(type=4, blen=26) -> 0" + InfoEnd
            + "* [WRITE] client_write(type=4, len=26) -> 0" + InfoEnd
            + "< Content-Length: 6" + HeaderEnd;
        Diagnostics.Assert("stderr contains the client writer lines after each header", true, StandardErrorText.Contains(expectedHeaderLines, StringComparison.Ordinal));
        StringAssert.Contains(
            StandardErrorText,
            expectedHeaderLines);
        string expectedBodyLines =
            "{ [6 bytes data]" + InfoEnd
            + "* [WRITE] [OUT] wrote 6 body bytes -> 6" + InfoEnd
            + "* [WRITE] [PAUSE] writing 6/6 bytes of type 1 -> 0" + InfoEnd
            + "* [WRITE] download_write body(type=1, blen=6) -> 0" + InfoEnd
            + "* [WRITE] client_write(type=1, len=6) -> 0" + InfoEnd
            + "* [WRITE] xfer_write_resp(len=70, eos=0) -> 0" + InfoEnd
            + "* [WRITE] [OUT] done" + InfoEnd
            + "* Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd;
        Diagnostics.Assert("stderr ends with the client writer lines after the body", true, StandardErrorText.EndsWith(expectedBodyLines, StringComparison.Ordinal));
        StringAssert.EndsWith(
            StandardErrorText,
            expectedBodyLines);
    }

    [TestMethod]
    [DataRow("network")]
    [DataRow("read")]
    [DataRow("-write")]
    public async Task RunAsync_TraceConfigWithoutWrite_WritesNoWriteLine(string components)
    {
        await RunAsync(["-s", "--trace-config", components, "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("stderr contains [WRITE]", false, StandardErrorText.Contains("[WRITE]", StringComparison.Ordinal));
        Assert.DoesNotContain("[WRITE]", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigWriteWithoutVerbose_WritesNothing()
    {
        await RunAsync(["-s", "--trace-config", "write", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-vvv")]
    [DataRow("-vvvv")]
    public async Task RunAsync_ThreeOrMoreVs_WriteTheClientWriterDoneLine(string verbosity)
    {
        await RunAsync(["-s", verbosity, "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("client writer done lines", 1, StandardErrorText.Split("* [WRITE] [OUT] done" + InfoEnd).Length - 1);
        Assert.AreEqual(1, StandardErrorText.Split("* [WRITE] [OUT] done" + InfoEnd).Length - 1);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigMulti_WritesTheTransferEngineLinesAroundTheTransfer()
    {
        // The lines curl 8.21.0 wrote under -s -v --trace-config multi (measured 2026-10-02, BL-1188 Notes); the
        // runner's clock stands still, so every [PGRS-*] number is 0.
        int exitCode = await RunAsync(["-s", "--trace-config", "multi", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expectedStandardError =
            "* [MULTI] [INIT] added to multi, mid=1, running=1, total=2" + InfoEnd
            + "* [MULTI] [INIT] pollset[], timeouts=0, paused 0/0 (r/w)" + InfoEnd
            + "* [MULTI] [INIT] multi_wait(fds=0, timeout=0) tinternal=0" + InfoEnd
            + "* [MULTI] [INIT] -> [SETUP]" + InfoEnd
            + "* [MULTI] [SETUP] [PGRS-STARTOP] set" + InfoEnd
            + "* [MULTI] [SETUP] [PGRS-STARTSINGLE] set" + InfoEnd
            + "* [MULTI] [SETUP] -> [CONNECT]" + InfoEnd
            + "* [MULTI] [CONNECT] transfer credentials: -" + InfoEnd
            + "* [MULTI] [CONNECT] [CPOOL] added connection 0. The cache now contains 1 members" + InfoEnd
            + "* [MULTI] [CONNECT] [PGRS-POSTQUEUE] set" + InfoEnd
            + "* [MULTI] [CONNECT] Curl_conn_setup() -> 0" + InfoEnd
            + "* [MULTI] [CONNECT] -> [CONNECTING]" + InfoEnd
            + "* [MULTI] [CONNECTING] [PGRS-NAMELOOKUP] added 0ns" + InfoEnd
            + "* [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP]" + InfoEnd
            + "* [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]" + InfoEnd
            + "*   Trying 127.0.0.1:18441..." + InfoEnd
            + "* [MULTI] [CONNECTING] pollset[fd=3 OUT], timeouts=0" + InfoEnd
            + "* [MULTI] [CONNECTING] multi_wait(fds=1, timeout=1000) tinternal=-1" + InfoEnd
            + "* [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]" + InfoEnd
            + "* [MULTI] [CONNECTING] [PGRS-CONNECT] added 0ns" + InfoEnd
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116 " + InfoEnd
            + "* [MULTI] [CONNECTING] connected [0][DNS][SETUP][HAPPY-EYEBALLS][TCP]" + InfoEnd
            + "* [MULTI] [CONNECTING] reduced to [0][TCP]" + InfoEnd
            + "* [MULTI] [CONNECTING] -> [PROTOCONNECT]" + InfoEnd
            + "* [MULTI] [PROTOCONNECT] -> [DO]" + InfoEnd
            + "* using HTTP/1.x" + InfoEnd
            + "* [MULTI] [DO] xfer_setup: recv_idx=0, send_idx=0" + InfoEnd
            + "> GET /f.txt HTTP/1.1" + HeaderEnd
            + "> Host: 127.0.0.1:18441" + HeaderEnd
            + "> User-Agent: curl/8.21.0" + HeaderEnd
            + "> Accept: */*" + HeaderEnd
            + "> " + HeaderEnd
            + "* Request completely sent off" + InfoEnd
            + "* [MULTI] [DO] -> [DID]" + InfoEnd
            + "* [MULTI] [DID] [PGRS-PRETRANSFER] added 0ns" + InfoEnd
            + "* [MULTI] [DID] [PGRS-POSTRANSFER] added 0ns" + InfoEnd
            + "* [MULTI] [DID] -> [PERFORMING]" + InfoEnd
            + "* [MULTI] [PERFORMING] pollset[fd=3 IN], timeouts=0" + InfoEnd
            + "* [MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=-1" + InfoEnd
            + "< HTTP/1.1 200 OK" + HeaderEnd
            + "* [MULTI] [PERFORMING] [PGRS-STARTTRANSFER] added 0ns" + InfoEnd
            + "< Content-Type: text/plain" + HeaderEnd
            + "< Content-Length: 6" + HeaderEnd
            + "< " + HeaderEnd
            + "{ [6 bytes data]" + InfoEnd
            + "* [MULTI] [PERFORMING] -> [DONE]" + InfoEnd
            + "* [MULTI] [DONE] multi_done: status: 0 prem: 0 done: 0" + InfoEnd
            + "* [MULTI] [DONE] multi_done_locked, in use=0" + InfoEnd
            + "* Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd
            + "* [MULTI] [DONE] -> [COMPLETED]" + InfoEnd
            + "* [MULTI] [COMPLETED] -> [MSGSENT]" + InfoEnd
            + "* [MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1" + InfoEnd;
        Diagnostics.Diff("stderr", expectedStandardError, StandardErrorText);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigNetworkWithMaxTimeAndAConnectTimeout_WritesTheResponseWaitsTimerLinesAmongThePollLines()
    {
        // curl 8.21.0 under -s -v --trace-config network -m 5 --connect-timeout 1 (measured 2026-10-02, BL-1258 Notes).
        await RunAsync(
            ["-s", "--trace-config", "network", "-v", "-m", "5", "--connect-timeout", "1", "http://127.0.0.1:18441/f.txt", "-o", "o"],
            MeasuredExchange(18441, 55116));

        string expectedPerformingLines =
            "* [MULTI] [DID] -> [PERFORMING]" + InfoEnd
            + "* [MULTI] [PERFORMING] pollset[fd=3 IN], timeouts=2" + InfoEnd
            + "* [TIMER] [CONNECTTIMEOUT] expires in 1000000ns" + InfoEnd
            + "* [TIMER] [TIMEOUT] expires in 5000000ns" + InfoEnd
            + "* [TIMER] [CONNECTTIMEOUT] gives multi timeout in 1000ms" + InfoEnd
            + "* [MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=1000" + InfoEnd;
        string expectedConnectingLines = "timeouts=2" + InfoEnd + "* [MULTI] [CONNECTING] multi_wait(fds=1, timeout=1000) tinternal=1000" + InfoEnd;

        Diagnostics.Assert("stderr contains the performing poll and timer lines", true, StandardErrorText.Contains(expectedPerformingLines, StringComparison.Ordinal));
        StringAssert.Contains(
            StandardErrorText,
            expectedPerformingLines);
        Diagnostics.Assert("stderr contains the connecting poll lines", true, StandardErrorText.Contains(expectedConnectingLines, StringComparison.Ordinal));
        StringAssert.Contains(
            StandardErrorText,
            expectedConnectingLines);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigMultiWithMaxTimeButNotTimer_WritesThePollLinesTimersButNoTimerLine()
    {
        // curl 8.21.0's multi counts the -m timer whether or not [TIMER] is traced (BL-1258 Notes).
        await RunAsync(["-s", "--trace-config", "multi", "-v", "-m", "5", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        string expectedPollLines =
            "* [MULTI] [PERFORMING] pollset[fd=3 IN], timeouts=1" + InfoEnd
            + "* [MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=5000" + InfoEnd;
        Diagnostics.Assert("stderr contains the poll lines counting one timer", true, StandardErrorText.Contains(expectedPollLines, StringComparison.Ordinal));
        StringAssert.Contains(
            StandardErrorText,
            expectedPollLines);
        Diagnostics.Assert("stderr contains [TIMER]", false, StandardErrorText.Contains("[TIMER]", StringComparison.Ordinal));
        Assert.DoesNotContain("[TIMER]", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigTimerWithoutMaxTimeOrConnectTimeout_WritesNoResponseWaitTimerLine()
    {
        await RunAsync(["-s", "--trace-config", "timer", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("stderr contains a multi timeout line", false, StandardErrorText.Contains("gives multi timeout", StringComparison.Ordinal));
        Assert.DoesNotContain("gives multi timeout", StandardErrorText);
    }

    [TestMethod]
    [DataRow("-m|5", "TIMEOUT", "5000")]
    [DataRow("-m|5|--connect-timeout|1", "CONNECTTIMEOUT", "1000")]
    [DataRow("-m|1|--connect-timeout|5", "TIMEOUT", "1000")]
    public async Task RunAsync_TraceConfigTimerWithTimeouts_TheNearestGivesTheMultiTimeoutAfterTheRequest(string timeouts, string nearest, string milliseconds)
    {
        // curl 8.21.0 under -s -v --trace-config timer -m 5, -m 5 --connect-timeout 1 and -m 1 --connect-timeout 5
        // (measured 2026-10-02, BL-1258 Notes): the response had not arrived at the first poll.
        await RunAsync(
            ["-s", "--trace-config", "timer", "-v", .. timeouts.Split('|'), "http://127.0.0.1:18441/f.txt", "-o", "o"],
            MeasuredExchange(18441, 55116));

        string expectedTimerLines =
            "* Request completely sent off" + InfoEnd
            + $"* [TIMER] [{nearest}] gives multi timeout in {milliseconds}ms" + InfoEnd
            + "< HTTP/1.1 200 OK";
        Diagnostics.Assert($"stderr contains the {nearest} multi timeout line after the request", true, StandardErrorText.Contains(expectedTimerLines, StringComparison.Ordinal));
        StringAssert.Contains(
            StandardErrorText,
            expectedTimerLines);
        Diagnostics.Assert("multi timeout lines", 1, StandardErrorText.Split("gives multi timeout").Length - 1);
        Assert.HasCount(1, StandardErrorText.Split("gives multi timeout").Skip(1));
    }

    [TestMethod]
    public async Task RunAsync_TraceConfigAll_WritesTheMultiDoneLinesBeforeTheWriteAndReadLines()
    {
        // curl 8.21.0 under --trace-config all (measured 2026-10-02, BL-1188 Notes).
        await RunAsync(["-s", "--trace-config", "all", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));
        string unstamped = string.Join(
            InfoEnd,
            StandardErrorText.Split(InfoEnd).Select(line => line[Math.Max(0, line.IndexOf("* ", StringComparison.Ordinal))..]));

        string expectedSetupLines = "* [MULTI] [SETUP] -> [CONNECT]" + InfoEnd + "* [READ] client_reset, clear readers" + InfoEnd + "* [MULTI] [CONNECT] transfer credentials: -" + InfoEnd;
        string expectedDoneLines =
            "* [MULTI] [PERFORMING] -> [DONE]" + InfoEnd
            + "* [MULTI] [DONE] multi_done: status: 0 prem: 0 done: 0" + InfoEnd
            + "* [WRITE] [OUT] done" + InfoEnd
            + "* [READ] client_reset, clear readers" + InfoEnd
            + "* [MULTI] [DONE] multi_done_locked, in use=0" + InfoEnd
            + "* Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd;
        Diagnostics.Bytes("unstamped stderr", Encoding.ASCII.GetBytes(unstamped));

        Diagnostics.Assert("unstamped stderr contains the client reset line after setup", true, unstamped.Contains(expectedSetupLines, StringComparison.Ordinal));
        StringAssert.Contains(
            unstamped,
            expectedSetupLines);
        Diagnostics.Assert("unstamped stderr contains the multi done lines before the write and read lines", true, unstamped.Contains(expectedDoneLines, StringComparison.Ordinal));
        StringAssert.Contains(
            unstamped,
            expectedDoneLines);
    }

    [TestMethod]
    [DataRow("--trace-config", "network", "-v")]
    [DataRow("-s", "-s", "-vvvv")]
    public async Task RunAsync_NetworkOrFourVs_WriteTheMultiLines(string first, string second, string third)
    {
        await RunAsync(["-s", first, second, third, "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("stderr contains the removed-from-multi line", true, StandardErrorText.Contains("[MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "[MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1");
    }

    [TestMethod]
    [DataRow("--trace-config", "read,write")]
    [DataRow("--trace-config", "-multi")]
    [DataRow("-v", "-vvv")]
    public async Task RunAsync_WithoutMultiNetworkOrAll_WritesNoMultiLine(string option, string value)
    {
        await RunAsync(["-s", "-v", option, value, "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Diagnostics.Assert("stderr contains [MULTI]", false, StandardErrorText.Contains("[MULTI]", StringComparison.Ordinal));
        Assert.DoesNotContain("[MULTI]", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceIdsWithResolveEntry_MarksTheLineBeforeTheConnectionWithAnX()
    {
        // curl 8.21.0 wrote "[0-x] * Added a.test:1:127.0.0.1 to DNS cache", then [0-0] (measured 2026-09-29, BL-648 Notes).
        RecordingProtocolHandler handler = MeasuredExchange(18441, 55116);
        string[] arguments = ["-s", "-v", "--trace-ids", "http://127.0.0.1:18441/f.txt", "-o", "o"];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("resolve entries", "report \"Added a.test:1:127.0.0.1 to DNS cache\"");
        int exitCode;

        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(
                        new ProtocolDispatcher([handler]),
                        [],
                        loadResolveEntries: events => events.ReportInfo("Added a.test:1:127.0.0.1 to DNS cache")),
                    files,
                    files,
                    standardOutput,
                    standardError,
                    standardInput,
                    runsOnWindows: true,
                    standardOutputIsTerminal: false,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        WriteRunResult(exitCode);

        string expectedStart = "[0-x] * Added a.test:1:127.0.0.1 to DNS cache" + InfoEnd + "[0-0] *   Trying 127.0.0.1:18441..." + InfoEnd;
        Diagnostics.Assert("stderr starts with the x-marked DNS cache line", true, StandardErrorText.StartsWith(expectedStart, StringComparison.Ordinal));
        StringAssert.StartsWith(
            StandardErrorText,
            expectedStart);
    }

    [TestMethod]
    public async Task RunAsync_TraceAsciiWithTraceIdsAndTraceTime_WritesTheStampThenTheIds()
    {
        // As curl 8.21.0 wrote --trace - --trace-ids --trace-time: "04:09:58.738000 [0-0] => Send header",
        // with the dumped bytes' lines unmarked (measured 2026-09-29, BL-648 Notes).
        await RunAsync(
            ["-s", "--trace-ascii", "-", "--trace-ids", "--trace-time", "http://127.0.0.1:18442/f.txt", "-o", "o"],
            MeasuredExchange(18442, 55117, clock));

        string expectedStart =
            "11:54:11.571000 [0-0] *   Trying 127.0.0.1:18442..." + InfoEnd
            + "11:54:11.572000 [0-0] * Established connection to 127.0.0.1 (127.0.0.1 port 18442) from 127.0.0.1 port 55117 " + InfoEnd
            + "11:54:11.572000 [0-0] * using HTTP/1.x" + InfoEnd
            + "11:54:11.572000 [0-0] => Send header, 84 bytes (0x54)" + InfoEnd
            + "0000: GET /f.txt HTTP/1.1" + InfoEnd;
        Diagnostics.Assert("stdout starts with the stamped, marked dump", true, StandardOutputText.StartsWith(expectedStart, StringComparison.Ordinal));
        StringAssert.StartsWith(
            StandardOutputText,
            expectedStart);
    }

    /// <summary>
    /// The <c>-v</c> lines curl 8.21.0 wrote for the measured exchange on port 18441, each that
    /// starts an event marked with <paramref name="ids" />.
    /// </summary>
    private static string MeasuredVerboseLines(string ids) =>
        ids + "*   Trying 127.0.0.1:18441..." + InfoEnd
        + ids + "* Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116 " + InfoEnd
        + ids + "* using HTTP/1.x" + InfoEnd
        + ids + "> GET /f.txt HTTP/1.1" + HeaderEnd
        + ids + "> Host: 127.0.0.1:18441" + HeaderEnd
        + ids + "> User-Agent: curl/8.21.0" + HeaderEnd
        + ids + "> Accept: */*" + HeaderEnd
        + ids + "> " + HeaderEnd
        + ids + "* Request completely sent off" + InfoEnd
        + ids + "< HTTP/1.1 200 OK" + HeaderEnd
        + ids + "< Content-Type: text/plain" + HeaderEnd
        + ids + "< Content-Length: 6" + HeaderEnd
        + ids + "< " + HeaderEnd
        + ids + "{ [6 bytes data]" + InfoEnd
        + ids + "* Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd;

    [TestMethod]
    public async Task RunAsync_TraceFile_WritesTheMeasuredDumpToTheFile()
    {
        int exitCode = await RunAsync(["-s", "--trace", "t.txt", "http://127.0.0.1:18422/f.txt", "-o", "o"], MeasuredExchange(18422, 51405));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("trace file", MeasuredHexDump, Encoding.ASCII.GetString(files.Written["t.txt"].ToArray()));
        Assert.AreEqual(MeasuredHexDump, Encoding.ASCII.GetString(files.Written["t.txt"].ToArray()));
        Diagnostics.Assert("first write mode", FileWriteMode.Truncate, files.WriteModes[0]);
        Assert.AreEqual(FileWriteMode.Truncate, files.WriteModes[0]);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceAsciiToStandardOutputWithTraceTime_WritesTheMeasuredStampedDump()
    {
        int exitCode = await RunAsync(
            ["-s", "--trace-ascii", "-", "--trace-time", "http://127.0.0.1:18442/f.txt", "-o", "o"],
            MeasuredExchange(18442, 55117, clock));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expectedStandardOutput =
            "11:54:11.571000 *   Trying 127.0.0.1:18442..." + InfoEnd
            + "11:54:11.572000 * Established connection to 127.0.0.1 (127.0.0.1 port 18442) from 127.0.0.1 port 55117 " + InfoEnd
            + "11:54:11.572000 * using HTTP/1.x" + InfoEnd
            + "11:54:11.572000 => Send header, 84 bytes (0x54)" + InfoEnd
            + "0000: GET /f.txt HTTP/1.1" + InfoEnd
            + "0015: Host: 127.0.0.1:18442" + InfoEnd
            + "002c: User-Agent: curl/8.21.0" + InfoEnd
            + "0045: Accept: */*" + InfoEnd
            + "0052: " + InfoEnd
            + "11:54:11.572000 * Request completely sent off" + InfoEnd
            + "11:54:11.574000 <= Recv header, 17 bytes (0x11)" + InfoEnd
            + "0000: HTTP/1.1 200 OK" + InfoEnd
            + "11:54:11.574000 <= Recv header, 26 bytes (0x1a)" + InfoEnd
            + "0000: Content-Type: text/plain" + InfoEnd
            + "11:54:11.574000 <= Recv header, 19 bytes (0x13)" + InfoEnd
            + "0000: Content-Length: 6" + InfoEnd
            + "11:54:11.574000 <= Recv header, 2 bytes (0x2)" + InfoEnd
            + "0000: " + InfoEnd
            + "11:54:11.574000 <= Recv data, 6 bytes (0x6)" + InfoEnd
            + "0000: hello." + InfoEnd
            + "11:54:11.574000 * Connection #0 to host 127.0.0.1:18442 left intact" + InfoEnd;
        Diagnostics.Diff("stdout", expectedStandardOutput, StandardOutputText);
        Assert.AreEqual(expectedStandardOutput, StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceToPercent_WritesTheDumpToStandardError()
    {
        await RunAsync(["-s", "--trace", "%", "http://127.0.0.1:18422/f.txt", "-o", "o"], MeasuredExchange(18422, 51405));

        Diagnostics.Diff("stderr", MeasuredHexDump, StandardErrorText);
        Assert.AreEqual(MeasuredHexDump, StandardErrorText);
        Diagnostics.Assert("file % written", false, files.Written.ContainsKey("%"));
        Assert.IsFalse(files.Written.ContainsKey("%"));
    }

    [TestMethod]
    public async Task RunAsync_TraceFileThatCannotBeOpened_WritesTheDumpToStandardErrorWithNoWarning()
    {
        files.UnwritablePaths.Add("nodir/t.txt");

        int exitCode = await RunAsync(["-s", "--trace", "nodir/t.txt", "http://127.0.0.1:18422/f.txt", "-o", "o"], MeasuredExchange(18422, 51405));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", MeasuredHexDump, StandardErrorText);
        Assert.AreEqual(MeasuredHexDump, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceWithTwoUrls_OpensTheFileOnceAndDumpsBothTransfersIntoIt()
    {
        await RunAsync(
            ["-s", "--trace", "t.txt", "http://127.0.0.1:18422/f.txt", "http://127.0.0.1:18422/f.txt", "-o", "o", "-o", "p"],
            MeasuredExchange(18422, 51405));

        Diagnostics.Diff("trace file", MeasuredHexDump + MeasuredHexDump, Encoding.ASCII.GetString(files.Written["t.txt"].ToArray()));
        Assert.AreEqual(MeasuredHexDump + MeasuredHexDump, Encoding.ASCII.GetString(files.Written["t.txt"].ToArray()));
        Diagnostics.Assert("trace file can still be read", false, files.Written["t.txt"].CanRead);
        Assert.IsTrue(files.Written["t.txt"].CanRead is false, "the run closes the trace file");
    }

    [TestMethod]
    public async Task RunAsync_TraceWithAUrlThatIsNoGlob_OpensNoTraceFile()
    {
        int exitCode = await RunAsync(["-s", "--trace", "t.txt", "http://[bad"], MeasuredExchange(18422, 51405));

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Assert("file t.txt written", false, files.Written.ContainsKey("t.txt"));
        Assert.IsFalse(files.Written.ContainsKey("t.txt"));
    }

    [TestMethod]
    public async Task RunAsync_NoTraceOption_GivesTheHandlerTheSinkThatDoesNothing()
    {
        RecordingProtocolHandler handler = MeasuredExchange(18422, 51405);

        await RunAsync(["http://127.0.0.1:18422/f.txt", "-o", "o"], handler);

        Diagnostics.Assert("handler's events are the sink that does nothing", true, ReferenceEquals(NoTransferEvents.Instance, handler.Contexts[0].Events));
        Assert.AreSame(NoTransferEvents.Instance, handler.Contexts[0].Events);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    /// <summary>
    /// The <c>--trace</c> file curl 8.21.0 wrote for <c>http://127.0.0.1:18422/f.txt</c> from local
    /// port 51405, byte for byte.
    /// </summary>
    private static string MeasuredHexDump =>
        "*   Trying 127.0.0.1:18422..." + InfoEnd
        + "* Established connection to 127.0.0.1 (127.0.0.1 port 18422) from 127.0.0.1 port 51405 " + InfoEnd
        + "* using HTTP/1.x" + InfoEnd
        + "=> Send header, 84 bytes (0x54)" + InfoEnd
        + "0000: 47 45 54 20 2f 66 2e 74 78 74 20 48 54 54 50 2f GET /f.txt HTTP/" + InfoEnd
        + "0010: 31 2e 31 0d 0a 48 6f 73 74 3a 20 31 32 37 2e 30 1.1..Host: 127.0" + InfoEnd
        + "0020: 2e 30 2e 31 3a 31 38 34 32 32 0d 0a 55 73 65 72 .0.1:18422..User" + InfoEnd
        + "0030: 2d 41 67 65 6e 74 3a 20 63 75 72 6c 2f 38 2e 32 -Agent: curl/8.2" + InfoEnd
        + "0040: 31 2e 30 0d 0a 41 63 63 65 70 74 3a 20 2a 2f 2a 1.0..Accept: */*" + InfoEnd
        + "0050: 0d 0a 0d 0a                                     ...." + InfoEnd
        + "* Request completely sent off" + InfoEnd
        + "<= Recv header, 17 bytes (0x11)" + InfoEnd
        + "0000: 48 54 54 50 2f 31 2e 31 20 32 30 30 20 4f 4b 0d HTTP/1.1 200 OK." + InfoEnd
        + "0010: 0a                                              ." + InfoEnd
        + "<= Recv header, 26 bytes (0x1a)" + InfoEnd
        + "0000: 43 6f 6e 74 65 6e 74 2d 54 79 70 65 3a 20 74 65 Content-Type: te" + InfoEnd
        + "0010: 78 74 2f 70 6c 61 69 6e 0d 0a                   xt/plain.." + InfoEnd
        + "<= Recv header, 19 bytes (0x13)" + InfoEnd
        + "0000: 43 6f 6e 74 65 6e 74 2d 4c 65 6e 67 74 68 3a 20 Content-Length: " + InfoEnd
        + "0010: 36 0d 0a                                        6.." + InfoEnd
        + "<= Recv header, 2 bytes (0x2)" + InfoEnd
        + "0000: 0d 0a                                           .." + InfoEnd
        + "<= Recv data, 6 bytes (0x6)" + InfoEnd
        + "0000: 68 65 6c 6c 6f 0a                               hello." + InfoEnd
        + "* Connection #0 to host 127.0.0.1:18422 left intact" + InfoEnd;

    /// <summary>
    /// An <c>http</c> handler that reports the events curl 8.21.0 showed for the measured exchange
    /// and writes its six-byte body, setting <paramref name="stampClock" />, when given, to the
    /// times curl stamped each event with.
    /// </summary>
    private static RecordingProtocolHandler MeasuredExchange(int port, int localPort, SettableClock? stampClock = null) =>
        new("http", async context =>
        {
            ITransferEvents events = context.Events;
            stampClock?.Set(571);
            events.ReportInfo($"  Trying 127.0.0.1:{port}...");
            stampClock?.Set(572);
            events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = "127.0.0.1",
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port),
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, localPort),
                ConnectionNumber = 0,
            });
            events.ReportInfo("using HTTP/1.x");
            events.ReportRequestHeader(Encoding.ASCII.GetBytes(
                $"GET /f.txt HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"));
            events.ReportInfo("Request completely sent off");
            stampClock?.Set(574);
            events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
            events.ReportResponseHeader("Content-Type: text/plain\r\n"u8);
            events.ReportResponseHeader("Content-Length: 6\r\n"u8);
            events.ReportResponseHeader("\r\n"u8);
            byte[] body = "hello\n"u8.ToArray();
            events.ReportDataReceived(body);
            await context.Output.WriteAsync(body, context.CancellationToken);
            events.ReportInfo($"Connection #0 to host 127.0.0.1:{port} left intact");

            return TransferResult.Success(body.Length);
        });

    private async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        RecordingProtocolHandler handler,
        bool runsOnWindows = true,
        bool standardOutputIsTerminal = false)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("handler", "http handler reporting the scripted transfer events");
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        Diagnostics.Arrange("stdout is a terminal", standardOutputIsTerminal);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    files,
                    files,
                    standardOutput,
                    standardError,
                    standardInput,
                    runsOnWindows,
                    standardOutputIsTerminal: standardOutputIsTerminal,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        WriteRunResult(exitCode);
        return exitCode;
    }

    private void WriteRunResult(int exitCode)
    {
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout bytes", standardOutput.ToArray());
        Diagnostics.Bytes("stderr bytes", standardError.ToArray());
        Diagnostics.Act("files written", string.Join(' ', files.Written.Keys.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// A clock in UTC, which is also its local time zone, at 11:54:11 plus the milliseconds a test
    /// sets, as curl 8.21.0 stamped the measured <c>--trace-time</c> dump.
    /// </summary>
    private sealed class SettableClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 27, 11, 54, 11, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => now.UtcTicks;

        public void Set(int milliseconds) =>
            now = new DateTimeOffset(2026, 9, 27, 11, 54, 11, milliseconds, TimeSpan.Zero);
    }
}
