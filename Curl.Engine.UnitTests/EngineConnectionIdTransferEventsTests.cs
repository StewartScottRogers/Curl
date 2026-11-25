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
        RecordingEvents inner = new();
        EngineConnectionIdTransferEvents events = new(inner, new EngineTransferState(), new ConcurrentDictionary<long, long>(), () => 0, _ => { });
        ConnectionOpenedEvent opened = Opened(1, isSecond: false);
        ConnectionReusedEvent reused = new() { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 1 };
        TlsHandshakeEvent handshake = new() { ProtocolVersion = SslProtocols.Tls13, CipherSuite = null, NegotiatedApplicationProtocol = null, OfferedApplicationProtocols = [], ServerCertificate = null, CertificateVerified = false };

        events.ReportConnectionOpened(opened);
        events.ReportConnectionReused(reused);
        events.ReportCertificateVerifyResult(11, isProxy: true);
        events.ReportTlsEarlyData(12);
        events.ReportInfo("i");
        events.ReportTlsHandshake(handshake);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportRequestHeader([2]);
        events.ReportResponseHeader([3]);
        events.ReportDataSent([4]);
        events.ReportDataReceived([5]);

        Assert.HasCount(13, inner.Calls);
        Assert.AreSame(opened, inner.Calls[0]);
        Assert.AreSame(reused, inner.Calls[1]);
        string[] expectedRest =
        [
            "ReportCertificateVerifyResult 11 True",
            "ReportTlsEarlyData 12",
            "ReportInfo i",
        ];
        CollectionAssert.AreEqual(expectedRest, inner.Calls.Skip(2).Take(3).ToArray());
        Assert.AreSame(handshake, inner.Calls[5]);
        string[] expectedTail =
        [
            "ReportTlsData 01 True",
            "ReportTlsMessage",
            "ReportTlsTrust",
            "ReportRequestHeader 02",
            "ReportResponseHeader 03",
            "ReportDataSent 04",
            "ReportDataReceived 05",
        ];
        CollectionAssert.AreEqual(expectedTail, inner.Calls.Skip(6).ToArray());
    }

    private static ConnectionOpenedEvent Opened(long connectionNumber, bool isSecond) => new()
    {
        HostName = "h",
        RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80),
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
        IsSecondConnection = isSecond,
        ConnectionNumber = connectionNumber,
    };

    /// <summary>Records every event it is given, by method name and argument, in order.</summary>
    private sealed class RecordingEvents : ITransferEvents
    {
        public List<object> Calls { get; } = [];

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add(opened);

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add(reused);

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Calls.Add($"ReportCertificateVerifyResult {verifyResult} {isProxy}");

        public void ReportTlsEarlyData(long bytes) => Calls.Add($"ReportTlsEarlyData {bytes}");

        public void ReportInfo(string text) => Calls.Add($"ReportInfo {text}");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add(handshake);

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add($"ReportTlsData {Convert.ToHexString(bytes)} {sent}");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("ReportTlsMessage");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("ReportTlsTrust");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ReportRequestHeader {Convert.ToHexString(bytes)}");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ReportResponseHeader {Convert.ToHexString(bytes)}");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add($"ReportDataSent {Convert.ToHexString(bytes)}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add($"ReportDataReceived {Convert.ToHexString(bytes)}");
    }
}