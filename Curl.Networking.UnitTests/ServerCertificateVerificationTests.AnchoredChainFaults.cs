using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins which faults count under a non-self-signed <c>--cacert</c> anchor in the Schannel build
/// (STD-204, ADR-1356): those of the chain up to the anchor, named by their trust error, and not
/// the basic constraints an anchor does not have. Expected texts are curl 8.22.0 Schannel's.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationAnchoredChainFaultsTests
{
    private const string Host = "anchored-faults.test";

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-anchored-faults-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(X509ChainStatusFlags.InvalidBasicConstraints, X509ChainStatusFlags.NoError, false, X509ChainStatusFlags.NoError, DisplayName = "anchor without basic constraints")]
    [DataRow(X509ChainStatusFlags.InvalidBasicConstraints, X509ChainStatusFlags.NoError, true, X509ChainStatusFlags.InvalidBasicConstraints, DisplayName = "anchor with basic constraints")]
    [DataRow(X509ChainStatusFlags.NoError, X509ChainStatusFlags.InvalidBasicConstraints, true, X509ChainStatusFlags.NoError, DisplayName = "above the anchor")]
    [DataRow(X509ChainStatusFlags.NotValidForUsage, X509ChainStatusFlags.NotValidForUsage, true, X509ChainStatusFlags.NotValidForUsage, DisplayName = "at and above the anchor")]
    public void WithoutFaultsOutsideAnchoredChain_KeepsOnlyTheAnchoredChainsFaults(
        X509ChainStatusFlags anchorFaults, X509ChainStatusFlags aboveFaults, bool anchorHasBasicConstraints, X509ChainStatusFlags kept)
    {
        X509ChainStatus[] chainStatus =
        [
            new() { Status = X509ChainStatusFlags.PartialChain },
            new() { Status = anchorFaults | aboveFaults },
            new() { Status = X509ChainStatusFlags.NoIssuanceChainPolicy },
        ];

        var left = ServerCertificateVerification.WithoutFaultsOutsideAnchoredChain(
            chainStatus, [X509ChainStatusFlags.NoError, anchorFaults, aboveFaults], 1, anchorHasBasicConstraints);

        var expected = new[] { X509ChainStatusFlags.PartialChain, kept, X509ChainStatusFlags.NoIssuanceChainPolicy }
            .Where(status => status != X509ChainStatusFlags.NoError).ToArray();
        CollectionAssert.AreEqual(expected, left.Select(status => status.Status).ToArray());
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.NotValidForUsage, true, "schannel: error 0x00000010")]
    [DataRow(X509ChainStatusFlags.NoIssuanceChainPolicy, true, "schannel: error 0x02000000")]
    [DataRow(X509ChainStatusFlags.HasNotSupportedCriticalExtension | X509ChainStatusFlags.InvalidExtension, true, "schannel: error 0x08000100")]
    [DataRow(X509ChainStatusFlags.NotValidForUsage | X509ChainStatusFlags.RevocationStatusUnknown, false, "schannel: error 0x00000010")]
    public void VerifyPeer_FaultUnderAListedNonSelfSignedAnchor_IsNamedByItsTrustError(
        X509ChainStatusFlags fault, bool skipRevocationCheck, string message)
    {
        using var root = Create("CN=Faults Root", null);
        using var anchor = Create("CN=Faults Anchor", root);
        using var leaf = CreateLeaf(anchor);

        var failure = Verify(leaf, anchor, skipRevocationCheck, PartialChainWith(fault));

        Assert.AreEqual((CurlExitCode.PeerFailedVerification, message), failure);
    }

    [TestMethod]
    public void VerifyPeer_FaultAndNoUsableListBelowTheAnchor_IsRevocationStatusUnknown()
    {
        using var root = Create("CN=Faults Root", null);
        using var anchor = Create("CN=Faults Anchor", root);
        using var leaf = CreateLeaf(anchor);
        var status = PartialChainWith(X509ChainStatusFlags.NotValidForUsage | X509ChainStatusFlags.RevocationStatusUnknown);

        var failure = Verify(leaf, anchor, false, status, X509ChainStatusFlags.RevocationStatusUnknown);

        Assert.AreEqual((CurlExitCode.PeerFailedVerification, TlsFailureMessages.SchannelRevocationStatusUnknown), failure);
    }

    [TestMethod]
    public void VerifyPeer_FaultAndTheLeafOnTheAnchorsList_IsTrustRevoked()
    {
        using var root = Create("CN=Faults Root", null);
        using var anchor = Create("CN=Faults Anchor", root, usage: X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign);
        using var leaf = CreateLeaf(anchor, "http://lists.test/anchor.crl");
        var builder = new CertificateRevocationListBuilder();
        builder.AddEntry(leaf);
        var list = builder.Build(
            anchor, 1, DateTimeOffset.UtcNow.AddDays(1), HashAlgorithmName.SHA256, null, DateTimeOffset.UtcNow.AddHours(-1));
        var status = PartialChainWith(X509ChainStatusFlags.NotValidForUsage | X509ChainStatusFlags.RevocationStatusUnknown);

        var failure = Verify(leaf, anchor, false, status, X509ChainStatusFlags.RevocationStatusUnknown, list: list);

        Assert.AreEqual((CurlExitCode.PeerFailedVerification, TlsFailureMessages.SchannelChainRevoked), failure);
    }

    // HttpClient follows a list's redirect to a file or opaque address and then throws these (STD-205).
    [TestMethod]
    [DataRow(false, false, DisplayName = "no host to connect to")]
    [DataRow(true, false, DisplayName = "no port to connect to")]
    [DataRow(false, true, DisplayName = "no host to connect to, best effort")]
    [DataRow(true, true, DisplayName = "no port to connect to, best effort")]
    public void VerifyPeer_ListFetchRedirectedToAnAddressHttpCannotOpen_LeavesTheStatusUnknown(bool hasHost, bool bestEffort)
    {
        using var root = Create("CN=Faults Root", null);
        using var anchor = Create("CN=Faults Anchor", root);
        using var leaf = CreateLeaf(anchor, "http://lists.test/anchor.crl");
        Exception thrown = hasHost
            ? new ArgumentOutOfRangeException("port")
            : new UriFormatException("Invalid URI: The hostname could not be parsed.");
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        var failure = Verify(
            leaf, anchor, false, PartialChainWith(X509ChainStatusFlags.RevocationStatusUnknown), X509ChainStatusFlags.RevocationStatusUnknown,
            fetch: (_, _) => throw thrown, bestEffort: bestEffort, log: log);

        (CurlExitCode, string)? expected = bestEffort
            ? null
            : (CurlExitCode.PeerFailedVerification, TlsFailureMessages.SchannelRevocationStatusUnknown);
        Assert.AreEqual(expected, failure);
        Assert.AreEqual($"CRL http://lists.test/anchor.crl could not be fetched: {thrown.Message}", log.Lines.Single().Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_AnchorWithoutBasicConstraints_Accepts()
    {
        using var root = Create("CN=Faults Root", null);
        using var anchor = Create("CN=Faults Anchor", root, basicConstraints: false);
        using var leaf = CreateLeaf(anchor);

        var failure = Verify(leaf, anchor, true, null);

        Assert.IsNull(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_PathLengthBrokenAboveTheAnchor_Accepts()
    {
        using var root = Create("CN=Faults Root", null);
        using var parent = Create("CN=Faults Parent", root, pathLength: 0);
        using var anchor = Create("CN=Faults Anchor", parent);
        using var leaf = CreateLeaf(anchor);

        var failure = Verify(leaf, anchor, true, null, sent: parent);

        Assert.IsNull(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_AnchorWithoutKeyCertSign_IsError0x00000010()
    {
        using var root = Create("CN=Faults Root", null);
        using var anchor = Create("CN=Faults Anchor", root, usage: X509KeyUsageFlags.DigitalSignature);
        using var leaf = CreateLeaf(anchor);

        var failure = Verify(leaf, anchor, true, null);

        Assert.AreEqual((CurlExitCode.PeerFailedVerification, "schannel: error 0x00000010"), failure);
    }

    private static X509ChainStatus[] PartialChainWith(X509ChainStatusFlags fault) =>
        [new() { Status = X509ChainStatusFlags.PartialChain }, new() { Status = fault }];

    // A null chainStatus reads the platform's own; sent is a certificate the server sends beside its own;
    // list answers every revocation list fetch, unless fetch is given to answer them.
    private (CurlExitCode ExitCode, string Message)? Verify(
        X509Certificate2 leaf, X509Certificate2 listed, bool skipRevocationCheck, X509ChainStatus[]? chainStatus,
        X509ChainStatusFlags belowAnchor = X509ChainStatusFlags.NoError, X509Certificate2? sent = null, byte[]? list = null,
        Func<Uri, CancellationToken, byte[]>? fetch = null, bool bestEffort = false, IDiagnosticLog? log = null)
    {
        var caFile = Path.Combine(_directory, "anchor.pem");
        File.WriteAllText(caFile, listed.ExportCertificatePem());
        var options = new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: skipRevocationCheck, RevocationCheckBestEffort: bestEffort);
        var verification = chainStatus is null
            ? new ServerCertificateVerification(options, true, TimeProvider.System)
            : new ServerCertificateVerification(options, true, TimeProvider.System)
            {
                ReadChainStatus = _ => chainStatus,
                ReadCertificatesBelowAnchorStatus = _ => [new() { Status = belowAnchor }],
                FetchRevocationList = fetch ?? ((_, _) => list ?? throw new IOException("no list")),
                RevocationDiagnosticLog = log ?? NoDiagnosticLog.Instance,
            };
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        chainPolicy.DisableCertificateDownloads = true;
        if (sent is not null)
        {
            chainPolicy.ExtraStore.Add(sent);
        }

        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
    }

    private static X509Certificate2 Create(
        string subject, X509Certificate2? issuer, bool basicConstraints = true, int? pathLength = null,
        X509KeyUsageFlags usage = X509KeyUsageFlags.KeyCertSign)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"{subject} {Guid.NewGuid():N}", key, HashAlgorithmName.SHA256);
        if (basicConstraints)
        {
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, pathLength.HasValue, pathLength ?? 0, true));
        }

        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (issuer is null)
        {
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(30));
        }

        using var certificate = Sign(request, issuer);
        return certificate.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string? listAddress = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        if (listAddress is not null)
        {
            request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([listAddress]));
        }

        return Sign(request, issuer);
    }

    private static X509Certificate2 Sign(CertificateRequest request, X509Certificate2 issuer)
    {
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        using var issuerKey = issuer.GetECDsaPrivateKey()!;
        return request.Create(
            issuer.SubjectName, X509SignatureGenerator.CreateForECDsa(issuerKey),
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), RandomNumberGenerator.GetBytes(8));
    }
}
