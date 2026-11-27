using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// A connect a time limit ends prints one <c>Connection timed out after N milliseconds</c>,
/// the same N in the <c>-v</c> line and in the failure (BL-6071, ADR-1459).
/// </summary>
[TestClass]
public sealed class HttpConnectTimedOutLineTests
{
    private const string ConnectorsMessage = "Connection timed out after 3004 milliseconds";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_ConnectorReturnsItsTimedOutResultAsMaxTimePasses_FailsWithItsMessageAndReportsNoLine()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RecordingTransferEvents events = new();
        LateConnector connector = new();

        Task<TransferResult> transfer = TransferAsync(connector, time, events);
        await connector.Started;
        time.Advance(TimeSpan.FromSeconds(3));
        connector.Finish(ConnectResult.Failed(CurlExitCode.OperationTimedOut, ConnectorsMessage));
        TransferResult result = await transfer;

        Diagnostics.Assert("error message", ConnectorsMessage, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(ConnectorsMessage, result.ErrorMessage);
        Assert.IsFalse(events.Info.Any(IsTimedOutLine), "The connector has reported its line already.");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectorThrowsCancellationAsMaxTimePasses_ReportsOneLineWithTheFailuresMessage()
    {
        // A redirect hop: -m's clock started 2 s before this request, and curl counts N from the request (measured).
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        long operationStarted = time.GetTimestamp();
        time.Advance(TimeSpan.FromSeconds(2));
        RecordingTransferEvents events = new();
        LateConnector connector = new();

        Task<TransferResult> transfer = TransferAsync(connector, time, events, operationStarted);
        await connector.Started;
        time.Advance(TimeSpan.FromSeconds(1));
        connector.Fail(new OperationCanceledException());
        TransferResult result = await transfer;

        Diagnostics.Assert("information lines", result.ErrorMessage, string.Join(" | ", events.Info));
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1000 milliseconds", result.ErrorMessage);
        Assert.AreEqual(result.ErrorMessage, events.Info.Single(IsTimedOutLine));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectorReturnsAnotherFailureAfterMaxTimePasses_ReportsOneLineWithTheTimedOutMessage()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RecordingTransferEvents events = new();
        LateConnector connector = new();

        Task<TransferResult> transfer = TransferAsync(connector, time, events);
        await connector.Started;
        time.Advance(TimeSpan.FromSeconds(3));
        connector.Finish(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));
        TransferResult result = await transfer;

        Diagnostics.Assert("information lines", result.ErrorMessage, string.Join(" | ", events.Info));
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 3000 milliseconds", result.ErrorMessage);
        Assert.AreEqual(result.ErrorMessage, events.Info.Single(IsTimedOutLine));
    }

    [TestMethod]
    public async Task ConnectAsync_AbandonedConnect_ReportsNoLine()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        RecordingTransferEvents events = new();
        LateConnector connector = new();
        TransferContext context = ContextWith(time, events);
        using HttpTransferDeadline deadline = new(context);
        using CancellationTokenSource abandoned = new();

        Task<ConnectResult> connecting = deadline
            .ConnectAsync(connector, new ConnectTarget("192.0.2.20", 1, false) { Events = events }, abandoned.Token)
            .AsTask();
        await connector.Started;
        await abandoned.CancelAsync();
        connector.Fail(new OperationCanceledException());
        ConnectResult result = await connecting;

        Diagnostics.Assert("information lines", 0, events.Info.Count);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsEmpty(events.Info);
    }

    private static bool IsTimedOutLine(string line) => line.StartsWith("Connection timed out", StringComparison.Ordinal);

    private static Task<TransferResult> TransferAsync(
        IConnector connector, FakeTimeProvider time, RecordingTransferEvents events, long? operationStarted = null) =>
        new HttpProtocolHandler(connector, new SilentAuthenticator())
            .ExecuteAsync(ContextWith(time, events, operationStarted))
            .AsTask();

    private static TransferContext ContextWith(FakeTimeProvider time, RecordingTransferEvents events, long? operationStarted = null) =>
        new()
        {
            Url = CurlUrl.Parse("http://192.0.2.20:1/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            Events = events,
            MaxTime = TimeSpan.FromSeconds(3),
            OperationStarted = operationStarted,
        };
}
