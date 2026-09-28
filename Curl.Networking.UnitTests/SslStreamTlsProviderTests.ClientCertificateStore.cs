using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="SslStreamTlsProvider" /> with a <c>--cert</c> store path
/// (<c>CurrentUser\MY\&lt;thumbprint&gt;</c>): the Schannel build finds the certificate in
/// the store a <see cref="FakeClientCertificateStore" /> stands in for, and the OpenSSL
/// build reads the same value as a file. Every failure is pinned to the text curl 8.21.0's
/// Schannel build printed, measured 2026-09-27 (ADR-0066).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAStorePathInTheSchannelBuild_PresentsTheCertificateFromTheStore()
    {
        var store = StoreHoldingTheClientCertificate();

        var handshake = await HandshakeWithClientCertificateRequestAsync(
            ClientCertificate($@"CurrentUser\MY\{s_clientCertificate.Thumbprint}"), SchannelBuild, store);

        AssertPresented(handshake);
        CollectionAssert.AreEqual(new[] { (ClientCertificateStoreLocation.CurrentUser, "MY") }, store.Opened);
    }

    // Measured: the thumbprint matches in either case, and --cert-type, --key and the
    // passphrase are not read for a certificate from a store.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithALowerCaseThumbprintPassphraseAndPemTypeInTheSchannelBuild_PresentsTheCertificateFromTheStore()
    {
        var options = ClientCertificate($@"CurrentUser\MY\{s_clientCertificate.Thumbprint.ToLowerInvariant()}:{Passphrase}") with
        {
            CertificateType = "PEM",
            PrivateKey = Path.Combine(_caFileDirectory, "missing.pem"),
        };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild, StoreHoldingTheClientCertificate());

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAStorePathWhoseThumbprintIsNotInTheStoreInTheSchannelBuild_FailsWithSslCertProblem()
    {
        var handshake = await HandshakeWithClientCertificateRequestAsync(
            ClientCertificate($@"CurrentUser\MY\{new string('0', 40)}"), SchannelBuild, StoreHoldingTheClientCertificate());

        AssertFailed(handshake, CurlExitCode.SslCertProblem, "schannel: client cert not found in cert store");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAStorePathNamingNoStoreInTheSchannelBuild_FailsWithSslCertProblem()
    {
        var store = new FakeClientCertificateStore { Certificates = null };

        var handshake = await HandshakeWithClientCertificateRequestAsync(
            ClientCertificate($@"CurrentUser\NOSUCHSTORE\{new string('z', 40)}"), SchannelBuild, store);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, "schannel: Failed to open cert store 10000 NOSUCHSTORE, last error is 0x00000002");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAStorePathWhoseThumbprintIsNotHexInTheSchannelBuild_FailsWithCurlsOwnText()
    {
        var handshake = await HandshakeWithClientCertificateRequestAsync(
            ClientCertificate($@"CurrentUser\MY\zz{new string('0', 38)}"), SchannelBuild, StoreHoldingTheClientCertificate());

        AssertFailed(handshake, CurlExitCode.SslCertProblem, "Problem with the local SSL certificate");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAStorePathInTheOpenSslBuild_ReadsItAsAFile()
    {
        var store = StoreHoldingTheClientCertificate();
        var path = $@"CurrentUser\MY\{s_clientCertificate.Thumbprint}";

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(path), OpenSslBuild, store);

        AssertFailed(
            handshake,
            CurlExitCode.SslCertProblem,
            $"could not load PEM client certificate from {path}, OpenSSL error error:80000002:system library::No such file or directory, (no key found, wrong passphrase, or wrong file format?)");
        Assert.IsEmpty(store.Opened);
    }

    [TestMethod]
    public void Constructor_WithNullCertificateStore_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new SslStreamTlsProvider(new TlsClientOptions(), SchannelBuild, TimeProvider.System, null!));
    }

    // A store's certificate has a persisted key, which Schannel signs with; a PKCS#12 round
    // trip gives the test's certificate one.
    private static FakeClientCertificateStore StoreHoldingTheClientCertificate() => new()
    {
        Certificates = [X509CertificateLoader.LoadPkcs12(s_clientCertificate.Export(X509ContentType.Pkcs12), null)],
    };
}
