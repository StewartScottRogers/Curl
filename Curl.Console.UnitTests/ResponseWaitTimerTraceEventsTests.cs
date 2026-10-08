using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ResponseWaitTimerTraceEvents" />: the response wait's <c>[TIMER]</c> lines follow each
/// <c>Request completely sent off</c> line, and every event goes on unchanged (measured, BL-1258 Notes).
/// </summary>
[TestClass]
public sealed class ResponseWaitTimerTraceEventsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportInfo_TheRequestSentLine_IsFollowedByTheWaitLines()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new ResponseWaitTimerTraceEvents(inner, ["[TIMER] [TIMEOUT] gives multi timeout in 5000ms"]);
        Diagnostics.Arrange("wait lines", "[TIMER] [TIMEOUT] gives multi timeout in 5000ms");
        Diagnostics.Arrange("info lines reported", $"using HTTP/1.x | {ResponseWaitTimerTraceEvents.RequestSentLine}");

        events.ReportInfo("using HTTP/1.x");
        events.ReportInfo(ResponseWaitTimerTraceEvents.RequestSentLine);
        Diagnostics.Act("inner calls", string.Join(" | ", inner.Calls));

        string[] expected = ["Info using HTTP/1.x", "Info Request completely sent off", "Info [TIMER] [TIMEOUT] gives multi timeout in 5000ms"];
        Diagnostics.Assert("inner calls", string.Join(" | ", expected), string.Join(" | ", inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherEvent_GoesOnToTheInnerEvents()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new ResponseWaitTimerTraceEvents(inner, []);
        Diagnostics.Arrange("wait lines", "none");
        Diagnostics.Arrange("events reported", "every event but info, each once");

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
        Diagnostics.Act("inner calls", string.Join(" | ", inner.Calls));

        string[] expected =
        [
            "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust",
            "VerifyResult 18 False", "EarlyData -7", "RequestHeader 2", "ResponseHeader 3", "DataSent 4", "DataReceived 5",
        ];
        Diagnostics.Assert("inner calls", string.Join(" | ", expected), string.Join(" | ", inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
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
