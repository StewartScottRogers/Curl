using System.Numerics;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="SslStreamTlsProvider" /> with <c>--crlfile</c>
/// (<see cref="TlsClientOptions.CertificateRevocationListFile" />), as measured on 2026-09-29 with
/// Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile -TlsEmptyCrlFile -TlsRevokingCrlFile and
/// <c>-sS --cacert root.pem</c> (BL-609, ADR-0197):
/// <list type="bullet">
/// <item>curl 8.18.0's OpenSSL 3.5.5 build: an empty list from the root, exit 0; a list revoking
/// the server certificate, exit 60 <c>SSL certificate OpenSSL verify result: certificate revoked (23)</c>;
/// a list from another CA, exit 60 <c>... unable to get certificate CRL (3)</c>; a file of garbage,
/// exit 82 <c>error loading CRL file: &lt;path&gt;</c>; under <c>-k</c> the garbage file and the
/// revoking list, exit 0.</item>
/// <item>curl 8.21.0's Schannel build ignores the option: with <c>--ssl-no-revoke</c> the revoking
/// list and the garbage file, exit 0; without it every case is ADR-0321's exit 60
/// <c>schannel: the revocation status is unknown</c>, as with no <c>--crlfile</c>.</item>
/// </list>
/// A missing file is refused by the parser, exit 2, in both builds.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnEmptyListFromTheRootInTheOpenSslBuild_Succeeds()
    {
        using var root = CreateListSigningRoot();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(RevocationListOptions(root, ListFrom(root)), OpenSslBuild), leaf);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAListRevokingTheServerCertificateInTheOpenSslBuild_FailsWithExit60CertificateRevoked()
    {
        using var root = CreateListSigningRoot();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(RevocationListOptions(root, ListFrom(root, leaf)), OpenSslBuild), leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate revoked (23)", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAListFromAnotherAuthorityInTheOpenSslBuild_FailsWithExit60UnableToGetTheList()
    {
        using var root = CreateListSigningRoot();
        using var other = CertificateRevocationListTests.CreateAuthority("CN=BL609 Other CA", RSA.Create(2048));
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(RevocationListOptions(root, ListFrom(other)), OpenSslBuild), leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: unable to get certificate CRL (3)", result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAGarbageListFileInTheOpenSslBuild_FailsWithExit82BeforeTheHandshake()
    {
        using var root = CreateListSigningRoot();
        var listFile = WriteCaFile("garbage.crl", "not a crl\n");
        var plaintext = new FakeConnection();

        var result = await new SslStreamTlsProvider(RevocationListOptions(root, listFile), OpenSslBuild)
            .AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCrlBadfile, result.ExitCode);
        Assert.AreEqual($"error loading CRL file: {listFile}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAGarbageListFileAndInsecureInTheOpenSslBuild_Succeeds()
    {
        var listFile = WriteCaFile("garbage.crl", "not a crl\n");

        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, CertificateRevocationListFile: listFile), CertificateHost, SslProtocols.Tls12, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAListRevokingTheServerCertificateInTheSchannelBuild_IgnoresIt()
    {
        using var root = CreateListSigningRoot();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var options = RevocationListOptions(root, ListFrom(root, leaf)) with { SkipRevocationCheck = true };

        var result = await HandshakeWithServerCertificateAsync(new SslStreamTlsProvider(options, SchannelBuild), leaf);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAGarbageListFileInTheSchannelBuild_IgnoresIt()
    {
        using var root = CreateListSigningRoot();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var options = RevocationListOptions(root, WriteCaFile("garbage.crl", "not a crl\n")) with { SkipRevocationCheck = true };

        var result = await HandshakeWithServerCertificateAsync(new SslStreamTlsProvider(options, SchannelBuild), leaf);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    // A private root that may sign lists, as Record-CurlExchange.ps1's throwaway root may.
    private static X509Certificate2 CreateListSigningRoot() =>
        CertificateRevocationListTests.CreateAuthority("CN=BL609 Test Root", RSA.Create(2048));

    private TlsClientOptions RevocationListOptions(X509Certificate2 root, string listFile) =>
        new(CaCertificateFile: WriteCaFile("root.pem", root.ExportCertificatePem()), CertificateRevocationListFile: listFile);

    private string ListFrom(X509Certificate2 authority, X509Certificate2? revoked = null)
    {
        var builder = new CertificateRevocationListBuilder();
        if (revoked is not null)
        {
            builder.AddEntry(revoked);
        }

        var der = builder.Build(authority, BigInteger.One, DateTimeOffset.UtcNow.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, DateTimeOffset.UtcNow.AddHours(-1));
        return WriteCaFile("list.crl", TestRevocationList.Pem(der));
    }
}
