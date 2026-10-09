using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

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

    private static readonly string TooManyRequestsLine = "curl: (22) The requested URL returned error: 429" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly ImmediateTimerTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string WaitsText => string.Join(", ", clock.Waits.Select(wait => (long)wait.TotalMilliseconds));

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private string WrittenText(string name) => Encoding.Latin1.GetString(outputFiles.Written[name].ToArray());

    [TestMethod]
    public async Task RunAsync_RetryAfterServiceUnavailable_WarnsWaitsAndSucceedsOnTheSecondAttempt()
    {
        int exitCode = await RunAsync([Busy, Ok], "--retry", "1", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(RetryWarningLine), Lf(StandardErrorText));
        Diagnostics.Diff("stdout", "busyhello", StandardOutputText);
        Diagnostics.Assert("waits (ms)", "1000", WaitsText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(RetryWarningLine, StandardErrorText);
        Assert.AreEqual("busyhello", StandardOutputText);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    /// <summary>
    /// Replays upstream test1633: a <c>-d</c> POST redirected by a <c>301</c> to a <c>429</c> with
    /// <c>Retry-After: 1</c> is retried from the first URL, so curl 8.21.0 sends the POST with its
    /// body again and follows the <c>301</c> again, and <c>-i</c> writes all four heads.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetryOfARedirectedPostAnswered429_ResendsThePostFromTheFirstUrl()
    {
        const string moved = "HTTP/1.1 301 OK\r\nAccept-Ranges: bytes\r\nContent-Length: 0\r\nConnection: close\r\nLocation: /16330002\r\n\r\n";
        const string tooMany = "HTTP/1.1 429 too many requests\r\nRetry-After: 1\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        ScriptedConnector server = new(new[] { moved, tooMany, moved, tooMany }.Select(Encoding.Latin1.GetBytes));

        int exitCode = await RunAsync(server, writesProgressMeter: false, "http://127.0.0.1:18241/1633", "-d", "moo", "--retry", "1", "-L", "-i");
        string[] requestLines = [.. Encoding.Latin1.GetString(server.Written).Split("\r\n").Where(line => line.StartsWith("POST ", StringComparison.Ordinal) || line.StartsWith("GET ", StringComparison.Ordinal) || line.StartsWith("moo", StringComparison.Ordinal))];

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", moved + tooMany + moved + tooMany, StandardOutputText);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "POST /1633 HTTP/1.1", "mooGET /16330002 HTTP/1.1", "POST /1633 HTTP/1.1", "mooGET /16330002 HTTP/1.1" }, requestLines);
        Assert.AreEqual(moved + tooMany + moved + tooMany, StandardOutputText);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    /// <summary>
    /// Replays upstream test1634: under <c>--fail</c> a <c>429</c> with <c>Retry-After: 1</c> is
    /// retried, and with the <c>-i</c> runtests adds curl 8.21.0 writes the <c>429</c>'s head but
    /// not its body, then the <c>200</c>'s head and body, and exits 0 (tests/data/test1634's datacheck).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetryUnderFailOfA429WithRetryAfter_KeepsThe429sHeadAndWritesTheRetrysResponse()
    {
        const string tooManyHead = "HTTP/1.1 429 too many requests swsbounce\r\nRetry-After: 1\r\nContent-Length: 4\r\n\r\n";
        const string okHead = "HTTP/1.1 200 OK\r\nContent-Length: 4\r\nConnection: close\r\n\r\n";
        ScriptedConnector server = new(new[] { tooManyHead + "moo\n", okHead + "hey\n" }.Select(Encoding.Latin1.GetBytes));

        int exitCode = await RunAsync(server, writesProgressMeter: false, "http://127.0.0.1:18241/1634", "--retry", "1", "--fail", "-i");
        string[] requestLines = [.. Encoding.Latin1.GetString(server.Written).Split("\r\n").Where(line => line.StartsWith("GET ", StringComparison.Ordinal))];

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", tooManyHead + okHead + "hey\n", StandardOutputText);
        Diagnostics.Diff("stderr", Lf(TooManyRequestsLine + RetryWarningLine), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "GET /1634 HTTP/1.1", "GET /1634 HTTP/1.1" }, requestLines);
        Assert.AreEqual(tooManyHead + okHead + "hey\n", StandardOutputText);
        Assert.AreEqual(TooManyRequestsLine + RetryWarningLine, StandardErrorText);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    /// <summary>
    /// Replays upstream test366: a <c>503</c> whose <c>Retry-After: 200</c> ends past
    /// <c>--retry-max-time 10</c> is not retried, so curl 8.21.0 sends one GET, writes the body,
    /// warns in two lines wrapped at 79 columns and exits 0 (measured with Record-CurlExchange.ps1
    /// on 2026-10-08, BL-1844).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetryAfterLongerThanRetryMaxTime_SendsOneRequestWarnsAndExitsZero()
    {
        const string busy = "HTTP/1.1 503 BAD\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nContent-Length: 21\r\nRetry-After: 200\r\n\r\nserver not available\n";
        ScriptedConnector server = new(new[] { busy, Ok }.Select(Encoding.Latin1.GetBytes));
        string expectedError =
            "Warning: The Retry-After: time would make this command line exceed the maximum " + NewLine
            + "Warning: allowed time for retries." + NewLine;

        int exitCode = await RunAsync(server, writesProgressMeter: false, "http://127.0.0.1:18241/366", "--retry", "2", "--retry-max-time", "10");
        string[] requestLines = [.. Encoding.Latin1.GetString(server.Written).Split("\r\n").Where(line => line.StartsWith("GET ", StringComparison.Ordinal))];

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedError), Lf(StandardErrorText));
        Diagnostics.Diff("stdout", "server not available\n", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "GET /366 HTTP/1.1" }, requestLines);
        Assert.AreEqual(expectedError, StandardErrorText);
        Assert.AreEqual("server not available\n", StandardOutputText);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task RunAsync_RetryIntoOutputFile_KeepsOnlyTheLastAttemptsBody()
    {
        int exitCode = await RunAsync([Busy, Ok], "--retry", "1", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("out.txt content", "hello", WrittenText("out.txt"));
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_RetryUnderShowErrorAndFail_PrintsEachAttemptsLineAndNoWarning()
    {
        int exitCode = await RunAsync([Busy, Busy], "-sS", "-f", "--retry", "1", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Diagnostics.Diff("stderr", Lf(ServiceUnavailableLine + ServiceUnavailableLine), Lf(StandardErrorText));
        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(ServiceUnavailableLine + ServiceUnavailableLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RetryUnderSilent_PrintsNothing()
    {
        int exitCode = await RunAsync([Busy, Ok], "-s", "--retry", "1", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RetryWithFailAndMeter_PrintsTheHeaderOnceAndEachAttemptsMeterLineAndFailure()
    {
        int exitCode = await RunAsync([Busy, Busy], writesProgressMeter: true, "-f", "--retry", "1", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Diagnostics.Diff(
            "stderr",
            Lf(
                ProgressMeterLines.FirstHeaderLine + NewLine
                + ProgressMeterLines.SecondHeaderLine + NewLine
                + ProgressMeterLines.ZeroStatusLine + NewLine
                + ServiceUnavailableLine
                + RetryWarningLine
                + ProgressMeterLines.ZeroStatusLine + NewLine
                + ServiceUnavailableLine),
            Lf(StandardErrorText));
        Diagnostics.Assert("out.txt written", false, outputFiles.Written.ContainsKey("out.txt"));
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: The Retry-After: time would make this command line exceed the maximum \nWarning: allowed time for retries.\n",
            Lf(StandardErrorText));
        Diagnostics.Diff("stdout", "busy", StandardOutputText);
        Diagnostics.Assert("wait count", 0, clock.Waits.Count);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "ok2", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok2", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NumRetriesAfterRetriesRunOut_PrintsTwo()
    {
        // curl -s --retry 2 --retry-delay 1 -w '%{num_retries}' against three 503s (BL-513 Notes).
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, EmptyBusy], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{num_retries}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "2", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_JsonAfterTwoRetries_PrintsNumRetriesTwo()
    {
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, OkOk], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{json}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stdout contains num_retries 2", true, StandardOutputText.Contains("\"num_redirects\":0,\"num_retries\":2,", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardOutputText, "\"num_redirects\":0,\"num_retries\":2,");
    }

    [TestMethod]
    public async Task RunAsync_XferIdAndConnIdAfterTwoRetriesThatSucceed_CountEveryAttempt()
    {
        // curl -s --retry 2 --retry-delay 1 -w '%{xfer_id} %{conn_id}\n' against 503, 503, 200,
        // each closing its connection, printed "2 2" (BL-799 Notes).
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, EmptyOk], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}\\n", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "2 2\n", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2 2\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_XferIdAndConnIdAfterRetriesRunOut_CountEveryAttempt()
    {
        // The same against three 503s printed "2 2" (BL-799 Notes).
        int exitCode = await RunAsync([EmptyBusy, EmptyBusy, EmptyBusy], "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}\\n", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "2 2\n", StandardOutputText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "2 2\n3 3\n", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("2 2\n3 3\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ConnIdAfterRetriesOverOneKeptAliveConnection_KeepsTheFirstConnectionsNumber()
    {
        // Against 503, 503, 200 on one kept-alive connection curl printed "2 0" (BL-799 Notes).
        ScriptedConnector server = new(new[] { EmptyBusy, EmptyBusy, EmptyOk }.Select(Encoding.Latin1.GetBytes)) { ReusesTheFirstConnection = true };

        int exitCode = await RunAsync(server, writesProgressMeter: false, "-s", "--retry", "2", "--retry-delay", "1", "-w", "%{xfer_id} %{conn_id}\\n", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "2 0\n", StandardOutputText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "1 1", StandardOutputText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "1 1", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("1 1", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NumRetriesWithoutRetry_PrintsZero()
    {
        int exitCode = await RunAsync([EmptyBusy], "-s", "-w", "%{num_retries}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "0", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_LimitRate_WritesTheBodyOneSecondsWorthAtATime()
    {
        int exitCode = await RunAsync([Ok], "-sS", "--limit-rate", "2", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "hello", StandardOutputText);
        Diagnostics.Assert("waits (ms)", "1000, 1000", WaitsText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_LimitRateZero_SetsNoLimit()
    {
        int exitCode = await RunAsync([Ok], "-sS", "--limit-rate", "0", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("out.txt content", "hello", WrittenText("out.txt"));
        Diagnostics.Assert("wait count", 0, clock.Waits.Count);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.AreEqual(0, clock.Waits.Count);
    }

    [TestMethod]
    public async Task RunAsync_RetryWithLogLevelWarningToLogFile_LogsTheRetryUnderRetry()
    {
        int exitCode = await RunAsync([Busy, Ok], "-s", "--retry", "1", "--log-level", "warning", "--log-file", "x.log", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string log = Encoding.UTF8.GetString(outputFiles.Written["x.log"].ToArray());
        Diagnostics.Act("log", Lf(log));
        Diagnostics.Assert("log contains the retry line", true, log.Contains("] [warning] [retry] attempt 1 failed (HttpError); retrying in 1000 ms, 1 retries left" + NewLine, StringComparison.Ordinal));
        Diagnostics.Assert("log contains [info]", false, log.Contains("[info]", StringComparison.Ordinal));
        StringAssert.Contains(log, "] [warning] [retry] attempt 1 failed (HttpError); retrying in 1000 ms, 1 retries left" + NewLine);
        Assert.IsFalse(log.Contains("[info]", StringComparison.Ordinal), log);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeAfterRangesAccepted_KeepsTheBytesAndResumesAfterThem()
    {
        // curl -v -C - --retry 1 --retry-all-errors -o out.txt (BL-1402 Context).
        ScriptedConnector server = ShortOkThen(AcceptsRanges, PartialRest);

        int exitCode = await RunAsync(server, writesProgressMeter: false, ResumingRetryArguments("-v", "-C", "-"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the keeping note", true, StandardErrorText.Contains(EndOfResponseLine + AllErrorsRetryWarningLine + "Note: Keeping 5 bytes" + NewLine, StringComparison.Ordinal));
        Diagnostics.Assert("second request has Range 5-", true, SecondRequest(server).Contains("\r\nRange: bytes=5-\r\n", StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "hello56789", OutputFileText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the throwing note", true, StandardErrorText.Contains(AllErrorsRetryWarningLine + "Note: Throwing away 5 bytes" + NewLine, StringComparison.Ordinal));
        Diagnostics.Assert("second request has Range", false, SecondRequest(server).Contains("Range:", StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "0123456789", OutputFileText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains Note:", false, StandardErrorText.Contains("Note:", StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "0123456789", OutputFileText);
        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(StandardErrorText.Contains("Note:", StringComparison.Ordinal), StandardErrorText);
        Assert.AreEqual("0123456789", OutputFileText);
    }

    [TestMethod]
    public async Task RunAsync_RetryWithoutResumeAfterRangesAccepted_ThrowsTheBytesAway()
    {
        ScriptedConnector server = ShortOkThen(AcceptsRanges, FullOk);

        int exitCode = await RunAsync(server, writesProgressMeter: false, ResumingRetryArguments("-v"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the throwing note", true, StandardErrorText.Contains(AllErrorsRetryWarningLine + "Note: Throwing away 5 bytes" + NewLine, StringComparison.Ordinal));
        Diagnostics.Assert("stderr contains Keeping", false, StandardErrorText.Contains("Keeping", StringComparison.Ordinal));
        Diagnostics.Assert("second request has Range", false, SecondRequest(server).Contains("Range:", StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "0123456789", OutputFileText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the throwing note", true, StandardErrorText.Contains("Note: Throwing away 5 bytes" + NewLine, StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "0123456789", OutputFileText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the throwing note", true, StandardErrorText.Contains("Note: Throwing away 5 bytes" + NewLine, StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "hello", OutputFileText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the keeping note", true, StandardErrorText.Contains("Note: Keeping 5 bytes" + NewLine, StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "hellohello", OutputFileText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("keeping notes", 2, StandardErrorText.Split("Note: Keeping 5 bytes").Length - 1);
        Diagnostics.Diff("out.txt content", "hellohellohello", OutputFileText);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the throwing note", true, StandardErrorText.Contains("Note: Throwing away 5 bytes" + NewLine, StringComparison.Ordinal));
        Diagnostics.Diff("out.txt content", "hello", OutputFileText);
        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "Note: Throwing away 5 bytes" + NewLine);
        Assert.AreEqual("hello", OutputFileText);
    }

    /// <summary>
    /// Replays upstream test3036: <c>--no-clobber --output-dir present -OJ --retry 1
    /// --retry-all-errors</c> where <c>present</c> is a file, so the <c>-J</c> file cannot be
    /// created. curl 8.21.0 reports each attempt on its own: the first fails opening the file as
    /// the <c>Content-Disposition</c> line names it (52 bytes, that line's length), the second takes
    /// no name from its headers and fails opening it at its first body write (6 bytes); both warn,
    /// and the run exits 23 with nothing on standard output (measured with Record-CurlExchange.ps1
    /// on 2026-10-09, BL-1849 Notes).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetriedRemoteHeaderNameUnderAFile_ReportsEachAttemptsOpenFailure()
    {
        const string disposed = "HTTP/1.1 200 OK\r\nContent-Length: 6\r\nConnection: close\r\nContent-Disposition: inline; filename=\"MMM3036MMM\"\r\nContent-Type: text/html\r\n\r\n-foo-\n";
        const string openWarning = "Warning: Failed to open the file present/MMM3036MMM: No such file or directory";
        outputFiles.UnwritablePaths.Add("present/MMM3036MMM");
        Diagnostics.Arrange("unwritable files", "present/MMM3036MMM");
        ScriptedConnector server = new(new[] { disposed, disposed }.Select(Encoding.Latin1.GetBytes));
        string expected = "Warning: No remote filename, uses \"curl_response\"" + NewLine
            + openWarning + NewLine
            + "curl: (23) client returned ERROR on write of 52 bytes" + NewLine
            + AllErrorsRetryWarningLine
            + openWarning + NewLine
            + "curl: (23) client returned ERROR on write of 6 bytes" + NewLine;

        int exitCode = await RunAsync(
            server,
            writesProgressMeter: false,
            "http://127.0.0.1:18241/", "--no-progress-meter", "--no-clobber", "--output-dir", "present", "-OJ", "--retry", "1", "--retry-all-errors");
        int requests = Encoding.Latin1.GetString(server.Written).Split("GET / HTTP/1.1").Length - 1;

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Diagnostics.Assert("requests", 2, requests);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(expected, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.AreEqual(2, requests);
        Assert.IsEmpty(outputFiles.Written);
    }

    /// <summary>
    /// Under <c>-sS</c> the test3036 run keeps each attempt's error line and drops the warnings,
    /// the open failures' as the retry's, as curl 8.21.0 does (measured with Record-CurlExchange.ps1
    /// on 2026-10-09, BL-1849 Notes).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetriedRemoteHeaderNameUnderAFileSilent_PrintsOnlyEachAttemptsErrorLine()
    {
        const string disposed = "HTTP/1.1 200 OK\r\nContent-Length: 6\r\nConnection: close\r\nContent-Disposition: inline; filename=\"MMM3036MMM\"\r\n\r\n-foo-\n";
        outputFiles.UnwritablePaths.Add("present/MMM3036MMM");
        Diagnostics.Arrange("unwritable files", "present/MMM3036MMM");
        string expected = "curl: (23) client returned ERROR on write of 52 bytes" + NewLine
            + "curl: (23) client returned ERROR on write of 6 bytes" + NewLine;

        int exitCode = await RunAsync(
            [disposed, disposed],
            "http://127.0.0.1:18241/", "-sS", "--no-clobber", "--output-dir", "present", "-OJ", "--retry", "1", "--retry-all-errors");

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(expected, StandardErrorText);
    }

    /// <summary>
    /// A retried attempt's progress meter starts once the retry wait is over, as curl 8.21.0's
    /// does: after a one-second wait the second attempt's status lines show no time spent, where
    /// progress started before the wait drew an extra <c>00:01</c> line (measured with
    /// Record-CurlExchange.ps1 on 2026-10-09, a 503 then a 200 into <c>-o</c>, BL-1849 Notes).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetriedAttemptsProgressMeter_StartsAfterTheRetryWait()
    {
        int exitCode = await RunAsync([Busy, Ok], writesProgressMeter: true, "--retry", "1", "-o", "out.txt", Url);
        string secondAttempt = StandardErrorText[(StandardErrorText.IndexOf(RetryWarningLine, StringComparison.Ordinal) + RetryWarningLine.Length)..];
        string expected = "\r  0      0   0      0   0      0      0      0                              0"
            + "\r100      5 100      5   0      0  4.76M      0                              0"
            + "\r100      5 100      5   0      0  4.76M      0                              0"
            + "\r100      5 100      5   0      0  4.76M      0                              0"
            + NewLine;

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("second attempt's meter", Lf(expected), Lf(secondAttempt));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expected, secondAttempt);
    }

    [TestMethod]
    public async Task RunAsync_RetriedResumeToStandardOutput_KeepsAndNotesNothing()
    {
        ScriptedConnector server = ShortOkThen(AcceptsRanges, FullOk);

        int exitCode = await RunAsync(server, writesProgressMeter: false, "-v", "-C", "-", "--no-progress-meter", "--retry", "1", "--retry-all-errors", "--retry-delay", "1", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains Note:", false, StandardErrorText.Contains("Note:", StringComparison.Ordinal));
        Diagnostics.Assert("second request has Range", false, SecondRequest(server).Contains("Range:", StringComparison.Ordinal));
        Diagnostics.Diff("stdout", "hello0123456789", StandardOutputText);
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
    private async Task<int> RunAsync(IProtocolHandler handler, bool writesProgressMeter, params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("handler", $"{handler.GetType().Name} for {string.Join(", ", handler.SupportedSchemes)} on a clock whose every wait passes at once");
        Diagnostics.Arrange("progress meter", writesProgressMeter);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
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

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("waits (ms)", WaitsText);
        Diagnostics.Act("files written", string.Join(", ", outputFiles.Written.Keys.Order(StringComparer.Ordinal)));
        return exitCode;
    }
}
