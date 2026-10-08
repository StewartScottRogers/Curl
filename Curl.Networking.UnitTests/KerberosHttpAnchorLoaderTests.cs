using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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

        X509Certificate2Collection roots = Load([$"FILE:{file}"], _ => null);

        Diagnostics.Assert("roots", "CN=first.example.test, CN=second.example.test", Subjects(roots));
        CollectionAssert.AreEqual(new[] { First.Value.Thumbprint, Second.Value.Thumbprint }, roots.Select(root => root.Thumbprint).ToArray());
    }

    [TestMethod]
    public void Load_FileWithNoCertificate_LoadsNothing()
    {
        string file = Write("empty.pem", "no certificates here\n");

        X509Certificate2Collection roots = Load([$"FILE:{file}"], _ => null);

        Diagnostics.Assert("roots", "none", Subjects(roots));
        Assert.IsEmpty(roots);
    }

    [TestMethod]
    public void Load_MissingFile_ThrowsIOException()
    {
        var failure = Assert.ThrowsExactly<FileNotFoundException>(() => Load([$"FILE:{Path.Combine(_directory, "missing.pem")}"], _ => null));

        Diagnostics.Assert("exception", nameof(FileNotFoundException), failure.GetType().Name);
    }

    [TestMethod]
    public void Load_FileWhoseCertificateDoesNotDecode_ThrowsIOExceptionNamingIt()
    {
        string file = Write("bad.pem", NotACertificate);

        IOException failure = Assert.ThrowsExactly<IOException>(() => Load([$"FILE:{file}"], _ => null));

        Diagnostics.Assert(
            "message starts with \"The http_anchors file <temp dir>/bad.pem cannot be loaded: \"",
            true,
            failure.Message.StartsWith($"The http_anchors file {file} cannot be loaded: ", StringComparison.Ordinal));
        Diagnostics.Assert("inner exception", nameof(CryptographicException), failure.InnerException?.GetType().Name);
        StringAssert.StartsWith(failure.Message, $"The http_anchors file {file} cannot be loaded: ");
        Assert.IsInstanceOfType<CryptographicException>(failure.InnerException);
    }

    [TestMethod]
    public void Load_FileThatIsADirectory_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => Load([$"FILE:{_directory}"], _ => null));

        Diagnostics.Assert("inner exception", nameof(UnauthorizedAccessException), failure.InnerException?.GetType().Name);
        Assert.IsInstanceOfType<UnauthorizedAccessException>(failure.InnerException);
    }

    [TestMethod]
    public void Load_Directory_LoadsEveryFileThatLoadsButThoseStartingWithADot()
    {
        Write("first.pem", First.Value.ExportCertificatePem());
        Write("bad.pem", NotACertificate);
        Write(".hidden.pem", Second.Value.ExportCertificatePem());

        X509Certificate2Collection roots = Load([$"DIR:{_directory}"], _ => null);

        Diagnostics.Assert("roots", "CN=first.example.test", Subjects(roots));
        CollectionAssert.AreEqual(new[] { First.Value.Thumbprint }, roots.Select(root => root.Thumbprint).ToArray());
    }

    [TestMethod]
    public void Load_DirectoryWithNoFileThatLoads_ThrowsIOException()
    {
        Write("bad.pem", NotACertificate);
        Write(".hidden.pem", First.Value.ExportCertificatePem());

        IOException failure = Assert.ThrowsExactly<IOException>(() => Load([$"DIR:{_directory}"], _ => null));

        Diagnostics.Diff("message", "The http_anchors directory <temp dir> has no file that loads.", Redact(failure.Message));
        Assert.AreEqual($"The http_anchors directory {_directory} has no file that loads.", failure.Message);
    }

    [TestMethod]
    public void Load_MissingDirectory_ThrowsIOException()
    {
        var failure = Assert.ThrowsExactly<DirectoryNotFoundException>(() => Load([$"DIR:{Path.Combine(_directory, "missing")}"], _ => null));

        Diagnostics.Assert("exception", nameof(DirectoryNotFoundException), failure.GetType().Name);
    }

    [TestMethod]
    public void Load_EnvironmentVariableNamingAFile_LoadsTheFile()
    {
        string file = Write("first.pem", First.Value.ExportCertificatePem());

        X509Certificate2Collection roots = Load(["ENV:KDCPROXY_CA"], name => name == "KDCPROXY_CA" ? $"FILE:{file}" : null);

        Diagnostics.Assert("roots", "CN=first.example.test", Subjects(roots));
        Assert.AreEqual(First.Value.Thumbprint, roots.Single().Thumbprint);
    }

    [TestMethod]
    public void Load_UnsetEnvironmentVariable_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => Load(["ENV:KDCPROXY_CA"], _ => null));

        Diagnostics.Diff("message", "The http_anchors environment variable KDCPROXY_CA is not set.", failure.Message);
        Assert.AreEqual("The http_anchors environment variable KDCPROXY_CA is not set.", failure.Message);
    }

    [TestMethod]
    public void Load_EnvironmentVariableNamingAnotherVariable_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => Load(["ENV:KDCPROXY_CA"], _ => "ENV:KDCPROXY_CA"));

        Diagnostics.Diff("message", "The http_anchors value ENV:KDCPROXY_CA is not a FILE:, DIR: or ENV: anchor.", failure.Message);
        Assert.AreEqual("The http_anchors value ENV:KDCPROXY_CA is not a FILE:, DIR: or ENV: anchor.", failure.Message);
    }

    [TestMethod]
    public void Load_ValueWithNoKnownPrefix_ThrowsIOException()
    {
        IOException failure = Assert.ThrowsExactly<IOException>(() => Load(["/etc/ca.pem"], _ => null));

        Diagnostics.Diff("message", "The http_anchors value /etc/ca.pem is not a FILE:, DIR: or ENV: anchor.", failure.Message);
        Assert.AreEqual("The http_anchors value /etc/ca.pem is not a FILE:, DIR: or ENV: anchor.", failure.Message);
    }

    [TestMethod]
    public void Load_SeveralAnchors_LoadsThemAllInOrder()
    {
        string first = Write("first.pem", First.Value.ExportCertificatePem());
        string second = Write("second.pem", Second.Value.ExportCertificatePem());

        X509Certificate2Collection roots = Load([$"FILE:{second}", $"FILE:{first}"], _ => null);

        Diagnostics.Assert("roots", "CN=second.example.test, CN=first.example.test", Subjects(roots));
        CollectionAssert.AreEqual(new[] { Second.Value.Thumbprint, First.Value.Thumbprint }, roots.Select(root => root.Thumbprint).ToArray());
    }

    private static X509Certificate2 SelfSigned(string host)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private X509Certificate2Collection Load(IReadOnlyList<string> httpAnchors, Func<string, string?> readEnvironmentVariable)
    {
        Diagnostics.Arrange("http_anchors", Redact(string.Join(", ", httpAnchors)));
        Diagnostics.Arrange("KDCPROXY_CA", Redact(readEnvironmentVariable("KDCPROXY_CA") ?? "unset"));
        try
        {
            X509Certificate2Collection roots = KerberosHttpAnchorLoader.Load(httpAnchors, readEnvironmentVariable);
            Diagnostics.Act("roots", Subjects(roots));
            return roots;
        }
        catch (Exception exception) when (WriteFailure(exception))
        {
            throw;
        }
    }

    // The type names only: a missing path's message and a decode failure's words are the platform's own.
    private bool WriteFailure(Exception exception)
    {
        Diagnostics.Act("exception", $"{exception.GetType().Name}, inner {exception.InnerException?.GetType().Name ?? "none"}");
        return false;
    }

    private static string Subjects(X509Certificate2Collection roots) =>
        roots.Count == 0 ? "none" : string.Join(", ", roots.Select(root => root.Subject));

    // The temporary directory differs by platform and run, so the lines show it as <temp dir>.
    private string Redact(string text) => text.Replace(_directory, "<temp dir>", StringComparison.Ordinal);

    private string Write(string name, string contents)
    {
        Diagnostics.Arrange($"file {name}", $"{contents.Length} characters");
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, contents);
        return path;
    }
}
