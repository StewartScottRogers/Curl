using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins EXT-097: a TLS handshake <c>-m</c> or the connect limit ends over an extended protocol reports the open TCP
/// connection's end points as <c>%{remote_ip}</c> and <c>%{remote_port}</c>, as the plain-TCP extended protocols do;
/// curl's own schemes keep them empty, as curl prints <c>ip=:-1</c> for such an https transfer (measured, ADR-1475).
/// </summary>
[TestClass]
public sealed class Ext097Tests
{
    private static readonly ConnectTarget Target = new("127.0.0.1", 28950, true);

    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 28950);

    private static readonly ConnectResult HandshakeTimedOut =
        ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Connection timed out after 2000 milliseconds")
            .WithTcpConnectedBeforeFailure(true)
            .WithEndPointsBeforeFailure(Local, Remote);

    [TestMethod]
    [DataRow("amqps", true, DisplayName = "An extended protocol reports the end points")]
    [DataRow("https", false, DisplayName = "curl's own scheme reports none, as curl does")]
    public async Task ExecuteAsync_HandshakeTimedOut_ReportsTheEndPointsOnlyForAnExtendedProtocol(string scheme, bool reports)
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);
        EndPointReportingProtocolHandler handler = HandlerConnectingThrough(connector, recorder, scheme, () => TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out"));

        TransferResult result = await handler.ExecuteAsync(ContextFor(scheme));

        Assert.AreEqual(reports ? Remote : null, result.Report!.RemoteEndPoint);
        Assert.AreEqual(reports ? Local : null, result.Report.LocalEndPoint);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandshakeCancelledByMaxTime_CarriesTheEndPointsOnTheCancellationsReport()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => throw CancellationCarrying(HandshakeTimedOut)), recorder);
        EndPointReportingProtocolHandler handler = HandlerConnectingThrough(connector, recorder, "mqtt5s", () => TransferResult.Success(0));

        var cancelled = await Assert.ThrowsAsync<OperationCanceledException>(async () => await handler.ExecuteAsync(ContextFor("mqtt5s")));

        TransferReport report = EndPointReportingProtocolHandler.ReportCarriedBy(cancelled)!;
        Assert.AreEqual(Remote, report.RemoteEndPoint);
        Assert.AreEqual(1, report.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_SucceededAfterAHandshakeFailed_KeepsNoEndPointOfTheFailedConnect()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);
        EndPointReportingProtocolHandler handler = HandlerConnectingThrough(connector, recorder, "amqps", () => TransferResult.Success(0));

        TransferResult result = await handler.ExecuteAsync(ContextFor("amqps"));

        Assert.IsNull(result.Report?.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReportOn_FailedConnectWithNoKnownPeer_ReportsNoEndPoint()
    {
        ConnectionEndPointRecorder recorder = new() { ReportsEndPointsOfFailedConnects = true };
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut.WithEndPointsBeforeFailure(null, null)), recorder);
        await connector.ConnectAsync(Target, CancellationToken.None);

        TransferResult result = recorder.ReportOn(TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out"));

        Assert.IsNull(result.Report!.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReportOn_HandlerReportedItsOwnPeer_KeepsIt()
    {
        IPEndPoint own = new(IPAddress.Loopback, 1);
        ConnectionEndPointRecorder recorder = new() { ReportsEndPointsOfFailedConnects = true };
        EndPointRecordingConnector connector = new(new AnsweringConnector(() => HandshakeTimedOut), recorder);
        await connector.ConnectAsync(Target, CancellationToken.None);
        await connector.ConnectAsync(Target, CancellationToken.None);

        TransferResult result = recorder.ReportOn(TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out") with { Report = new TransferReport { RemoteEndPoint = own } });

        Assert.AreEqual(own, result.Report!.RemoteEndPoint);
        Assert.AreEqual(2, result.Report.ConnectionCount);
    }

    private static TransferContext ContextFor(string scheme) =>
        new() { Url = CurlUrl.Parse($"{scheme}://127.0.0.1:28950/q"), Output = new MemoryStream() };

    private static EndPointReportingProtocolHandler HandlerConnectingThrough(
        EndPointRecordingConnector connector, ConnectionEndPointRecorder recorder, string scheme, Func<TransferResult> outcome) =>
        new(
            new RecordingProtocolHandler(scheme, async _ =>
            {
                await connector.ConnectAsync(Target, CancellationToken.None);
                return outcome();
            }),
            recorder);

    private static OperationCanceledException CancellationCarrying(ConnectResult carried)
    {
        OperationCanceledException canceled = new();
        canceled.Data[ConnectResult.TcpConnectedOnCancellationKey] = carried;
        return canceled;
    }

    /// <summary>A connector whose every connect runs the given answer; QUIC keeps the interface's default refusal.</summary>
    private sealed class AnsweringConnector(Func<ConnectResult> answer) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(answer());

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
            ValueTask.FromResult(answer());
    }
}
