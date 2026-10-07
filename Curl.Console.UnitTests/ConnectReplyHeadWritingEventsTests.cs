using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ConnectReplyHeadWritingEvents" />: a CONNECT reply head goes to the header
/// output byte for byte, and every other event goes on to the transfer's own events unchanged.
/// </summary>
[TestClass]
public sealed class ConnectReplyHeadWritingEventsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteConnectReplyHeadAsync_WritesTheHeadToTheHeaderOutput()
    {
        using MemoryStream headerOutput = new();
        CallRecordingEvents inner = new();
        ConnectReplyHeadWritingEvents events = new(inner, headerOutput);
        Diagnostics.Arrange("reply head", "HTTP/1.1 200 OK, blank line");

        await events.WriteConnectReplyHeadAsync(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\n"), CancellationToken.None);

        Diagnostics.Act("header output length", headerOutput.Length);
        Diagnostics.Bytes("header output", headerOutput.ToArray());
        Diagnostics.Assert("header output length", 19, headerOutput.Length);
        Diagnostics.Assert("inner calls", 0, inner.Calls.Count);
        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\n", Encoding.Latin1.GetString(headerOutput.ToArray()));
        Assert.IsEmpty(inner.Calls);
    }

    [TestMethod]
    public void EveryOtherEvent_GoesOnToTheInnerEvents()
    {
        using MemoryStream headerOutput = new();
        CallRecordingEvents inner = new();
        ITransferEvents events = new ConnectReplyHeadWritingEvents(inner, headerOutput);
        Diagnostics.Arrange("events to report", 13);

        events.ReportInfo("text");
        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(18, isProxy: true);
        events.ReportTlsEarlyData(-7);
        events.ReportRequestHeader([2]);
        events.ReportResponseHeader([3]);
        events.ReportDataSent([4]);
        events.ReportDataReceived([5]);

        Diagnostics.Act("inner calls", string.Join(", ", inner.Calls));
        Diagnostics.Assert("inner call count", 13, inner.Calls.Count);
        Diagnostics.Assert("header output length", 0L, headerOutput.Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "Info text", "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust", "VerifyResult 18 True", "EarlyData -7",
                "RequestHeader 2", "ResponseHeader 3", "DataSent 4", "DataReceived 5",
            },
            inner.Calls);
        Assert.AreEqual(0, headerOutput.Length);
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
