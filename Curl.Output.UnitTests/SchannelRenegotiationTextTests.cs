using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Output;

[TestClass]
public sealed class SchannelRenegotiationTextTests
{
    private static readonly string[] MeasuredLines =
    [
        "schannel: remote party requests renegotiation",
        "schannel: renegotiating SSL/TLS connection",
        "schannel: SSL/TLS connection renegotiated",
    ];

    // Measured: curl 8.21.0 (mingw, Schannel) -v https://example.com/ (BL-1089).
    [TestMethod]
    public void Lines_ReceivedSessionTicket_ReturnsTheThreeRenegotiationLines()
    {
        CollectionAssert.AreEqual(MeasuredLines, SchannelRenegotiationText.Lines(Ticket()).ToArray());
    }

    [TestMethod]
    public void Lines_SentHandshakeMessage_ReturnsNone()
    {
        Assert.AreEqual(0, SchannelRenegotiationText.Lines(Ticket() with { Sent = true }).Count);
    }

    [TestMethod]
    public void Lines_AlertWithTheTicketsTypeByte_ReturnsNone()
    {
        Assert.AreEqual(0, SchannelRenegotiationText.Lines(Ticket() with { ContentType = TlsContentType.Alert }).Count);
    }

    [TestMethod]
    public void Lines_ReceivedFinished_ReturnsNone()
    {
        Assert.AreEqual(0, SchannelRenegotiationText.Lines(Ticket() with { Bytes = new byte[] { 20 } }).Count);
    }

    [TestMethod]
    public void Lines_ReceivedEmptyHandshakeMessage_ReturnsNone()
    {
        Assert.AreEqual(0, SchannelRenegotiationText.Lines(Ticket() with { Bytes = ReadOnlyMemory<byte>.Empty }).Count);
    }

    [TestMethod]
    public void TlsMessage_Schannel_ReturnsTheRenegotiationLines()
    {
        CollectionAssert.AreEqual(MeasuredLines, TransferEventInfoText.TlsMessage(Ticket(), TlsBackend.Schannel).ToArray());
    }

    [TestMethod]
    public void TlsMessage_OpenSsl_ReturnsOpenSslsTicketLine()
    {
        CollectionAssert.AreEqual(
            new[] { "TLSv1.3 (IN), TLS handshake, Newsession Ticket (4):" },
            TransferEventInfoText.TlsMessage(Ticket(), TlsBackend.OpenSsl).ToArray());
    }

    [TestMethod]
    public void TlsMessage_OpenSslRecordHeader_ReturnsNone()
    {
        Assert.AreEqual(0, TransferEventInfoText.TlsMessage(Ticket() with { ContentType = TlsContentType.RecordHeader }, TlsBackend.OpenSsl).Count);
    }

    // Measured: the three lines fall after "Request completely sent off" and before the
    // response's status line, where the first read of the response runs (BL-1089).
    [TestMethod]
    public void VerboseWriter_SchannelTicket_WritesTheLinesBetweenTheRequestAndTheResponse()
    {
        using MemoryStream output = new();
        var writer = new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel);

        writer.ReportInfo("Request completely sent off");
        writer.ReportTlsMessage(Ticket());
        writer.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);

        Assert.AreEqual(
            "* Request completely sent off\n" +
            "* schannel: remote party requests renegotiation\n" +
            "* schannel: renegotiating SSL/TLS connection\n" +
            "* schannel: SSL/TLS connection renegotiated\n" +
            "< HTTP/1.1 200 OK\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public void TraceWriter_SchannelTicket_WritesTheLinesAsInfoAndNoData()
    {
        using MemoryStream output = new();
        var writer = new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System, TlsBackend.Schannel);

        writer.ReportTlsMessage(Ticket());

        Assert.AreEqual(
            "* schannel: remote party requests renegotiation\n" +
            "* schannel: renegotiating SSL/TLS connection\n" +
            "* schannel: SSL/TLS connection renegotiated\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    private static TlsMessageEvent Ticket() => new()
    {
        ProtocolVersion = 0x0304,
        ContentType = TlsContentType.Handshake,
        Sent = false,
        Bytes = new byte[] { 4 },
    };
}
