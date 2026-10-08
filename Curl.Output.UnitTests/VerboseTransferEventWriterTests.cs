using System.Formats.Asn1;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Timestamp_OnWindows_ShowsTheClocksMilliseconds()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("clock", "2026-09-27T23:04:05.1234567Z, timestamps on");
        DateTimeOffset instant = new DateTimeOffset(2026, 9, 27, 23, 4, 5, TimeSpan.Zero).AddTicks(1_234_567);
        VerboseTransferEventWriter writer = new(output, writesDataLines: false, writesTimestamps: true, new QueuedTimeProvider(instant));

        writer.ReportInfo("x");

        string actual = Written();
        Report(diagnostics, "23:04:05.123000 * x\n", actual);
        Assert.AreEqual("23:04:05.123000 * x\n", actual);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Timestamp_OffWindows_ShowsTheClocksMicroseconds()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("clock", "2026-09-27T23:04:05.1234567Z, timestamps on");
        DateTimeOffset instant = new DateTimeOffset(2026, 9, 27, 23, 4, 5, TimeSpan.Zero).AddTicks(1_234_567);
        VerboseTransferEventWriter writer = new(output, writesDataLines: false, writesTimestamps: true, new QueuedTimeProvider(instant));

        writer.ReportInfo("x");

        string actual = Written();
        Report(diagnostics, "23:04:05.123456 * x\n", actual);
        Assert.AreEqual("23:04:05.123456 * x\n", actual);
    }

    [TestMethod]
    public void HttpExchange_RendersAsCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exchange", "plain HTTP GET /f.txt on 127.0.0.1:18228, response 200 with 6 bytes body");
        diagnostics.Bytes("request head", RequestHead18228);
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

        const string expected =
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
            "* Connection #0 to host 127.0.0.1:18228 left intact\r\n";
        string actual = WrittenAsWindowsStandardError();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(@"C:\Users\Public\s.sock", @"C:\Users\Public\s.sock")]
    [DataRow(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock", @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl")]
    [DataRow("abs1", "")]
    public void ConnectionOpenedThroughAUnixSocket_NamesTheSocketAsCurl(string hostName, string unixSocketRemoteIp)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("hostName", hostName);
        diagnostics.Arrange("unixSocketRemoteIp", unixSocketRemoteIp);

        // Measured 2026-09-28, curl 8.21.0 (mingw, Schannel), BL-507: -v --unix-socket C:\Users\Public\s.sock
        // http://localhost/a -> * Established connection to C:\Users\Public\s.sock (C:\Users\Public\s.sock port 0) from  port 0
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = hostName,
            RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0),
            LocalEndPoint = new IPEndPoint(IPAddress.Any, 0),
            UnixSocketRemoteIp = unixSocketRemoteIp,
            ConnectionNumber = 0,
        });

        string expected = $"* Established connection to {hostName} ({unixSocketRemoteIp} port 0) from  port 0 \r\n";
        string actual = WrittenAsWindowsStandardError();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SecondConnectionOpened_SaysEstablished2ndConnectionAndTheControlConnectionDoesNot()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connections", "localhost:47942 then second connection localhost:53195");

        // Measured 2026-09-30, curl 8.21.0 (mingw, Schannel), BL-944: -v ftp://localhost:47942/dir/file.txt ->
        // * Established connection to localhost (127.0.0.1 port 47942) from 127.0.0.1 port 53197
        // * Established 2nd connection to localhost (127.0.0.1 port 53195) from 127.0.0.1 port 53198
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportConnectionOpened(Opened(new IPEndPoint(IPAddress.Loopback, 47942), 53197) with { HostName = "localhost" });
        writer.ReportConnectionOpened(Opened(new IPEndPoint(IPAddress.Loopback, 53195), 53198) with { HostName = "localhost", IsSecondConnection = true });

        const string expected =
            "* Established connection to localhost (127.0.0.1 port 47942) from 127.0.0.1 port 53197 \r\n" +
            "* Established 2nd connection to localhost (127.0.0.1 port 53195) from 127.0.0.1 port 53198 \r\n";
        string actual = WrittenAsWindowsStandardError();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void HttpExchangeWithTraceTime_StampsEachLineStartAsCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exchange", "POST /f.txt body abc on 127.0.0.1:18358 with --trace-time, 12 queued clock readings");

        // Measured 2026-09-27, curl 8.21.0 (mingw, Schannel): Record-CurlExchange.ps1 -Port 18358
        // -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n'
        // -CurlArgs @('-s','-v','--trace-time','-d','abc','http://127.0.0.1:18358/f.txt','-o','NUL').
        VerboseTransferEventWriter writer = new(
            output,
            writesDataLines: true,
            writesTimestamps: true,
            new QueuedTimeProvider(
                At(1, 442),
                At(1, 443),
                At(1, 443),
                At(1, 443),
                At(1, 447),
                At(1, 447),
                At(1, 494),
                At(1, 494),
                At(1, 494),
                At(1, 494),
                At(1, 494),
                At(1, 494)));

        writer.ReportInfo("  Trying 127.0.0.1:18358...");
        writer.ReportConnectionOpened(Opened(new IPEndPoint(IPAddress.Loopback, 18358), 62241));
        writer.ReportInfo("using HTTP/1.x");
        writer.ReportRequestHeader(
            "POST /f.txt HTTP/1.1\r\nHost: 127.0.0.1:18358\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"u8 +
            "Content-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n"u8);
        writer.ReportDataSent("abc"u8);
        writer.ReportInfo("upload completely sent off: 3 bytes");
        writer.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        writer.ReportResponseHeader("Content-Type: text/plain\r\n"u8);
        writer.ReportResponseHeader("Content-Length: 6\r\n"u8);
        writer.ReportResponseHeader("\r\n"u8);
        writer.ReportDataReceived("hello\n"u8);
        writer.ReportInfo("Connection #0 to host 127.0.0.1:18358 left intact");

        const string expected =
            "12:02:01.442000 *   Trying 127.0.0.1:18358...\r\n" +
            "12:02:01.443000 * Established connection to 127.0.0.1 (127.0.0.1 port 18358) from 127.0.0.1 port 62241 \r\n" +
            "12:02:01.443000 * using HTTP/1.x\r\n" +
            "12:02:01.443000 > POST /f.txt HTTP/1.1\r\r\n" +
            "12:02:01.443000 > Host: 127.0.0.1:18358\r\r\n" +
            "12:02:01.443000 > User-Agent: curl/8.21.0\r\r\n" +
            "12:02:01.443000 > Accept: */*\r\r\n" +
            "12:02:01.443000 > Content-Length: 3\r\r\n" +
            "12:02:01.443000 > Content-Type: application/x-www-form-urlencoded\r\r\n" +
            "12:02:01.443000 > \r\r\n" +
            "12:02:01.447000 } [3 bytes data]\r\n" +
            "12:02:01.447000 * upload completely sent off: 3 bytes\r\n" +
            "12:02:01.494000 < HTTP/1.1 200 OK\r\r\n" +
            "12:02:01.494000 < Content-Type: text/plain\r\r\n" +
            "12:02:01.494000 < Content-Length: 6\r\r\n" +
            "12:02:01.494000 < \r\r\n" +
            "12:02:01.494000 { [6 bytes data]\r\n" +
            "12:02:01.494000 * Connection #0 to host 127.0.0.1:18358 left intact\r\n";
        string actual = WrittenAsWindowsStandardError();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ResponseHeaderContinuingAnOpenLine_GetsNoStamp()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("response head chunks", "\"HTTP/1.1 2\", \"00 OK\\r\\n\", \"\\r\\n\" with two clock readings");
        VerboseTransferEventWriter writer = new(
            output,
            writesDataLines: true,
            writesTimestamps: true,
            new QueuedTimeProvider(At(1, 1), At(2, 2)),
            TlsBackend.Schannel);

        writer.ReportResponseHeader("HTTP/1.1 2"u8);
        writer.ReportResponseHeader("00 OK\r\n"u8);
        writer.ReportResponseHeader("\r\n"u8);

        string actual = Written();
        Report(diagnostics, "12:02:01.001000 < HTTP/1.1 200 OK\r\n12:02:02.002000 < \r\n", actual);
        Assert.AreEqual("12:02:01.001000 < HTTP/1.1 200 OK\r\n12:02:02.002000 < \r\n", actual);
    }

    [TestMethod]
    public void RequestHeaderContinuingAnOpenLine_StampsOnlyTheLinesItStarts()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request head chunks", "\"GET / HT\", \"TP/1.1\\r\\nAccept: */*\\r\\n\" with two clock readings");
        VerboseTransferEventWriter writer = new(
            output,
            writesDataLines: true,
            writesTimestamps: true,
            new QueuedTimeProvider(At(1, 1), At(2, 2)),
            TlsBackend.Schannel);

        writer.ReportRequestHeader("GET / HT"u8);
        writer.ReportRequestHeader("TP/1.1\r\nAccept: */*\r\n"u8);

        string actual = Written();
        Report(diagnostics, "12:02:01.001000 > GET / HTTP/1.1\r\n12:02:02.002000 > Accept: */*\r\n", actual);
        Assert.AreEqual("12:02:01.001000 > GET / HTTP/1.1\r\n12:02:02.002000 > Accept: */*\r\n", actual);
    }

    [TestMethod]
    public void DataRunWithTraceTime_ReadsTheClockOnlyForTheLineItWrites()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("data reads", "3 bytes then 2 bytes, one clock reading queued");
        VerboseTransferEventWriter writer = new(
            output,
            writesDataLines: true,
            writesTimestamps: true,
            new QueuedTimeProvider(At(1, 1)),
            TlsBackend.Schannel);

        writer.ReportDataReceived("abc"u8);
        writer.ReportDataReceived("de"u8);

        string actual = Written();
        Report(diagnostics, "12:02:01.001000 { [3 bytes data]\n", actual);
        Assert.AreEqual("12:02:01.001000 { [3 bytes data]\n", actual);
    }

    [TestMethod]
    public void HttpsExchange_RendersAsCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exchange", "Schannel HTTPS GET /f.txt on 127.0.0.1:18229, ALPN http/1.1, response 200 with 6 bytes body");
        diagnostics.Bytes("request head", RequestHead18229);
        VerboseTransferEventWriter writer = new(output, writesDataLines: true, TlsBackend.Schannel);

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

        const string expected =
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
            "* Connection #0 to host 127.0.0.1:18229 left intact\r\n";
        string actual = WrittenAsWindowsStandardError();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    // curl 8.21.0 (x86_64-pc-linux-musl) OpenSSL/3.5.7: `curl -v -k -o /dev/null
    // https://host.docker.internal:28356/` against openssl s_server with the self-signed
    // Fixtures/openssl-verbose-localhost.pem, on 2026-09-27 (BL-356 Notes). The
    // "SSL Trust: peer verification disabled" line curl prints after the ALPN offer comes
    // from the connect options, not the handshake, and is not this event's to write.
    [TestMethod]
    public void ReportTlsHandshake_OpenSslSelfSignedExchange_RendersAsCurlsOpenSslBuild()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL TLS 1.3, offered h2,http/1.1, accepted http/1.1, self-signed fixture, verify result 18");
        using var certificate = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "openssl-verbose-localhost.pem"));
        VerboseTransferEventWriter writer = new(output, writesDataLines: true, TlsBackend.OpenSsl);

        writer.ReportTlsHandshake(Handshake(["h2", "http/1.1"], "http/1.1") with
        {
            CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
            NegotiatedGroupName = "X25519MLKEM768",
            PeerSignatureTypeName = "RSASSA-PSS",
            ServerCertificate = certificate,
            PeerCertificateChain = [certificate],
            CertificateVerifyResult = 18,
        });
        writer.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = "host.docker.internal",
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.65.254"), 28356),
            LocalEndPoint = new IPEndPoint(IPAddress.Parse("172.17.0.3"), 40032),
            ConnectionNumber = 0,
        });

        const string expected =
            "* ALPN: curl offers h2,http/1.1\n" +
            "* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS\n" +
            "* ALPN: server accepted http/1.1\n" +
            "* Server certificate:\n" +
            "*   subject: CN=localhost\n" +
            "*   start date: Sep 27 15:58:54 2026 GMT\n" +
            "*   expire date: Sep 27 15:58:54 2027 GMT\n" +
            "*   issuer: CN=localhost\n" +
            "*   Certificate level 0: Public key type RSA (2048/112 Bits/secBits), signed using sha256WithRSAEncryption\n" +
            "* OpenSSL verify result: 12\n" +
            "*  SSL certificate verification failed, continuing anyway!\n" +
            "* Established connection to host.docker.internal (192.168.65.254 port 28356) from 172.17.0.3 port 40032 \n";
        string actual = Written();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    // The same server with `--tls-max 1.2 --no-alpn`: OpenSSL's own name for the cipher.
    [TestMethod]
    public void ReportTlsHandshake_OpenSslTls12WithoutAlpn_NamesTheCipherAsOpenSsl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL TLS 1.2, ECDHE_RSA_WITH_AES_256_GCM_SHA384, group x25519, no ALPN");
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
            NegotiatedGroupName = "x25519",
            PeerSignatureTypeName = "RSASSA-PSS",
        });

        string actual = Written();
        Report(diagnostics, "* SSL connection using TLSv1.2 / ECDHE-RSA-AES256-GCM-SHA384 / x25519 / RSASSA-PSS\n", actual);
        Assert.AreEqual("* SSL connection using TLSv1.2 / ECDHE-RSA-AES256-GCM-SHA384 / x25519 / RSASSA-PSS\n", actual);
    }

    // curl 8.21.0 on OpenSSL 4.0.0's ECH build writes its ECH result between `SSL connection
    // using` and the ALPN answer (ADR-0359, BL-1170).
    [TestMethod]
    [DataRow("status is sent GREASE, inner is NULL, outer is NULL")]
    [DataRow("status is not configured, inner is NULL, outer is NULL")]
    [DataRow("status is bad name (tolerated without peer verification), inner is curl.test, outer is public.test")]
    [DataRow("status is success, inner is curl.test, outer is public.test")]
    public void ReportTlsHandshake_OpenSslWithAnEchResult_WritesItAfterTheConnectionLine(string echResult)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("echResult", echResult);
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake(["h2", "http/1.1"], null) with
        {
            EchResult = echResult,
        });

        string expected =
            "* ALPN: curl offers h2,http/1.1\n" +
            "* SSL connection using TLSv1.3 / (NONE) / [blank] / UNDEF\n" +
            "* ECH: result: " + echResult + "\n" +
            "* ALPN: server did not agree on a protocol. Uses default.\n";
        string actual = Written();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    // A server that answers GREASE with retry_configs: curl traces them straight after the
    // result line (measured with curl 8.21.0 and OpenSSL 4.0.0, BL-1171).
    [TestMethod]
    public void ReportTlsHandshake_OpenSslWithEchRetryConfigLines_WritesThemAfterTheResultLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, ECH result with two retry_configs lines");
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake(["h2", "http/1.1"], null) with
        {
            EchResult = "status is sent GREASE, got retry-configs, inner is NULL, outer is NULL",
            EchRetryConfigLines = ["ECH: retry_configs AAA=", "ECH: retry_configs for NULL from NULL, 0 3"],
        });

        const string expected =
            "* ALPN: curl offers h2,http/1.1\n" +
            "* SSL connection using TLSv1.3 / (NONE) / [blank] / UNDEF\n" +
            "* ECH: result: status is sent GREASE, got retry-configs, inner is NULL, outer is NULL\n" +
            "* ECH: retry_configs AAA=\n" +
            "* ECH: retry_configs for NULL from NULL, 0 3\n" +
            "* ALPN: server did not agree on a protocol. Uses default.\n";
        string actual = Written();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReportTlsHandshake_OpenSslFactsUnknown_UsesCurlsFallbacks()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, ProtocolVersion=None, no cipher, no ALPN");
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ProtocolVersion = SslProtocols.None,
        });

        string actual = Written();
        Report(diagnostics, "* SSL connection using unknown / (NONE) / [blank] / UNDEF\n", actual);
        Assert.AreEqual("* SSL connection using unknown / (NONE) / [blank] / UNDEF\n", actual);
    }

    [TestMethod]
    [DataRow(0x0300, TlsCipherSuite.TLS_AES_128_GCM_SHA256, "TLSv1.1 / TLS_AES_128_GCM_SHA256")]
    [DataRow(0x00C0, TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA, "TLSv1 / AES128-SHA")]
    [DataRow(0x0030, TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA, "SSLv3 / AES256-SHA")]
    public void ReportTlsHandshake_OpenSslOlderVersions_NamesThemAsOpenSsl(int version, TlsCipherSuite suite, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("version", "0x" + version.ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        diagnostics.Arrange("suite", suite);
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ProtocolVersion = (SslProtocols)version,
            CipherSuite = suite,
        });

        string expectedLine = $"* SSL connection using {expected} / [blank] / UNDEF\n";
        string actual = Written();
        Report(diagnostics, expectedLine, actual);
        Assert.AreEqual(expectedLine, actual);
    }

    [TestMethod]
    [DataRow(true, "0", "SSL certificate verified via OpenSSL.")]
    [DataRow(false, "1", " SSL certificate verification failed, continuing anyway!")]
    public void ReportTlsHandshake_OpenSslNoVerifyCode_FallsBackOnWhetherItWasVerified(bool verified, string code, string verdict)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("verified", verified);
        diagnostics.Arrange("expected code", code);
        diagnostics.Arrange("expected verdict", verdict);
        using var certificate = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "openssl-verbose-localhost.pem"));

        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ServerCertificate = certificate,
            CertificateVerified = verified,
        });

        string expectedEnd = $"*   issuer: CN=localhost\n* OpenSSL verify result: {code}\n* {verdict}\n";
        string actual = Written();
        diagnostics.Act("written", actual);
        diagnostics.Assert("ends with expected tail", true, actual.EndsWith(expectedEnd, StringComparison.Ordinal));
        StringAssert.EndsWith(actual, expectedEnd);
    }

    [TestMethod]
    public void ReportTlsHandshake_OpenSslChainKeyNotDescribed_SkipsItsLevelLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("certificate", "self-made CN=x with a brainpoolP160r1 key, verify result 0");
        using var undescribed = CertificateWithBrainpoolP160r1Key();

        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ServerCertificate = undescribed,
            PeerCertificateChain = [undescribed],
            CertificateVerifyResult = 0,
        });

        const string expectedEnd = "*   issuer: CN=x\n* OpenSSL verify result: 0\n* SSL certificate verified via OpenSSL.\n";
        string actual = Written();
        diagnostics.Act("written", actual);
        diagnostics.Assert("ends with expected tail", true, actual.EndsWith(expectedEnd, StringComparison.Ordinal));
        StringAssert.EndsWith(actual, expectedEnd);
    }

    [TestMethod]
    public void ReportTlsHandshake_ServerChoseNoProtocol_SaysItUsesTheDefault()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, offered http/1.1, nothing negotiated");
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel).ReportTlsHandshake(Handshake(["http/1.1"], null));

        const string expected = "* ALPN: curl offers http/1.1\r\n* ALPN: server did not agree on a protocol. Uses default.\r\n";
        string actual = WrittenAsWindowsStandardError();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(
            expected,
            actual);
    }

    [TestMethod]
    public void ReportTlsHandshake_SeveralProtocolsOffered_JoinsThemWithCommas()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, offered h2,http/1.1, negotiated h2");
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel).ReportTlsHandshake(Handshake(["h2", "http/1.1"], "h2"));

        string actual = Written();
        Report(diagnostics, "* ALPN: curl offers h2,http/1.1\n* ALPN: server accepted h2\n", actual);
        Assert.AreEqual("* ALPN: curl offers h2,http/1.1\n* ALPN: server accepted h2\n", actual);
    }

    [TestMethod]
    public void ReportTlsHandshake_NothingOffered_WritesNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, nothing offered, nothing negotiated");
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel).ReportTlsHandshake(Handshake([], null));

        string actual = Written();
        Report(diagnostics, string.Empty, actual);
        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    [DataRow("http", false, "127.0.0.1", "* Reusing existing http: connection with host 127.0.0.1\n")]
    [DataRow("https", false, "example.com", "* Reusing existing https: connection with host example.com\n")]
    [DataRow("http", true, "127.0.0.1", "* Reusing existing http: connection with proxy 127.0.0.1\n")]
    public void ReportConnectionReused_WritesCurlsReuseLine(string scheme, bool isProxy, string hostName, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scheme", scheme);
        diagnostics.Arrange("isProxy", isProxy);
        diagnostics.Arrange("hostName", hostName);
        new VerboseTransferEventWriter(output, writesDataLines: true).ReportConnectionReused(new ConnectionReusedEvent
        {
            Scheme = scheme,
            IsProxy = isProxy,
            HostName = hostName,
            Port = 18231,
            ConnectionNumber = 0,
        });

        string actual = Written();
        Report(diagnostics, expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReportDataReceived_SeveralReadsInARow_WritesOneLineWithTheFirstCount()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("reads", "102357 bytes then 97643 bytes");
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportDataReceived(new byte[102357]);
        writer.ReportDataReceived(new byte[97643]);

        string actual = Written();
        Report(diagnostics, "{ [102357 bytes data]\n", actual);
        Assert.AreEqual("{ [102357 bytes data]\n", actual);
    }

    [TestMethod]
    public void ReportDataReceived_AfterAnotherEvent_WritesANewLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("events", "data 3, response header CRLF, data 4, info x, data 5");
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportDataReceived(new byte[3]);
        writer.ReportResponseHeader("\r\n"u8);
        writer.ReportDataReceived(new byte[4]);
        writer.ReportInfo("x");
        writer.ReportDataReceived(new byte[5]);

        string actual = Written();
        Report(diagnostics, "{ [3 bytes data]\n< \r\n{ [4 bytes data]\n* x\n{ [5 bytes data]\n", actual);
        Assert.AreEqual("{ [3 bytes data]\n< \r\n{ [4 bytes data]\n* x\n{ [5 bytes data]\n", actual);
    }

    [TestMethod]
    public void ReportDataSent_WritesTheSentDataLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sent data", "hi");
        new VerboseTransferEventWriter(output, writesDataLines: true).ReportDataSent("hi"u8);

        string actual = Written();
        Report(diagnostics, "} [2 bytes data]\n", actual);
        Assert.AreEqual("} [2 bytes data]\n", actual);
    }

    [TestMethod]
    public void DataEvents_DataLinesNotWritten_WriteNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("writesDataLines", false);
        VerboseTransferEventWriter writer = new(output, writesDataLines: false);

        writer.ReportDataSent("hi"u8);
        writer.ReportDataReceived("hello\n"u8);

        string actual = Written();
        Report(diagnostics, string.Empty, actual);
        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    public void ReportTlsData_OnSchannel_WritesNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("tls data", "01 02 03 sent and received on Schannel");
        VerboseTransferEventWriter writer = new(output, writesDataLines: true, TlsBackend.Schannel);

        writer.ReportTlsData([1, 2, 3], sent: true);
        writer.ReportTlsData([1, 2, 3], sent: false);

        string actual = Written();
        Report(diagnostics, string.Empty, actual);
        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    public void ReportRequestHeader_HeadSplitMidLine_ContinuesTheLineWithoutAPrefix()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request head chunks", "\"GET / HTTP/1.1\\r\\nHo\", \"st: a\\r\\n\\r\\n\"");
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportRequestHeader("GET / HTTP/1.1\r\nHo"u8);
        writer.ReportRequestHeader("st: a\r\n\r\n"u8);

        string actual = Written();
        Report(diagnostics, "> GET / HTTP/1.1\r\n> Host: a\r\n> \r\n", actual);
        Assert.AreEqual("> GET / HTTP/1.1\r\n> Host: a\r\n> \r\n", actual);
    }

    [TestMethod]
    public void ReportRequestHeader_Empty_WritesNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request head", "empty");
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportRequestHeader([]);

        string actual = Written();
        Report(diagnostics, string.Empty, actual);
        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    public void ReportResponseHeader_LineSplitAcrossCalls_ContinuesTheLineWithoutAPrefix()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("response head chunks", "\"HTTP/1.1 2\", \"00 OK\\r\\n\", empty, \"\\r\\n\"");
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportResponseHeader("HTTP/1.1 2"u8);
        writer.ReportResponseHeader("00 OK\r\n"u8);
        writer.ReportResponseHeader([]);
        writer.ReportResponseHeader("\r\n"u8);

        string actual = Written();
        Report(diagnostics, "< HTTP/1.1 200 OK\r\n< < \r\n", actual);
        Assert.AreEqual("< HTTP/1.1 200 OK\r\n< < \r\n", actual);
    }

    // A certificate whose key is on brainpoolP160r1, a curve OpenSslCertificateText does not
    // describe. The key is written by hand and signed with a P-256 key, because macOS cannot
    // generate a brainpool key (BL-426).
    private static X509Certificate2 CertificateWithBrainpoolP160r1Key()
    {
        AsnWriter curve = new(AsnEncodingRules.DER);
        curve.WriteObjectIdentifier("1.3.36.3.3.2.8.1.1.1");
        var uncompressedPoint = new byte[41];
        uncompressedPoint[0] = 0x04;
        PublicKey publicKey = new(new Oid("1.2.840.10045.2.1"), new AsnEncodedData(curve.Encode()), new AsnEncodedData(uncompressedPoint));

        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        X500DistinguishedName name = new("CN=x");
        return new CertificateRequest(name, publicKey, HashAlgorithmName.SHA256)
            .Create(name, X509SignatureGenerator.CreateForECDsa(signingKey), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), [1]);
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

    private static void Report(TestDiagnostics diagnostics, string expected, string actual)
    {
        diagnostics.Act("written", actual);
        diagnostics.Diff("written", expected, actual);
    }

    private string Written()
    {
        return Encoding.UTF8.GetString(output.ToArray());
    }

    // curl's standard error is in text mode on Windows, so every line feed is written as CR LF.
    private static DateTimeOffset At(int second, int millisecond)
    {
        return new DateTimeOffset(2026, 9, 27, 12, 2, second, millisecond, TimeSpan.Zero);
    }

    private string WrittenAsWindowsStandardError()
    {
        return Written().Replace("\n", "\r\n", StringComparison.Ordinal);
    }
}
