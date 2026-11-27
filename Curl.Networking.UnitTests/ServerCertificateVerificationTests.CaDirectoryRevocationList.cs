using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins OpenSSL curl's <c>--crlfile</c> check of a chain anchored by a <c>--capath</c> root the
/// server does not send (STD-219): the lists are checked up to that root.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationCaDirectoryRevocationListTests
{
    private const string Host = "capath-crl.test";

    private static X509Certificate2 s_root = null!;
    private static X509Certificate2 s_intermediate = null!;
    private static X509Certificate2 s_leaf = null!;

    private string _directory = null!;

    [ClassInitialize]
    public static void CreateCertificates(TestContext context)
    {
        s_root = CreateCa("CN=STD219 Root", null);
        s_intermediate = CreateCa("CN=STD219 Intermediate", s_root);
        s_leaf = CreateLeaf(s_intermediate);
    }

    [ClassCleanup]
    public static void DisposeCertificates()
    {
        s_leaf.Dispose();
        s_intermediate.Dispose();
        s_root.Dispose();
    }

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-capath-crl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void VerifyChain_CaDirectoryRootAndEmptyListsFromBothAuthorities_Accepts()
    {
        var verdict = Verify(s_root, EmptyList(s_root), EmptyList(s_intermediate));

        Assert.AreEqual(ServerCertificateChainVerdict.Accepted, verdict);
    }

    [TestMethod]
    public void VerifyChain_CaDirectoryRootAndAListRevokingTheServerCertificate_IsCertificateRevoked()
    {
        var verdict = Verify(s_root, EmptyList(s_root), List(s_intermediate, s_leaf));

        AssertRefused("SSL certificate OpenSSL verify result: certificate revoked (23)", verdict);
    }

    [TestMethod]
    public void VerifyChain_CaDirectoryRootAndNoListFromTheRoot_IsUnableToGetTheList()
    {
        var verdict = Verify(s_root, EmptyList(s_intermediate));

        AssertRefused("SSL certificate OpenSSL verify result: unable to get certificate CRL (3)", verdict);
    }

    [TestMethod]
    public void VerifyChain_CaDirectoryHoldingOnlyTheIntermediate_IsUnableToGetIssuerCertificate()
    {
        var verdict = Verify(s_intermediate, EmptyList(s_root), EmptyList(s_intermediate));

        AssertRefused("SSL certificate OpenSSL verify result: unable to get issuer certificate (2)", verdict);
    }

    private static void AssertRefused(string message, ServerCertificateChainVerdict verdict)
    {
        Assert.AreEqual(message, verdict.ErrorMessage);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, verdict.ExitCode);
    }

    // The OpenSSL build, as --capath <directory> --crlfile <lists>, for a server sending its
    // certificate and the intermediate.
    private ServerCertificateChainVerdict Verify(X509Certificate2 anchor, params string[] lists)
    {
        var directory = Path.Combine(_directory, "capath");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{OpenSslSubjectNameHash.Of(anchor.SubjectName)}.0"), anchor.ExportCertificatePem());
        var listFile = Path.Combine(_directory, "lists.pem");
        File.WriteAllText(listFile, string.Join('\n', lists));
        var options = new TlsClientOptions(CaCertificateDirectory: directory, CertificateRevocationListFile: listFile);
        var verification = new ServerCertificateVerification(options, false, TimeProvider.System);
        return verification.VerifyChain([s_leaf.RawData, s_intermediate.RawData], Host);
    }

    private static string EmptyList(X509Certificate2 issuer) => List(issuer, null);

    private static string List(X509Certificate2 issuer, X509Certificate2? revoked)
    {
        var builder = new CertificateRevocationListBuilder();
        if (revoked is not null)
        {
            builder.AddEntry(revoked);
        }

        var now = DateTimeOffset.UtcNow;
        var der = builder.Build(issuer, 1, now.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, now.AddHours(-1));
        return PemEncoding.WriteString("X509 CRL", der);
    }

    private static X509Certificate2 CreateCa(string subject, X509Certificate2? issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (issuer is null)
        {
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
        }

        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        // The issuer's validity is truncated to whole seconds, so a child ending "now + 30 days" can outlast it.
        using var certificate = request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-2), new DateTimeOffset(issuer.NotAfter.AddDays(-1)), RandomNumberGenerator.GetBytes(8));
        return certificate.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
