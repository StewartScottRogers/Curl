using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ClientWriterTraceEvents" />: curl 8.21.0's <c>[WRITE]</c> lines after each response header
/// line and body block, and <c>[WRITE] [OUT] done</c> before a finished transfer's last connection line, with
/// every event passed on unchanged (measured, BL-1187 Notes).
/// </summary>
[TestClass]
public sealed class ClientWriterTraceEventsTests
{
    [TestMethod]
    public void TheMeasuredTwoByteResponse_WritesCurlsWriteLines()
    {
        // curl 8.21.0, -s -v --trace-config write, a 200 with Content-Length: 2 and "hi" (measured 2026-10-02, BL-1187 Notes).
        CallRecordingEvents inner = new();
        ClientWriterTraceEvents events = new(inner);

        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportResponseHeader("Content-Length: 2\r\n"u8);
        events.ReportResponseHeader("\r\n"u8);
        events.ReportDataReceived("hi"u8);
        events.ReportInfo("Connection #0 to host 127.0.0.1:47813 left intact");

        CollectionAssert.AreEqual(
            new[]
            {
                "ResponseHeader 17",
                "Info [WRITE] [OUT] wrote 17 header bytes -> 17",
                "Info [WRITE] [PAUSE] writing 17/17 bytes of type c -> 0",
                "Info [WRITE] download_write header(type=c, blen=17) -> 0",
                "Info [WRITE] client_write(type=c, len=17) -> 0",
                "ResponseHeader 19",
                "Info [WRITE] header_collect pushed(type=1, len=19) -> 0",
                "Info [WRITE] [OUT] wrote 19 header bytes -> 19",
                "Info [WRITE] [PAUSE] writing 19/19 bytes of type 4 -> 0",
                "Info [WRITE] download_write header(type=4, blen=19) -> 0",
                "Info [WRITE] client_write(type=4, len=19) -> 0",
                "ResponseHeader 2",
                "Info [WRITE] header_collect pushed(type=1, len=2) -> 0",
                "Info [WRITE] [OUT] wrote 2 header bytes -> 2",
                "Info [WRITE] [PAUSE] writing 2/2 bytes of type 4 -> 0",
                "Info [WRITE] download_write header(type=4, blen=2) -> 0",
                "Info [WRITE] client_write(type=4, len=2) -> 0",
                "DataReceived 2",
                "Info [WRITE] [OUT] wrote 2 body bytes -> 2",
                "Info [WRITE] [PAUSE] writing 2/2 bytes of type 1 -> 0",
                "Info [WRITE] download_write body(type=1, blen=2) -> 0",
                "Info [WRITE] client_write(type=1, len=2) -> 0",
                "Info [WRITE] xfer_write_resp(len=40, eos=0) -> 0",
                "Info [WRITE] [OUT] done",
                "Info Connection #0 to host 127.0.0.1:47813 left intact",
            },
            inner.Calls);
    }

    [TestMethod]
    public void AResponseWithNoBody_WritesItsHeadsXferWriteRespBeforeDone()
    {
        // curl 8.21.0 wrote xfer_write_resp(len=38) then [OUT] done for Content-Length: 0 (measured 2026-10-02, BL-1187 Notes).
        CallRecordingEvents inner = new();
        ClientWriterTraceEvents events = new(inner);

        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportResponseHeader("Content-Length: 0\r\n"u8);
        events.ReportResponseHeader("\r\n"u8);
        events.ReportInfo("shutting down connection #0");

        CollectionAssert.AreEqual(
            new[] { "Info [WRITE] xfer_write_resp(len=38, eos=0) -> 0", "Info [WRITE] [OUT] done", "Info shutting down connection #0" },
            inner.Calls.Skip(inner.Calls.Count - 3).ToArray());
    }

    [TestMethod]
    public void ABodyBlockAfterTheFirst_CountsOnlyItsOwnBytes()
    {
        CallRecordingEvents inner = new();
        ClientWriterTraceEvents events = new(inner);

        events.ReportResponseHeader("\r\n"u8);
        events.ReportDataReceived("ab"u8);
        events.ReportDataReceived("cde"u8);

        Assert.AreEqual("Info [WRITE] xfer_write_resp(len=4, eos=0) -> 0", inner.Calls[10]);
        Assert.AreEqual("Info [WRITE] xfer_write_resp(len=3, eos=0) -> 0", inner.Calls[^1]);
    }

    [TestMethod]
    public void TheHeaderAfterABlankLine_IsTheNextResponsesStatusLine()
    {
        CallRecordingEvents inner = new();
        ClientWriterTraceEvents events = new(inner);

        events.ReportResponseHeader("HTTP/1.1 100 Continue\r\n"u8);
        events.ReportResponseHeader("\r\n"u8);
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);

        CollectionAssert.AreEqual(
            new[] { "ResponseHeader 17", "Info [WRITE] [OUT] wrote 17 header bytes -> 17" },
            inner.Calls.Skip(11).Take(2).ToArray());
    }

    [TestMethod]
    public void TheNextTransfersStatusLine_IsTypeCAgainAfterDone()
    {
        CallRecordingEvents inner = new();
        ClientWriterTraceEvents events = new(inner);

        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportInfo("Connection #0 to host 127.0.0.1:1 left intact");
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);

        Assert.AreEqual("Info [WRITE] client_write(type=c, len=17) -> 0", inner.Calls[^1]);
    }

    [TestMethod]
    [DataRow("Connection #0 to host 127.0.0.1:47813 left intact")]
    [DataRow("shutting down connection #0")]
    public void TheConnectionsLastLineBeforeAnyResponse_IsPassedOnAlone(string line)
    {
        CallRecordingEvents inner = new();

        new ClientWriterTraceEvents(inner).ReportInfo(line);

        CollectionAssert.AreEqual(new[] { $"Info {line}" }, inner.Calls);
    }

    [TestMethod]
    [DataRow("closing connection #0")]
    [DataRow("Connection #0 to host 127.0.0.1:47813 was reset")]
    public void AnyOtherLineAfterTheResponse_WritesNoDoneLine(string line)
    {
        CallRecordingEvents inner = new();
        ClientWriterTraceEvents events = new(inner);
        events.ReportDataReceived("hi"u8);

        events.ReportInfo(line);

        Assert.AreEqual($"Info {line}", inner.Calls[^1]);
        Assert.AreEqual("Info [WRITE] xfer_write_resp(len=2, eos=0) -> 0", inner.Calls[^2]);
    }

    [TestMethod]
    public void EveryOtherEvent_GoesOnToTheInnerEvents()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new ClientWriterTraceEvents(inner);

        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(18, isProxy: false);
        events.ReportTlsEarlyData(-7);
        events.ReportRequestHeader([2]);
        events.ReportDataSent([4, 5]);

        CollectionAssert.AreEqual(
            new[]
            {
                "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust",
                "VerifyResult 18 False", "EarlyData -7", "RequestHeader 1", "DataSent 2",
            },
            inner.Calls);
    }

    /// <summary>Records each event it is given as one line naming it and its payload's length.</summary>
    private sealed class CallRecordingEvents : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public void ReportInfo(string text) => Calls.Add($"Info {text}");

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add("Opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add("Reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add("Handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add($"TlsData {bytes.Length} {sent}");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("TlsMessage");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("TlsTrust");

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Calls.Add($"VerifyResult {verifyResult} {isProxy}");

        public void ReportTlsEarlyData(long bytes) => Calls.Add($"EarlyData {bytes}");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"RequestHeader {bytes.Length}");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ResponseHeader {bytes.Length}");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add($"DataSent {bytes.Length}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add($"DataReceived {bytes.Length}");
    }
}
