using System.ComponentModel;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="TlsFailureMessages" /> from recorded exceptions and chains built here, so
/// both builds' text is pinned on every platform: the measured lines ADR-0009 records,
/// and the rule it gives for the cases it did not measure.
/// </summary>
[TestClass]
public sealed partial class TlsFailureMessagesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SchannelSslConnectError_WithTheMeasuredSecurityStatus_IsTheMeasuredLine()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new Win32Exception(unchecked((int)0x80090302), "The function requested is not supported"));

        Diagnostics.Arrange("exception", "AuthenticationException over Win32Exception 0x80090302 'The function requested is not supported'");

        var message = TlsFailureMessages.SchannelSslConnectError(exception);

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message",
            "schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported",
            message);

        Assert.AreEqual(
            "schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported",
            message);
    }

    [TestMethod]
    public void SchannelSslConnectError_UnsupportedFunctionWhenOfferingOnlyVersionsBelowTls12_IsTheMeasuredHandshakeNotReceivedLine()
    {
        // curl 8.21.0 (Schannel) --tlsv1.1 --tls-max 1.1 against a TLS 1.2 server, 2026-09-28 (BL-502).
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new Win32Exception(unchecked((int)0x80090302), "The function requested is not supported"));

        Diagnostics.Arrange("exception", "AuthenticationException over Win32Exception 0x80090302");
        Diagnostics.Arrange("offersOnlyVersionsBelowTls12", true);

        var message = TlsFailureMessages.SchannelSslConnectError(exception, offersOnlyVersionsBelowTls12: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "schannel: failed to receive handshake, SSL/TLS connection failed", message);

        Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", message);
    }

    [TestMethod]
    public void SchannelSslConnectError_ServerRefusalWhenOfferingOnlyVersionsBelowTls12_IsTheMeasuredHandshakeNotReceivedLine()
    {
        // curl 8.21.0 (Schannel) --tls-max 1.1 against a TLS 1.2 server, 2026-09-28 (BL-502).
        var exception = new AuthenticationException(
            "Authentication failed because the remote party sent a TLS alert: 'ProtocolVersion'.",
            new Win32Exception(unchecked((int)0x80090326), "The message received was unexpected or badly formatted."));

        Diagnostics.Arrange("exception", "AuthenticationException over Win32Exception 0x80090326, TLS alert 'ProtocolVersion'");
        Diagnostics.Arrange("offersOnlyVersionsBelowTls12", true);

        var message = TlsFailureMessages.SchannelSslConnectError(exception, offersOnlyVersionsBelowTls12: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "schannel: failed to receive handshake, SSL/TLS connection failed", message);

        Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", message);
    }

    [TestMethod]
    public void SchannelSslConnectError_SocketErrorWhenOfferingOnlyVersionsBelowTls12_IsTheRecvFailure()
    {
        Diagnostics.Arrange("socket error", SocketError.ConnectionReset);
        Diagnostics.Arrange("offersOnlyVersionsBelowTls12", true);

        var message = TlsFailureMessages.SchannelSslConnectError(
            ResetDuringHandshake(SocketError.ConnectionReset), offersOnlyVersionsBelowTls12: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "Recv failure: Connection was reset", message);

        Assert.AreEqual("Recv failure: Connection was reset", message);
    }

    [TestMethod]
    public void OpenSslSslConnectError_NoProtocolsAvailable_IsTheMeasuredLine()
    {
        // curl 8.18.0 (OpenSSL 3.5.5, Ubuntu) --tls-max 1.1 and --tlsv1.0 --tls-max 1.0, 2026-09-28 (BL-502);
        // SslStream on the same OpenSSL throws this chain for a TLS 1.0/1.1-only client.
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new InvalidOperationException(
                "SSL Handshake failed with OpenSSL error - SSL_ERROR_SSL.",
                new CryptographicException("error:0A0000BF:SSL routines::no protocols available")));

        Diagnostics.Arrange("innermost exception", "error:0A0000BF:SSL routines::no protocols available");

        var message = TlsFailureMessages.OpenSslSslConnectError(exception);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "TLS connect error: error:0A0000BF:SSL routines::no protocols available", message);

        Assert.AreEqual("TLS connect error: error:0A0000BF:SSL routines::no protocols available", message);
    }

    [TestMethod]
    public void SchannelSslConnectError_WithASecurityStatusCurlDoesNotName_SaysUnknownError()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new Win32Exception(unchecked((int)0x8009035D), "Some status."));

        Diagnostics.Arrange("exception", "AuthenticationException over Win32Exception 0x8009035D 'Some status.'");

        var message = TlsFailureMessages.SchannelSslConnectError(exception);

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "schannel: next InitializeSecurityContext failed: Unknown error (0x8009035D) - Some status.", message);

        Assert.AreEqual("schannel: next InitializeSecurityContext failed: Unknown error (0x8009035D) - Some status.", message);
    }

    [TestMethod]
    public void SchannelSslConnectError_WhenTheServerClosesMidHandshake_IsTheMeasuredHandshakeNotReceivedLine()
    {
        Diagnostics.Arrange("exception", "IOException 'Received an unexpected EOF or 0 bytes from the transport stream.'");

        var message = TlsFailureMessages.SchannelSslConnectError(
            new IOException("Received an unexpected EOF or 0 bytes from the transport stream."));

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "schannel: failed to receive handshake, SSL/TLS connection failed", message);

        Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", message);
    }

    [TestMethod]
    public void OpenSslSslConnectError_WithTheMeasuredOpenSslErrorString_IsTheMeasuredLine()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new InvalidOperationException(
                "SSL Handshake failed with OpenSSL error - SSL_ERROR_SSL.",
                new CryptographicException("error:0A00042E:SSL routines::tlsv1 alert protocol version")));

        Diagnostics.Arrange("innermost exception", "error:0A00042E:SSL routines::tlsv1 alert protocol version");

        var message = TlsFailureMessages.OpenSslSslConnectError(exception);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version", message);

        Assert.AreEqual("TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version", message);
    }

    // What SslStream throws when the server closes after the ClientHello (BL-150).
    [TestMethod]
    public void OpenSslSslConnectError_WhenTheServerClosesMidHandshake_IsTheMeasuredUnexpectedEofLine()
    {
        var exception = new IOException("Received an unexpected EOF or 0 bytes from the transport stream.");

        Diagnostics.Arrange("exception", "IOException 'Received an unexpected EOF or 0 bytes from the transport stream.'");

        var message = TlsFailureMessages.OpenSslSslConnectError(exception);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "TLS connect error: error:0A000126:SSL routines::unexpected eof while reading", message);

        Assert.AreEqual("TLS connect error: error:0A000126:SSL routines::unexpected eof while reading", message);
    }

    [TestMethod]
    public void OpenSslSslConnectError_WithNoOpenSslErrorStringAndNoEndOfStream_UsesTheInnermostMessage()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new InvalidOperationException("Some failure."));

        Diagnostics.Arrange("innermost exception", "Some failure.");

        var message = TlsFailureMessages.OpenSslSslConnectError(exception);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "TLS connect error: Some failure.", message);

        Assert.AreEqual("TLS connect error: Some failure.", message);
    }

    // BL-369: what SslStream throws when the server resets the connection mid-handshake,
    // measured against curl 8.21.0 in each build.
    [TestMethod]
    [DataRow(SocketError.ConnectionReset, "Recv failure: Connection was reset")]
    [DataRow(SocketError.ConnectionAborted, "Recv failure: Connection was aborted")]
    public void SchannelSslConnectError_WhenTheServerResetsMidHandshake_IsTheMeasuredRecvFailureLine(
        SocketError socketError,
        string expected)
    {
        Diagnostics.Arrange("socket error", socketError);

        var message = TlsFailureMessages.SchannelSslConnectError(ResetDuringHandshake(socketError));

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", expected, message);

        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    public void OpenSslSslConnectError_WhenTheServerResetsMidHandshake_IsTheMeasuredRecvFailureLine()
    {
        Diagnostics.Arrange("socket error", SocketError.ConnectionReset);

        var message = TlsFailureMessages.OpenSslSslConnectError(ResetDuringHandshake(SocketError.ConnectionReset));

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "Recv failure: Connection reset by peer", message);

        Assert.AreEqual("Recv failure: Connection reset by peer", message);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public void SslConnectError_WithASocketErrorCurlIsNotMeasuredFor_UsesTheSocketErrorsOwnMessage(bool schannelBuild)
    {
        var exception = ResetDuringHandshake(SocketError.NetworkDown);

        Diagnostics.Arrange("socket error, Schannel build", $"{SocketError.NetworkDown}, {schannelBuild}");

        var message = schannelBuild
            ? TlsFailureMessages.SchannelSslConnectError(exception)
            : TlsFailureMessages.OpenSslSslConnectError(exception);

        // The socket error's own message is operating system text, so only its prefix is printed.
        Diagnostics.Act("message starts with 'Recv failure: '", message.StartsWith("Recv failure: ", StringComparison.Ordinal));
        Diagnostics.Assert("message starts with 'Recv failure: '", true, message.StartsWith("Recv failure: ", StringComparison.Ordinal));

        Assert.AreEqual($"Recv failure: {new SocketException((int)SocketError.NetworkDown).Message}", message);
    }

    private const bool SchannelBuild = true;

    private const bool OpenSslBuild = false;

    private static IOException ResetDuringHandshake(SocketError socketError) =>
        new("Unable to read data from the transport connection.", new SocketException((int)socketError));

    [TestMethod]
    [DataRow(false, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.")]
    [DataRow(true, "schannel: the certificate or certificate chain is based on an untrusted root")]
    public void SchannelPeerFailedVerification_WithChainErrors_IsTheMeasuredLineForTheTrustStore(bool hasCaCertificateFile, string expected)
    {
        Diagnostics.Arrange(
            "errors, host, has CA certificate file",
            $"{SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch}, localhost, {hasCaCertificateFile}");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch,
            null,
            "localhost",
            hasCaCertificateFile);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", expected, message);

        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    [DataRow("wrong.host.badssl.com")]
    [DataRow("140.82.112.4")]
    public void SchannelPeerFailedVerification_WithOnlyANameMismatchAndNoCaCertificateFile_IsTheMeasuredWrongPrincipalLine(string targetHost)
    {
        Diagnostics.Arrange("errors, host, has CA certificate file", $"{SslPolicyErrors.RemoteCertificateNameMismatch}, {targetHost}, False");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, null, targetHost, hasCaCertificateFile: false);

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message",
            "schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.",
            message);

        Assert.AreEqual(
            "schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.",
            message);
    }

    [TestMethod]
    public void SchannelPeerFailedVerification_WithOnlyANameMismatchOnAHostNameAndACaCertificateFile_IsTheMeasuredCertGetNameStringLine()
    {
        using var chain = BuildLeafChain(null);

        Diagnostics.Arrange("errors, host, has CA certificate file", $"{SslPolicyErrors.RemoteCertificateNameMismatch}, other, True");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, "other", hasCaCertificateFile: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message",
            "schannel: CertGetNameString() failed to match connection hostname (other) against server certificate names",
            message);

        Assert.AreEqual(
            "schannel: CertGetNameString() failed to match connection hostname (other) against server certificate names",
            message);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    public void SchannelPeerFailedVerification_WithOnlyANameMismatchOnAnIpAddressAgainstNoAlternativeNames_IsTheMeasuredCertFindExtensionLine(
        string targetHost)
    {
        using var chain = BuildLeafChain(null);

        Diagnostics.Arrange("errors, host, has CA certificate file", $"{SslPolicyErrors.RemoteCertificateNameMismatch}, {targetHost}, True");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, targetHost, hasCaCertificateFile: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "schannel: CertFindExtension() returned no extension.", message);

        Assert.AreEqual("schannel: CertFindExtension() returned no extension.", message);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    public void SchannelPeerFailedVerification_WithOnlyANameMismatchOnAnIpAddressAgainstAlternativeNames_IsLibcurlsExit60Text(
        string targetHost)
    {
        using var chain = BuildLeafChain(names => names.AddIpAddress(IPAddress.Parse("10.9.9.9")));

        Diagnostics.Arrange("errors, host, has CA certificate file", $"{SslPolicyErrors.RemoteCertificateNameMismatch}, {targetHost}, True");
        Diagnostics.Arrange("alternative names", "IP 10.9.9.9");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, targetHost, hasCaCertificateFile: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "SSL peer certificate or SSH remote key was not OK", message);

        Assert.AreEqual("SSL peer certificate or SSH remote key was not OK", message);
    }

    [TestMethod]
    [DataRow("localhost", "hostname 'localhost'")]
    [DataRow("10.1.2.3", "ipv4 address '10.1.2.3'")]
    [DataRow("fd00::1", "ipv6 address '[fd00::1]'")]
    public void OpenSslPeerFailedVerification_WithANameMismatchAgainstDnsAlternativeNames_IsTheMeasuredNoAlternativeNameLine(
        string targetHost,
        string target)
    {
        using var chain = BuildLeafChain(names =>
        {
            names.AddDnsName("foo.test");
            names.AddDnsName("bar.test");
        });

        Diagnostics.Arrange("host", targetHost);
        Diagnostics.Arrange("alternative names", "DNS foo.test, DNS bar.test");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, targetHost);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", $"SSL: no alternative certificate subject name matches target {target}", message);

        Assert.AreEqual($"SSL: no alternative certificate subject name matches target {target}", message);
    }

    [TestMethod]
    [DataRow("localhost", "hostname 'localhost'")]
    [DataRow("10.1.2.3", "ipv4 address '10.1.2.3'")]
    [DataRow("fd00::1", "ipv6 address '[fd00::1]'")]
    public void OpenSslPeerFailedVerification_WithANameMismatchAgainstIpAlternativeNames_IsTheMeasuredNoAlternativeNameLine(
        string targetHost,
        string target)
    {
        using var chain = BuildLeafChain(names =>
        {
            names.AddIpAddress(IPAddress.Parse("10.9.9.9"));
            names.AddIpAddress(IPAddress.Parse("fd00::9"));
        });

        Diagnostics.Arrange("host", targetHost);
        Diagnostics.Arrange("alternative names", "IP 10.9.9.9, IP fd00::9");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, targetHost);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", $"SSL: no alternative certificate subject name matches target {target}", message);

        Assert.AreEqual($"SSL: no alternative certificate subject name matches target {target}", message);
    }

    [TestMethod]
    [DataRow("otherhost", "otherhost")]
    [DataRow("10.1.2.3", "10.1.2.3")]
    [DataRow("fd00::1", "[fd00::1]")]
    public void OpenSslPeerFailedVerification_WithANameMismatchAgainstNoAlternativeNames_IsTheMeasuredSubjectNameLine(
        string targetHost,
        string shownHost)
    {
        using var chain = BuildLeafChain(null);

        Diagnostics.Arrange("host", targetHost);
        Diagnostics.Arrange("alternative names", "none");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, targetHost);

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", $"SSL: certificate subject name 'localhost' does not match target hostname '{shownHost}'", message);

        Assert.AreEqual($"SSL: certificate subject name 'localhost' does not match target hostname '{shownHost}'", message);
    }

    // curl matches the common name when subjectAltName names no DNS name and no IP address.
    [TestMethod]
    public void OpenSslPeerFailedVerification_WithANameMismatchAgainstOnlyAnEmailAlternativeName_IsTheSubjectNameLine()
    {
        using var chain = BuildLeafChain(names => names.AddEmailAddress("someone@example.test"));

        Diagnostics.Arrange("host", "otherhost");
        Diagnostics.Arrange("alternative names", "email someone@example.test");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, "otherhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "SSL: certificate subject name 'localhost' does not match target hostname 'otherhost'", message);

        Assert.AreEqual("SSL: certificate subject name 'localhost' does not match target hostname 'otherhost'", message);
    }

    // AF-0031: the line names the certificate's subject, never its issuer.
    [TestMethod]
    public void OpenSslPeerFailedVerification_WithANameMismatchOnAnIssuedCertificate_NamesTheSubjectNotTheIssuer()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateTrustingChain(authority);
        chain.Build(leaf);

        Diagnostics.Arrange("certificates", "leaf CN=localhost issued by CN=Test Authority");
        Diagnostics.Arrange("host", "otherhost");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, "otherhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "SSL: certificate subject name 'localhost' does not match target hostname 'otherhost'", message);

        Assert.AreEqual("SSL: certificate subject name 'localhost' does not match target hostname 'otherhost'", message);
    }

    // BL-150, measured: an untrusted or expired certificate that also names another host
    // is reported by its name.
    [TestMethod]
    public void OpenSslPeerFailedVerification_WithChainErrorsAndANameMismatch_ReportsTheNameMismatch()
    {
        using var chain = BuildLeafChain(null);

        Diagnostics.Arrange(
            "errors, host",
            $"{SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch}, other");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, chain, "other");

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "SSL: certificate subject name 'localhost' does not match target hostname 'other'", message);

        Assert.AreEqual("SSL: certificate subject name 'localhost' does not match target hostname 'other'", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithNoChain_ReportsVerifyResult20()
    {
        Diagnostics.Arrange("errors, chain, host", $"{SslPolicyErrors.RemoteCertificateNotAvailable}, none, localhost");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNotAvailable, null, "localhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)", message);

        Assert.AreEqual("SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithAChainEndingInAnUntrustedSelfSignedAuthority_ReportsVerifyResult19()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateChain();
        chain.ChainPolicy.ExtraStore.Add(authority);
        chain.Build(leaf);

        Diagnostics.Arrange("chain", "leaf CN=localhost, untrusted self-signed authority in the extra store");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "SSL certificate OpenSSL verify result: self-signed certificate in certificate chain (19)", message);

        Assert.AreEqual("SSL certificate OpenSSL verify result: self-signed certificate in certificate chain (19)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithAChainMissingItsIssuer_ReportsVerifyResult20()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateChain();
        chain.Build(leaf);

        Diagnostics.Arrange("chain", "leaf CN=localhost, issuer unavailable");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)", message);

        Assert.AreEqual("SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithATrustedButExpiredCertificate_ReportsVerifyResult10()
    {
        using var expired = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        using var chain = CreateTrustingChain(expired);
        chain.Build(expired);

        Diagnostics.Arrange("certificate", "trusted self-signed, expired 5 days ago");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "SSL certificate OpenSSL verify result: certificate has expired (10)", message);

        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate has expired (10)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithATrustedButNotYetValidCertificate_ReportsVerifyResult9()
    {
        using var notYetValid = CreateAuthority(DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(10));
        using var chain = CreateTrustingChain(notYetValid);
        chain.Build(notYetValid);

        Diagnostics.Arrange("certificate", "trusted self-signed, valid from 5 days ahead");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "SSL certificate OpenSSL verify result: certificate is not yet valid (9)", message);

        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate is not yet valid (9)", message);
    }

    [TestMethod]
    public void SchannelPeerFailedVerification_WithACaCertificateFileAndAnExpiredTrustedCertificate_IsTheMeasuredNotTimeValidLine()
    {
        using var expired = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        using var chain = CreateTrustingChain(expired);
        chain.Build(expired);

        Diagnostics.Arrange("certificate, has CA certificate file", "trusted self-signed, expired 5 days ago, True");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost", hasCaCertificateFile: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message",
            "schannel: this certificate or one of the certificates in the certificate chain is not time valid",
            message);

        Assert.AreEqual(
            "schannel: this certificate or one of the certificates in the certificate chain is not time valid", message);
    }

    [TestMethod]
    public void SchannelPeerFailedVerification_WithACaCertificateFileAndAChainMissingItsIssuer_IsTheMeasuredIncompleteLine()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateChain();
        chain.Build(leaf);

        Diagnostics.Arrange("chain, has CA certificate file", "leaf CN=localhost, issuer unavailable, True");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost", hasCaCertificateFile: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "schannel: the certificate chain is incomplete", message);

        Assert.AreEqual("schannel: the certificate chain is incomplete", message);
    }

    [TestMethod]
    public void SchannelPeerFailedVerification_WithACaCertificateFileAndATrustedChainOfUnknownRevocationStatus_IsTheMeasuredRevocationLine()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateTrustingChain(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.Build(leaf);

        Diagnostics.Arrange("chain, revocation mode, has CA certificate file", "trusted, Online, True");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost", hasCaCertificateFile: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "schannel: the revocation status is unknown", message);

        Assert.AreEqual("schannel: the revocation status is unknown", message);
    }

    // curl checks an untrusted root before the revocation status.
    [TestMethod]
    public void SchannelPeerFailedVerification_WithACaCertificateFileAndAnUntrustedChainOfUnknownRevocationStatus_IsTheUntrustedRootLine()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateChain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.ExtraStore.Add(authority);
        chain.Build(leaf);

        Diagnostics.Arrange("chain, revocation mode, has CA certificate file", "untrusted, Online, True");

        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost", hasCaCertificateFile: true);

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message", "schannel: the certificate or certificate chain is based on an untrusted root", message);

        Assert.AreEqual("schannel: the certificate or certificate chain is based on an untrusted root", message);
    }

    [TestMethod]
    public void IsSchannelCertificateExpired_WithATrustedButExpiredCertificate_IsTrue()
    {
        using var expired = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        using var chain = CreateTrustingChain(expired);
        chain.Build(expired);

        Diagnostics.Arrange("errors, certificate", $"{SslPolicyErrors.RemoteCertificateChainErrors}, trusted, expired 5 days ago");

        var isExpired = TlsFailureMessages.IsSchannelCertificateExpired(SslPolicyErrors.RemoteCertificateChainErrors, chain);

        Diagnostics.Act("is expired", isExpired);
        Diagnostics.Assert("is expired", true, isExpired);

        Assert.IsTrue(TlsFailureMessages.IsSchannelCertificateExpired(SslPolicyErrors.RemoteCertificateChainErrors, chain));
    }

    [TestMethod]
    public void IsSchannelCertificateExpired_WithAnExpiredCertificateNamingAnotherHost_IsFalse()
    {
        using var expired = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        using var chain = CreateTrustingChain(expired);
        chain.Build(expired);

        var errors = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;
        Diagnostics.Arrange("errors, certificate", $"{errors}, trusted, expired 5 days ago");

        var isExpired = TlsFailureMessages.IsSchannelCertificateExpired(errors, chain);

        Diagnostics.Act("is expired", isExpired);
        Diagnostics.Assert("is expired", false, isExpired);

        Assert.IsFalse(TlsFailureMessages.IsSchannelCertificateExpired(
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, chain));
    }

    [TestMethod]
    public void IsSchannelCertificateExpired_WithAnUntrustedExpiredCertificate_IsFalse()
    {
        using var expired = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        using var chain = CreateChain();
        chain.Build(expired);

        Diagnostics.Arrange("errors, certificate", $"{SslPolicyErrors.RemoteCertificateChainErrors}, untrusted, expired 5 days ago");

        var isExpired = TlsFailureMessages.IsSchannelCertificateExpired(SslPolicyErrors.RemoteCertificateChainErrors, chain);

        Diagnostics.Act("is expired", isExpired);
        Diagnostics.Assert("is expired", false, isExpired);

        Assert.IsFalse(TlsFailureMessages.IsSchannelCertificateExpired(SslPolicyErrors.RemoteCertificateChainErrors, chain));
    }

    [TestMethod]
    public void IsSchannelCertificateExpired_WithNoChain_IsFalse()
    {
        Diagnostics.Arrange("errors, chain", $"{SslPolicyErrors.RemoteCertificateChainErrors}, none");

        var isExpired = TlsFailureMessages.IsSchannelCertificateExpired(SslPolicyErrors.RemoteCertificateChainErrors, null);

        Diagnostics.Act("is expired", isExpired);
        Diagnostics.Assert("is expired", false, isExpired);

        Assert.IsFalse(TlsFailureMessages.IsSchannelCertificateExpired(SslPolicyErrors.RemoteCertificateChainErrors, null));
    }

    [TestMethod]
    public void SchannelCaCertificateFileUnusable_IsTheMeasuredLine()
    {
        Diagnostics.Arrange("file", "dir.pem");

        var message = TlsFailureMessages.SchannelCaCertificateFileUnusable("dir.pem");

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "schannel: failed to open CA file 'dir.pem'", message);

        Assert.AreEqual("schannel: failed to open CA file 'dir.pem'", TlsFailureMessages.SchannelCaCertificateFileUnusable("dir.pem"));
    }

    [TestMethod]
    public void OpenSslCaCertificateFileUnusable_IsTheMeasuredLine()
    {
        Diagnostics.Arrange("file", "bad.pem");

        var message = TlsFailureMessages.OpenSslCaCertificateFileUnusable("bad.pem");

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "error adding trust anchors from file: bad.pem", message);

        Assert.AreEqual("error adding trust anchors from file: bad.pem", TlsFailureMessages.OpenSslCaCertificateFileUnusable("bad.pem"));
    }

    // A CN=localhost certificate, with the subjectAltName entries the callback adds, or
    // none, and the chain built for it.
    private static X509Chain BuildLeafChain(Action<SubjectAlternativeNameBuilder>? addAlternativeNames)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (addAlternativeNames is not null)
        {
            var names = new SubjectAlternativeNameBuilder();
            addAlternativeNames(names);
            request.CertificateExtensions.Add(names.Build());
        }

        using var leaf = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var chain = CreateChain();
        chain.Build(leaf);
        return chain;
    }

    private static X509Chain CreateChain()
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain;
    }

    private static X509Chain CreateTrustingChain(X509Certificate2 root)
    {
        var chain = CreateChain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        return chain;
    }

    private static X509Certificate2 CreateAuthority(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Test Authority", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 authority)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.Create(
            authority,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            [1, 2, 3, 4, 5, 6, 7, 8]);
    }
}
