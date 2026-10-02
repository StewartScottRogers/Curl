using System.Formats.Asn1;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    [DataRow(@"C:\Users\Public\s.sock", @"C:\Users\Public\s.sock")]
    [DataRow(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock", @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl")]
    [DataRow("abs1", "")]
    public void ConnectionOpenedThroughAUnixSocket_NamesTheSocketAsCurl(string hostName, string unixSocketRemoteIp)
    {
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

        Assert.AreEqual($"* Established connection to {hostName} ({unixSocketRemoteIp} port 0) from  port 0 \r\n", WrittenAsWindowsStandardError());
    }

    [TestMethod]
    public void SecondConnectionOpened_SaysEstablished2ndConnectionAndTheControlConnectionDoesNot()
    {
        // Measured 2026-09-30, curl 8.21.0 (mingw, Schannel), BL-944: -v ftp://localhost:47942/dir/file.txt ->
        // * Established connection to localhost (127.0.0.1 port 47942) from 127.0.0.1 port 53197
        // * Established 2nd connection to localhost (127.0.0.1 port 53195) from 127.0.0.1 port 53198
        VerboseTransferEventWriter writer = new(output, writesDataLines: true);

        writer.ReportConnectionOpened(Opened(new IPEndPoint(IPAddress.Loopback, 47942), 53197) with { HostName = "localhost" });
        writer.ReportConnectionOpened(Opened(new IPEndPoint(IPAddress.Loopback, 53195), 53198) with { HostName = "localhost", IsSecondConnection = true });

        Assert.AreEqual(
            "* Established connection to localhost (127.0.0.1 port 47942) from 127.0.0.1 port 53197 \r\n" +
            "* Established 2nd connection to localhost (127.0.0.1 port 53195) from 127.0.0.1 port 53198 \r\n",
            WrittenAsWindowsStandardError());
    }

    [TestMethod]
    public void HttpExchangeWithTraceTime_StampsEachLineStartAsCurl()
    {
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

        Assert.AreEqual(
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
            "12:02:01.494000 * Connection #0 to host 127.0.0.1:18358 left intact\r\n",
            WrittenAsWindowsStandardError());
    }

    [TestMethod]
    public void ResponseHeaderContinuingAnOpenLine_GetsNoStamp()
    {
        VerboseTransferEventWriter writer = new(
            output,
            writesDataLines: true,
            writesTimestamps: true,
            new QueuedTimeProvider(At(1, 1), At(2, 2)),
            TlsBackend.Schannel);

        writer.ReportResponseHeader("HTTP/1.1 2"u8);
        writer.ReportResponseHeader("00 OK\r\n"u8);
        writer.ReportResponseHeader("\r\n"u8);

        Assert.AreEqual("12:02:01.001000 < HTTP/1.1 200 OK\r\n12:02:02.002000 < \r\n", Written());
    }

    [TestMethod]
    public void RequestHeaderContinuingAnOpenLine_StampsOnlyTheLinesItStarts()
    {
        VerboseTransferEventWriter writer = new(
            output,
            writesDataLines: true,
            writesTimestamps: true,
            new QueuedTimeProvider(At(1, 1), At(2, 2)),
            TlsBackend.Schannel);

        writer.ReportRequestHeader("GET / HT"u8);
        writer.ReportRequestHeader("TP/1.1\r\nAccept: */*\r\n"u8);

        Assert.AreEqual("12:02:01.001000 > GET / HTTP/1.1\r\n12:02:02.002000 > Accept: */*\r\n", Written());
    }

    [TestMethod]
    public void DataRunWithTraceTime_ReadsTheClockOnlyForTheLineItWrites()
    {
        VerboseTransferEventWriter writer = new(
            output,
            writesDataLines: true,
            writesTimestamps: true,
            new QueuedTimeProvider(At(1, 1)),
            TlsBackend.Schannel);

        writer.ReportDataReceived("abc"u8);
        writer.ReportDataReceived("de"u8);

        Assert.AreEqual("12:02:01.001000 { [3 bytes data]\n", Written());
    }

    [TestMethod]
    public void HttpsExchange_RendersAsCurl()
    {
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

    // curl 8.21.0 (x86_64-pc-linux-musl) OpenSSL/3.5.7: `curl -v -k -o /dev/null
    // https://host.docker.internal:28356/` against openssl s_server with the self-signed
    // Fixtures/openssl-verbose-localhost.pem, on 2026-09-27 (BL-356 Notes). The
    // "SSL Trust: peer verification disabled" line curl prints after the ALPN offer comes
    // from the connect options, not the handshake, and is not this event's to write.
    [TestMethod]
    public void ReportTlsHandshake_OpenSslSelfSignedExchange_RendersAsCurlsOpenSslBuild()
    {
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

        Assert.AreEqual(
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
            "* Established connection to host.docker.internal (192.168.65.254 port 28356) from 172.17.0.3 port 40032 \n",
            Written());
    }

    // The same server with `--tls-max 1.2 --no-alpn`: OpenSSL's own name for the cipher.
    [TestMethod]
    public void ReportTlsHandshake_OpenSslTls12WithoutAlpn_NamesTheCipherAsOpenSsl()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
            NegotiatedGroupName = "x25519",
            PeerSignatureTypeName = "RSASSA-PSS",
        });

        Assert.AreEqual("* SSL connection using TLSv1.2 / ECDHE-RSA-AES256-GCM-SHA384 / x25519 / RSASSA-PSS\n", Written());
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
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake(["h2", "http/1.1"], null) with
        {
            EchResult = echResult,
        });

        Assert.AreEqual(
            "* ALPN: curl offers h2,http/1.1\n" +
            "* SSL connection using TLSv1.3 / (NONE) / [blank] / UNDEF\n" +
            "* ECH: result: " + echResult + "\n" +
            "* ALPN: server did not agree on a protocol. Uses default.\n",
            Written());
    }

    // A server that answers GREASE with retry_configs: curl traces them straight after the
    // result line (measured with curl 8.21.0 and OpenSSL 4.0.0, BL-1171).
    [TestMethod]
    public void ReportTlsHandshake_OpenSslWithEchRetryConfigLines_WritesThemAfterTheResultLine()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake(["h2", "http/1.1"], null) with
        {
            EchResult = "status is sent GREASE, got retry-configs, inner is NULL, outer is NULL",
            EchRetryConfigLines = ["ECH: retry_configs AAA=", "ECH: retry_configs for NULL from NULL, 0 3"],
        });

        Assert.AreEqual(
            "* ALPN: curl offers h2,http/1.1\n" +
            "* SSL connection using TLSv1.3 / (NONE) / [blank] / UNDEF\n" +
            "* ECH: result: status is sent GREASE, got retry-configs, inner is NULL, outer is NULL\n" +
            "* ECH: retry_configs AAA=\n" +
            "* ECH: retry_configs for NULL from NULL, 0 3\n" +
            "* ALPN: server did not agree on a protocol. Uses default.\n",
            Written());
    }

    [TestMethod]
    public void ReportTlsHandshake_OpenSslFactsUnknown_UsesCurlsFallbacks()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ProtocolVersion = SslProtocols.None,
        });

        Assert.AreEqual("* SSL connection using unknown / (NONE) / [blank] / UNDEF\n", Written());
    }

    [TestMethod]
    [DataRow(0x0300, TlsCipherSuite.TLS_AES_128_GCM_SHA256, "TLSv1.1 / TLS_AES_128_GCM_SHA256")]
    [DataRow(0x00C0, TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA, "TLSv1 / AES128-SHA")]
    [DataRow(0x0030, TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA, "SSLv3 / AES256-SHA")]
    public void ReportTlsHandshake_OpenSslOlderVersions_NamesThemAsOpenSsl(int version, TlsCipherSuite suite, string expected)
    {
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ProtocolVersion = (SslProtocols)version,
            CipherSuite = suite,
        });

        Assert.AreEqual($"* SSL connection using {expected} / [blank] / UNDEF\n", Written());
    }

    [TestMethod]
    [DataRow(true, "0", "SSL certificate verified via OpenSSL.")]
    [DataRow(false, "1", " SSL certificate verification failed, continuing anyway!")]
    public void ReportTlsHandshake_OpenSslNoVerifyCode_FallsBackOnWhetherItWasVerified(bool verified, string code, string verdict)
    {
        using var certificate = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "openssl-verbose-localhost.pem"));

        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ServerCertificate = certificate,
            CertificateVerified = verified,
        });

        StringAssert.EndsWith(Written(), $"*   issuer: CN=localhost\n* OpenSSL verify result: {code}\n* {verdict}\n");
    }

    [TestMethod]
    public void ReportTlsHandshake_OpenSslChainKeyNotDescribed_SkipsItsLevelLine()
    {
        using var undescribed = CertificateWithBrainpoolP160r1Key();

        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl).ReportTlsHandshake(Handshake([], null) with
        {
            ServerCertificate = undescribed,
            PeerCertificateChain = [undescribed],
            CertificateVerifyResult = 0,
        });

        StringAssert.EndsWith(Written(), "*   issuer: CN=x\n* OpenSSL verify result: 0\n* SSL certificate verified via OpenSSL.\n");
    }

    [TestMethod]
    public void ReportTlsHandshake_ServerChoseNoProtocol_SaysItUsesTheDefault()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel).ReportTlsHandshake(Handshake(["http/1.1"], null));

        Assert.AreEqual(
            "* ALPN: curl offers http/1.1\r\n* ALPN: server did not agree on a protocol. Uses default.\r\n",
            WrittenAsWindowsStandardError());
    }

    [TestMethod]
    public void ReportTlsHandshake_SeveralProtocolsOffered_JoinsThemWithCommas()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel).ReportTlsHandshake(Handshake(["h2", "http/1.1"], "h2"));

        Assert.AreEqual("* ALPN: curl offers h2,http/1.1\n* ALPN: server accepted h2\n", Written());
    }

    [TestMethod]
    public void ReportTlsHandshake_NothingOffered_WritesNothing()
    {
        new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel).ReportTlsHandshake(Handshake([], null));

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
    public void ReportTlsData_OnSchannel_WritesNothing()
    {
        VerboseTransferEventWriter writer = new(output, writesDataLines: true, TlsBackend.Schannel);

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
