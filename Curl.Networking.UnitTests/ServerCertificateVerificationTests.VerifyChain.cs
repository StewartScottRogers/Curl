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

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ReadTrustAnchors_WithCaCertificates_RequiresTheServerAuthApplicationPolicy(bool matchesSchannelBuild)
    {
        using var trusted = IssuedChain.Create();
        var verification = new ServerCertificateVerification(trusted.TrustingOptions(), matchesSchannelBuild, TimeProvider.System);

        var policy = verification.ReadTrustAnchors().ChainPolicy;

        Diagnostics.Act("application policy", string.Join(",", policy!.ApplicationPolicy.Cast<Oid>().Select(oid => oid.Value)));
        Assert.AreEqual("1.3.6.1.5.5.7.3.1", policy.ApplicationPolicy.Cast<Oid>().Single().Value);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Verify_ClientAuthOnlyLeafUnderCaCertificate_IsRejectedWithExit60(bool matchesSchannelBuild)
    {
        using var issued = IssuedChain.Create("1.3.6.1.5.5.7.3.2");

        var verdict = HandBuiltVerdict(issued, matchesSchannelBuild);

        Diagnostics.Act("rejection", verdict.Rejection);
        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, RejectionOf(verdict).ExitCode);
    }

    [TestMethod]
    public void Verify_ClientAuthOnlyLeafInTheOpenSslBuild_ReportsUnsuitableCertificatePurpose()
    {
        using var issued = IssuedChain.Create("1.3.6.1.5.5.7.3.2");

        var verdict = HandBuiltVerdict(issued, matchesSchannelBuild: false);

        Diagnostics.Act("message", RejectionOf(verdict).Message);
        StringAssert.Contains(RejectionOf(verdict).Message, "OpenSSL verify result: unsuitable certificate purpose (26)");
    }

    [TestMethod]
    [DataRow("1.3.6.1.5.5.7.3.1")]
    [DataRow(null)]
    public void Verify_LeafWithServerAuthOrNoExtendedKeyUsageUnderCaCertificate_IsAccepted(string? usage)
    {
        using var issued = IssuedChain.Create(usage);

        var verdict = HandBuiltVerdict(issued, matchesSchannelBuild: false);

        Diagnostics.Act("rejection", verdict.Rejection);
        Assert.IsTrue(verdict.IsAccepted);
    }

    [TestMethod]
    public void Verify_KeyCertSignOnlyLeafInTheOpenSslBuild_ReportsUnsuitableCertificatePurposeWithExit60()
    {
        using var issued = IssuedChain.Create(keyUsage: X509KeyUsageFlags.KeyCertSign);

        var verdict = HandBuiltVerdict(issued, matchesSchannelBuild: false);

        Diagnostics.Act("rejection", verdict.Rejection);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, RejectionOf(verdict).ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)", RejectionOf(verdict).Message);
    }

    [TestMethod]
    public void Verify_KeyCertSignOnlyLeafInTheSchannelBuild_IsAccepted()
    {
        using var issued = IssuedChain.Create(keyUsage: X509KeyUsageFlags.KeyCertSign);

        var verdict = HandBuiltVerdict(issued, matchesSchannelBuild: true);

        Diagnostics.Act("rejection", verdict.Rejection);
        Assert.IsTrue(verdict.IsAccepted);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(X509KeyUsageFlags.DigitalSignature)]
    [DataRow(X509KeyUsageFlags.KeyEncipherment)]
    [DataRow(X509KeyUsageFlags.KeyAgreement)]
    public void Verify_LeafWithNoKeyUsageOrOneForServerUseInTheOpenSslBuild_IsAccepted(X509KeyUsageFlags? keyUsage)
    {
        using var issued = IssuedChain.Create(keyUsage: keyUsage);

        var verdict = HandBuiltVerdict(issued, matchesSchannelBuild: false);

        Diagnostics.Act("rejection", verdict.Rejection);
        Assert.IsTrue(verdict.IsAccepted);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void VerifyServerCertificateChain_SchannelIpTargetAgainstCertificateWithoutSubjectAltName_RejectsItWithExit60(bool handBuilt)
    {
        using var trusted = IssuedChain.Create(commonName: "127.0.0.1", withDnsName: false);
        var provider = Provider(trusted.TrustingOptions(), handBuilt, matchesSchannelBuild: true);
        Diagnostics.Arrange("chain", "leaf CN=127.0.0.1, no subjectAltName; Schannel profile, --cacert");

        var verdict = provider.VerifyServerCertificateChain([trusted.Leaf.RawData], "127.0.0.1");

        Diagnostics.Act("message", verdict.ErrorMessage);
        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, verdict.ExitCode);
        Assert.AreEqual("schannel: CertFindExtension() returned no extension.", verdict.ErrorMessage);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void VerifyServerCertificateChain_OpenSslIpTargetAgainstCertificateWithoutSubjectAltName_AcceptsIt(bool handBuilt)
    {
        using var trusted = IssuedChain.Create(commonName: "127.0.0.1", withDnsName: false);
        var provider = Provider(trusted.TrustingOptions(), handBuilt, matchesSchannelBuild: false);

        var verdict = provider.VerifyServerCertificateChain([trusted.Leaf.RawData], "127.0.0.1");

        Diagnostics.Act("message", verdict.ErrorMessage);
        Assert.IsTrue(verdict.IsAccepted, verdict.ErrorMessage);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void VerifyServerCertificateChain_SchannelIpTargetAgainstCertificateWithIpSubjectAltName_AcceptsIt(bool handBuilt)
    {
        using var trusted = IssuedChain.Create(commonName: "127.0.0.1", withDnsName: false, ipAddressName: "127.0.0.1");
        var provider = Provider(trusted.TrustingOptions(), handBuilt, matchesSchannelBuild: true);

        var verdict = provider.VerifyServerCertificateChain([trusted.Leaf.RawData], "127.0.0.1");

        Diagnostics.Act("message", verdict.ErrorMessage);
        Assert.IsTrue(verdict.IsAccepted, verdict.ErrorMessage);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void VerifyServerCertificateChain_SchannelIpTargetAgainstCommonNameLocalhostWithDnsName_KeepsTheIpNotAmongNamesMessage(bool handBuilt)
    {
        using var trusted = IssuedChain.Create(commonName: "localhost");
        var provider = Provider(trusted.TrustingOptions(), handBuilt, matchesSchannelBuild: true);

        var verdict = provider.VerifyServerCertificateChain([trusted.Leaf.RawData], "127.0.0.1");

        Diagnostics.Act("message", verdict.ErrorMessage);
        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreNotEqual("schannel: CertFindExtension() returned no extension.", verdict.ErrorMessage);
    }

    private static (CurlExitCode ExitCode, string Message) RejectionOf(ServerCertificateVerdict verdict) =>
        ((CurlExitCode ExitCode, string Message))verdict.Rejection!;

    private static ServerCertificateVerdict HandBuiltVerdict(IssuedChain issued, bool matchesSchannelBuild)
    {
        var verification = new ServerCertificateVerification(issued.TrustingOptions(), matchesSchannelBuild, TimeProvider.System);
        var anchors = verification.ReadTrustAnchors();
        var verifier = new HandBuiltCertificateVerifier(verification, anchors.ChainPolicy, anchors.AnchorsBesideSystemStore, anchors.RevocationLists, Host);
        return verifier.Verify(new ServerCertificateChain([issued.Leaf.RawData, issued.Ca.RawData], Host, null));
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

        public static IssuedChain Create(
            string? usage = "1.3.6.1.5.5.7.3.1",
            X509KeyUsageFlags? keyUsage = X509KeyUsageFlags.DigitalSignature,
            string commonName = Host,
            bool withDnsName = true,
            string? ipAddressName = null)
        {
            var now = DateTimeOffset.UtcNow;
            using var caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var caRequest = new CertificateRequest("CN=Verify Chain Test CA", caKey, HashAlgorithmName.SHA256);
            caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            caRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, false));
            var ca = caRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(30));

            using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var leafRequest = new CertificateRequest("CN=" + commonName, leafKey, HashAlgorithmName.SHA256);
            if (withDnsName || ipAddressName is not null)
            {
                var names = new SubjectAlternativeNameBuilder();
                if (withDnsName)
                {
                    names.AddDnsName(Host);
                }

                if (ipAddressName is not null)
                {
                    names.AddIpAddress(System.Net.IPAddress.Parse(ipAddressName));
                }

                leafRequest.CertificateExtensions.Add(names.Build());
            }

            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            if (keyUsage is { } flags)
            {
                leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(flags, true));
            }

            if (usage is not null)
            {
                leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(usage)], false));
            }

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
