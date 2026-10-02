using Curl.Networking.Fakes;

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
    [TestMethod]
    public void CountTicketRecords_OneTicketBeforeTheResponse_CountsOne()
    {
        var detector = AfterHandshake(Records(445, 1049, 22, 19));

        Assert.AreEqual(1, detector.CountTicketRecords(1037));
    }

    [TestMethod]
    public void CountTicketRecords_TwoTicketsReadBeforeTheResponse_CountsTwo()
    {
        var detector = AfterHandshake(Records(74, 74));
        detector.ObserveReceived(Records(1356));

        Assert.AreEqual(2, detector.CountTicketRecords(1339));
    }

    [TestMethod]
    public void CountTicketRecords_ResponseFirst_CountsNone()
    {
        var detector = AfterHandshake(Records(117, 445));

        Assert.AreEqual(0, detector.CountTicketRecords(100));
    }

    [TestMethod]
    public void CountTicketRecords_ReadStoppingInsideARecord_CountsNone()
    {
        var detector = AfterHandshake(Records(445, 16401));

        Assert.AreEqual(0, detector.CountTicketRecords(4096));
    }

    [TestMethod]
    public void CountTicketRecords_NoRunOfRecordsAccountsForTheRead_CountsNone()
    {
        var detector = AfterHandshake(Records(445, 1049));

        Assert.AreEqual(0, detector.CountTicketRecords(5000));
    }

    [TestMethod]
    public void CountTicketRecords_NothingRead_CountsNoneAndKeepsWatching()
    {
        var detector = AfterHandshake(Records(445));

        Assert.AreEqual(0, detector.CountTicketRecords(0));

        detector.ObserveReceived(Records(117));
        Assert.AreEqual(1, detector.CountTicketRecords(100));
    }

    [TestMethod]
    public void CountTicketRecords_SecondRead_CountsNoneAndStopsWatching()
    {
        var detector = AfterHandshake(Records(117));
        detector.CountTicketRecords(100);

        detector.ObserveReceived(Records(445, 117));

        Assert.AreEqual(0, detector.CountTicketRecords(100));
    }

    [TestMethod]
    public void CountTicketRecords_RecordsBeforeTheHandshakeCompleted_AreForgotten()
    {
        var detector = new SessionTicketRecordDetector();
        detector.ObserveReceived(Records(122, 445));
        detector.MarkHandshakeComplete();
        detector.ObserveReceived(Records(117));

        Assert.AreEqual(0, detector.CountTicketRecords(100));
    }

    [TestMethod]
    public void ObserveReceived_RecordsSplitAcrossReads_FollowsTheBoundaries()
    {
        var detector = AfterHandshake([]);
        var bytes = Records(445, 117);
        foreach (var piece in new[] { bytes[..3], bytes[3..200], bytes[200..452], bytes[452..] })
        {
            detector.ObserveReceived(piece);
        }

        Assert.AreEqual(1, detector.CountTicketRecords(100));
    }

    [TestMethod]
    public void ReportTicketRecords_TwoTickets_ReportsTwoReceivedNewSessionTickets()
    {
        var detector = AfterHandshake(Records(74, 74, 117));
        var events = new RecordingTransferEvents();

        detector.ReportTicketRecords(100, events);

        Assert.AreEqual(2, events.TlsMessages.Count);
        Assert.IsTrue(events.TlsMessages.TrueForAll(message =>
            !message.Sent && message.ProtocolVersion == 0x0304 && message.ContentType == Protocol.Abstractions.TlsContentType.Handshake && message.Bytes.Span[0] == 4));
    }

    [TestMethod]
    public void ReportTicketRecords_NoTicket_ReportsNothing()
    {
        var detector = AfterHandshake(Records(117));
        var events = new RecordingTransferEvents();

        detector.ReportTicketRecords(100, events);

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
