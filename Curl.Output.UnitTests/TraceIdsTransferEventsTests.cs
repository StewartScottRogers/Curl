using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Prefix_New_IsTransferZeroWithNoConnection()
    {
        Assert.AreEqual("[0-x] ", new TraceIdsPrefix().Text);
    }

    [TestMethod]
    public void EveryEvent_SetsTheMarkerToTheTransfersIdsBeforeForwarding()
    {
        TraceIdsPrefix prefix = new();
        MarkerRecordingEvents recorder = new(prefix);
        long? connectionId = null;
        TraceIdsTransferEvents events = new(recorder, prefix, 7, () => connectionId);

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

        Assert.AreEqual(
            "[0-0] * using HTTP/1.x\n"
            + "[0-0] > GET /a HTTP/1.1\r\n"
            + "[0-0] > Host: h\r\n"
            + "[0-0] > \r\n"
            + "[0-0] < HTTP/1.1 200 OK\r\n"
            + "[0-0] { [2 bytes data]\n"
            + "[1-1] * shutting down connection #1\n",
            Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public void VerboseWriter_WithTraceTime_WritesTheStampThenTheMarker()
    {
        TraceIdsPrefix prefix = new();
        // As curl 8.21.0 wrote -v --trace-ids --trace-time: "04:09:46.314000 [0-0] * Request completely sent off".
        TimeProvider clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 4, 9, 46, 314, TimeSpan.Zero), TimeZoneInfo.Utc);
        VerboseTransferEventWriter writer = new(output, true, true, clock, TlsBackend.Schannel, prefix);

        new TraceIdsTransferEvents(writer, prefix, 0, () => 0).ReportInfo("Request completely sent off");

        Assert.AreEqual("04:09:46.314000 [0-0] * Request completely sent off\n", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public void TraceWriter_MarksTitleAndInfoLinesButNotTheDumpedBytes()
    {
        // As curl 8.21.0 wrote --trace-ascii - --trace-ids: "[0-0] => Send header, 80 bytes (0x50)",
        // then "0000: GET /a HTTP/1.1" unmarked.
        TraceIdsPrefix prefix = new();
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, false, TimeProvider.System, TlsBackend.Schannel, prefix);
        TraceIdsTransferEvents events = new(writer, prefix, 0, () => 0);

        events.ReportInfo("using HTTP/1.x");
        events.ReportRequestHeader("GET /a HTTP/1.1\r\n\r\n"u8);

        Assert.AreEqual(
            "[0-0] * using HTTP/1.x\n"
            + "[0-0] => Send header, 19 bytes (0x13)\n"
            + "0000: GET /a HTTP/1.1\n"
            + "0011: \n",
            Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public void Writers_WithoutPrefix_WriteNoMarker()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, false, TimeProvider.System, TlsBackend.Schannel).ReportInfo("a");
        new VerboseTransferEventWriter(output, true, false, TimeProvider.System, TlsBackend.Schannel).ReportInfo("b");

        Assert.AreEqual("* a\n* b\n", Encoding.ASCII.GetString(output.ToArray()));
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
