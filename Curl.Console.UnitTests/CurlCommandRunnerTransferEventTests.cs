using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_VerboseToOutputFile_WritesTheMeasuredLinesToStandardError()
    {
        int exitCode = await RunAsync(["-s", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
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
            + "* Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd,
            StandardErrorText);
        Assert.AreEqual("hello\n", Encoding.ASCII.GetString(files.Written["o"].ToArray()));
        Assert.AreEqual(string.Empty, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithBodyOnStandardOutputThatIsNoTerminal_WritesTheDataLine()
    {
        await RunAsync(["-s", "-v", "http://127.0.0.1:18423/f.txt"], MeasuredExchange(18423, 51406));

        StringAssert.Contains(StandardErrorText, "< " + HeaderEnd + "{ [6 bytes data]" + InfoEnd + "* Connection #0");
        Assert.AreEqual("hello\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithStandardOutputATerminal_WritesNoDataLine()
    {
        await RunAsync(
            ["-s", "-v", "http://127.0.0.1:18423/f.txt", "-o", "o"],
            MeasuredExchange(18423, 51406),
            standardOutputIsTerminal: true);

        StringAssert.Contains(StandardErrorText, "< " + HeaderEnd + "* Connection #0");
        Assert.IsFalse(StandardErrorText.Contains("bytes data]", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_VerboseOffWindows_EndsEachLineWithTheBytesAlone()
    {
        await RunAsync(["-s", "-v", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116), runsOnWindows: false);

        StringAssert.StartsWith(StandardErrorText, "*   Trying 127.0.0.1:18441...\n* Established");
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

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
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
            + "11:54:11.574000 * Connection #0 to host 127.0.0.1:18441 left intact" + InfoEnd,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DoubleVerbose_StampsTheVerboseLines()
    {
        await RunAsync(["-s", "-vv", "http://127.0.0.1:18441/f.txt", "-o", "o"], MeasuredExchange(18441, 55116, clock));

        StringAssert.StartsWith(StandardErrorText, "11:54:11.571000 *   Trying 127.0.0.1:18441..." + InfoEnd);
    }

    [TestMethod]
    public async Task RunAsync_TraceFile_WritesTheMeasuredDumpToTheFile()
    {
        int exitCode = await RunAsync(["-s", "--trace", "t.txt", "http://127.0.0.1:18422/f.txt", "-o", "o"], MeasuredExchange(18422, 51405));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(MeasuredHexDump, Encoding.ASCII.GetString(files.Written["t.txt"].ToArray()));
        Assert.AreEqual(FileWriteMode.Truncate, files.WriteModes[0]);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceAsciiToStandardOutputWithTraceTime_WritesTheMeasuredStampedDump()
    {
        int exitCode = await RunAsync(
            ["-s", "--trace-ascii", "-", "--trace-time", "http://127.0.0.1:18442/f.txt", "-o", "o"],
            MeasuredExchange(18442, 55117, clock));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
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
            + "11:54:11.574000 * Connection #0 to host 127.0.0.1:18442 left intact" + InfoEnd,
            StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceToPercent_WritesTheDumpToStandardError()
    {
        await RunAsync(["-s", "--trace", "%", "http://127.0.0.1:18422/f.txt", "-o", "o"], MeasuredExchange(18422, 51405));

        Assert.AreEqual(MeasuredHexDump, StandardErrorText);
        Assert.IsFalse(files.Written.ContainsKey("%"));
    }

    [TestMethod]
    public async Task RunAsync_TraceFileThatCannotBeOpened_WritesTheDumpToStandardErrorWithNoWarning()
    {
        files.UnwritablePaths.Add("nodir/t.txt");

        int exitCode = await RunAsync(["-s", "--trace", "nodir/t.txt", "http://127.0.0.1:18422/f.txt", "-o", "o"], MeasuredExchange(18422, 51405));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(MeasuredHexDump, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TraceWithTwoUrls_OpensTheFileOnceAndDumpsBothTransfersIntoIt()
    {
        await RunAsync(
            ["-s", "--trace", "t.txt", "http://127.0.0.1:18422/f.txt", "http://127.0.0.1:18422/f.txt", "-o", "o", "-o", "p"],
            MeasuredExchange(18422, 51405));

        Assert.AreEqual(MeasuredHexDump + MeasuredHexDump, Encoding.ASCII.GetString(files.Written["t.txt"].ToArray()));
        Assert.IsTrue(files.Written["t.txt"].CanRead is false, "the run closes the trace file");
    }

    [TestMethod]
    public async Task RunAsync_TraceWithAUrlThatIsNoGlob_OpensNoTraceFile()
    {
        int exitCode = await RunAsync(["-s", "--trace", "t.txt", "http://[bad"], MeasuredExchange(18422, 51405));

        Assert.AreEqual(3, exitCode);
        Assert.IsFalse(files.Written.ContainsKey("t.txt"));
    }

    [TestMethod]
    public async Task RunAsync_NoTraceOption_GivesTheHandlerTheSinkThatDoesNothing()
    {
        RecordingProtocolHandler handler = MeasuredExchange(18422, 51405);

        await RunAsync(["http://127.0.0.1:18422/f.txt", "-o", "o"], handler);

        Assert.AreSame(NoTransferEvents.Instance, handler.Contexts[0].Events);
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

    private Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        RecordingProtocolHandler handler,
        bool runsOnWindows = true,
        bool standardOutputIsTerminal = false) =>
        new CurlCommandRunner(
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

    /// <summary>
    /// A clock in UTC, which is also its local time zone, at 11:54:11 plus the milliseconds a test
    /// sets, as curl 8.21.0 stamped the measured <c>--trace-time</c> dump.
    /// </summary>
    private sealed class SettableClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 27, 11, 54, 11, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => now;

        public void Set(int milliseconds) =>
            now = new DateTimeOffset(2026, 9, 27, 11, 54, 11, milliseconds, TimeSpan.Zero);
    }
}
