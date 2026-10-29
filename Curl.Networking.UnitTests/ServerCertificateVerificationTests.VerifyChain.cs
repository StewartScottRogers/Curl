using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="ITlsProvider.VerifyServerCertificateChain" /> in both engine providers
/// (ADR-1052): a chain judged apart from any handshake with the transfer's trust settings,
/// <c>-k</c> accepting any chain, a chain the <c>--cacert</c> CA did not sign exit 60, a pin
/// the key does not match exit 90, an unreadable <c>--cacert</c> exit 77 and a chain the CA
/// signed for the host accepted.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationVerifyChainTests
{
    private const string Host = "coap.example";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void VerifyServerCertificateChain_UnderInsecure_AcceptsAnyChain(bool handBuilt)
    {
        using var stranger = IssuedChain.Create();
        var provider = Provider(new TlsClientOptions(Insecure: true), handBuilt);
        Diagnostics.Arrange("options", "Insecure: true");
        Diagnostics.Arrange("chain", "leaf of an unrelated CA, host other.example");

        var verdict = provider.VerifyServerCertificateChain([stranger.Leaf.RawData], "other.example");

        Diagnostics.Act("exit code", verdict.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, verdict.ExitCode);
        Assert.IsTrue(verdict.IsAccepted);
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void VerifyServerCertificateChain_WithAChainTheCaDidNotSign_RejectsItWithExit60(bool handBuilt, bool matchesSchannelBuild)
    {
        using var trusted = IssuedChain.Create();
        using var stranger = IssuedChain.Create();
        var provider = Provider(trusted.TrustingOptions(), handBuilt, matchesSchannelBuild);
        Diagnostics.Arrange("options", "--cacert of one CA");
        Diagnostics.Arrange("chain", "leaf and CA of another CA");

        var verdict = provider.VerifyServerCertificateChain([stranger.Leaf.RawData, stranger.Ca.RawData], Host);

        Diagnostics.Act("exit code", verdict.ExitCode);
        Diagnostics.Act("message", verdict.ErrorMessage);
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, verdict.ExitCode);
        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, verdict.ExitCode);
        Assert.IsFalse(string.IsNullOrEmpty(verdict.ErrorMessage));
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void VerifyServerCertificateChain_WithAChainTheCaSignedForTheHost_AcceptsIt(bool handBuilt, bool matchesSchannelBuild)
    {
        using var trusted = IssuedChain.Create();
        var provider = Provider(trusted.TrustingOptions(), handBuilt, matchesSchannelBuild);
        Diagnostics.Arrange("options", "--cacert of the leaf's CA");
        Diagnostics.Arrange("chain", "leaf for coap.example");

        var verdict = provider.VerifyServerCertificateChain([trusted.Leaf.RawData], Host);

        Diagnostics.Act("exit code", verdict.ExitCode);
        Diagnostics.Act("message", verdict.ErrorMessage);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, verdict.ExitCode);
        Assert.IsTrue(verdict.IsAccepted, verdict.ErrorMessage);
    }

    [TestMethod]
    public void VerifyServerCertificateChain_WithAPinTheKeyDoesNotMatch_RejectsItWithExit90()
    {
        using var trusted = IssuedChain.Create();
        var provider = Provider(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="), handBuilt: false);
        Diagnostics.Arrange("options", "Insecure: true, --pinnedpubkey of another key");

        var verdict = provider.VerifyServerCertificateChain([trusted.Leaf.RawData], Host);

        Diagnostics.Act("exit code", verdict.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, verdict.ExitCode);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, verdict.ExitCode);
    }

    [TestMethod]
    public void VerifyServerCertificateChain_WithACaFileThatDoesNotExist_RejectsItWithExit77()
    {
        using var trusted = IssuedChain.Create();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ca.pem");
        var provider = Provider(new TlsClientOptions(CaCertificateFile: missing, SkipRevocationCheck: true), handBuilt: false);
        Diagnostics.Arrange("options", "--cacert of a file that does not exist");

        var verdict = provider.VerifyServerCertificateChain([trusted.Leaf.RawData], Host);

        Diagnostics.Act("exit code", verdict.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.SslCacertBadfile, verdict.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCacertBadfile, verdict.ExitCode);
    }

    [TestMethod]
    public void VerifyServerCertificateChain_WithNullCertificatesOrHost_ThrowsArgumentNullException()
    {
        var provider = Provider(new TlsClientOptions(Insecure: true), handBuilt: false);
        Diagnostics.Arrange("arguments", "null certificates, then a null host");

        var noCertificates = Assert.ThrowsExactly<ArgumentNullException>(() => provider.VerifyServerCertificateChain(null!, Host));
        var noHost = Assert.ThrowsExactly<ArgumentNullException>(() => provider.VerifyServerCertificateChain([], null!));

        Diagnostics.Act("parameters", $"{noCertificates.ParamName}, {noHost.ParamName}");
        Diagnostics.Assert("parameters", "certificates, targetHost", $"{noCertificates.ParamName}, {noHost.ParamName}");
        Assert.AreEqual("certificates", noCertificates.ParamName);
        Assert.AreEqual("targetHost", noHost.ParamName);
    }

    private static ITlsProvider Provider(TlsClientOptions options, bool handBuilt, bool matchesSchannelBuild = false) =>
        handBuilt
            ? new HandBuiltTlsProvider(options, matchesSchannelBuild, TimeProvider.System, new SystemClientCertificateStore(), SystemTlsRandomSource.Instance)
            : new SslStreamTlsProvider(options, matchesSchannelBuild);

    // A CA and a leaf it issued for Host, made in the test.
    private sealed class IssuedChain : IDisposable
    {
        private IssuedChain(X509Certificate2 ca, X509Certificate2 leaf)
        {
            Ca = ca;
            Leaf = leaf;
        }

        public X509Certificate2 Ca { get; }

        public X509Certificate2 Leaf { get; }

        public static IssuedChain Create()
        {
            var now = DateTimeOffset.UtcNow;
            using var caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var caRequest = new CertificateRequest("CN=Verify Chain Test CA", caKey, HashAlgorithmName.SHA256);
            caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            caRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, false));
            var ca = caRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(30));

            using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var leafRequest = new CertificateRequest("CN=" + Host, leafKey, HashAlgorithmName.SHA256);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(Host);
            leafRequest.CertificateExtensions.Add(names.Build());
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            leafRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(ca, true, false));
            var leaf = leafRequest.Create(ca, now.AddHours(-1), now.AddDays(10), RandomNumberGenerator.GetBytes(8));
            return new IssuedChain(ca, leaf);
        }

        public TlsClientOptions TrustingOptions() =>
            new(SkipRevocationCheck: true, CaCertificateBlob: Encoding.ASCII.GetBytes(Ca.ExportCertificatePem()));

        public void Dispose()
        {
            Ca.Dispose();
            Leaf.Dispose();
        }
    }
}
