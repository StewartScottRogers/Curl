using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="TraceTransferEventWriter"/> to the trace files curl 8.21.0 (mingw, Schannel)
/// wrote on 2026-09-27; the commands are in BL-229's Notes and the files are in <c>Fixtures</c>.
/// </summary>
[TestClass]
public sealed class TraceTransferEventWriterTests
{
    private readonly MemoryStream output = new();

    [TestMethod]
    public void HttpExchange_Trace_RendersAsCurl()
    {
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.HexAndText, writesTimestamps: false, TimeProvider.System);

        WriteHttpExchange(writer, port: 18293, localPort: 57091);

        Assert.AreEqual(Fixture("trace-http.txt"), WrittenAsWindowsTextFile());
    }

    [TestMethod]
    public void HttpExchange_TraceWithTraceTime_RendersAsCurl()
    {
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.HexAndText, writesTimestamps: true, Clock(
            307, 311, 311, 311, 311, 362, 365, 365, 365, 365, 365));

        WriteHttpExchange(writer, port: 18291, localPort: 57085);

        Assert.AreEqual(Fixture("trace-time-http.txt"), WrittenAsWindowsTextFile());
    }

    [TestMethod]
    public void HttpExchange_TraceAsciiWithTraceTime_RendersAsCurl()
    {
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, writesTimestamps: true, Clock(
            912, 937, 937, 937, 937, 951, 952, 952, 952, 952, 952));

        WriteHttpExchange(writer, port: 18292, localPort: 57089);

        Assert.AreEqual(Fixture("trace-ascii-time-http.txt"), WrittenAsWindowsTextFile());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Timestamp_OnWindows_ShowsTheClocksMilliseconds()
    {
        DateTimeOffset instant = new DateTimeOffset(2026, 9, 27, 23, 4, 5, TimeSpan.Zero).AddTicks(1_234_567);
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, writesTimestamps: true, new QueuedTimeProvider(instant));

        writer.ReportInfo("x");

        Assert.AreEqual("23:04:05.123000 * x\n", Written());
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Timestamp_OffWindows_ShowsTheClocksMicroseconds()
    {
        DateTimeOffset instant = new DateTimeOffset(2026, 9, 27, 23, 4, 5, TimeSpan.Zero).AddTicks(1_234_567);
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, writesTimestamps: true, new QueuedTimeProvider(instant));

        writer.ReportInfo("x");

        Assert.AreEqual("23:04:05.123456 * x\n", Written());
    }

    [TestMethod]
    public void ReportDataSent_WritesASendDataDump()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.HexAndText, writesTimestamps: false, TimeProvider.System)
            .ReportDataSent("hi"u8);

        Assert.AreEqual(
            "=> Send data, 2 bytes (0x2)\n" +
            "0000: 68 69                                           hi\n",
            Written());
    }

    [TestMethod]
    public void ReportDataReceived_SeveralReads_WritesADumpForEach()
    {
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System);

        writer.ReportDataReceived("a"u8);
        writer.ReportDataReceived("b"u8);

        Assert.AreEqual("<= Recv data, 1 bytes (0x1)\n0000: a\n<= Recv data, 1 bytes (0x1)\n0000: b\n", Written());
    }

    [TestMethod]
    public void ReportDataReceived_Empty_WritesOnlyTheTitle()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.HexAndText, writesTimestamps: false, TimeProvider.System)
            .ReportDataReceived([]);

        Assert.AreEqual("<= Recv data, 0 bytes (0x0)\n", Written());
    }

    [TestMethod]
    public void TextOnly_LineLongerThan64Bytes_WrapsAt64()
    {
        byte[] bytes = Encoding.ASCII.GetBytes(new string('a', 64) + "bc");

        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System)
            .ReportDataReceived(bytes);

        Assert.AreEqual("<= Recv data, 66 bytes (0x42)\n0000: " + new string('a', 64) + "\n0040: bc\n", Written());
    }

    [TestMethod]
    public void TextOnly_CrLfAtLineStart_EndsAnEmptyLine()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System)
            .ReportDataReceived("\r\n\r\nx"u8);

        Assert.AreEqual("<= Recv data, 5 bytes (0x5)\n0000: \n0002: \n0004: x\n", Written());
    }

    [TestMethod]
    public void TextOnly_LoneCarriageReturnAndHighBytes_ShowAsDots()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System)
            .ReportDataReceived([0x61, 0x0D, 0x80, 0x1F, 0x7F, 0x0D]);

        Assert.AreEqual("<= Recv data, 6 bytes (0x6)\n0000: a..." + (char)0x7F + ".\n", Written());
    }

    [TestMethod]
    public void HexAndText_CrLf_DoesNotEndTheLine()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.HexAndText, writesTimestamps: false, TimeProvider.System)
            .ReportDataReceived("a\r\nb"u8);

        Assert.AreEqual(
            "<= Recv data, 4 bytes (0x4)\n" +
            "0000: 61 0d 0a 62                                     a..b\n",
            Written());
    }

    [TestMethod]
    public void ReportConnectionReused_WritesCurlsReuseLine()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System)
            .ReportConnectionReused(new ConnectionReusedEvent
            {
                Scheme = "http",
                IsProxy = false,
                HostName = "127.0.0.1",
                Port = 18231,
                ConnectionNumber = 0,
            });

        Assert.AreEqual("* Reusing existing http: connection with host 127.0.0.1\n", Written());
    }

    [TestMethod]
    public void ReportTlsHandshake_WritesTheAlpnLines()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System, TlsBackend.Schannel)
            .ReportTlsHandshake(new TlsHandshakeEvent
            {
                ProtocolVersion = SslProtocols.Tls13,
                CipherSuite = null,
                NegotiatedApplicationProtocol = "http/1.1",
                OfferedApplicationProtocols = ["http/1.1"],
                ServerCertificate = null,
                CertificateVerified = false,
            });

        Assert.AreEqual("* ALPN: curl offers http/1.1\n* ALPN: server accepted http/1.1\n", Written());
    }

    [TestMethod]
    public void ReportTlsHandshake_OpenSsl_WritesTheOpenSslLines()
    {
        new TraceTransferEventWriter(output, TraceDumpFormat.TextOnly, writesTimestamps: false, TimeProvider.System, TlsBackend.OpenSsl)
            .ReportTlsHandshake(new TlsHandshakeEvent
            {
                ProtocolVersion = SslProtocols.Tls13,
                CipherSuite = null,
                NegotiatedApplicationProtocol = "http/1.1",
                OfferedApplicationProtocols = ["http/1.1"],
                ServerCertificate = null,
                CertificateVerified = false,
            });

        Assert.AreEqual(
            "* ALPN: curl offers http/1.1\n* SSL connection using TLSv1.3 / (NONE) / [blank] / UNDEF\n* ALPN: server accepted http/1.1\n",
            Written());
    }

    [TestMethod]
    public void TlsDataMessagesAndTrust_Schannel_WriteOnlyTheSchannelTrustLine()
    {
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.HexAndText, writesTimestamps: false, TimeProvider.System, TlsBackend.Schannel);

        writer.ReportTlsData([1, 2, 3], sent: true);
        writer.ReportTlsData([1, 2, 3], sent: false);
        writer.ReportTlsMessage(new TlsMessageEvent
        {
            ProtocolVersion = 0x0304,
            ContentType = TlsContentType.Handshake,
            Sent = true,
            Bytes = new byte[] { 1, 0, 0, 0 },
        });
        writer.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });

        Assert.AreEqual("* schannel: disabled automatic use of client certificate\n", Written());
    }

    private static void WriteHttpExchange(TraceTransferEventWriter writer, int port, int localPort)
    {
        writer.ReportInfo($"  Trying 127.0.0.1:{port}...");
        writer.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = "127.0.0.1",
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, localPort),
            ConnectionNumber = 0,
        });
        writer.ReportInfo("using HTTP/1.x");
        writer.ReportRequestHeader(Encoding.ASCII.GetBytes(
            $"GET /f.txt HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"));
        writer.ReportInfo("Request completely sent off");
        writer.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        writer.ReportResponseHeader("Content-Type: text/plain\r\n"u8);
        writer.ReportResponseHeader("Content-Length: 6\r\n"u8);
        writer.ReportResponseHeader("\r\n"u8);
        writer.ReportDataReceived("hello\n"u8);
        writer.ReportInfo($"Connection #0 to host 127.0.0.1:{port} left intact");
    }

    // The measured instants, as milliseconds past 03:25:30, one per event in order.
    private static QueuedTimeProvider Clock(params int[] milliseconds)
    {
        DateTimeOffset second = new(2026, 9, 27, 3, 25, 30, TimeSpan.Zero);
        return new QueuedTimeProvider([.. milliseconds.Select(millisecond => second.AddMilliseconds(millisecond))]);
    }

    private static string Fixture(string name)
    {
        return Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));
    }

    private string Written()
    {
        return Encoding.UTF8.GetString(output.ToArray());
    }

    // curl opens the trace file in text mode on Windows, so every line feed is written as CR LF.
    private string WrittenAsWindowsTextFile()
    {
        return Written().Replace("\n", "\r\n", StringComparison.Ordinal);
    }
}
