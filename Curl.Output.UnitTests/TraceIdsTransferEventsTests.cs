using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="TraceIdsTransferEvents"/> and <see cref="TraceIdsPrefix"/>, and the
/// <c>--trace-ids</c> marker the <c>-v</c> and trace writers write, to what curl 8.21.0 (mingw,
/// Schannel) wrote on 2026-09-29; the commands and output are in BL-648's Notes.
/// </summary>
[TestClass]
public sealed class TraceIdsTransferEventsTests
{
    private readonly MemoryStream output = new();

    public TestContext TestContext { get; set; } = null!;

    private static void Check(TestDiagnostics diagnostics, string expected, string actual)
    {
        diagnostics.Act("rendered", actual);
        diagnostics.Diff("rendered", expected, actual);
        diagnostics.Assert("rendered", expected, actual);
    }

    [TestMethod]
    public void Prefix_New_IsTransferZeroWithNoConnection()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("prefix", "new TraceIdsPrefix()");
        string text = new TraceIdsPrefix().Text;
        Check(diagnostics, "[0-x] ", text);

        Assert.AreEqual("[0-x] ", text);
    }

    [TestMethod]
    public void EveryEvent_SetsTheMarkerToTheTransfersIdsBeforeForwarding()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TraceIdsPrefix prefix = new();
        MarkerRecordingEvents recorder = new(prefix);
        long? connectionId = null;
        TraceIdsTransferEvents events = new(recorder, prefix, 7, () => connectionId);

        diagnostics.Arrange("transfer id and connection id", "7, then 3 after the first event");
        events.ReportInfo("info");
        connectionId = 3;
        events.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = "h",
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 1),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 2),
            ConnectionNumber = 3,
        });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 1, ConnectionNumber = 3 });
        events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = false,
        });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0304, ContentType = TlsContentType.Handshake, Sent = false, Bytes = new byte[] { 1 } });
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true });
        events.ReportRequestHeader("GET"u8);
        events.ReportResponseHeader("HTTP"u8);
        events.ReportDataSent([1]);
        events.ReportDataReceived([2]);

        diagnostics.Act("calls", string.Join(" | ", recorder.Calls));
        diagnostics.Assert("call count", 11, recorder.Calls.Count);
        diagnostics.Assert("first call", "ReportInfo [7-x] ", recorder.Calls[0]);
        diagnostics.Assert("last call", "ReportDataReceived [7-3] ", recorder.Calls[^1]);

        CollectionAssert.AreEqual(
            new[]
            {
                "ReportInfo [7-x] ",
                "ReportConnectionOpened [7-3] ",
                "ReportConnectionReused [7-3] ",
                "ReportTlsHandshake [7-3] ",
                "ReportTlsData [7-3] ",
                "ReportTlsMessage [7-3] ",
                "ReportTlsTrust [7-3] ",
                "ReportRequestHeader [7-3] ",
                "ReportResponseHeader [7-3] ",
                "ReportDataSent [7-3] ",
                "ReportDataReceived [7-3] ",
            },
            recorder.Calls);
    }

    [TestMethod]
    public void VerboseWriter_TwoTransfers_MarksEachLineThatStartsAnEvent()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TraceIdsPrefix prefix = new();
        VerboseTransferEventWriter writer = new(output, true, false, TimeProvider.System, TlsBackend.Schannel, prefix);
        TraceIdsTransferEvents first = new(writer, prefix, 0, () => 0);
        TraceIdsTransferEvents second = new(writer, prefix, 1, () => 1);

        first.ReportInfo("using HTTP/1.x");
        first.ReportRequestHeader("GET /a HTTP/1.1\r\nHost: h\r\n\r\n"u8);
        first.ReportResponseHeader("HTTP/1.1 200"u8);
        first.ReportResponseHeader(" OK\r\n"u8);
        first.ReportDataReceived("hi"u8);
        second.ReportInfo("shutting down connection #1");
        diagnostics.Arrange("transfers", "0 on connection 0, 1 on connection 1");
        string rendered = Encoding.ASCII.GetString(output.ToArray());
        string expected = "[0-0] * using HTTP/1.x\n"
            + "[0-0] > GET /a HTTP/1.1\r\n"
            + "[0-0] > Host: h\r\n"
            + "[0-0] > \r\n"
            + "[0-0] < HTTP/1.1 200 OK\r\n"
            + "[0-0] { [2 bytes data]\n"
            + "[1-1] * shutting down connection #1\n";
        Check(diagnostics, expected, rendered);

        Assert.AreEqual(
            "[0-0] * using HTTP/1.x\n"
            + "[0-0] > GET /a HTTP/1.1\r\n"
            + "[0-0] > Host: h\r\n"
            + "[0-0] > \r\n"
            + "[0-0] < HTTP/1.1 200 OK\r\n"
            + "[0-0] { [2 bytes data]\n"
            + "[1-1] * shutting down connection #1\n",
            rendered);
    }

    [TestMethod]
    public void VerboseWriter_WithTraceTime_WritesTheStampThenTheMarker()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TraceIdsPrefix prefix = new();
        // As curl 8.21.0 wrote -v --trace-ids --trace-time: "04:09:46.314000 [0-0] * Request completely sent off".
        TimeProvider clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 4, 9, 46, 314, TimeSpan.Zero), TimeZoneInfo.Utc);
        VerboseTransferEventWriter writer = new(output, true, true, clock, TlsBackend.Schannel, prefix);

        diagnostics.Arrange("clock", "2026-09-29T04:09:46.314Z");
        new TraceIdsTransferEvents(writer, prefix, 0, () => 0).ReportInfo("Request completely sent off");
        string rendered = Encoding.ASCII.GetString(output.ToArray());
        Check(diagnostics, "04:09:46.314000 [0-0] * Request completely sent off\n", rendered);

        Assert.AreEqual("04:09:46.314000 [0-0] * Request completely sent off\n", rendered);
    }

    [TestMethod]
    public void TraceWriter_MarksTitleAndInfoLinesButNotTheDumpedBytes()
    {
        // As curl 8.21.0 wrote --trace-ascii - --trace-ids: "[0-0] => Send header, 80 bytes (0x50)",
        // then "0000: GET /a HTTP/1.1" unmarked.
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TraceIdsPrefix prefix = new();
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, false, TimeProvider.System, TlsBackend.Schannel, prefix);
        TraceIdsTransferEvents events = new(writer, prefix, 0, () => 0);

        events.ReportInfo("using HTTP/1.x");
        events.ReportRequestHeader("GET /a HTTP/1.1\r\n\r\n"u8);
        diagnostics.Arrange("events", "info, then a 19 byte request header");
        string rendered = Encoding.ASCII.GetString(output.ToArray());
        Check(diagnostics, "[0-0] * using HTTP/1.x\n[0-0] => Send header, 19 bytes (0x13)\n0000: GET /a HTTP/1.1\n0011: \n", rendered);

        Assert.AreEqual(
            "[0-0] * using HTTP/1.x\n"
            + "[0-0] => Send header, 19 bytes (0x13)\n"
            + "0000: GET /a HTTP/1.1\n"
            + "0011: \n",
            rendered);
    }

    [TestMethod]
    public void Writers_WithoutPrefix_WriteNoMarker()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("writers", "trace and verbose, no prefix");
        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, false, TimeProvider.System, TlsBackend.Schannel).ReportInfo("a");
        new VerboseTransferEventWriter(output, true, false, TimeProvider.System, TlsBackend.Schannel).ReportInfo("b");

        string rendered = Encoding.ASCII.GetString(output.ToArray());
        Check(diagnostics, "* a\n* b\n", rendered);

        Assert.AreEqual("* a\n* b\n", rendered);
    }

    /// <summary>Records each call's name with the marker it would be written with.</summary>
    private sealed class MarkerRecordingEvents(TraceIdsPrefix prefix) : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public void ReportInfo(string text) => Record(nameof(ReportInfo));

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Record(nameof(ReportConnectionOpened));

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Record(nameof(ReportConnectionReused));

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Record(nameof(ReportTlsHandshake));

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Record(nameof(ReportTlsData));

        public void ReportTlsMessage(TlsMessageEvent message) => Record(nameof(ReportTlsMessage));

        public void ReportTlsTrust(TlsTrustEvent trust) => Record(nameof(ReportTlsTrust));

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Record(nameof(ReportRequestHeader));

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Record(nameof(ReportResponseHeader));

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Record(nameof(ReportDataSent));

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Record(nameof(ReportDataReceived));

        private void Record(string name) => Calls.Add(name + " " + prefix.Text);
    }
}
