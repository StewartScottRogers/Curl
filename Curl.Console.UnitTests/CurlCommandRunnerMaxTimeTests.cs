using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>-m</c> ending a transfer for the schemes with no time limit of their own
/// (ADR-0117, BL-511): the runner's <see cref="MaxTimeWatchdog" /> cancels the attempt, and
/// the production handler set, over a <see cref="StallingConnector" /> whose server sends a few
/// bytes and goes silent, ends with curl's exit 28 and message, on a
/// <see cref="SteppingTimeProvider" /> so no second is real. Measured on 2026-09-28 with curl
/// 8.21.0 (mingw, Schannel) through <c>Record-CurlExchange.ps1 -HoldOpenMilliseconds</c>,
/// against a server that sent <c>hello\r\n</c> (or one MQTT PUBLISH) and stalled
/// (BL-511 Notes): dict and gopher printed <c>Operation timed out after N milliseconds with 7
/// bytes received</c>, telnet <c>Time-out</c>, and mqtt <c>with 5 out of 5 bytes received</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerMaxTimeTests
{
    private static readonly byte[] Hello = "hello\r\n"u8.ToArray();

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly SteppingTimeProvider clock = new();

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_DictStalledAfterAReply_EndsAtMaxTimeWithTheBytesReceived()
    {
        StallingConnector connector = new(Hello);

        int exitCode = await RunUntilMaxTimeAsync(connector, "dict://127.0.0.1/d:x");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 7 bytes received" + NewLine, StandardErrorText);
        CollectionAssert.AreEqual(Hello, standardOutput.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_GopherStalledAfterAReply_EndsAtMaxTimeWithTheBytesReceived()
    {
        StallingConnector connector = new(Hello);

        int exitCode = await RunUntilMaxTimeAsync(connector, "gopher://127.0.0.1/1");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 7 bytes received" + NewLine, StandardErrorText);
        CollectionAssert.AreEqual(Hello, standardOutput.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_TelnetStalledAfterAReply_EndsAtMaxTimeWithCurlsTelnetMessage()
    {
        StallingConnector connector = new(Hello);

        int exitCode = await RunUntilMaxTimeAsync(connector, "telnet://127.0.0.1/");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Time-out" + NewLine, StandardErrorText);
        CollectionAssert.AreEqual(Hello, standardOutput.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_MqttSubscriptionStalledAfterAPublish_EndsAtMaxTimeWithThePublishSize()
    {
        byte[] connack = [0x20, 0x02, 0x00, 0x00];
        byte[] suback = [0x90, 0x03, 0x00, 0x01, 0x00];
        byte[] publish = [0x30, 0x05, 0x00, 0x01, (byte)'t', (byte)'h', (byte)'i'];
        StallingConnector connector = new(connack, suback, publish);

        int exitCode = await RunUntilMaxTimeAsync(connector, "mqtt://127.0.0.1/t");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 5 out of 5 bytes received" + NewLine, StandardErrorText);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, (byte)'t', (byte)'h', (byte)'i' }, standardOutput.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_StalledBeforeMaxTime_IsStillRunning()
    {
        StallingConnector connector = new(Hello);
        Task<int> run = RunAsync(connector, "-sS", "-m", "1", "dict://127.0.0.1/d:x");
        await connector.Stalled;

        clock.Advance(TimeSpan.FromMilliseconds(999));
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, await run);
    }

    [TestMethod]
    public async Task RunAsync_ConnectStillRunningAtMaxTime_EndsWithTheConnectMessage()
    {
        StallingConnectConnector connector = new();
        Task<int> run = RunAsync(connector, "-sS", "-m", "2", "gopher://127.0.0.1/1");
        await connector.Stalled;

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, await run);
        Assert.AreEqual("curl: (28) Connection timed out after 2000 milliseconds" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CancelledBeforeMaxTime_LetsTheCancellationOut()
    {
        RecordingProtocolHandler cancelled = new("dict", _ => throw new OperationCanceledException());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => RunAsync(new TransferDispatch(new ProtocolDispatcher([cancelled])), "-sS", "-m", "1", "dict://127.0.0.1/d:x"));
    }

    [TestMethod]
    public async Task RunAsync_WithMaxTime_GivesTheContextTheWatchdogsStartAsOperationStarted()
    {
        RecordingProtocolHandler writing = RecordingProtocolHandler.WritingPath("dict");
        clock.Advance(TimeSpan.FromSeconds(5));

        int exitCode = await RunAsync(new TransferDispatch(new ProtocolDispatcher([writing])), "-sS", "-m", "1", "-o", "out.txt", "dict://127.0.0.1/d:x");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(TimeSpan.FromSeconds(5).Ticks, writing.Contexts[0].OperationStarted);
    }

    /// <summary>Runs <c>-sS -m 1</c> for <paramref name="url" /> and advances the clock one second once the server has stalled.</summary>
    private async Task<int> RunUntilMaxTimeAsync(StallingConnector connector, string url)
    {
        Task<int> run = RunAsync(connector, "-sS", "-m", "1", url);
        await connector.Stalled;
        clock.Advance(TimeSpan.FromSeconds(1));
        return await run;
    }

    private Task<int> RunAsync(IConnector connector, params string[] arguments) =>
        RunAsync(
            new TransferDispatch(
                new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
                    connector,
                    new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                    new PassThroughTlsProvider(),
                    new LoopbackDnsResolver()))),
            arguments);

    private Task<int> RunAsync(TransferDispatch dispatch, params string[] arguments)
    {
        InMemoryFileSystem files = new();
        return new CurlCommandRunner(
                _ => dispatch,
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                timeProvider: clock)
            .RunAsync(arguments);
    }

    /// <summary>A connector whose every connect waits until its token is cancelled.</summary>
    private sealed class StallingConnectConnector : IConnector
    {
        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Stalled => stalled.Task;

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("An infinite delay ended without being cancelled.");
        }
    }
}
