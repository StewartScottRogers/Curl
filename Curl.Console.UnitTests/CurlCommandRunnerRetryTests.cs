using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>--retry</c> and <c>--limit-rate</c> end to end through the runner and
/// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />, on an
/// <see cref="ImmediateTimerTimeProvider" /> so no wait takes real time. Every expectation was
/// measured on 2026-09-27 with curl 8.21.0 (Windows, Schannel) through
/// <c>Record-CurlExchange.ps1</c>, answering <c>503 Service Unavailable</c> with <c>busy</c>
/// (BL-241 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRetryTests
{
    private const string Url = "http://127.0.0.1:18241/a";

    private const string Busy = "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 4\r\n\r\nbusy";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string BusyForLong = "HTTP/1.1 503 Service Unavailable\r\nRetry-After: 100\r\nContent-Length: 4\r\n\r\nbusy";

    private const string EmptyBusy = "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n";

    private const string EmptyOk = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string OkOk = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string RetryWarningLine =
        "Warning: Problem : HTTP error. Retrying in 1 second. 1 retry left." + NewLine;

    private static readonly string ServiceUnavailableLine = "curl: (22) The requested URL returned error: 503" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly ImmediateTimerTimeProvider clock = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_RetryAfterServiceUnavailable_WarnsWaitsAndSucceedsOnTheSecondAttempt()
    {
        int exitCode = await RunAsync([Busy, Ok], "--retry", "1", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(RetryWarningLine, StandardErrorText);
        Assert.AreEqual("busyhello", StandardOutputText);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_RetryIntoOutputFile_KeepsOnlyTheLastAttemptsBody()
    {
        int exitCode = await RunAsync([Busy, Ok], "--retry", "1", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_RetryUnderShowErrorAndFail_PrintsEachAttemptsLineAndNoWarning()
    {
        int exitCode = await RunAsync([Busy, Busy], "-sS", "-f", "--retry", "1", Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(ServiceUnavailableLine + ServiceUnavailableLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RetryUnderSilent_PrintsNothing()
    {
        int exitCode = await RunAsync([Busy, Ok], "-s", "--retry", "1", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RetryWithFailAndMeter_PrintsTheHeaderOnceAndEachAttemptsMeterLineAndFailure()
    {
        int exitCode = await RunAsync([Busy, Busy], writesProgressMeter: true, "-f", "--retry", "1", "-o", "out.txt", Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(
            ProgressMeterLines.FirstHeaderLine + NewLine
            + ProgressMeterLines.SecondHeaderLine + NewLine
            + ProgressMeterLines.ZeroStatusLine + NewLine
            + ServiceUnavailableLine
            + RetryWarningLine
            + ProgressMeterLines.ZeroStatusLine + NewLine
            + ServiceUnavailableLine,
            StandardErrorText);
        Assert.IsFalse(outputFiles.Written.ContainsKey("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterPastMaxTime_WarnsAndDoesNotRetry()
    {
        int exitCode = await RunAsync([BusyForLong, Ok], "--retry", "1", "--retry-max-time", "10", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "Warning: The Retry-After: time would make this command line exceed the maximum " + NewLine
            + "Warning: allowed time for retries." + NewLine,
            StandardErrorText);
        Assert.AreEqual("busy", StandardOutputText);
        Assert.AreEqual(0, clock.Waits.Count);
    }

    [TestMethod]
    public async Task RunAsync_NumRetriesAfterTwoRetriesThatSucceed_PrintsTwo()
    {
        // curl -s --retry 2 --retry-delay 1 -w '%{num_retries}' against 503, 503, 200 (BL-513 Notes).
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, OkOk], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{num_retries}", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok2", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NumRetriesAfterRetriesRunOut_PrintsTwo()
    {
        // curl -s --retry 2 --retry-delay 1 -w '%{num_retries}' against three 503s (BL-513 Notes).
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, EmptyBusy], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{num_retries}", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_JsonAfterTwoRetries_PrintsNumRetriesTwo()
    {
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, OkOk], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{json}", Url);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardOutputText, "\"num_redirects\":0,\"num_retries\":2,");
    }

    [TestMethod]
    public async Task RunAsync_XferIdAndConnIdAfterTwoRetriesThatSucceed_CountEveryAttempt()
    {
        // curl -s --retry 2 --retry-delay 1 -w '%{xfer_id} %{conn_id}\n' against 503, 503, 200,
        // each closing its connection, printed "2 2" (BL-799 Notes).
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, EmptyOk], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}\\n", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2 2\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_XferIdAndConnIdAfterRetriesRunOut_CountEveryAttempt()
    {
        // The same against three 503s printed "2 2" (BL-799 Notes).
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, EmptyBusy], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}\\n", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2 2\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_XferIdAndConnIdOfAUrlAfterARetriedOne_CountOnFromTheLastAttempt()
    {
        // A second URL after the first's 503, 503, 200 printed "2 2" then "3 3" (BL-799 Notes).
        int exitCode = await RunAsync(
            [EmptyBusy, EmptyBusy, EmptyOk, EmptyOk],
            "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}\\n", Url, Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2 2\n3 3\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ConnIdAfterRetriesOverOneKeptAliveConnection_KeepsTheFirstConnectionsNumber()
    {
        // Against 503, 503, 200 on one kept-alive connection curl printed "2 0" (BL-799 Notes).
        ScriptedConnector server = new(new[] { EmptyBusy, EmptyBusy, EmptyOk }.Select(Encoding.Latin1.GetBytes)) { ReusesTheFirstConnection = true };

        int exitCode = await RunAsync(server, writesProgressMeter: false, "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}\\n", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2 0\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ConnIdAfterARetriedAttemptThatReportsNothing_TakesANewNumber()
    {
        // An HTTP attempt with no report cannot say it reused a connection, so it is taken to have opened one.
        ScriptedResultHandler handler = new(
            "http",
            TransferResult.Failure(CurlExitCode.CouldntResolveHost, "Could not resolve host: a"),
            TransferResult.Success(0));

        int exitCode = await RunAsync(handler, writesProgressMeter: false, "-s", "--retry", "1", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("1 1", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ConnIdAfterARetriedFtpAttempt_TakesANewNumber()
    {
        // Only HTTP counts the connections it opened, so an FTP report's zero says nothing about reuse.
        ScriptedResultHandler handler = new(
            "ftp",
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out"),
            TransferResult.Success(0) with { Report = new TransferReport() });

        int exitCode = await RunAsync(handler, writesProgressMeter: false, "-s", "--retry", "1", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}", "ftp://127.0.0.1:18241/a");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("1 1", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NumRetriesWithoutRetry_PrintsZero()
    {
        int exitCode = await RunAsync([EmptyBusy], "-s", "-w", "%{num_retries}", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_LimitRate_WritesTheBodyOneSecondsWorthAtATime()
    {
        int exitCode = await RunAsync([Ok], "-sS", "--limit-rate", "2", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_LimitRateZero_SetsNoLimit()
    {
        int exitCode = await RunAsync([Ok], "-sS", "--limit-rate", "0", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.AreEqual(0, clock.Waits.Count);
    }

    [TestMethod]
    public async Task RunAsync_RetryWithLogLevelWarningToLogFile_LogsTheRetryUnderRetry()
    {
        int exitCode = await RunAsync([Busy, Ok], "-s", "--retry", "1", "--log-level", "warning", "--log-file", "x.log", Url);

        Assert.AreEqual(0, exitCode);
        string log = Encoding.UTF8.GetString(outputFiles.Written["x.log"].ToArray());
        StringAssert.Contains(log, "] [warning] [retry] attempt 1 failed (HttpError); retrying in 1000 ms, 1 retries left" + NewLine);
        Assert.IsFalse(log.Contains("[info]", StringComparison.Ordinal), log);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeAfterRangesAccepted_KeepsTheBytesAndResumesAfterThem()
    {
        // curl -v -C - --retry 1 --retry-all-errors -o out.txt (BL-1402 Context).
        ScriptedConnector server = ShortOkThen(AcceptsRanges, PartialRest);

        int exitCode = await RunAsync(server, writesProgressMeter: false, ResumingRetryArguments("-v", "-C", "-"));

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, EndOfResponseLine + AllErrorsRetryWarningLine + "Note: Keeping 5 bytes" + NewLine);
        StringAssert.Contains(SecondRequest(server), "\r\nRange: bytes=5-\r\n");
        Assert.AreEqual("hello56789", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeWithoutAcceptRanges_ThrowsTheBytesAwayAndStartsAgain()
    {
        ScriptedConnector server = ShortOkThen(string.Empty, FullOk);

        int exitCode = await RunAsync(server, writesProgressMeter: false, ResumingRetryArguments("-v", "-C", "-"));

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, AllErrorsRetryWarningLine + "Note: Throwing away 5 bytes" + NewLine);
        Assert.IsFalse(SecondRequest(server).Contains("Range:", StringComparison.Ordinal));
        Assert.AreEqual("0123456789", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeWithoutAcceptRangesOrVerbose_ThrowsTheBytesAwayWithoutANote()
    {
        ScriptedConnector server = ShortOkThen(string.Empty, FullOk);

        int exitCode = await RunAsync(server, writesProgressMeter: false, ResumingRetryArguments("-C", "-"));

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(StandardErrorText.Contains("Note:", StringComparison.Ordinal), StandardErrorText);
        Assert.AreEqual("0123456789", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetryWithoutResumeAfterRangesAccepted_ThrowsTheBytesAway()
    {
        ScriptedConnector server = ShortOkThen(AcceptsRanges, FullOk);

        int exitCode = await RunAsync(server, writesProgressMeter: false, ResumingRetryArguments("-v"));

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, AllErrorsRetryWarningLine + "Note: Throwing away 5 bytes" + NewLine);
        Assert.IsFalse(StandardErrorText.Contains("Keeping", StringComparison.Ordinal), StandardErrorText);
        Assert.IsFalse(SecondRequest(server).Contains("Range:", StringComparison.Ordinal));
        Assert.AreEqual("0123456789", OutputFileText);
    }

    [TestMethod]
    [DataRow("-X", "GET")]
    [DataRow("-d", "x")]
    public async Task RunAsync_RetriedResumeOfARequestNotAPlainGet_ThrowsTheBytesAway(string option, string value)
    {
        ScriptedConnector server = ShortOkThen(AcceptsRanges, FullOk);

        int exitCode = await RunAsync(server, writesProgressMeter: false, ResumingRetryArguments("-v", "-C", "-", option, value));

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "Note: Throwing away 5 bytes" + NewLine);
        Assert.AreEqual("0123456789", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeOfAnAttemptThatFailedToWrite_KeepsNothing()
    {
        HelloWritingHandler handler = new(
            "http",
            TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination") with { Report = AcceptingRangesReport });

        int exitCode = await RunAsync(handler, writesProgressMeter: false, ResumingRetryArguments("-v", "-C", "-"));

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "Note: Throwing away 5 bytes" + NewLine);
        Assert.AreEqual("hello", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeOfAnAttemptThatAcceptedRanges_KeepsTheBytes()
    {
        HelloWritingHandler handler = new(
            "http",
            TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 5 bytes missing") with { Report = AcceptingRangesReport });

        int exitCode = await RunAsync(handler, writesProgressMeter: false, ResumingRetryArguments("-v", "-C", "-"));

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "Note: Keeping 5 bytes" + NewLine);
        Assert.AreEqual("hellohello", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeAnsweredPartialToAResume_KeepsTheBytesAgain()
    {
        TransferResult shortPartial = TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 5 bytes missing");
        HelloWritingHandler handler = new(
            "http",
            shortPartial with { Report = AcceptingRangesReport },
            shortPartial with { Report = AcceptingRangesReport with { ResponseCode = 206, ResponseHeaders = [] } });

        int exitCode = await RunAsync(handler, writesProgressMeter: false, "-v", "-C", "-", "--no-progress-meter", "--retry", "2", "--retry-all-errors", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(2, StandardErrorText.Split("Note: Keeping 5 bytes").Length - 1, StandardErrorText);
        Assert.AreEqual("hellohellohello", OutputFileText);
    }

    [TestMethod]
    [DataRow(206, "GET", CurlExitCode.PartialFile, "http", DisplayName = "206 to a request that did not resume")]
    [DataRow(503, "GET", CurlExitCode.PartialFile, "http", DisplayName = "neither 200 nor 206")]
    [DataRow(200, "GET", CurlExitCode.RangeError, "http", DisplayName = "exit 33")]
    [DataRow(200, "GET", CurlExitCode.PartialFile, "ftp", DisplayName = "not http")]
    [DataRow(0, null, CurlExitCode.PartialFile, "http", DisplayName = "no report")]
    public async Task RunAsync_RetriedResumeTheServerDidNotTake_ThrowsTheBytesAway(int responseCode, string? method, CurlExitCode exit, string scheme)
    {
        TransferResult first = TransferResult.Failure(exit, "failed") with
        {
            Report = method is null ? null : AcceptingRangesReport with { ResponseCode = responseCode },
        };
        HelloWritingHandler handler = new(scheme, first);

        int exitCode = await RunAsync(handler, writesProgressMeter: false, "-v", "-C", "-", "--no-progress-meter", "--retry", "1", "--retry-all-errors", "-o", "out.txt", $"{scheme}://127.0.0.1:18241/a");

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "Note: Throwing away 5 bytes" + NewLine);
        Assert.AreEqual("hello", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeToStandardOutput_KeepsAndNotesNothing()
    {
        ScriptedConnector server = ShortOkThen(AcceptsRanges, FullOk);

        int exitCode = await RunAsync(server, writesProgressMeter: false, "-v", "-C", "-", "--no-progress-meter", "--retry", "1", "--retry-all-errors", "--retry-delay", "1", Url);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(StandardErrorText.Contains("Note:", StringComparison.Ordinal), StandardErrorText);
        Assert.IsFalse(SecondRequest(server).Contains("Range:", StringComparison.Ordinal));
        Assert.AreEqual("hello0123456789", StandardOutputText);
    }

    private const string AcceptsRanges = "Accept-Ranges: bytes\r\n";

    private const string PartialRest = "HTTP/1.1 206 Partial\r\nContent-Range: bytes 5-9/10\r\nContent-Length: 5\r\n\r\n56789";

    private const string FullOk = "HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123456789";

    private static readonly string EndOfResponseLine = "curl: (18) end of response with 5 bytes missing" + NewLine;

    private static readonly string AllErrorsRetryWarningLine =
        "Warning: Problem (retrying all errors). Retrying in 1 second. 1 retry left." + NewLine;

    private static readonly TransferReport AcceptingRangesReport = new()
    {
        ResponseCode = 200,
        Method = "GET",
        ResponseHeaders = [new("Accept-Ranges", "bytes")],
    };

    private string OutputFileText => Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray());

    private static string[] ResumingRetryArguments(params string[] options) =>
        [.. options, "--no-progress-meter", "--retry", "1", "--retry-all-errors", "--retry-delay", "1", "-o", "out.txt", Url];

    /// <summary>
    /// Serves a <c>200</c> promising ten bytes that closes after <c>hello</c>, with
    /// <paramref name="firstHeaders" />, then <paramref name="second" />; the empty read between
    /// them is the first connection closing.
    /// </summary>
    private static ScriptedConnector ShortOkThen(string firstHeaders, string second) =>
        new(new[] { $"HTTP/1.1 200 OK\r\n{firstHeaders}Content-Length: 10\r\n\r\nhello", string.Empty, second }.Select(Encoding.Latin1.GetBytes));

    /// <summary>Gets the second request <paramref name="server" /> was sent.</summary>
    private static string SecondRequest(ScriptedConnector server)
    {
        string sent = Encoding.Latin1.GetString(server.Written);
        int second = sent.IndexOf("GET ", 1, StringComparison.Ordinal);

        return second < 0 ? string.Empty : sent[second..];
    }

    /// <summary>
    /// A handler for <paramref name="scheme" /> whose every attempt writes <c>hello</c>: the first
    /// ones end with <paramref name="firstResults" />, in order, and the rest succeed.
    /// </summary>
    private sealed class HelloWritingHandler(string scheme, params TransferResult[] firstResults) : IProtocolHandler
    {
        private int attempts;

        public IReadOnlyCollection<string> SupportedSchemes { get; } = [scheme];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            await context.Output.WriteAsync("hello"u8.ToArray());

            return attempts < firstResults.Length ? firstResults[attempts++] : TransferResult.Success(5);
        }
    }

    private Task<int> RunAsync(string[] responses, params string[] arguments) =>
        RunAsync(responses, writesProgressMeter: false, arguments);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> serving <paramref name="responses" />, one per
    /// connection, on <see cref="clock" />.
    /// </summary>
    private Task<int> RunAsync(string[] responses, bool writesProgressMeter, params string[] arguments) =>
        RunAsync(new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes)), writesProgressMeter, arguments);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over
    /// <paramref name="server" />, on <see cref="clock" />.
    /// </summary>
    private Task<int> RunAsync(ScriptedConnector server, bool writesProgressMeter, params string[] arguments) =>
        RunAsync(new HttpProtocolHandler(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false))), writesProgressMeter, arguments);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <paramref name="handler" />, on <see cref="clock" />.
    /// </summary>
    private Task<int> RunAsync(IProtocolHandler handler, bool writesProgressMeter, params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: writesProgressMeter,
                timeProvider: clock)
            .RunAsync(arguments);
}
