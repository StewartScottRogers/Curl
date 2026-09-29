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

    private Task<int> RunAsync(string[] responses, params string[] arguments) =>
        RunAsync(responses, writesProgressMeter: false, arguments);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> serving <paramref name="responses" />, one per
    /// connection, on <see cref="clock" />.
    /// </summary>
    private Task<int> RunAsync(string[] responses, bool writesProgressMeter, params string[] arguments)
    {
        ScriptedConnector server = new(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
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
}
