using System.Formats.Asn1;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Protocol.Abstractions;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins how the OpenSSL build reads <c>TRUSTED CERTIFICATE</c> blocks of a <c>--cacert</c> file
/// and their trust and reject settings, each verdict as <c>openssl verify -partial_chain
/// -purpose sslserver</c> 3.5 gives it (BL-5879, STD-198), and of <c>--capath</c> hashed files,
/// as curl 8.18.0 on OpenSSL 3.5.5 gives it (BL-5917).
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationTrustedCertificateTests
{
    private const string Host = "localhost";
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";
    private const string EmailProtection = "1.3.6.1.5.5.7.3.4";
    private const string AnyUse = "2.5.29.37.0";
    private const string Rejected = "SSL certificate OpenSSL verify result: certificate rejected (28)";

    private static readonly Asn1Tag RejectTag = new(TagClass.ContextSpecific, 0, isConstructed: true);

    private readonly List<string> _directories = [];

    [TestMethod]
    [DataRow(ServerAuthentication, null, true)]
    [DataRow(AnyUse, null, true)]
    [DataRow(EmailProtection, null, false)]
    [DataRow(null, ServerAuthentication, false)]
    [DataRow(null, AnyUse, false)]
    [DataRow(null, EmailProtection, true)]
    [DataRow(ServerAuthentication, ServerAuthentication, false)]
    [DataRow(ServerAuthentication, EmailProtection, true)]
    [DataRow(null, null, true)]
    public void VerifyChain_RootWithTrustSettings_IsAcceptedOrRejected(string? trusted, string? rejected, bool accepted)
    {
        using var chain = IssuedChain.Create();

        var verdict = Verify(Trusted(chain.Root, trusted, rejected), chain);

        AssertVerdict(accepted, verdict);
    }

    [TestMethod]
    public void VerifyChain_RejectedRootThenThePlainRoot_IsRejected()
    {
        using var chain = IssuedChain.Create();

        var verdict = Verify(Trusted(chain.Root, null, ServerAuthentication) + Plain(chain.Root), chain);

        AssertVerdict(false, verdict);
    }

    [TestMethod]
    public void VerifyChain_PlainRootThenTheRejectedRoot_IsAccepted()
    {
        using var chain = IssuedChain.Create();

        var verdict = Verify(Plain(chain.Root) + Trusted(chain.Root, null, ServerAuthentication), chain);

        AssertVerdict(true, verdict);
    }

    [TestMethod]
    public void VerifyChain_RejectedRootAndAnotherHostName_ReportsTheRejectionFirst()
    {
        using var chain = IssuedChain.Create();

        var verdict = Verify(Trusted(chain.Root, null, ServerAuthentication), chain, "other.test");

        AssertVerdict(false, verdict);
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow(ServerAuthentication, false)]
    public void VerifyChain_ListedIntermediateBelowARejectedRoot_TheIntermediateDecides(string? intermediateRejects, bool accepted)
    {
        using var chain = IssuedChain.Create();
        var intermediate = intermediateRejects is null ? Plain(chain.Intermediate) : Trusted(chain.Intermediate, null, intermediateRejects);

        var verdict = Verify(intermediate + Trusted(chain.Root, null, ServerAuthentication), chain);

        AssertVerdict(accepted, verdict);
    }

    [TestMethod]
    public void VerifyChain_RejectedLeafAlone_IsRejected()
    {
        using var chain = IssuedChain.Create();

        var verdict = Verify(Trusted(chain.Leaf, null, ServerAuthentication), chain);

        AssertVerdict(false, verdict);
    }

    [TestMethod]
    public void VerifyChain_RejectedLeafBelowAListedRoot_IsAccepted()
    {
        using var chain = IssuedChain.Create();

        var verdict = Verify(Trusted(chain.Leaf, null, ServerAuthentication) + Plain(chain.Root), chain);

        AssertVerdict(true, verdict);
    }

    [TestMethod]
    public void VerifyChain_SchannelBuild_IgnoresATrustedCertificateBlock()
    {
        using var chain = IssuedChain.Create();
        var pem = Trusted(chain.Root, null, ServerAuthentication) + Plain(chain.Root);

        var verdict = Verification(pem, matchesSchannelBuild: true).VerifyChain(chain.Sent, Host);

        Assert.AreNotEqual(Rejected, verdict.ErrorMessage);
    }

    [TestMethod]
    public void Judge_RejectedRoot_ReportsVerifyResult28()
    {
        using var chain = IssuedChain.Create();
        var verification = Verification(Trusted(chain.Root, null, ServerAuthentication), matchesSchannelBuild: false);
        var (chainPolicy, anchors, _) = verification.ReadTrustAnchors();
        using var built = new X509Chain { ChainPolicy = chainPolicy! };
        built.ChainPolicy.ExtraStore.Add(chain.Intermediate);
        built.Build(chain.Leaf);

        var (observed, failure) = verification.Judge(SslPolicyErrors.None, built, Host, anchors, null, chain.Sent);

        Assert.AreEqual(28L, observed.VerifyResult);
        Assert.AreEqual((CurlExitCode.PeerFailedVerification, Rejected), failure);
    }

    [TestMethod]
    [DataRow(ServerAuthentication, null, true)]
    [DataRow(null, ServerAuthentication, false)]
    public void VerifyChain_CaDirectoryRootWithTrustSettings_IsAcceptedOrRejected(string? trusted, string? rejected, bool accepted)
    {
        using var chain = IssuedChain.Create();
        var directory = Directory(chain.Root, Trusted(chain.Root, trusted, rejected));

        var verdict = VerifyDirectory(directory, chain);

        AssertVerdict(accepted, verdict);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void VerifyChain_TwoCopiesInOneCaDirectory_TheFirstWins(bool rejectedFirst, bool accepted)
    {
        using var chain = IssuedChain.Create();
        string[] copies = [Trusted(chain.Root, null, ServerAuthentication), Plain(chain.Root)];
        var directory = Directory(chain.Root, rejectedFirst ? copies : [copies[1], copies[0]]);

        var verdict = VerifyDirectory(directory, chain);

        AssertVerdict(accepted, verdict);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void VerifyChain_TwoCaDirectories_TheFirstWins(bool rejectedFirst, bool accepted)
    {
        using var chain = IssuedChain.Create();
        var rejecting = Directory(chain.Root, Trusted(chain.Root, null, ServerAuthentication));
        var plain = Directory(chain.Root, Plain(chain.Root));
        string[] directories = rejectedFirst ? [rejecting, plain] : [plain, rejecting];

        var verdict = VerifyDirectory(string.Join(Path.PathSeparator, directories), chain);

        AssertVerdict(accepted, verdict);
    }

    [TestMethod]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    public void VerifyChain_RootInCaFileAndCaDirectory_TheFileWins(bool fileRejects, bool directoryRejects, bool accepted)
    {
        using var chain = IssuedChain.Create();
        string Copy(bool rejects) => rejects ? Trusted(chain.Root, null, ServerAuthentication) : Plain(chain.Root);
        var directory = Directory(chain.Root, Copy(directoryRejects));

        var verdict = VerifyDirectory(directory, chain, Copy(fileRejects));

        AssertVerdict(accepted, verdict);
    }

    [TestMethod]
    public void VerifyChain_RejectedCaDirectoryRootBesideAnotherCaFile_IsRejected()
    {
        using var chain = IssuedChain.Create();
        using var other = IssuedChain.Create();
        var directory = Directory(chain.Root, Trusted(chain.Root, null, ServerAuthentication));

        var verdict = VerifyDirectory(directory, chain, Plain(other.Leaf));

        AssertVerdict(false, verdict);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void VerifyChain_RejectedCaDirectoryLeaf_DecidesOnlyWithNothingListedAboveIt(bool listsRoot, bool accepted)
    {
        using var chain = IssuedChain.Create();
        var directory = Directory(chain.Leaf, Trusted(chain.Leaf, null, ServerAuthentication));
        if (listsRoot)
        {
            Write(directory, chain.Root, Plain(chain.Root));
        }

        var verdict = VerifyDirectory(directory, chain);

        AssertVerdict(accepted, verdict);
    }

    [TestMethod]
    public void VerifyChain_UndecodableBlockAfterARejectedRootInACaDirectoryFile_KeepsTheRoot()
    {
        using var chain = IssuedChain.Create();
        var directory = Directory(chain.Root, Trusted(chain.Root, null, ServerAuthentication) + Undecodable);

        var verdict = VerifyDirectory(directory, chain);

        AssertVerdict(false, verdict);
    }

    [TestMethod]
    public void VerifyChain_UndecodableBlockBeforeTheRootInACaDirectoryFile_AddsNothing()
    {
        using var chain = IssuedChain.Create();
        var directory = Directory(chain.Root, Undecodable + Plain(chain.Root));

        var verdict = VerifyDirectory(directory, chain);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, verdict.ExitCode, verdict.ErrorMessage);
        Assert.AreNotEqual(Rejected, verdict.ErrorMessage);
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void Judge_RejectedCaDirectoryCertificateInASoundSystemChain_CountsUnlessItIsTheRoot(bool rejectsRoot, bool accepted)
    {
        using var chain = IssuedChain.Create();
        var certificate = rejectsRoot ? chain.Root : chain.Intermediate;
        var directory = Directory(certificate, Trusted(certificate, null, ServerAuthentication));
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateDirectory: directory), matchesSchannelBuild: false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();

        // Stands for the system store's chain: built without fault up to its root.
        using var sound = new X509Chain();
        sound.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        sound.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        sound.ChainPolicy.CustomTrustStore.Add(chain.Root);
        sound.ChainPolicy.ExtraStore.Add(chain.Intermediate);
        Assert.IsTrue(sound.Build(chain.Leaf));

        var (_, failure) = verification.Judge(SslPolicyErrors.None, sound, Host, anchors, null, chain.Sent);

        Assert.AreEqual(accepted ? null : Rejected, failure?.Message);
    }

    [TestMethod]
    public void Judge_RejectedCaDirectoryRootAndNoChain_DoesNotReportARejection()
    {
        using var chain = IssuedChain.Create();
        var directory = Directory(chain.Root, Trusted(chain.Root, null, ServerAuthentication));
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateDirectory: directory), matchesSchannelBuild: false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();

        var (_, failure) = verification.Judge(SslPolicyErrors.RemoteCertificateNotAvailable, null, Host, anchors, null, []);

        Assert.AreNotEqual(Rejected, failure?.Message);
    }

    private const string Undecodable = "-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n";

    // A new CA directory holding the certificate's hashed files, one per content, <hash>.0 first.
    private string Directory(X509Certificate2 certificate, params string[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "curl-capath-trusted-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        _directories.Add(directory);
        Write(directory, certificate, contents);
        return directory;
    }

    private static void Write(string directory, X509Certificate2 certificate, params string[] contents)
    {
        var hash = OpenSslSubjectNameHash.Of(certificate.SubjectName);
        for (var number = 0; number < contents.Length; number++)
        {
            File.WriteAllText(Path.Combine(directory, $"{hash}.{number}"), contents[number]);
        }
    }

    [TestCleanup]
    public void DeleteDirectories() => _directories.ForEach(directory => System.IO.Directory.Delete(directory, recursive: true));

    private static ServerCertificateChainVerdict VerifyDirectory(string directory, IssuedChain chain, string? caFile = null)
    {
        var options = new TlsClientOptions(
            CaCertificateBlob: caFile is null ? null : (ReadOnlyMemory<byte>?)Encoding.ASCII.GetBytes(caFile),
            CaCertificateDirectory: directory);
        return new ServerCertificateVerification(options, matchesSchannelBuild: false, TimeProvider.System).VerifyChain(chain.Sent, Host);
    }

    private static ServerCertificateVerification Verification(string pem, bool matchesSchannelBuild) =>
        new(new TlsClientOptions(CaCertificateBlob: Encoding.ASCII.GetBytes(pem)), matchesSchannelBuild, TimeProvider.System);

    private static ServerCertificateChainVerdict Verify(string pem, IssuedChain chain, string host = Host) =>
        Verification(pem, matchesSchannelBuild: false).VerifyChain(chain.Sent, host);

    private static void AssertVerdict(bool accepted, ServerCertificateChainVerdict verdict)
    {
        Assert.AreEqual(accepted ? CurlExitCode.Ok : CurlExitCode.PeerFailedVerification, verdict.ExitCode, verdict.ErrorMessage);
        if (!accepted)
        {
            Assert.AreEqual(Rejected, verdict.ErrorMessage);
        }
    }

    private static string Plain(X509Certificate2 certificate) => certificate.ExportCertificatePem() + "\n";

    // openssl x509 -trustout: the certificate's DER followed by X509_CERT_AUX.
    private static string Trusted(X509Certificate2 certificate, string? trusted, string? rejected)
    {
        var settings = new AsnWriter(AsnEncodingRules.DER);
        using (settings.PushSequence())
        {
            WriteUses(settings, trusted, Asn1Tag.Sequence);
            WriteUses(settings, rejected, RejectTag);
        }

        return new string(PemEncoding.Write("TRUSTED CERTIFICATE", [.. certificate.RawData, .. settings.Encode()])) + "\n";
    }

    private static void WriteUses(AsnWriter writer, string? use, Asn1Tag tag)
    {
        if (use is null)
        {
            return;
        }

        using (writer.PushSequence(tag))
        {
            writer.WriteObjectIdentifier(use);
        }
    }

    // A root, an intermediate it issued and a leaf for localhost the intermediate issued.
    private sealed class IssuedChain : IDisposable
    {
        private IssuedChain(X509Certificate2 root, X509Certificate2 intermediate, X509Certificate2 leaf)
        {
            Root = root;
            Intermediate = intermediate;
            Leaf = leaf;
        }

        public X509Certificate2 Root { get; }

        public X509Certificate2 Intermediate { get; }

        public X509Certificate2 Leaf { get; }

        public ReadOnlyMemory<byte>[] Sent => [Leaf.RawData, Intermediate.RawData];

        public static IssuedChain Create()
        {
            var from = DateTimeOffset.UtcNow.AddDays(-1);
            using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var root = AuthorityRequest("CN=trusted-root", rootKey).CreateSelfSigned(from, from.AddDays(30));
            using var unsigned = AuthorityRequest("CN=trusted-intermediate", intermediateKey).Create(root, from, from.AddDays(20), [1]);
            var intermediate = unsigned.CopyWithPrivateKey(intermediateKey);
            var leafRequest = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(Host);
            leafRequest.CertificateExtensions.Add(names.Build());
            var leaf = leafRequest.Create(intermediate, from, from.AddDays(10), [2]);
            return new IssuedChain(root, intermediate, leaf);
        }

        public void Dispose()
        {
            Root.Dispose();
            Intermediate.Dispose();
            Leaf.Dispose();
        }

        private static CertificateRequest AuthorityRequest(string name, ECDsa key)
        {
            var request = new CertificateRequest(name, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            return request;
        }
    }
}
