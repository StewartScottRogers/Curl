using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="HandBuiltTlsProvider" /> with <c>--crlfile</c>, as
/// <see cref="SslStreamTlsProviderTests" /> pins it for the other provider: the OpenSSL build
/// refuses a revoked server certificate with exit 60 and a file it cannot load with exit 82;
/// the Schannel build ignores the option (ADR-0194, BL-609).
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAListRevokingTheServerCertificateInTheOpenSslBuild_FailsWithExit60CertificateRevoked()
    {
        var result = await HandshakeAsync(Provider(RevokingListOptions(), OpenSslBuild), CertificateHost);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate revoked (23)", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAListRevokingTheServerCertificateInTheSchannelBuild_IgnoresIt()
    {
        var result = await HandshakeAsync(Provider(RevokingListOptions(), SchannelBuild), CertificateHost);

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

        var result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
        var expected = await new SslStreamTlsProvider(options, OpenSslBuild).AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCrlBadfile, result.ExitCode);
        Assert.AreEqual(expected.ErrorMessage, result.ErrorMessage);
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
