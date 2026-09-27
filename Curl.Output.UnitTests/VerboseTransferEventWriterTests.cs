using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="VerboseTransferEventWriter"/> to what curl 8.21.0 (mingw, Schannel) wrote to
/// standard error for <c>-v</c> on 2026-09-27; the commands are in BL-228's Notes.
/// </summary>
[TestClass]
public sealed class VerboseTransferEventWriterTests
{
    private static readonly IPEndPoint Loopback18228 = new(IPAddress.Loopback, 18228);
    private static readonly IPEndPoint Loopback18229 = new(IPAddress.Loopback, 18229);

    private static readonly byte[] RequestHead18228 =
        "GET /f.txt HTTP/1.1\r\nHost: 127.0.0.1:18228\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8.ToArray();

    private static readonly byte[] RequestHead18229 =
        "GET /f.txt HTTP/1.1\r\nHost: 127.0.0.1:18229\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8.ToArray();

    private readonly MemoryStream output = new();

    [TestMethod]
    public void HttpExchange_RendersAsCurl()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportInfo("  Trying 127.0.0.1:18228...");
        writer.ReportConnectionOpened(Opened(Loopback18228, 54485));
        writer.ReportInfo("using HTTP/1.x");
        writer.ReportRequestHeader(RequestHead18228);
        writer.ReportInfo("Request completely sent off");
        writer.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        writer.ReportResponseHeader("Content-Type: text/plain\r\n"u8);
        writer.ReportResponseHeader("Content-Length: 6\r\n"u8);
        writer.ReportResponseHeader("\r\n"u8);
        writer.ReportDataReceived("hello\n"u8);
        writer.ReportInfo("Connection #0 to host 127.0.0.1:18228 left intact");

        Assert.AreEqual(
            "*   Trying 127.0.0.1:18228...\r\n" +
            "* Established connection to 127.0.0.1 (127.0.0.1 port 18228) from 127.0.0.1 port 54485 \r\n" +
            "* using HTTP/1.x\r\n" +
            "> GET /f.txt HTTP/1.1\r\r\n" +
            "> Host: 127.0.0.1:18228\r\r\n" +
            "> User-Agent: curl/8.21.0\r\r\n" +
            "> Accept: */*\r\r\n" +
            "> \r\r\n" +
            "* Request completely sent off\r\n" +
            "< HTTP/1.1 200 OK\r\r\n" +
            "< Content-Type: text/plain\r\r\n" +
            "< Content-Length: 6\r\r\n" +
            "< \r\r\n" +
            "{ [6 bytes data]\r\n" +
            "* Connection #0 to host 127.0.0.1:18228 left intact\r\n",
            WrittenAsWindowsStandardError());
    }

    [TestMethod]
    public void HttpsExchange_RendersAsCurl()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportInfo("  Trying 127.0.0.1:18229...");
        writer.ReportInfo("schannel: disabled automatic use of client certificate");
        writer.ReportInfo("schannel: using IP address, SNI is not supported by OS.");
        writer.ReportTlsHandshake(Handshake(["http/1.1"], "http/1.1"));
        writer.ReportConnectionOpened(Opened(Loopback18229, 50442));
        writer.ReportInfo("using HTTP/1.x");
        writer.ReportRequestHeader(RequestHead18229);
        writer.ReportInfo("Request completely sent off");
        writer.ReportInfo("schannel: remote party requests renegotiation");
        writer.ReportInfo("schannel: renegotiating SSL/TLS connection");
        writer.ReportInfo("schannel: SSL/TLS connection renegotiated");
        writer.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        writer.ReportResponseHeader("Content-Length: 6\r\n"u8);
        writer.ReportResponseHeader("\r\n"u8);
        writer.ReportDataReceived("hello\n"u8);
        writer.ReportInfo("Connection #0 to host 127.0.0.1:18229 left intact");

        Assert.AreEqual(
            "*   Trying 127.0.0.1:18229...\r\n" +
            "* schannel: disabled automatic use of client certificate\r\n" +
            "* schannel: using IP address, SNI is not supported by OS.\r\n" +
            "* ALPN: curl offers http/1.1\r\n" +
            "* ALPN: server accepted http/1.1\r\n" +
            "* Established connection to 127.0.0.1 (127.0.0.1 port 18229) from 127.0.0.1 port 50442 \r\n" +
            "* using HTTP/1.x\r\n" +
            "> GET /f.txt HTTP/1.1\r\r\n" +
            "> Host: 127.0.0.1:18229\r\r\n" +
            "> User-Agent: curl/8.21.0\r\r\n" +
            "> Accept: */*\r\r\n" +
            "> \r\r\n" +
            "* Request completely sent off\r\n" +
            "* schannel: remote party requests renegotiation\r\n" +
            "* schannel: renegotiating SSL/TLS connection\r\n" +
            "* schannel: SSL/TLS connection renegotiated\r\n" +
            "< HTTP/1.1 200 OK\r\r\n" +
            "< Content-Length: 6\r\r\n" +
            "< \r\r\n" +
            "{ [6 bytes data]\r\n" +
            "* Connection #0 to host 127.0.0.1:18229 left intact\r\n",
            WrittenAsWindowsStandardError());
    }

    [TestMethod]
    public void ReportTlsHandshake_ServerChoseNoProtocol_SaysItUsesTheDefault()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true).ReportTlsHandshake(Handshake(["http/1.1"], null));

        Assert.AreEqual(
            "* ALPN: curl offers http/1.1\r\n* ALPN: server did not agree on a protocol. Uses default.\r\n",
            WrittenAsWindowsStandardError());
    }

    [TestMethod]
    public void ReportTlsHandshake_SeveralProtocolsOffered_JoinsThemWithCommas()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true).ReportTlsHandshake(Handshake(["h2", "http/1.1"], "h2"));

        Assert.AreEqual("* ALPN: curl offers h2,http/1.1\n* ALPN: server accepted h2\n", Written());
    }

    [TestMethod]
    public void ReportTlsHandshake_NothingOffered_WritesNothing()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true).ReportTlsHandshake(Handshake([], null));

        Assert.AreEqual(string.Empty, Written());
    }

    [TestMethod]
    [DataRow("http", false, "127.0.0.1", "* Reusing existing http: connection with host 127.0.0.1\n")]
    [DataRow("https", false, "example.com", "* Reusing existing https: connection with host example.com\n")]
    [DataRow("http", true, "127.0.0.1", "* Reusing existing http: connection with proxy 127.0.0.1\n")]
    public void ReportConnectionReused_WritesCurlsReuseLine(string scheme, bool isProxy, string hostName, string expected)
    {
        new VerboseTransferEventWriter(output, writesDataLines: true).ReportConnectionReused(new ConnectionReusedEvent
        {
            Scheme = scheme,
            IsProxy = isProxy,
            HostName = hostName,
            Port = 18231,
            ConnectionNumber = 0,
        });

        Assert.AreEqual(expected, Written());
    }

    [TestMethod]
    public void ReportDataReceived_SeveralReadsInARow_WritesOneLineWithTheFirstCount()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportDataReceived(new byte[102357]);
        writer.ReportDataReceived(new byte[97643]);

        Assert.AreEqual("{ [102357 bytes data]\n", Written());
    }

    [TestMethod]
    public void ReportDataReceived_AfterAnotherEvent_WritesANewLine()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportDataReceived(new byte[3]);
        writer.ReportResponseHeader("\r\n"u8);
        writer.ReportDataReceived(new byte[4]);
        writer.ReportInfo("x");
        writer.ReportDataReceived(new byte[5]);

        Assert.AreEqual("{ [3 bytes data]\n< \r\n{ [4 bytes data]\n* x\n{ [5 bytes data]\n", Written());
    }

    [TestMethod]
    public void ReportDataSent_WritesTheSentDataLine()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true).ReportDataSent("hi"u8);

        Assert.AreEqual("} [2 bytes data]\n", Written());
    }

    [TestMethod]
    public void DataEvents_DataLinesNotWritten_WriteNothing()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: false);

        writer.ReportDataSent("hi"u8);
        writer.ReportDataReceived("hello\n"u8);

        Assert.AreEqual(string.Empty, Written());
    }

    [TestMethod]
    public void ReportTlsData_WritesNothing()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportTlsData([1, 2, 3], sent: true);
        writer.ReportTlsData([1, 2, 3], sent: false);

        Assert.AreEqual(string.Empty, Written());
    }

    [TestMethod]
    public void ReportRequestHeader_HeadSplitMidLine_ContinuesTheLineWithoutAPrefix()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportRequestHeader("GET / HTTP/1.1\r\nHo"u8);
        writer.ReportRequestHeader("st: a\r\n\r\n"u8);

        Assert.AreEqual("> GET / HTTP/1.1\r\n> Host: a\r\n> \r\n", Written());
    }

    [TestMethod]
    public void ReportRequestHeader_Empty_WritesNothing()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportRequestHeader([]);

        Assert.AreEqual(string.Empty, Written());
    }

    [TestMethod]
    public void ReportResponseHeader_LineSplitAcrossCalls_ContinuesTheLineWithoutAPrefix()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportResponseHeader("HTTP/1.1 2"u8);
        writer.ReportResponseHeader("00 OK\r\n"u8);
        writer.ReportResponseHeader([]);
        writer.ReportResponseHeader("\r\n"u8);

        Assert.AreEqual("< HTTP/1.1 200 OK\r\n< < \r\n", Written());
    }

    private static ConnectionOpenedEvent Opened(IPEndPoint remote, int localPort)
    {
        return new ConnectionOpenedEvent
        {
            HostName = "127.0.0.1",
            RemoteEndPoint = remote,
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, localPort),
            ConnectionNumber = 0,
        };
    }

    private static TlsHandshakeEvent Handshake(string[] offered, string? negotiated)
    {
        return new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = null,
            NegotiatedApplicationProtocol = negotiated,
            OfferedApplicationProtocols = offered,
            ServerCertificate = null,
            CertificateVerified = false,
        };
    }

    private string Written()
    {
        return Encoding.UTF8.GetString(output.ToArray());
    }

    // curl's standard error is in text mode on Windows, so every line feed is written as CR LF.
    private string WrittenAsWindowsStandardError()
    {
        return Written().Replace("\n", "\r\n", StringComparison.Ordinal);
    }
}
