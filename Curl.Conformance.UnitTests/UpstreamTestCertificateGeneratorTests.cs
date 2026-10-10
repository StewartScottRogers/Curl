using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins what <see cref="UpstreamTestCertificateGenerator"/> writes from the vendored
/// <c>tests/certs/*.prm</c> files: every file a vendored case names, and per kind of
/// certificate the subject, subject alternative names and extensions its <c>.prm</c> names.
/// </summary>
[TestClass]
public sealed class UpstreamTestCertificateGeneratorTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly string ParametersFolder = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", "certs");

    private static string outputFolder = null!;

    private const string MinimalParameters =
        "extensions = x509v3\n[ x509v3 ]\nbasicConstraints = CA:false\n[ req_DN ]\ncountryName_value = NN\norganizationName_value = O\ncommonName_value = c\n";

    [ClassInitialize]
    public static void GenerateOnce(TestContext context)
    {
        _ = context;
        outputFolder = Path.Combine(Path.GetTempPath(), "curl-certs-" + Guid.NewGuid().ToString("N"));
        new UpstreamTestCertificateGenerator(TimeProvider.System).Generate(ParametersFolder, outputFolder);
    }

    [ClassCleanup]
    public static void DeleteOutput() => Directory.Delete(outputFolder, recursive: true);

    [TestMethod]
    [DataRow("test-ca.crt")]
    [DataRow("test-ca.cacert")]
    [DataRow("test-ca.der")]
    [DataRow("test-ca.key")]
    [DataRow("test-client-cert.crt")]
    [DataRow("test-client-cert.key")]
    [DataRow("test-client-eku-only.crt")]
    [DataRow("test-client-eku-only.key")]
    [DataRow("test-localhost.crl")]
    [DataRow("test-localhost.crt")]
    [DataRow("test-localhost.der")]
    [DataRow("test-localhost.pem")]
    [DataRow("test-localhost.pub.der")]
    [DataRow("test-localhost.pub.pem")]
    [DataRow("test-localhost.nn.pub.der")]
    [DataRow("test-localhost.nn.pem")]
    [DataRow("test-localhost0h.pem")]
    [DataRow("test-localhost-san-first.pem")]
    [DataRow("test-localhost-san-last.pem")]
    public void Generate_VendoredParameters_WritesTheFileACaseNames(string fileName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file", fileName);
        bool exists = File.Exists(Path.Combine(outputFolder, fileName));
        diagnostics.Assert("file exists", true, exists);
        Assert.IsTrue(exists);
    }

    [TestMethod]
    public void Generate_CertificateAuthority_HasTheSubjectAndExtensionsOfItsPrm()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using X509Certificate2 authority = Load("test-ca.crt");
        diagnostics.Act("subject", authority.Subject);
        X509BasicConstraintsExtension basic = Single<X509BasicConstraintsExtension>(authority);
        X509KeyUsageExtension usage = Single<X509KeyUsageExtension>(authority);
        diagnostics.Assert("subject", "C=NN/O=Edel curl Arctic Illudium Research Cloud/CN=Northern Nowhere Trust Anchor", Names(authority.SubjectName));
        Assert.AreEqual("C=NN/O=Edel curl Arctic Illudium Research Cloud/CN=Northern Nowhere Trust Anchor", Names(authority.SubjectName));
        Assert.AreEqual(authority.Subject, authority.Issuer);
        Assert.IsTrue(basic.CertificateAuthority && basic.Critical);
        Assert.AreEqual(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, usage.KeyUsages);
        Assert.IsTrue(usage.Critical);
        Assert.AreEqual(
            Convert.ToHexString(Single<X509SubjectKeyIdentifierExtension>(authority).SubjectKeyIdentifierBytes.Span),
            Convert.ToHexString(Single<X509AuthorityKeyIdentifierExtension>(authority).KeyIdentifier!.Value.Span));
        Assert.AreEqual("http://test.curl.se/ca/EdelCurlRoot.cer", Single<X509AuthorityInformationAccessExtension>(authority).EnumerateCAIssuersUris().Single());
        StringAssert.Contains(Encoding(authority, "2.5.29.31"), "http://test.curl.se/ca/EdelCurlRoot.crl");
        Assert.AreEqual(6000, (authority.NotAfter - authority.NotBefore).Days);
    }

    [TestMethod]
    public void Generate_Localhost_IsAServerCertificateTheAuthoritySigned()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using X509Certificate2 authority = Load("test-ca.der");
        using X509Certificate2 server = Load("test-localhost.crt");
        diagnostics.Act("subject", server.Subject);
        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        bool built = chain.Build(server);
        diagnostics.Assert("chain builds to the generated CA", true, built);
        Assert.IsTrue(built);
        Assert.AreEqual("C=NN/O=Edel curl Arctic Illudium Research Cloud/CN=localhost", Names(server.SubjectName));
        Assert.AreEqual("localhost", string.Join(",", Single<X509SubjectAlternativeNameExtension>(server).EnumerateDnsNames()));
        Assert.AreEqual("1.3.6.1.5.5.7.3.1", string.Join(",", Usages(server)));
        Assert.AreEqual(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement, Single<X509KeyUsageExtension>(server).KeyUsages);
        Assert.IsFalse(Single<X509BasicConstraintsExtension>(server).CertificateAuthority);
        Assert.AreEqual(300, (server.NotAfter - server.NotBefore).Days);
        CollectionAssert.AreEqual(server.RawData, File.ReadAllBytes(Path.Combine(outputFolder, "test-localhost.der")));
        CollectionAssert.AreEqual(server.PublicKey.ExportSubjectPublicKeyInfo(), File.ReadAllBytes(Path.Combine(outputFolder, "test-localhost.pub.der")));
    }

    [TestMethod]
    [DataRow("test-localhost.nn", "localhost.nn", "localhost.nn")]
    [DataRow("test-localhost-san-first", "localhost.nn", "localhost,localhost1,localhost2")]
    [DataRow("test-localhost-san-last", "localhost.nn", "localhost1,localhost2,localhost")]
    public void Generate_ServerVariant_HasItsCommonNameAndDnsNamesInOrder(string prefix, string commonName, string dnsNames)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using X509Certificate2 server = Load(prefix + ".crt");
        string names = string.Join(",", Single<X509SubjectAlternativeNameExtension>(server).EnumerateDnsNames());
        diagnostics.Act("dns names", names);
        diagnostics.Assert("dns names", dnsNames, names);
        Assert.AreEqual(dnsNames, names);
        Assert.AreEqual(commonName, server.GetNameInfo(X509NameType.SimpleName, forIssuer: false));
    }

    [TestMethod]
    public void Generate_Localhost0h_PutsTheRawDerSubjectAlternativeNameWithItsNul()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using X509Certificate2 server = Load("test-localhost0h.crt");
        string encoded = Convert.ToHexString(server.Extensions["2.5.29.17"]!.RawData);
        diagnostics.Assert("subject alternative name", "300D820B6C6F63616C686F73740068", encoded);
        Assert.AreEqual("300D820B6C6F63616C686F73740068", encoded);
    }

    [TestMethod]
    [DataRow("test-client-cert", "1.3.6.1.5.5.7.3.1,1.3.6.1.5.5.7.3.2")]
    [DataRow("test-client-eku-only", "1.3.6.1.5.5.7.3.2")]
    public void Generate_ClientCertificate_HasTheExtendedKeyUsagesOfItsPrm(string prefix, string usages)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using X509Certificate2 client = X509Certificate2.CreateFromPemFile(
            Path.Combine(outputFolder, prefix + ".crt"), Path.Combine(outputFolder, prefix + ".key"));
        string actual = string.Join(",", Usages(client));
        diagnostics.Assert("extended key usages", usages, actual);
        Assert.AreEqual(usages, actual);
        Assert.IsTrue(client.HasPrivateKey);
    }

    [TestMethod]
    public void Generate_ServerPem_HoldsThePrmTheKeyAndTheCertificate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string pemPath = Path.Combine(outputFolder, "test-localhost.pem");
        string pem = File.ReadAllText(pemPath);
        using X509Certificate2 server = X509Certificate2.CreateFromPemFile(pemPath);
        diagnostics.Assert("pem loads with its private key", true, server.HasPrivateKey);
        Assert.IsTrue(server.HasPrivateKey);
        Assert.StartsWith(File.ReadAllText(Path.Combine(ParametersFolder, "test-localhost.prm")), pem);
        Assert.AreEqual("C=NN/O=Edel curl Arctic Illudium Research Cloud/CN=localhost", Names(server.SubjectName));
    }

    [TestMethod]
    public void Generate_RevocationList_RevokesItsCertificate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using X509Certificate2 server = Load("test-localhost.crt");
        CertificateRevocationListBuilder list = CertificateRevocationListBuilder.LoadPem(
            File.ReadAllText(Path.Combine(outputFolder, "test-localhost.crl")), out _);
        bool revoked = list.RemoveEntry(server.SerialNumberBytes.Span);
        diagnostics.Assert("serial is on the list", true, revoked);
        Assert.IsTrue(revoked);
    }

    [TestMethod]
    public void GenerateCertificateAuthority_NoRequestSection_ReadsTheDefaultDistinguishedNameSection()
    {
        using TemporaryFolder folder = new();
        using X509Certificate2 authority = new UpstreamTestCertificateGenerator(TimeProvider.System)
            .GenerateCertificateAuthority(MinimalParameters, folder.Path);
        Assert.AreEqual("C=NN/O=O/CN=c", Names(authority.SubjectName));
        Assert.IsTrue(authority.HasPrivateKey);
    }

    [TestMethod]
    [DataRow("keyUsage = nonRepudiation", "nonRepudiation")]
    [DataRow("extendedKeyUsage = codeSigning", "codeSigning")]
    [DataRow("subjectAltName = IP:127.0.0.1", "IP:127.0.0.1")]
    [DataRow("crlDistributionPoints = URI:http://x/", "URI:http://x/")]
    [DataRow("nameConstraints = critical,permitted;DNS:x", "nameConstraints")]
    public void GenerateCertificateAuthority_ExtensionValueItDoesNotWrite_Throws(string line, string named)
    {
        using TemporaryFolder folder = new();
        string parameters = MinimalParameters.Replace("basicConstraints = CA:false", line, StringComparison.Ordinal);
        var exception = Assert.ThrowsExactly<FormatException>(
            () => new UpstreamTestCertificateGenerator(TimeProvider.System).GenerateCertificateAuthority(parameters, folder.Path));
        StringAssert.Contains(exception.Message, named);
    }

    [TestMethod]
    [DataRow("[ req_DN ]\ncountryName_value = NN\norganizationName_value = O\ncommonName_value = c\n", "extensions")]
    [DataRow("extensions = x509v3\n[ req ]\ndistinguished_name = dn\n[ dn ]\ncountryName_value = NN\n", "organizationName_value")]
    public void GenerateCertificateAuthority_MissingParameter_Throws(string parameters, string named)
    {
        using TemporaryFolder folder = new();
        var exception = Assert.ThrowsExactly<FormatException>(
            () => new UpstreamTestCertificateGenerator(TimeProvider.System).GenerateCertificateAuthority(parameters, folder.Path));
        StringAssert.Contains(exception.Message, named);
    }

    [TestMethod]
    public void GenerateCertificate_AuthorityWithoutPrivateKey_Throws()
    {
        using TemporaryFolder folder = new();
        using X509Certificate2 authority = Load("test-ca.der");
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new UpstreamTestCertificateGenerator(TimeProvider.System).GenerateCertificate("x", MinimalParameters, authority, folder.Path));
        Assert.AreEqual("authority", exception.ParamName);
    }

    [TestMethod]
    public void GenerateCertificate_AuthorityWithoutSubjectKeyIdentifier_Throws()
    {
        using TemporaryFolder folder = new();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 authority = new CertificateRequest("CN=a", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new UpstreamTestCertificateGenerator(TimeProvider.System).GenerateCertificate("x", MinimalParameters, authority, folder.Path));
        StringAssert.Contains(exception.Message, "subject key identifier");
    }

    private static X509Certificate2 Load(string fileName) =>
        X509CertificateLoader.LoadCertificateFromFile(Path.Combine(outputFolder, fileName));

    // The name's attributes in DER order, as OpenSSL's req writes them, the same on every platform.
    private static string Names(X500DistinguishedName name) =>
        string.Join("/", name.EnumerateRelativeDistinguishedNames().Select(
            rdn => ShortName(rdn.GetSingleElementType().Value) + "=" + rdn.GetSingleElementValue()));

    private static string ShortName(string? oid) => oid switch
    {
        "2.5.4.6" => "C",
        "2.5.4.10" => "O",
        _ => "CN",
    };

    private static T Single<T>(X509Certificate2 certificate)
        where T : X509Extension => certificate.Extensions.OfType<T>().Single();

    private static string Encoding(X509Certificate2 certificate, string oid) =>
        System.Text.Encoding.ASCII.GetString(certificate.Extensions[oid]!.RawData);

    private static IEnumerable<string?> Usages(X509Certificate2 certificate) =>
        Single<X509EnhancedKeyUsageExtension>(certificate).EnhancedKeyUsages.Cast<Oid>().Select(oid => oid.Value);

    private sealed class TemporaryFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "curl-certs-" + Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
