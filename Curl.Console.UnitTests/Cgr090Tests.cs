using System.Collections.Concurrent;
using System.Net;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-090 (BL-3619): once a transfer leaves its connection for another request, a followed
/// redirect's hop or a fresh connect after a reused connection died, the connection it opens next takes
/// the next <c>%{conn_id}</c>, as curl 8.22.0 printed <c>2</c> for <c>-w '%{conn_id}'</c> across three
/// hops on closed connections and marked the second hop's <c>--trace-ids</c> lines <c>[0-1]</c>.
/// </summary>
[TestClass]
public sealed class Cgr090Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportConnectionOpened_AfterIssueAnotherRequest_TakesTheNextConnId()
    {
        RunningTransferState state = NewState();
        long nextConnectionId = 0;
        ConcurrentDictionary<long, long> connectionIds = new();
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(NoTransferEvents.Instance, state, connectionIds, () => state.ConnectionId ??= nextConnectionId++);
        events.ReportConnectionOpened(Opened(connectionNumber: 0));
        Diagnostics.Arrange("first hop's conn_id", state.ConnectionId);

        events.ReportInfo(RedirectFollower.IssueAnotherRequestMessagePrefix + "http://h/b'");
        events.ReportConnectionOpened(Opened(connectionNumber: 1));

        Diagnostics.Act("second hop's conn_id", state.ConnectionId);
        Diagnostics.Assert("second hop's conn_id", 1L, state.ConnectionId);
        Diagnostics.Assert("remembered id for pool number 1", 1L, connectionIds[1]);
        Assert.AreEqual(1L, state.ConnectionId);
        Assert.AreEqual(0L, connectionIds[0]);
        Assert.AreEqual(1L, connectionIds[1]);
    }

    [TestMethod]
    public void ReportInfo_IssueAnotherRequest_TheLineAfterItTakesTheNextConnId()
    {
        RunningTransferState state = NewState();
        long nextConnectionId = 0;
        Func<long> takeConnectionId = () => state.ConnectionId ??= nextConnectionId++;
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(NoTransferEvents.Instance, state, new ConcurrentDictionary<long, long>(), takeConnectionId);
        events.ReportConnectionOpened(Opened(connectionNumber: 0));
        Diagnostics.Arrange("first hop's conn_id", state.ConnectionId);

        events.ReportInfo(RedirectFollower.IssueAnotherRequestMessagePrefix + "http://h/close'");
        long nextLineConnectionId = takeConnectionId();

        Diagnostics.Act("conn_id of the line after it", nextLineConnectionId);
        Diagnostics.Assert("conn_id of the line after it", 1L, nextLineConnectionId);
        Assert.AreEqual(1L, nextLineConnectionId);
    }

    [TestMethod]
    public void ReportInfo_AnyOtherLine_KeepsTheConnId()
    {
        RunningTransferState state = NewState();
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(NoTransferEvents.Instance, state, new ConcurrentDictionary<long, long>(), () => state.ConnectionId ??= 3);
        events.ReportConnectionOpened(Opened(connectionNumber: 0));
        Diagnostics.Arrange("conn_id", state.ConnectionId);

        events.ReportInfo("shutting down connection #0");

        Diagnostics.Act("conn_id", state.ConnectionId);
        Diagnostics.Assert("conn_id", 3L, state.ConnectionId);
        Assert.AreEqual(3L, state.ConnectionId);
    }

    private static ConnectionOpenedEvent Opened(long connectionNumber) => new()
    {
        HostName = "h",
        RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80),
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
        IsSecondConnection = false,
        ConnectionNumber = connectionNumber,
    };

    private static RunningTransferState NewState()
    {
        StandardOutputFailureDeferringStream standardOutput = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, standardOutput, standardOutput);
    }
}
