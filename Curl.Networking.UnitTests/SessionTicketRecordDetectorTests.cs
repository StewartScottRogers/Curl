using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins when <see cref="SessionTicketRecordDetector" /> reports a session ticket (BL-1089,
/// ADR-0309), with record sizes measured under <c>SslStream</c>: example.com sent one 445-byte
/// ticket record and then the response in a 1049-byte and a 22-byte record, read as 1037
/// bytes of plaintext; github.com sent two 74-byte ticket records on their own before the
/// response. Each record's plaintext is its length less 17 bytes.
/// </summary>
[TestClass]
public sealed class SessionTicketRecordDetectorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CountTicketRecords_OneTicketBeforeTheResponse_CountsOne()
    {
        var detector = AfterHandshake(Records(445, 1049, 22, 19));

        Diagnostics.Arrange("record lengths after handshake", "445, 1049, 22, 19");
        Diagnostics.Arrange("plaintext read", 1037);

        var count = detector.CountTicketRecords(1037);

        Diagnostics.Act("ticket records", count);
        Diagnostics.Assert("ticket records", 1, count);

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void CountTicketRecords_TwoTicketsReadBeforeTheResponse_CountsTwo()
    {
        var detector = AfterHandshake(Records(74, 74));
        detector.ObserveReceived(Records(1356));

        Diagnostics.Arrange("record lengths after handshake", "74, 74, then 1356");
        Diagnostics.Arrange("plaintext read", 1339);

        var count = detector.CountTicketRecords(1339);

        Diagnostics.Act("ticket records", count);
        Diagnostics.Assert("ticket records", 2, count);

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void CountTicketRecords_ResponseFirst_CountsNone()
    {
        var detector = AfterHandshake(Records(117, 445));

        Diagnostics.Arrange("record lengths after handshake", "117, 445");
        Diagnostics.Arrange("plaintext read", 100);

        var count = detector.CountTicketRecords(100);

        Diagnostics.Act("ticket records", count);
        Diagnostics.Assert("ticket records", 0, count);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void CountTicketRecords_ReadStoppingInsideARecord_CountsNone()
    {
        var detector = AfterHandshake(Records(445, 16401));

        Diagnostics.Arrange("record lengths after handshake", "445, 16401");
        Diagnostics.Arrange("plaintext read", 4096);

        var count = detector.CountTicketRecords(4096);

        Diagnostics.Act("ticket records", count);
        Diagnostics.Assert("ticket records", 0, count);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void CountTicketRecords_NoRunOfRecordsAccountsForTheRead_CountsNone()
    {
        var detector = AfterHandshake(Records(445, 1049));

        Diagnostics.Arrange("record lengths after handshake", "445, 1049");
        Diagnostics.Arrange("plaintext read", 5000);

        var count = detector.CountTicketRecords(5000);

        Diagnostics.Act("ticket records", count);
        Diagnostics.Assert("ticket records", 0, count);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void CountTicketRecords_NothingRead_CountsNoneAndKeepsWatching()
    {
        var detector = AfterHandshake(Records(445));

        Diagnostics.Arrange("record lengths after handshake", "445");
        Diagnostics.Arrange("plaintext read", 0);

        var emptyReadCount = detector.CountTicketRecords(0);

        Diagnostics.Act("ticket records for the empty read", emptyReadCount);
        Diagnostics.Assert("ticket records for the empty read", 0, emptyReadCount);

        Assert.AreEqual(0, emptyReadCount);

        detector.ObserveReceived(Records(117));
        var nextReadCount = detector.CountTicketRecords(100);

        Diagnostics.Act("ticket records after a 117-byte record and a 100-byte read", nextReadCount);
        Diagnostics.Assert("ticket records after a 117-byte record and a 100-byte read", 1, nextReadCount);
        Assert.AreEqual(1, nextReadCount);
    }

    [TestMethod]
    public void CountTicketRecords_SecondRead_CountsNoneAndStopsWatching()
    {
        var detector = AfterHandshake(Records(117));
        detector.CountTicketRecords(100);

        Diagnostics.Arrange("first read", "117-byte record, 100 bytes read");
        Diagnostics.Arrange("second read's record lengths", "445, 117");

        detector.ObserveReceived(Records(445, 117));
        var count = detector.CountTicketRecords(100);

        Diagnostics.Act("ticket records on the second read", count);
        Diagnostics.Assert("ticket records on the second read", 0, count);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void CountTicketRecords_RecordsBeforeTheHandshakeCompleted_AreForgotten()
    {
        var detector = new SessionTicketRecordDetector();
        detector.ObserveReceived(Records(122, 445));
        detector.MarkHandshakeComplete();
        detector.ObserveReceived(Records(117));

        Diagnostics.Arrange("record lengths before handshake", "122, 445");
        Diagnostics.Arrange("record lengths after handshake", "117");

        var count = detector.CountTicketRecords(100);

        Diagnostics.Act("ticket records", count);
        Diagnostics.Assert("ticket records", 0, count);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void ObserveReceived_RecordsSplitAcrossReads_FollowsTheBoundaries()
    {
        var detector = AfterHandshake([]);
        var bytes = Records(445, 117);

        Diagnostics.Arrange("record lengths", "445, 117");
        Diagnostics.Arrange("pieces", "[..3], [3..200], [200..452], [452..]");

        foreach (var piece in new[] { bytes[..3], bytes[3..200], bytes[200..452], bytes[452..] })
        {
            detector.ObserveReceived(piece);
        }

        var count = detector.CountTicketRecords(100);

        Diagnostics.Act("ticket records", count);
        Diagnostics.Assert("ticket records", 1, count);

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void ReportTicketRecords_TwoTickets_ReportsTwoReceivedNewSessionTickets()
    {
        var detector = AfterHandshake(Records(74, 74, 117));
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("record lengths after handshake", "74, 74, 117");

        detector.ReportTicketRecords(100, events);

        Diagnostics.Act("TLS messages reported", events.TlsMessages.Count);
        foreach (var message in events.TlsMessages)
        {
            Diagnostics.Bytes("reported message", message.Bytes.Span);
        }

        Diagnostics.Assert("TLS messages reported", 2, events.TlsMessages.Count);

        Assert.AreEqual(2, events.TlsMessages.Count);
        Assert.IsTrue(events.TlsMessages.TrueForAll(message =>
            !message.Sent && message.ProtocolVersion == 0x0304 && message.ContentType == Protocol.Abstractions.TlsContentType.Handshake && message.Bytes.Span[0] == 4));
    }

    [TestMethod]
    public void ReportTicketRecords_NoTicket_ReportsNothing()
    {
        var detector = AfterHandshake(Records(117));
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("record lengths after handshake", "117");

        detector.ReportTicketRecords(100, events);

        Diagnostics.Act("TLS messages reported", events.TlsMessages.Count);
        Diagnostics.Assert("TLS messages reported", 0, events.TlsMessages.Count);

        Assert.AreEqual(0, events.TlsMessages.Count);
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
}
