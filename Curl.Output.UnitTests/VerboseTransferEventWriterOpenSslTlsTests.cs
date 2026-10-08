using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="VerboseTransferEventWriter"/>'s TLS message, <c>SSL Trust</c>,
/// host-name and <c>Proxy certificate:</c> lines to what curl 8.21.0 (x86_64-pc-linux-musl,
/// OpenSSL/3.5.7, <c>curlimages/curl:8.21.0</c>) wrote for <c>-v</c> on 2026-09-28 against
/// Git for Windows' <c>openssl s_server -www</c>; the commands are in BL-405's Notes.
/// </summary>
[TestClass]
public sealed class VerboseTransferEventWriterOpenSslTlsTests
{
    private const int Tls12 = 0x0303;
    private const int Tls13 = 0x0304;

    private const string SelfSignedLevelLine =
        "*   Certificate level 0: Public key type RSA (2048/112 Bits/secBits), signed using sha256WithRSAEncryption\n";

    private readonly MemoryStream output = new();

    // `curl -v -k -o /dev/null https://host.docker.internal:28405/`, up to the handshake.
    public TestContext TestContext { get; set; } = null!;

    private static void WriteTextDiagnostics(TestDiagnostics diagnostics, string label, string expected, string actual)
    {
        diagnostics.Act(label, actual);
        diagnostics.Diff(label, expected, actual);
        diagnostics.Assert(label, expected, actual);
    }

    [TestMethod]
    public void Tls13HandshakeWithoutVerification_RendersRecordsAndTrustAsCurlsOpenSslBuild()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "Tls13HandshakeWithoutVerification_RendersRecordsAndTrustAsCurlsOpenSslBuild");
        var writer = OpenSslWriter();

        writer.ReportInfo("ALPN: curl offers h2,http/1.1");
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.RecordHeader, sent: true, 5));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: true, 1566, 1));
        writer.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.RecordHeader, sent: false, 5));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: false, 1210, 2));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.RecordHeader, sent: false, 5));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.ChangeCipherSpec, sent: false, 1, 1));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.RecordHeader, sent: false, 5));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.InnerContentType, sent: false, 1, 22));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: false, 21, 8));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: false, 822, 11));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: false, 264, 15));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: false, 52, 20));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.RecordHeader, sent: true, 5));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.ChangeCipherSpec, sent: true, 1, 1));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.RecordHeader, sent: true, 5));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.InnerContentType, sent: true, 1, 22));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: true, 52, 20));

        string expectedText = "* ALPN: curl offers h2,http/1.1\n" +
            "} [5 bytes data]\n" +
            "* TLSv1.3 (OUT), TLS handshake, Client hello (1):\n" +
            "} [1566 bytes data]\n" +
            "* SSL Trust: peer verification disabled\n" +
            "{ [5 bytes data]\n" +
            "* TLSv1.3 (IN), TLS handshake, Server hello (2):\n" +
            "{ [1210 bytes data]\n" +
            "* TLSv1.3 (IN), TLS change cipher, Change cipher spec (1):\n" +
            "{ [1 bytes data]\n" +
            "* TLSv1.3 (IN), TLS handshake, Encrypted Extensions (8):\n" +
            "{ [21 bytes data]\n" +
            "* TLSv1.3 (IN), TLS handshake, Certificate (11):\n" +
            "{ [822 bytes data]\n" +
            "* TLSv1.3 (IN), TLS handshake, CERT verify (15):\n" +
            "{ [264 bytes data]\n" +
            "* TLSv1.3 (IN), TLS handshake, Finished (20):\n" +
            "{ [52 bytes data]\n" +
            "* TLSv1.3 (OUT), TLS change cipher, Change cipher spec (1):\n" +
            "} [1 bytes data]\n" +
            "* TLSv1.3 (OUT), TLS handshake, Finished (20):\n" +
            "} [52 bytes data]\n";
        string actualText = Written();
        WriteTextDiagnostics(diagnostics, "output", expectedText, actualText);

        Assert.AreEqual(expectedText, actualText);
    }

    // The same exchange after the request: the session tickets and the server's close notify.
    [TestMethod]
    public void Tls13SessionTicketsAndCloseNotify_RenderAsCurlsOpenSslBuild()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "Tls13SessionTicketsAndCloseNotify_RenderAsCurlsOpenSslBuild");
        var writer = OpenSslWriter();

        writer.ReportTlsMessage(Message(Tls13, TlsContentType.RecordHeader, sent: false, 5));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: false, 249, 4));
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: false, 249, 4));
        writer.ReportInfo("HTTP 1.0, assume close after body");
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Alert, sent: false, 2, 1, 0));

        string expectedText = "{ [5 bytes data]\n" +
            "* TLSv1.3 (IN), TLS handshake, Newsession Ticket (4):\n" +
            "{ [249 bytes data]\n" +
            "* TLSv1.3 (IN), TLS handshake, Newsession Ticket (4):\n" +
            "{ [249 bytes data]\n" +
            "* HTTP 1.0, assume close after body\n" +
            "* TLSv1.3 (IN), TLS alert, close notify (256):\n" +
            "{ [2 bytes data]\n";
        string actualText = Written();
        WriteTextDiagnostics(diagnostics, "output", expectedText, actualText);

        Assert.AreEqual(expectedText, actualText);
    }

    // `curl -v -k --tls-max 1.2 -o /dev/null https://host.docker.internal:28405/`.
    [TestMethod]
    public void Tls12Handshake_RendersRecordsAsCurlsOpenSslBuild()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "Tls12Handshake_RendersRecordsAsCurlsOpenSslBuild");
        var writer = OpenSslWriter();

        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: true, 229, 1));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: false, 108, 2));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: false, 819, 11));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: false, 300, 12));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: false, 4, 14));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: true, 37, 16));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.ChangeCipherSpec, sent: true, 1, 1));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: true, 16, 20));
        writer.ReportTlsMessage(Message(Tls12, TlsContentType.Handshake, sent: false, 16, 20));

        string expectedText = "* TLSv1.2 (OUT), TLS handshake, Client hello (1):\n" +
            "} [229 bytes data]\n" +
            "* TLSv1.2 (IN), TLS handshake, Server hello (2):\n" +
            "{ [108 bytes data]\n" +
            "* TLSv1.2 (IN), TLS handshake, Certificate (11):\n" +
            "{ [819 bytes data]\n" +
            "* TLSv1.2 (IN), TLS handshake, Server key exchange (12):\n" +
            "{ [300 bytes data]\n" +
            "* TLSv1.2 (IN), TLS handshake, Server finished (14):\n" +
            "{ [4 bytes data]\n" +
            "* TLSv1.2 (OUT), TLS handshake, Client key exchange (16):\n" +
            "} [37 bytes data]\n" +
            "* TLSv1.2 (OUT), TLS change cipher, Change cipher spec (1):\n" +
            "} [1 bytes data]\n" +
            "* TLSv1.2 (OUT), TLS handshake, Finished (20):\n" +
            "} [16 bytes data]\n" +
            "* TLSv1.2 (IN), TLS handshake, Finished (20):\n" +
            "{ [16 bytes data]\n";
        string actualText = Written();
        WriteTextDiagnostics(diagnostics, "output", expectedText, actualText);

        Assert.AreEqual(expectedText, actualText);
    }

    [TestMethod]
    public void TlsMessage_DataLinesHidden_WritesOnlyTheLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsMessage_DataLinesHidden_WritesOnlyTheLine");
        new VerboseTransferEventWriter(output, writesDataLines: false, TlsBackend.OpenSsl)
            .ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: true, 1566, 1));

        string expectedText = "* TLSv1.3 (OUT), TLS handshake, Client hello (1):\n";
        string actualText = Written();
        WriteTextDiagnostics(diagnostics, "output", expectedText, actualText);

        Assert.AreEqual(expectedText, actualText);
    }

    // `--cacert /w/cert.pem --connect-to localhost:28405:host.docker.internal:28405
    // https://localhost:28405/` and the same with `--capath /w`, and with no CA option at all.
    [TestMethod]
    [DataRow("/w/cert.pem", null, "*   CAfile: /w/cert.pem\n")]
    [DataRow("/w/cert.pem", "/w", "*   CAfile: /w/cert.pem\n*   CApath: /w\n")]
    [DataRow("/cacert.pem", null, "*   CAfile: /cacert.pem\n")]
    public void TlsTrust_TrustAnchors_RenderAsCurlsOpenSslBuild(string caFile, string? caDirectory, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsTrust_TrustAnchors_RenderAsCurlsOpenSslBuild");
        diagnostics.Arrange("caFile", caFile);
        diagnostics.Arrange("caDirectory", caDirectory);
        diagnostics.Arrange("expected", expected);
        OpenSslWriter().ReportTlsTrust(new TlsTrustEvent
        {
            VerifiesPeer = true,
            CaCertificateFile = caFile,
            CaCertificateDirectory = caDirectory,
        });

        string expectedText = "* SSL Trust Anchors:\n" + expected;
        string actualText = Written();
        WriteTextDiagnostics(diagnostics, "output", expectedText, actualText);

        Assert.AreEqual(expectedText, actualText);
    }

    // `--cacert` with https://localhost:28405/ and https://127.0.0.1:28405/: the host-name line
    // goes between the certificate block and the verify result.
    [TestMethod]
    [DataRow("localhost", "*   subjectAltName: \"localhost\" matches cert's \"localhost\"\n")]
    [DataRow("127.0.0.1", "*   subjectAltName: \"127.0.0.1\" matches cert's IP address!\n")]
    public void TlsHandshake_HostNameMatches_WritesTheMatchBeforeTheVerifyResult(string hostName, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsHandshake_HostNameMatches_WritesTheMatchBeforeTheVerifyResult");
        diagnostics.Arrange("hostName", hostName);
        diagnostics.Arrange("expected", expected);
        using var certificate = MeasuredCertificate();

        OpenSslWriter().ReportTlsHandshake(Handshake(certificate) with { VerifiedHostName = hostName, CertificateVerifyResult = 0 });

        string actualText = Written();
        string expectedPart = "*   Certificate level 0: Public key type RSA (2048/112 Bits/secBits), signed using sha256WithRSAEncryption\n" +
            expected +
            "* OpenSSL verify result: 0\n" +
            "* SSL certificate verified via OpenSSL.\n";
        diagnostics.Act("output", actualText);
        diagnostics.Assert("output EndsWith expected part", true, actualText.EndsWith(expectedPart, StringComparison.Ordinal));

        StringAssert.EndsWith(actualText, expectedPart);
    }

    // `--cacert` with https://wrong.test:28405/: curl stops before the verify result.
    [TestMethod]
    public void TlsHandshake_HostNameDoesNotMatch_WritesTheMismatchAndNoVerifyResult()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsHandshake_HostNameDoesNotMatch_WritesTheMismatchAndNoVerifyResult");
        using var certificate = MeasuredCertificate();

        OpenSslWriter().ReportTlsHandshake(Handshake(certificate) with { VerifiedHostName = "wrong.test", CertificateVerifyResult = 0 });

        string actualText = Written();
        string expectedPart = "signed using sha256WithRSAEncryption\n*  subjectAltName does not match hostname wrong.test\n";
        diagnostics.Act("output", actualText);
        diagnostics.Assert("output EndsWith expected part", true, actualText.EndsWith(expectedPart, StringComparison.Ordinal));

        StringAssert.EndsWith(actualText, expectedPart);
    }

    // A certificate with no subjectAltName, CN=localhost, as https://localhost:28406/ and
    // https://other:28406/: a match writes the common name, a mismatch nothing.
    [TestMethod]
    [DataRow("localhost", "*  common name: localhost (matched)\n* OpenSSL verify result: 0\n* SSL certificate verified via OpenSSL.\n")]
    [DataRow("other", "")]
    public void TlsHandshake_CertificateWithoutAlternativeNames_ChecksTheCommonName(string hostName, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsHandshake_CertificateWithoutAlternativeNames_ChecksTheCommonName");
        diagnostics.Arrange("hostName", hostName);
        diagnostics.Arrange("expected", expected);
        using var certificate = SelfSigned("CN=localhost", alternativeNames: null);

        OpenSslWriter().ReportTlsHandshake(Handshake(certificate) with { VerifiedHostName = hostName, CertificateVerifyResult = 0 });

        string actualText = Written();
        string expectedPart = "*   issuer: CN=localhost\n" + SelfSignedLevelLine + expected;
        diagnostics.Act("output", actualText);
        diagnostics.Assert("output EndsWith expected part", true, actualText.EndsWith(expectedPart, StringComparison.Ordinal));

        StringAssert.EndsWith(actualText, expectedPart);
    }

    // subjectAltName other.test and *.example.test, as https://A.Example.test:28407/.
    [TestMethod]
    public void TlsHandshake_WildcardAlternativeName_WritesTheNameAsGivenAndThePattern()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsHandshake_WildcardAlternativeName_WritesTheNameAsGivenAndThePattern");
        using var certificate = SelfSigned("CN=wild", ["other.test", "*.example.test"]);

        OpenSslWriter().ReportTlsHandshake(Handshake(certificate) with { VerifiedHostName = "A.Example.test", CertificateVerifyResult = 0 });

        string actualText = Written();
        string expectedPart = "*   issuer: CN=wild\n" + SelfSignedLevelLine + "*   subjectAltName: \"A.Example.test\" matches cert's \"*.example.test\"\n* OpenSSL verify result: 0\n";
        diagnostics.Act("output", actualText);
        diagnostics.Assert("output Contains expected part", true, actualText.Contains(expectedPart, StringComparison.Ordinal));

        StringAssert.Contains(actualText, expectedPart);
    }

    // `-sv --proxy-insecure -x https://host.docker.internal:28405 http://example.test/`.
    [TestMethod]
    public void TlsHandshake_Proxy_SaysProxyCertificate()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsHandshake_Proxy_SaysProxyCertificate");
        using var certificate = MeasuredCertificate();

        OpenSslWriter().ReportTlsHandshake(Handshake(certificate) with { IsProxy = true, CertificateVerifyResult = 18 });

        string actualText = Written();
        string expectedPart = "* ALPN: server accepted http/1.1\n* Proxy certificate:\n*   subject: CN=localhost\n";
        diagnostics.Act("output", actualText);
        diagnostics.Assert("output Contains expected part", true, actualText.Contains(expectedPart, StringComparison.Ordinal));

        StringAssert.Contains(actualText, expectedPart);
    }

    [TestMethod]
    public void TlsEvents_Schannel_WriteOnlyTheSchannelTrustLines()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scenario", "TlsEvents_Schannel_WriteOnlyTheSchannelTrustLines");
        var writer = new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel);

        writer.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false, TargetsIpAddress = true });
        writer.ReportTlsMessage(Message(Tls13, TlsContentType.Handshake, sent: true, 1566, 1));
        writer.ReportTlsData(new byte[5], sent: false);

        string expectedText = "* schannel: disabled automatic use of client certificate\n" +
            "* schannel: using IP address, SNI is not supported by OS.\n";
        string actualText = Written();
        WriteTextDiagnostics(diagnostics, "output", expectedText, actualText);

        Assert.AreEqual(expectedText, actualText);
    }

    private static TlsMessageEvent Message(int version, TlsContentType contentType, bool sent, int length, params byte[] start)
    {
        var bytes = new byte[length];
        start.CopyTo(bytes, 0);
        return new TlsMessageEvent { ProtocolVersion = version, ContentType = contentType, Sent = sent, Bytes = bytes };
    }

    private static X509Certificate2 MeasuredCertificate()
    {
        return X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "openssl-verbose-localhost.pem"));
    }

    private static X509Certificate2 SelfSigned(string subject, string[]? alternativeNames)
    {
        using var key = RSA.Create(2048);
        CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (alternativeNames is not null)
        {
            SubjectAlternativeNameBuilder names = new();
            foreach (var name in alternativeNames)
            {
                names.AddDnsName(name);
            }

            request.CertificateExtensions.Add(names.Build());
        }

        return request.CreateSelfSigned(new DateTimeOffset(2026, 9, 28, 2, 38, 6, TimeSpan.Zero), new DateTimeOffset(2027, 9, 28, 2, 38, 6, TimeSpan.Zero));
    }

    private static TlsHandshakeEvent Handshake(X509Certificate2 certificate)
    {
        return new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
            NegotiatedApplicationProtocol = "http/1.1",
            OfferedApplicationProtocols = ["h2", "http/1.1"],
            ServerCertificate = certificate,
            CertificateVerified = true,
            NegotiatedGroupName = "X25519MLKEM768",
            PeerSignatureTypeName = "RSASSA-PSS",
            PeerCertificateChain = [certificate],
        };
    }

    private VerboseTransferEventWriter OpenSslWriter()
    {
        return new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.OpenSsl);
    }

    private string Written()
    {
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
