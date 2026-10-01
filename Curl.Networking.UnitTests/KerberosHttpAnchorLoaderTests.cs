using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="KerberosHttpAnchorLoader" /> against MIT's <c>load_anchor</c> (ADR-0300):
/// a <c>FILE:</c> PEM file's every certificate, a <c>DIR:</c> directory's files but those
/// starting with a dot and those that do not load, an <c>ENV:</c> variable's value, and an
/// <see cref="IOException" /> for every anchor that cannot be loaded.
/// </summary>
[TestClass]
public sealed class KerberosHttpAnchorLoaderTests
{
    private const string NotACertificate = "-----BEGIN CERTIFICATE-----\nAQIDBA==\n-----END CERTIFICATE-----\n";

    private static readonly Lazy<X509Certificate2> First = new(() => SelfSigned("first.example.test"));

    private static readonly Lazy<X509Certificate2> Second = new(() => SelfSigned("second.example.test"));

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"kdcproxy-anchors-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void Load_FileWithTwoCertificates_LoadsBoth()
    {
        string file = Write("both.pem", First.Value.ExportCertificatePem() + "\n" + Second.Value.ExportCertificatePem());

        X509Certificate2Collection roots = KerberosHttpAnchorLoader.Load([$"FILE:{file}"], _ => null);

        CollectionAssert.AreEqual(new[] { First.Value.Thumbprint, Second.Value.Thumbprint }, roots.Select(root => root.Thumbprint).ToArray());
    }

    [TestMethod]
    public void Load_FileWithNoCertificate_LoadsNothing()
    {
        string file = Write("empty.pem", "no certificates here\n");

        Assert.IsEmpty(KerberosHttpAnchorLoader.Load([$"FILE:{file}"], _ => null));
    }

    [TestMethod]
    public void Load_MissingFile_ThrowsIOException()
    {
        Assert.ThrowsExactly<FileNotFoundException>(() => KerberosHttpAnchorLoader.Load([$"FILE:{Path.Combine(_directory, "missing.pem")}"], _ => null));
    }

    [TestMethod]
    public void Load_FileWhoseCertificateDoesNotDecode_ThrowsIOExceptionNamingIt()
    {
        string file = Write("bad.pem", NotACertificate);

        IOException failure = Assert.ThrowsExactly<IOException>(() => KerberosHttpAnchorLoader.Load([$"FILE:{file}"], _ => null));

        StringAssert.StartsWith(failure.Message, $"The http_anchors file {file} cannot be loaded: ");
        Assert.IsInstanceOfType<CryptographicException>(failure.InnerException);
    }

    [TestMethod]
    public void Load_FileThatIsADirectory_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => KerberosHttpAnchorLoader.Load([$"FILE:{_directory}"], _ => null));

        Assert.IsInstanceOfType<UnauthorizedAccessException>(failure.InnerException);
    }

    [TestMethod]
    public void Load_Directory_LoadsEveryFileThatLoadsButThoseStartingWithADot()
    {
        Write("first.pem", First.Value.ExportCertificatePem());
        Write("bad.pem", NotACertificate);
        Write(".hidden.pem", Second.Value.ExportCertificatePem());

        X509Certificate2Collection roots = KerberosHttpAnchorLoader.Load([$"DIR:{_directory}"], _ => null);

        CollectionAssert.AreEqual(new[] { First.Value.Thumbprint }, roots.Select(root => root.Thumbprint).ToArray());
    }

    [TestMethod]
    public void Load_DirectoryWithNoFileThatLoads_ThrowsIOException()
    {
        Write("bad.pem", NotACertificate);
        Write(".hidden.pem", First.Value.ExportCertificatePem());

        IOException failure = Assert.ThrowsExactly<IOException>(() => KerberosHttpAnchorLoader.Load([$"DIR:{_directory}"], _ => null));

        Assert.AreEqual($"The http_anchors directory {_directory} has no file that loads.", failure.Message);
    }

    [TestMethod]
    public void Load_MissingDirectory_ThrowsIOException()
    {
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => KerberosHttpAnchorLoader.Load([$"DIR:{Path.Combine(_directory, "missing")}"], _ => null));
    }

    [TestMethod]
    public void Load_EnvironmentVariableNamingAFile_LoadsTheFile()
    {
        string file = Write("first.pem", First.Value.ExportCertificatePem());

        X509Certificate2Collection roots = KerberosHttpAnchorLoader.Load(["ENV:KDCPROXY_CA"], name => name == "KDCPROXY_CA" ? $"FILE:{file}" : null);

        Assert.AreEqual(First.Value.Thumbprint, roots.Single().Thumbprint);
    }

    [TestMethod]
    public void Load_UnsetEnvironmentVariable_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => KerberosHttpAnchorLoader.Load(["ENV:KDCPROXY_CA"], _ => null));

        Assert.AreEqual("The http_anchors environment variable KDCPROXY_CA is not set.", failure.Message);
    }

    [TestMethod]
    public void Load_EnvironmentVariableNamingAnotherVariable_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => KerberosHttpAnchorLoader.Load(["ENV:KDCPROXY_CA"], _ => "ENV:KDCPROXY_CA"));

        Assert.AreEqual("The http_anchors value ENV:KDCPROXY_CA is not a FILE:, DIR: or ENV: anchor.", failure.Message);
    }

    [TestMethod]
    public void Load_ValueWithNoKnownPrefix_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => KerberosHttpAnchorLoader.Load(["/etc/ca.pem"], _ => null));

        Assert.AreEqual("The http_anchors value /etc/ca.pem is not a FILE:, DIR: or ENV: anchor.", failure.Message);
    }

    [TestMethod]
    public void Load_SeveralAnchors_LoadsThemAllInOrder()
    {
        string first = Write("first.pem", First.Value.ExportCertificatePem());
        string second = Write("second.pem", Second.Value.ExportCertificatePem());

        X509Certificate2Collection roots = KerberosHttpAnchorLoader.Load([$"FILE:{second}", $"FILE:{first}"], _ => null);

        CollectionAssert.AreEqual(new[] { Second.Value.Thumbprint, First.Value.Thumbprint }, roots.Select(root => root.Thumbprint).ToArray());
    }

    private static X509Certificate2 SelfSigned(string host)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private string Write(string name, string contents)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, contents);
        return path;
    }
}
