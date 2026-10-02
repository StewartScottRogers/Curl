using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ClientReaderResetTraceEvents" />: curl 8.21.0's <c>[READ] client_reset, clear readers</c>
/// comes before a finished transfer's <c>left intact</c> or <c>shutting down connection</c> line and not
/// before a failed one's <c>closing connection</c>, and every event goes on unchanged (measured, BL-1159 Notes).
/// </summary>
[TestClass]
public sealed class ClientReaderResetTraceEventsTests
{
    [TestMethod]
    [DataRow("Connection #0 to host 127.0.0.1:47811 left intact")]
    [DataRow("shutting down connection #0")]
    public void ReportInfo_TheConnectionsLastLineAfterASuccess_FollowsTheResetLine(string line)
    {
        CallRecordingEvents inner = new();

        new ClientReaderResetTraceEvents(inner).ReportInfo(line);

        CollectionAssert.AreEqual(new[] { "Info [READ] client_reset, clear readers", $"Info {line}" }, inner.Calls);
    }

    [TestMethod]
    [DataRow("closing connection #0")]
    [DataRow("Connection #0 to host 127.0.0.1:47811 was reset")]
    [DataRow("Request completely sent off")]
    public void ReportInfo_AnyOtherLine_IsPassedOnAlone(string line)
    {
        CallRecordingEvents inner = new();

        new ClientReaderResetTraceEvents(inner).ReportInfo(line);

        CollectionAssert.AreEqual(new[] { $"Info {line}" }, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherEvent_GoesOnToTheInnerEvents()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new ClientReaderResetTraceEvents(inner);

        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
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

        CollectionAssert.AreEqual(
            new[]
            {
                "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust",
                "VerifyResult 18 False", "EarlyData -7", "RequestHeader 2", "ResponseHeader 3", "DataSent 4", "DataReceived 5",
            },
            inner.Calls);
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
