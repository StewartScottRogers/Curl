using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ClientReaderResetTraceEvents" />: curl 8.21.0's <c>[READ] client_reset, clear readers</c>
/// comes before a finished transfer's <c>left intact</c> or <c>shutting down connection</c> line and not
/// before a failed one's <c>closing connection</c>, and every event goes on unchanged (measured, BL-1159 Notes).
/// </summary>
[TestClass]
public sealed class ClientReaderResetTraceEventsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Connection #0 to host 127.0.0.1:47811 left intact")]
    [DataRow("shutting down connection #0")]
    public void ReportInfo_TheConnectionsLastLineAfterASuccess_FollowsTheResetLine(string line)
    {
        CallRecordingEvents inner = new();

        Diagnostics.Arrange("info line", line);
        new ClientReaderResetTraceEvents(inner).ReportInfo(line);
        Diagnostics.Act("call count", inner.Calls.Count);
        Diagnostics.Assert("call count", 2, inner.Calls.Count);

        CollectionAssert.AreEqual(new[] { "Info [READ] client_reset, clear readers", $"Info {line}" }, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_TheRedirectsIssueAnotherRequestLine_IsFollowedByTheResetLine()
    {
        // curl -v --trace-config read -L on a 302 to /b: the next hop's reset comes straight after (BL-1189 Notes).
        CallRecordingEvents inner = new();

        Diagnostics.Arrange("info line", "Issue another request to this URL: 'http://127.0.0.1:47811/b'");
        new ClientReaderResetTraceEvents(inner).ReportInfo("Issue another request to this URL: 'http://127.0.0.1:47811/b'");
        Diagnostics.Act("call count", inner.Calls.Count);
        Diagnostics.Assert("call count", 2, inner.Calls.Count);

        CollectionAssert.AreEqual(
            new[] { "Info Issue another request to this URL: 'http://127.0.0.1:47811/b'", "Info [READ] client_reset, clear readers" },
            inner.Calls);
    }

    [TestMethod]
    [DataRow("shutting down connection #0")]
    [DataRow("Connection #0 to host 127.0.0.1:47811 left intact")]
    public void ReportInfo_ARedirectHopThatNeedsItsBodyRewound_WritesCurlsRewindLines(string connectionsLastLine)
    {
        // curl -v --trace-config read -d ab -L on a 302 or 307 to /b, closing or kept alive (BL-1213 Notes).
        CallRecordingEvents inner = new();
        ClientReaderResetTraceEvents events = new(inner);

        Diagnostics.Arrange("connection last line", connectionsLastLine);
        events.ReportInfo("Need to rewind upload for next request");
        events.ReportInfo(connectionsLastLine);
        events.ReportInfo("Issue another request to this URL: 'http://127.0.0.1:47811/b'");
        events.ReportInfo("Connection #1 to host 127.0.0.1:47811 left intact");
        Diagnostics.Act("call count", inner.Calls.Count);
        Diagnostics.Assert("call count", 9, inner.Calls.Count);

        CollectionAssert.AreEqual(
            new[]
            {
                "Info [READ] client reader needs rewind before next request",
                "Info Need to rewind upload for next request",
                "Info [READ] client_reset, will rewind reader",
                $"Info {connectionsLastLine}",
                "Info [READ] client start, rewind readers",
                "Info Issue another request to this URL: 'http://127.0.0.1:47811/b'",
                "Info [READ] client_reset, clear readers",
                "Info [READ] client_reset, clear readers",
                "Info Connection #1 to host 127.0.0.1:47811 left intact",
            },
            inner.Calls);
    }

    [TestMethod]
    [DataRow("closing connection #0")]
    [DataRow("Connection #0 to host 127.0.0.1:47811 was reset")]
    [DataRow("Request completely sent off")]
    public void ReportInfo_AnyOtherLine_IsPassedOnAlone(string line)
    {
        CallRecordingEvents inner = new();

        Diagnostics.Arrange("info line", line);
        new ClientReaderResetTraceEvents(inner).ReportInfo(line);
        Diagnostics.Act("call count", inner.Calls.Count);
        Diagnostics.Assert("call count", 1, inner.Calls.Count);

        CollectionAssert.AreEqual(new[] { $"Info {line}" }, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherEvent_GoesOnToTheInnerEvents()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new ClientReaderResetTraceEvents(inner);
        Diagnostics.Arrange("event kinds", 12);

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
        Diagnostics.Act("call count", inner.Calls.Count);
        Diagnostics.Assert("call count", 12, inner.Calls.Count);

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
