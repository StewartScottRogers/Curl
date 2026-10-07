using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslTrustText"/> to <c>ossl_load_trust_anchors</c> in curl 8.21.0's
/// <c>lib/vtls/openssl.c</c> for the sources the measured exchanges in BL-405's Notes do not use.
/// </summary>
[TestClass]
public sealed class OpenSslTrustTextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Lines_BlobAndFile_BlobOverridesTheFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] expected = ["SSL Trust Anchors:", "  CA Blob from configuration", "  CApath: /d"];
        diagnostics.Arrange("trust", "VerifiesPeer, CA blob, file /f.pem, directory /d");

        var actual = OpenSslTrustText.Lines(new TlsTrustEvent
        {
            VerifiesPeer = true,
            HasCaCertificateBlob = true,
            CaCertificateFile = "/f.pem",
            CaCertificateDirectory = "/d",
        }).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Lines_DirectoryOnly_NamesOnlyTheDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] expected = ["SSL Trust Anchors:", "  CApath: /d"];
        diagnostics.Arrange("trust", "VerifiesPeer, directory /d");

        var actual = OpenSslTrustText.Lines(new TlsTrustEvent { VerifiesPeer = true, CaCertificateDirectory = "/d" }).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Lines_NoSource_SaysNoTrustAnchorsAreConfigured()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] expected = ["SSL Trust Anchors:", "  no trust anchors configured"];
        diagnostics.Arrange("trust", "VerifiesPeer only");

        var actual = OpenSslTrustText.Lines(new TlsTrustEvent { VerifiesPeer = true }).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    private static void Report(TestDiagnostics diagnostics, string[] expected, string[] actual)
    {
        var expectedText = string.Join("\n", expected);
        var actualText = string.Join("\n", actual);
        diagnostics.Act("lines", actualText);
        diagnostics.Diff("lines", expectedText, actualText);
    }
}
