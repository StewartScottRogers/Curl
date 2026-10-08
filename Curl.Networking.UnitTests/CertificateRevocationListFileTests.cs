using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="CertificateRevocationListFile" /> loads a <c>--crlfile</c> and checks a chain as
/// curl 8.18.0's OpenSSL 3.5.5 build does, measured with Record-CurlExchange.ps1 -Tls
/// -TlsRootCertificateFile and --cacert, 2026-09-29 (BL-609): garbage, an empty file, a DER list
/// and a directory are exit 82 (<c>error loading CRL file: &lt;path&gt;</c>); an empty list from
/// the root is accepted; a list revoking the server certificate is <c>certificate revoked (23)</c>;
/// a list from another CA is <c>unable to get certificate CRL (3)</c>; a list in the root's name
/// signed by another key is <c>CRL signature failure (8)</c>.
/// </summary>
[TestClass]
public sealed class CertificateRevocationListFileTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset ExpiryMoment = new(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static X509Certificate2 s_root = null!;

    private static X509Certificate2 s_leaf = null!;

    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [ClassInitialize]
    public static void CreateCertificates(TestContext context)
    {
        s_root = CertificateRevocationListTests.CreateAuthority("CN=BL609 File Root", RSA.Create(2048));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=leaf.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        s_leaf = request.Create(s_root, Now.AddDays(-1), Now.AddDays(1), [7]);
    }

    [ClassCleanup]
    public static void DisposeCertificates()
    {
        s_root.Dispose();
        s_leaf.Dispose();
    }

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Curl.Networking.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void Load_WithAFileThatDoesNotExist_ThrowsNamingTheFile()
    {
        var path = Path.Combine(_directory, "nosuch.crl");
        Diagnostics.Arrange("file name", Path.GetFileName(path));

        var exception = Assert.ThrowsExactly<CertificateRevocationListFileException>(() => CertificateRevocationListFile.Load(path));
        Diagnostics.Act("exception type", exception.GetType().Name);
        Diagnostics.Act("inner exception is IOException", exception.InnerException is IOException);

        Diagnostics.Assert("message", $"error loading CRL file: {Path.GetFileName(path)}", exception.Message.Replace(_directory + Path.DirectorySeparatorChar, string.Empty, StringComparison.Ordinal));
        Assert.AreEqual($"error loading CRL file: {path}", exception.Message);
        Diagnostics.Assert("inner exception is IOException", true, exception.InnerException is IOException);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
    }

    [TestMethod]
    public void Load_WithADirectory_Throws()
    {
        Diagnostics.Arrange("path is a directory", Directory.Exists(_directory));

        var exception = Assert.ThrowsExactly<CertificateRevocationListFileException>(() => CertificateRevocationListFile.Load(_directory));
        Diagnostics.Act("exception type", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(CertificateRevocationListFileException), exception.GetType().Name);
        Assert.IsInstanceOfType<CertificateRevocationListFileException>(exception);
    }

    [TestMethod]
    [DataRow("not a crl\n")]
    [DataRow("")]
    [DataRow("-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n")]
    public void Load_WithAFileHoldingNoList_Throws(string content)
    {
        var path = Write("garbage.crl", content);
        Diagnostics.Arrange("content length", content.Length);
        Diagnostics.Arrange("content", content);

        var exception = Assert.ThrowsExactly<CertificateRevocationListFileException>(() => CertificateRevocationListFile.Load(path));
        Diagnostics.Act("exception type", exception.GetType().Name);
        Diagnostics.Act("inner exception is CryptographicException", exception.InnerException is CryptographicException);

        Diagnostics.Assert("inner exception is CryptographicException", true, exception.InnerException is CryptographicException);
        Assert.IsInstanceOfType<CryptographicException>(exception.InnerException);
    }

    [TestMethod]
    public void Load_WithADerList_Throws()
    {
        var path = Path.Combine(_directory, "der.crl");
        var der = TestRevocationList.Write(s_root);
        File.WriteAllBytes(path, der);
        Diagnostics.Arrange("DER length", der.Length);

        var exception = Assert.ThrowsExactly<CertificateRevocationListFileException>(() => CertificateRevocationListFile.Load(path));
        Diagnostics.Act("exception type", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(CertificateRevocationListFileException), exception.GetType().Name);
        Assert.IsInstanceOfType<CertificateRevocationListFileException>(exception);
    }

    [TestMethod]
    public void Load_WithAPemBlockThatIsNotAList_Throws()
    {
        const string content = "-----BEGIN X509 CRL-----\nMAA=\n-----END X509 CRL-----\n";
        var path = Write("bad.crl", content);
        Diagnostics.Arrange("content", content);

        var exception = Assert.ThrowsExactly<CertificateRevocationListFileException>(() => CertificateRevocationListFile.Load(path));
        Diagnostics.Act("exception type", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(CertificateRevocationListFileException), exception.GetType().Name);
        Assert.IsInstanceOfType<CertificateRevocationListFileException>(exception);
    }

    [TestMethod]
    public void FirstRefusal_WithAnEmptyListFromTheRoot_AcceptsTheChain()
    {
        using var chain = BuildChain();
        Diagnostics.Arrange("root subject", s_root.Subject);
        Diagnostics.Arrange("chain length", chain.ChainElements.Count);

        var refusal = Load(TestRevocationList.Pem(EmptyListFromRoot())).FirstRefusal(chain, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal is null", true, refusal is null);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void FirstRefusal_WithTheListAmongOtherBlocksAndText_AcceptsTheChain()
    {
        using var chain = BuildChain();
        var text = "leading text\n" + s_root.ExportCertificatePem() + "\n" + TestRevocationList.Pem(EmptyListFromRoot());
        Diagnostics.Arrange("file text length", text.Length);
        Diagnostics.Arrange("chain length", chain.ChainElements.Count);

        var refusal = Load(text).FirstRefusal(chain, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal is null", true, refusal is null);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void FirstRefusal_WithAListRevokingTheServerCertificate_IsCertificateRevoked()
    {
        using var chain = BuildChain();
        var builder = new CertificateRevocationListBuilder();
        builder.AddEntry(s_leaf);
        var der = builder.Build(s_root, BigInteger.One, Now.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, Now.AddDays(-1));
        Diagnostics.Arrange("revoked leaf subject", s_leaf.Subject);
        Diagnostics.Arrange("DER length", der.Length);

        var refusal = Load(TestRevocationList.Pem(der)).FirstRefusal(chain, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.CertificateRevoked, refusal);
        Assert.AreEqual(OpenSslVerifyResult.CertificateRevoked, refusal);
    }

    [TestMethod]
    public void FirstRefusal_WithAListFromAnotherAuthority_IsUnableToGetTheList()
    {
        using var chain = BuildChain();
        using var other = CertificateRevocationListTests.CreateAuthority("CN=BL609 Other CA", RSA.Create(2048));
        Diagnostics.Arrange("other authority subject", other.Subject);

        var refusal = Load(TestRevocationList.Pem(TestRevocationList.Write(other))).FirstRefusal(chain, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.UnableToGetCertificateRevocationList, refusal);
        Assert.AreEqual(OpenSslVerifyResult.UnableToGetCertificateRevocationList, refusal);
    }

    [TestMethod]
    public void FirstRefusal_WithAListInTheRootsNameSignedByAnotherKey_IsASignatureFailure()
    {
        using var chain = BuildChain();
        using var impostor = CertificateRevocationListTests.CreateAuthority(s_root.Subject, RSA.Create(2048));
        Diagnostics.Arrange("impostor subject", impostor.Subject);

        var refusal = Load(TestRevocationList.Pem(TestRevocationList.Write(impostor))).FirstRefusal(chain, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.CertificateRevocationListSignatureFailure, refusal);
        Assert.AreEqual(OpenSslVerifyResult.CertificateRevocationListSignatureFailure, refusal);
    }

    [TestMethod]
    public void FirstRefusal_WithAnIntermediateTheListDoesNotCover_RefusesTheServerCertificateFirst()
    {
        using var intermediateKey = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest("CN=BL609 Intermediate", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var intermediate = intermediateRequest.Create(s_root, Now.AddDays(-1), Now.AddDays(1), [8]);
        using var intermediateWithKey = intermediate.CopyWithPrivateKey(intermediateKey);
        using var leafKey = RSA.Create(2048);
        using var leaf = new CertificateRequest("CN=deep.example", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Create(intermediateWithKey, Now.AddDays(-1), Now.AddDays(1), [9]);
        using var chain = BuildChain(leaf, intermediate);
        Diagnostics.Arrange("leaf subject", leaf.Subject);
        Diagnostics.Arrange("intermediate subject", intermediate.Subject);

        var refusal = Load(TestRevocationList.Pem(EmptyListFromRoot())).FirstRefusal(chain, Now);
        Diagnostics.Act("chain length", chain.ChainElements.Count);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("chain length", 3, chain.ChainElements.Count);
        Assert.AreEqual(3, chain.ChainElements.Count);
        Diagnostics.Assert("refusal", OpenSslVerifyResult.UnableToGetCertificateRevocationList, refusal);
        Assert.AreEqual(OpenSslVerifyResult.UnableToGetCertificateRevocationList, refusal);
    }

    [TestMethod]
    public void Refusal_WithAnIssuerWhoseKeyUsageForbidsSigningLists_IsKeyUsageDoesNotIncludeCrlSigning()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(s_root.Subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var issuer = request.CreateSelfSigned(Now.AddDays(-1), Now.AddDays(1));
        Diagnostics.Arrange("issuer subject", issuer.Subject);
        Diagnostics.Arrange("issuer key usage", X509KeyUsageFlags.KeyCertSign);

        var refusal = Load(TestRevocationList.Pem(EmptyListFromRoot())).Refusal(s_leaf, issuer, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.KeyUsageDoesNotIncludeCrlSigning, refusal);
        Assert.AreEqual(OpenSslVerifyResult.KeyUsageDoesNotIncludeCrlSigning, refusal);
    }

    [TestMethod]
    public void Refusal_WithAnIssuerWithoutKeyUsage_ChecksTheSignature()
    {
        using var key = RSA.Create(2048);
        using var issuer = new CertificateRequest(s_root.Subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(Now.AddDays(-1), Now.AddDays(1));
        Diagnostics.Arrange("issuer subject", issuer.Subject);
        Diagnostics.Arrange("issuer key usage", "none");

        var refusal = Load(TestRevocationList.Pem(EmptyListFromRoot())).Refusal(s_leaf, issuer, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.CertificateRevocationListSignatureFailure, refusal);
        Assert.AreEqual(OpenSslVerifyResult.CertificateRevocationListSignatureFailure, refusal);
    }

    [TestMethod]
    public void Refusal_BeforeTheListsIssueDate_IsNotYetValid()
    {
        var moment = Now.AddDays(-2);
        Diagnostics.Arrange("days from now", -2);

        var refusal = Load(TestRevocationList.Pem(EmptyListFromRoot())).Refusal(s_leaf, s_root, moment);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.CertificateRevocationListNotYetValid, refusal);
        Assert.AreEqual(OpenSslVerifyResult.CertificateRevocationListNotYetValid, refusal);
    }

    [TestMethod]
    public void Refusal_AfterTheListsExpiry_HasExpired()
    {
        var moment = Now.AddDays(2);
        Diagnostics.Arrange("days from now", 2);

        var refusal = Load(TestRevocationList.Pem(EmptyListFromRoot())).Refusal(s_leaf, s_root, moment);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.CertificateRevocationListHasExpired, refusal);
        Assert.AreEqual(OpenSslVerifyResult.CertificateRevocationListHasExpired, refusal);
    }

    [TestMethod]
    public void Refusal_AtTheListsExpiryMoment_HasExpired()
    {
        Diagnostics.Arrange("expiry moment", ExpiryMoment);
        Diagnostics.Arrange("checked at", ExpiryMoment);

        var refusal = Load(TestRevocationList.Pem(ListExpiringAt(ExpiryMoment))).Refusal(s_leaf, s_root, ExpiryMoment);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal", OpenSslVerifyResult.CertificateRevocationListHasExpired, refusal);
        Assert.AreEqual(OpenSslVerifyResult.CertificateRevocationListHasExpired, refusal);
    }

    [TestMethod]
    public void Refusal_OneSecondBeforeTheListsExpiry_Accepts()
    {
        var moment = ExpiryMoment.AddSeconds(-1);
        Diagnostics.Arrange("expiry moment", ExpiryMoment);
        Diagnostics.Arrange("checked at", moment);

        var refusal = Load(TestRevocationList.Pem(ListExpiringAt(ExpiryMoment))).Refusal(s_leaf, s_root, moment);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal is null", true, refusal is null);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void Refusal_WithAnExpiredListBeforeACurrentOne_UsesTheCurrentOne()
    {
        var expired = TestRevocationList.Write(s_root, thisUpdate: Now.AddDays(-3), nextUpdate: Now.AddDays(-2));
        var file = Load(TestRevocationList.Pem(expired) + TestRevocationList.Pem(EmptyListFromRoot()));
        Diagnostics.Arrange("expired list length", expired.Length);
        Diagnostics.Arrange("lists in file", 2);

        var refusal = file.Refusal(s_leaf, s_root, Now);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal is null", true, refusal is null);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void Refusal_WithOnlyAListWithoutNextUpdate_AcceptsAnyLaterMoment()
    {
        var moment = Now.AddYears(5);
        Diagnostics.Arrange("years from now", 5);
        Diagnostics.Arrange("next update", "none");

        var refusal = Load(TestRevocationList.Pem(TestRevocationList.Write(s_root))).Refusal(s_leaf, s_root, moment);
        Diagnostics.Act("refusal", refusal);

        Diagnostics.Assert("refusal is null", true, refusal is null);
        Assert.IsNull(refusal);
    }

    private static byte[] ListExpiringAt(DateTimeOffset nextUpdate) =>
        TestRevocationList.Write(s_root, thisUpdate: nextUpdate.AddDays(-1), nextUpdate: nextUpdate);

    private static byte[] EmptyListFromRoot() =>
        new CertificateRevocationListBuilder().Build(s_root, BigInteger.One, Now.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, Now.AddDays(-1));

    private static X509Chain BuildChain(X509Certificate2? leaf = null, X509Certificate2? intermediate = null)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(s_root);
        if (intermediate is not null)
        {
            chain.ChainPolicy.ExtraStore.Add(intermediate);
        }

        chain.Build(leaf ?? s_leaf);
        return chain;
    }

    private CertificateRevocationListFile Load(string pem) => CertificateRevocationListFile.Load(Write("list.crl", pem));

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
