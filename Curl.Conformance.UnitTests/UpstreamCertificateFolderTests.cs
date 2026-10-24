namespace Curl.Conformance;

[TestClass]
public sealed class UpstreamCertificateFolderTests
{
    private static readonly string VendoredCaParameters = Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", "certs", "test-ca.prm");

    [TestMethod]
    public void Prepare_CertsSourceFolder_WritesTheCertificatesIntoCertsAndReturnsTheOutputFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "curl-certfolder-" + Guid.NewGuid().ToString("N"));
        string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        File.Copy(VendoredCaParameters, Path.Combine(source, "test-ca.prm"));
        string output = Path.Combine(root, "out");
        try
        {
            string certificateDirectory = UpstreamCertificateFolder.Prepare(source, output, TimeProvider.System);

            Assert.AreEqual(output, certificateDirectory);
            Assert.IsTrue(File.Exists(Path.Combine(output, UpstreamCertificateFolder.CertsFolderName, "test-ca.crt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Prepare_CalledTwice_RegeneratesIntoTheSameFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "curl-certfolder-" + Guid.NewGuid().ToString("N"));
        string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        File.Copy(VendoredCaParameters, Path.Combine(source, "test-ca.prm"));
        string output = Path.Combine(root, "out");
        try
        {
            UpstreamCertificateFolder.Prepare(source, output, TimeProvider.System);
            string first = File.ReadAllText(Path.Combine(output, "certs", "test-ca.key"));

            UpstreamCertificateFolder.Prepare(source, output, TimeProvider.System);
            string second = File.ReadAllText(Path.Combine(output, "certs", "test-ca.key"));

            Assert.AreNotEqual(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Prepare_NullOutputDirectory_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCertificateFolder.Prepare("source", null!, TimeProvider.System));
    }
}
