namespace Curl.Conformance;

// The test CA, test-localhost and test-client-eku-only, generated once per process from the vendored .prm files
// into a folder beside the tests, for the https-mtls stand-in's tests (BL-2859).
internal static class MtlsTestCertificates
{
    private static readonly Lazy<string> Generated = new(() =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "MtlsCertificates");
        string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        foreach (string parameters in new[] { "test-ca.prm", "test-localhost.prm", "test-client-eku-only.prm" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", "certs", parameters), Path.Combine(source, parameters), overwrite: true);
        }

        return UpstreamCertificateFolder.Prepare(source, Path.Combine(root, "out"), TimeProvider.System);
    });

    /// <summary>Gets the certificate directory, the folder holding <c>certs</c>.</summary>
    public static string CertificateDirectory => Generated.Value;
}
