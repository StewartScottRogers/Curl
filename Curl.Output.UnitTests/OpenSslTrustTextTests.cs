using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslTrustText"/> to <c>ossl_load_trust_anchors</c> in curl 8.21.0's
/// <c>lib/vtls/openssl.c</c> for the sources the measured exchanges in BL-405's Notes do not use.
/// </summary>
[TestClass]
public sealed class OpenSslTrustTextTests
{
    [TestMethod]
    public void Lines_BlobAndFile_BlobOverridesTheFile()
    {
        CollectionAssert.AreEqual(
            new[] { "SSL Trust Anchors:", "  CA Blob from configuration", "  CApath: /d" },
            OpenSslTrustText.Lines(new TlsTrustEvent
            {
                VerifiesPeer = true,
                HasCaCertificateBlob = true,
                CaCertificateFile = "/f.pem",
                CaCertificateDirectory = "/d",
            }).ToArray());
    }

    [TestMethod]
    public void Lines_DirectoryOnly_NamesOnlyTheDirectory()
    {
        CollectionAssert.AreEqual(
            new[] { "SSL Trust Anchors:", "  CApath: /d" },
            OpenSslTrustText.Lines(new TlsTrustEvent { VerifiesPeer = true, CaCertificateDirectory = "/d" }).ToArray());
    }

    [TestMethod]
    public void Lines_NoSource_SaysNoTrustAnchorsAreConfigured()
    {
        CollectionAssert.AreEqual(
            new[] { "SSL Trust Anchors:", "  no trust anchors configured" },
            OpenSslTrustText.Lines(new TlsTrustEvent { VerifiesPeer = true }).ToArray());
    }
}
