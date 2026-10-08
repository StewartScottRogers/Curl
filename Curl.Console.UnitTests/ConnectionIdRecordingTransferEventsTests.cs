using System.Collections.Concurrent;
using System.Net;
using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ConnectionIdRecordingTransferEvents" />: a connection the transfer opens takes its
/// <c>%{conn_id}</c> and is remembered under the pool's number, a reused connection gives the transfer
/// that remembered <c>%{conn_id}</c> or else the pool's number, and every event goes on to the
/// transfer's own events unchanged (BL-1052).
/// </summary>
[TestClass]
public sealed class ConnectionIdRecordingTransferEventsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportConnectionOpened_FirstConnection_TakesTheConnIdAndRemembersItUnderThePoolsNumber()
    {
        RunningTransferState state = NewState();
        ConcurrentDictionary<long, long> connectionIds = new();
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(NoTransferEvents.Instance, state, connectionIds, () => state.ConnectionId ??= 4);

        Diagnostics.Arrange("connection number, is second connection", "7, False");

        events.ReportConnectionOpened(Opened(connectionNumber: 7, isSecondConnection: false));

        Diagnostics.Act("state connection id", state.ConnectionId);
        Diagnostics.Assert("state connection id", 4L, state.ConnectionId);
        Diagnostics.Assert("remembered id for pool number 7", 4L, connectionIds[7]);
        Assert.AreEqual(4L, state.ConnectionId);
        Assert.AreEqual(4L, connectionIds[7]);
    }

    [TestMethod]
    public void ReportConnectionOpened_SecondConnection_TakesNoConnId()
    {
        RunningTransferState state = NewState();
        ConcurrentDictionary<long, long> connectionIds = new();
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(NoTransferEvents.Instance, state, connectionIds, () => state.ConnectionId ??= 4);

        Diagnostics.Arrange("connection number, is second connection", "8, True");

        events.ReportConnectionOpened(Opened(connectionNumber: 8, isSecondConnection: true));

        Diagnostics.Act("state connection id", state.ConnectionId);
        Diagnostics.Assert("state connection id", null, state.ConnectionId);
        Diagnostics.Assert("remembered ids", 0, connectionIds.Count);
        Assert.IsNull(state.ConnectionId);
        Assert.IsEmpty(connectionIds);
    }

    [TestMethod]
    public void ReportConnectionReused_RememberedConnection_TakesItsConnId()
    {
        RunningTransferState state = NewState();
        ConcurrentDictionary<long, long> connectionIds = new() { [7] = 2 };
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(NoTransferEvents.Instance, state, connectionIds, () => 9);

        Diagnostics.Arrange("reused pool number, remembered id", "7, 2");

        events.ReportConnectionReused(Reused(connectionNumber: 7));

        Diagnostics.Act("state connection id", state.ConnectionId);
        Diagnostics.Assert("state connection id", 2L, state.ConnectionId);
        Assert.AreEqual(2L, state.ConnectionId);
    }

    [TestMethod]
    public void ReportConnectionReused_ConnectionNotSeenOpening_TakesThePoolsNumber()
    {
        RunningTransferState state = NewState();
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(NoTransferEvents.Instance, state, new ConcurrentDictionary<long, long>(), () => 9);

        Diagnostics.Arrange("reused pool number, remembered id", "3, none");

        events.ReportConnectionReused(Reused(connectionNumber: 3));

        Diagnostics.Act("state connection id", state.ConnectionId);
        Diagnostics.Assert("state connection id", 3L, state.ConnectionId);
        Assert.AreEqual(3L, state.ConnectionId);
    }

    [TestMethod]
    public void EveryEvent_GoesOnToTheInnerEvents()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new ConnectionIdRecordingTransferEvents(inner, NewState(), new ConcurrentDictionary<long, long>(), () => 0);
        Diagnostics.Arrange("events to report", 13);

        events.ReportInfo("text");
        events.ReportConnectionOpened(Opened(connectionNumber: 0, isSecondConnection: false));
        events.ReportConnectionReused(Reused(connectionNumber: 0));
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(18, isProxy: false);
        events.ReportTlsEarlyData(-7);
        events.ReportRequestHeader([2]);
        events.ReportResponseHeader([3]);
        events.ReportDataSent([4]);
        events.ReportDataReceived([5]);

        Diagnostics.Act("inner calls", string.Join(", ", inner.Calls));
        Diagnostics.Assert("inner call count", 13, inner.Calls.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                "Info text", "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust",
                "VerifyResult 18 False", "EarlyData -7", "RequestHeader 2", "ResponseHeader 3", "DataSent 4", "DataReceived 5",
            },
            inner.Calls);
    }

    private static ConnectionOpenedEvent Opened(long connectionNumber, bool isSecondConnection) => new()
    {
        HostName = "h",
        RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80),
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
        IsSecondConnection = isSecondConnection,
        ConnectionNumber = connectionNumber,
    };

    private static ConnectionReusedEvent Reused(long connectionNumber) => new()
    {
        Scheme = "http",
        IsProxy = false,
        HostName = "h",
        Port = 80,
        ConnectionNumber = connectionNumber,
    };

    private static RunningTransferState NewState()
    {
        StandardOutputFailureDeferringStream standardOutput = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, standardOutput, standardOutput);
    }

    /// <summary>Records each event it is given as one line naming it and its payload.</summary>
    private sealed class CallRecordingEvents : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public void ReportInfo(string text) => Calls.Add($"Info {text}");

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add("Opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add("Reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add("Handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add($"TlsData {bytes[0]} {sent}");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("TlsMessage");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("TlsTrust");

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Calls.Add($"VerifyResult {verifyResult} {isProxy}");

        public void ReportTlsEarlyData(long bytes) => Calls.Add($"EarlyData {bytes}");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"RequestHeader {bytes[0]}");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ResponseHeader {bytes[0]}");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add($"DataSent {bytes[0]}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add($"DataReceived {bytes[0]}");
    }
}
