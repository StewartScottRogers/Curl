using System.Collections.Concurrent;
using System.Net;
using System.Security.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="EngineConnectionIdTransferEvents" />: a connection the transfer opens takes its
/// <c>%{conn_id}</c> under the pool's number, a reuse takes the opener's (or the pool's number for a
/// connection no transfer saw open), a second connection takes none, and every event goes on unchanged.
/// </summary>
[TestClass]
public sealed class EngineConnectionIdTransferEventsTests
{
    [TestMethod]
    public void ReportConnectionOpened_NewConnection_RemembersTheTakenIdUnderThePoolNumberAndTellsIt()
    {
        EngineTransferState state = new();
        ConcurrentDictionary<long, long> connectionIds = new();
        List<long> told = [];
        EngineConnectionIdTransferEvents events = new(NoTransferEvents.Instance, state, connectionIds, () => state.ConnectionId ??= 4, told.Add);

        events.ReportConnectionOpened(Opened(7, isSecond: false));

        Assert.AreEqual(4, connectionIds[7]);
        Assert.AreEqual(4, state.ConnectionId);
        CollectionAssert.AreEqual(new long[] { 4 }, told);
    }

    [TestMethod]
    public void ReportConnectionOpened_SecondConnection_TakesNoId()
    {
        EngineTransferState state = new();
        ConcurrentDictionary<long, long> connectionIds = new();
        EngineConnectionIdTransferEvents events = new(NoTransferEvents.Instance, state, connectionIds, () => throw new AssertFailedException("taken"), _ => throw new AssertFailedException("told"));

        events.ReportConnectionOpened(Opened(7, isSecond: true));

        Assert.IsEmpty(connectionIds);
        Assert.IsNull(state.ConnectionId);
    }

    [TestMethod]
    [DataRow(7L, 4L)]
    [DataRow(9L, 9L)]
    public void ReportConnectionReused_PoolNumber_TakesAndTellsTheOpenersIdOrThePoolNumber(long connectionNumber, long expected)
    {
        EngineTransferState state = new();
        ConcurrentDictionary<long, long> connectionIds = new() { [7] = 4 };
        List<long> told = [];
        EngineConnectionIdTransferEvents events = new(NoTransferEvents.Instance, state, connectionIds, () => 0, told.Add);

        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = connectionNumber });

        Assert.AreEqual(expected, state.ConnectionId);
        CollectionAssert.AreEqual(new[] { expected }, told);
    }

    [TestMethod]
    public void EveryEvent_GoesOnToTheInnerEvents()
    {
        CountingEvents inner = new();
        EngineConnectionIdTransferEvents events = new(inner, new EngineTransferState(), new ConcurrentDictionary<long, long>(), () => 0, _ => { });

        events.ReportConnectionOpened(Opened(1, isSecond: false));
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 1 });
        events.ReportCertificateVerifyResult(0, isProxy: false);
        events.ReportTlsEarlyData(0);
        events.ReportInfo("i");
        events.ReportTlsHandshake(new TlsHandshakeEvent { ProtocolVersion = SslProtocols.Tls13, CipherSuite = null, NegotiatedApplicationProtocol = null, OfferedApplicationProtocols = [], ServerCertificate = null, CertificateVerified = false });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportRequestHeader([1]);
        events.ReportResponseHeader([1]);
        events.ReportDataSent([1]);
        events.ReportDataReceived([1]);

        Assert.AreEqual(13, inner.Count);
    }

    private static ConnectionOpenedEvent Opened(long connectionNumber, bool isSecond) => new()
    {
        HostName = "h",
        RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80),
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
        IsSecondConnection = isSecond,
        ConnectionNumber = connectionNumber,
    };

    /// <summary>Counts every event it is given.</summary>
    private sealed class CountingEvents : ITransferEvents
    {
        public int Count { get; private set; }

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Count++;

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Count++;

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Count++;

        public void ReportTlsEarlyData(long bytes) => Count++;

        public void ReportInfo(string text) => Count++;

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Count++;

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Count++;

        public void ReportTlsMessage(TlsMessageEvent message) => Count++;

        public void ReportTlsTrust(TlsTrustEvent trust) => Count++;

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Count++;
    }
}
