using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the read-side Schannel lines BL-3754 (CGR-225) left out, measured with curl 8.22.0
/// (Schannel) <c>-v --cacert</c> against <c>openssl s_server -WWW</c> (BL-4067): the server's
/// <c>close_notify</c>, already received with the response, is reported at the first read, and
/// the read that then returns 0 writes <c>server indicated shutdown in a prior call</c> and
/// <c>{ [0 bytes data]</c>.
/// </summary>
[TestClass]
public sealed class Cgr225RemainderTests
{
    // A TLS 1.3 close_notify record: 2 bytes of alert, 1 inner content type, a 16-byte tag.
    private const int CloseNotifyRecordLength = 19;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ReportTicketRecords_CloseNotifyAfterResponse_ReportsCloseNotifyAlert()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var detector = AfterHandshake(Records(17 + 10, CloseNotifyRecordLength));
        var events = new LineRecordingEvents();
        diagnostics.Arrange("records", "27-byte data record, 19-byte record");

        detector.ReportTicketRecords(10, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        CollectionAssert.AreEqual(new[] { "alert 1,0" }, events.Lines);
        Assert.IsTrue(detector.CloseNotifyFollows);
    }

    [TestMethod]
    public void ReportTicketRecords_ZeroReadAfterCloseNotifyReported_ReportsItNoMore()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var detector = AfterHandshake(Records(17 + 10, CloseNotifyRecordLength));
        detector.ReportTicketRecords(10, new LineRecordingEvents());
        var events = new LineRecordingEvents();
        diagnostics.Arrange("first read", "10 bytes, close_notify reported");

        detector.ReportTicketRecords(0, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        Assert.IsEmpty(events.Lines);
    }

    [TestMethod]
    public void ReportTicketRecords_CloseNotifyRecordNotYetComplete_ReportsNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] records = Records(17 + 10, CloseNotifyRecordLength);
        var detector = AfterHandshake(records[..^1]);
        var events = new LineRecordingEvents();
        diagnostics.Arrange("records", "27-byte data record, 19-byte record less its last byte");

        detector.ReportTicketRecords(10, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        Assert.IsEmpty(events.Lines);
        Assert.IsFalse(detector.CloseNotifyFollows);
    }

    [TestMethod]
    public void ReportTicketRecords_DataRecordAfterResponse_ReportsNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var detector = AfterHandshake(Records(17 + 10, CloseNotifyRecordLength, 17 + 5));
        var events = new LineRecordingEvents();
        diagnostics.Arrange("records", "27-byte data record, 19-byte record, 22-byte record");

        detector.ReportTicketRecords(10, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        Assert.IsEmpty(events.Lines);
    }

    [TestMethod]
    public void ReportTicketRecords_TicketBeforeResponse_ReportsTheTicketMessageGiven()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var detector = AfterHandshake(Records(445, 17 + 10));
        detector.TicketMessage = SessionTicketRecordDetector.NewSessionTicketReceived with { ProtocolVersion = 1 };
        var events = new LineRecordingEvents();
        diagnostics.Arrange("records", "445-byte ticket record, 27-byte data record");

        detector.ReportTicketRecords(10, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        CollectionAssert.AreEqual(new[] { "handshake 4 version 1" }, events.Lines);
    }

    [TestMethod]
    public void ReportShutdownAfterCloseNotify_ZeroReadAfterCloseNotify_ReportsShutdownLineAndZeroBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var detector = AfterHandshake(Records(17 + 10, CloseNotifyRecordLength));
        detector.CountTicketRecords(10);
        var events = new LineRecordingEvents();
        diagnostics.Arrange("close_notify", detector.CloseNotifyFollows);

        SslStreamConnection.ReportShutdownAfterCloseNotify(0, bufferIsEmpty: false, detector, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        CollectionAssert.AreEqual(
            new[] { "info schannel: server indicated shutdown in a prior call", "data 0" },
            events.Lines);
    }

    [TestMethod]
    [DataRow(5, false, true)]
    [DataRow(0, true, true)]
    [DataRow(0, false, false)]
    public void ReportShutdownAfterCloseNotify_NotAZeroReadAfterCloseNotify_ReportsNothing(int read, bool bufferIsEmpty, bool closeNotifyFollows)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var detector = AfterHandshake(closeNotifyFollows ? Records(17 + 10, CloseNotifyRecordLength) : Records(17 + 10));
        detector.CountTicketRecords(10);
        var events = new LineRecordingEvents();
        diagnostics.Arrange("read", read);

        SslStreamConnection.ReportShutdownAfterCloseNotify(read, bufferIsEmpty, detector, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        Assert.IsEmpty(events.Lines);
    }

    [TestMethod]
    public void ReportShutdownAfterCloseNotify_NoDetector_ReportsNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var events = new LineRecordingEvents();
        diagnostics.Arrange("detector", "none");

        SslStreamConnection.ReportShutdownAfterCloseNotify(0, bufferIsEmpty: false, null, events);

        diagnostics.Act("lines", string.Join("|", events.Lines));
        Assert.IsEmpty(events.Lines);
    }

    private static SessionTicketRecordDetector AfterHandshake(byte[] records)
    {
        var detector = new SessionTicketRecordDetector();
        detector.MarkHandshakeComplete();
        detector.ObserveReceived(records);
        return detector;
    }

    // Application data records (type 23, legacy version 3.3) of the given lengths, bodies zero.
    private static byte[] Records(params int[] lengths)
    {
        var bytes = new List<byte>();
        foreach (var length in lengths)
        {
            bytes.AddRange([0x17, 0x03, 0x03, (byte)(length >> 8), (byte)length]);
            bytes.AddRange(new byte[length]);
        }

        return [.. bytes];
    }

    private sealed class LineRecordingEvents : ITransferEvents
    {
        public List<string> Lines { get; } = [];

        public void ReportInfo(string text) => Lines.Add("info " + text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportTlsMessage(TlsMessageEvent message) => Lines.Add(
            message.ContentType == TlsContentType.Alert
                ? $"alert {string.Join(',', message.Bytes.ToArray())}"
                : $"handshake {message.Bytes.Span[0]} version {message.ProtocolVersion}");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Lines.Add($"data {bytes.Length}");
    }
}
