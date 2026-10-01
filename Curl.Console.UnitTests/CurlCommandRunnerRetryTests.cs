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
