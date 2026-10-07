using System.Globalization;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    // Measured: curl 8.21.0 (mingw, Schannel) -v https://example.com/ (BL-1089).
    [TestMethod]
    public void Lines_ReceivedSessionTicket_ReturnsTheThreeRenegotiationLines()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", Describe(Ticket()));

        string[] actual = SchannelRenegotiationText.Lines(Ticket()).ToArray();

        ReportLines(diagnostics, MeasuredLines, actual);
        CollectionAssert.AreEqual(MeasuredLines, actual);
    }

    [TestMethod]
    public void Lines_SentHandshakeMessage_ReturnsNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TlsMessageEvent message = Ticket() with { Sent = true };
        diagnostics.Arrange("message", Describe(message));

        int count = SchannelRenegotiationText.Lines(message).Count;

        ReportCount(diagnostics, count);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void Lines_AlertWithTheTicketsTypeByte_ReturnsNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TlsMessageEvent message = Ticket() with { ContentType = TlsContentType.Alert };
        diagnostics.Arrange("message", Describe(message));

        int count = SchannelRenegotiationText.Lines(message).Count;

        ReportCount(diagnostics, count);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void Lines_ReceivedFinished_ReturnsNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TlsMessageEvent message = Ticket() with { Bytes = new byte[] { 20 } };
        diagnostics.Arrange("message", Describe(message));

        int count = SchannelRenegotiationText.Lines(message).Count;

        ReportCount(diagnostics, count);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void Lines_ReceivedEmptyHandshakeMessage_ReturnsNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TlsMessageEvent message = Ticket() with { Bytes = ReadOnlyMemory<byte>.Empty };
        diagnostics.Arrange("message", Describe(message));

        int count = SchannelRenegotiationText.Lines(message).Count;

        ReportCount(diagnostics, count);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void TlsMessage_Schannel_ReturnsTheRenegotiationLines()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", Describe(Ticket()) + ", backend Schannel");

        string[] actual = TransferEventInfoText.TlsMessage(Ticket(), TlsBackend.Schannel).ToArray();

        ReportLines(diagnostics, MeasuredLines, actual);
        CollectionAssert.AreEqual(MeasuredLines, actual);
    }

    [TestMethod]
    public void TlsMessage_OpenSsl_ReturnsOpenSslsTicketLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", Describe(Ticket()) + ", backend OpenSsl");
        string[] expected = ["TLSv1.3 (IN), TLS handshake, Newsession Ticket (4):"];

        string[] actual = TransferEventInfoText.TlsMessage(Ticket(), TlsBackend.OpenSsl).ToArray();

        ReportLines(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TlsMessage_OpenSslRecordHeader_ReturnsNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TlsMessageEvent message = Ticket() with { ContentType = TlsContentType.RecordHeader };
        diagnostics.Arrange("message", Describe(message) + ", backend OpenSsl");

        int count = TransferEventInfoText.TlsMessage(message, TlsBackend.OpenSsl).Count;

        ReportCount(diagnostics, count);
        Assert.AreEqual(0, count);
    }

    // Measured: the three lines fall after "Request completely sent off" and before the
    // response's status line, where the first read of the response runs (BL-1089).
    [TestMethod]
    public void VerboseWriter_SchannelTicket_WritesTheLinesBetweenTheRequestAndTheResponse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", Describe(Ticket()) + ", verbose writer, backend Schannel");
        using MemoryStream output = new();
        var writer = new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel);

        writer.ReportInfo("Request completely sent off");
        writer.ReportTlsMessage(Ticket());
        writer.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);

        string expected =
            "* Request completely sent off\n" +
            "* schannel: remote party requests renegotiation\n" +
            "* schannel: renegotiating SSL/TLS connection\n" +
            "* schannel: SSL/TLS connection renegotiated\n" +
            "< HTTP/1.1 200 OK\r\n";
        string actual = Encoding.UTF8.GetString(output.ToArray());
        diagnostics.Act("output", actual);
        diagnostics.Diff("output", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TraceWriter_SchannelTicket_WritesTheLinesAsInfoAndNoData()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", Describe(Ticket()) + ", trace writer TextOnly, backend Schannel");
        using MemoryStream output = new();
        var writer = new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System, TlsBackend.Schannel);

        writer.ReportTlsMessage(Ticket());

        string expected =
            "* schannel: remote party requests renegotiation\n" +
            "* schannel: renegotiating SSL/TLS connection\n" +
            "* schannel: SSL/TLS connection renegotiated\n";
        string actual = Encoding.UTF8.GetString(output.ToArray());
        diagnostics.Act("output", actual);
        diagnostics.Diff("output", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private static string Describe(TlsMessageEvent message) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"version 0x{message.ProtocolVersion:x4}, type {message.ContentType}, sent {message.Sent}, bytes {Convert.ToHexString(message.Bytes.Span)}");

    private static void ReportLines(TestDiagnostics diagnostics, string[] expected, string[] actual)
    {
        string expectedText = string.Join("\n", expected);
        string actualText = string.Join("\n", actual);
        diagnostics.Act("lines", actualText);
        diagnostics.Diff("lines", expectedText, actualText);
    }

    private static void ReportCount(TestDiagnostics diagnostics, int count)
    {
        diagnostics.Act("line count", count);
        diagnostics.Assert("line count", 0, count);
    }

    private static TlsMessageEvent Ticket() => new()
    {
        ProtocolVersion = 0x0304,
        ContentType = TlsContentType.Handshake,
        Sent = false,
        Bytes = new byte[] { 4 },
    };
}
