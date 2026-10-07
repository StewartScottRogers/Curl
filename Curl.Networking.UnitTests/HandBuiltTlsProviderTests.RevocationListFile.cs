using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="HandBuiltTlsProvider" /> with <c>--crlfile</c>, as
/// <see cref="SslStreamTlsProviderTests" /> pins it for the other provider: the OpenSSL build
/// refuses a revoked server certificate with exit 60 and a file it cannot load with exit 82;
/// the Schannel build ignores the option (ADR-0197, BL-609).
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAListRevokingTheServerCertificateInTheOpenSslBuild_FailsWithExit60CertificateRevoked()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "TLS 1.2 only, CaCertificateFile, CertificateRevocationListFile revoking the server certificate, SkipRevocationCheck: true");
        ArrangeCertificate("server certificate (also the CA and revoked)", s_serverCertificate);
        var provider = Provider(RevokingListOptions(), OpenSslBuild);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeAsync(provider, CertificateHost);
        }

        ActConnectResult(result.Result);
        Diagnostics.Act("plaintext disposed", result.PlaintextDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Diagnostics.Assert("error message", "SSL certificate OpenSSL verify result: certificate revoked (23)", result.Result.ErrorMessage);
        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate revoked (23)", result.Result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, result.PlaintextDisposed);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAListRevokingTheServerCertificateInTheSchannelBuild_IgnoresIt()
    {
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("options", "TLS 1.2 only, CaCertificateFile, CertificateRevocationListFile revoking the server certificate, SkipRevocationCheck: true");
        ArrangeCertificate("server certificate (also the CA and revoked)", s_serverCertificate);
        var provider = Provider(RevokingListOptions(), SchannelBuild);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeAsync(provider, CertificateHost);
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAGarbageListFileInTheOpenSslBuild_FailsWithExit82AsTheSslStreamProviderDoes()
    {
        var options = Tls12Only(new TlsClientOptions(
            CaCertificateFile: WriteFile("ca.pem", s_serverCertificate.ExportCertificatePem()),
            CertificateRevocationListFile: WriteFile("garbage.crl", "not a crl\n")));
        var (plaintext, plaintextStream) = Unanswered();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "TLS 1.2 only, CaCertificateFile: ca.pem, CertificateRevocationListFile: garbage.crl");
        Diagnostics.Arrange("revocation list contents", "not a crl");
        ArrangeCertificate("CA certificate", s_serverCertificate);

        ConnectResult result;
        ConnectResult expected;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
            expected = await new SslStreamTlsProvider(options, OpenSslBuild).AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("SslStream provider error message", expected.ErrorMessage ?? "(none)");
        Diagnostics.Assert("exit code", CurlExitCode.SslCrlBadfile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCrlBadfile, result.ExitCode);
        Diagnostics.Assert("error message", expected.ErrorMessage, result.ErrorMessage);
        Assert.AreEqual(expected.ErrorMessage, result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, plaintextStream.IsDisposed);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    // The class's self-signed server certificate is its own issuer, so it is trusted as the
    // --cacert and signs the list that revokes it.
    private TlsClientOptions RevokingListOptions()
    {
        var list = TestRevocationList.Write(
            s_serverCertificate, nextUpdate: DateTimeOffset.UtcNow.AddDays(1), revokedSerialNumbers: [s_serverCertificate.SerialNumberBytes.ToArray()]);
        return Tls12Only(new TlsClientOptions(
            CaCertificateFile: WriteFile("ca.pem", s_serverCertificate.ExportCertificatePem()),
            CertificateRevocationListFile: WriteFile("revoked.crl", TestRevocationList.Pem(list)),
            SkipRevocationCheck: true));
    }
}
