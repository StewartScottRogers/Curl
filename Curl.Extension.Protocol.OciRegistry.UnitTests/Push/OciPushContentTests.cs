using System.Text;

namespace Curl.Extension.Protocol.OciRegistry.Push;

/// <summary>
/// Checks the manifest media type a push sends as <c>Content-Type</c> (OCI Distribution Specification 1.1, "Pushing
/// manifests": the manifest's own <c>mediaType</c>) and the SHA-256 digest a blob push checks.
/// </summary>
[TestClass]
public sealed class OciPushContentTests
{
    [TestMethod]
    [DataRow("""{"mediaType":"application/vnd.docker.distribution.manifest.v2+json"}""", "application/vnd.docker.distribution.manifest.v2+json")]
    [DataRow("""{"schemaVersion":2}""", OciPushContent.DefaultManifestMediaType)]
    [DataRow("""{"mediaType":7}""", OciPushContent.DefaultManifestMediaType)]
    [DataRow("[]", OciPushContent.DefaultManifestMediaType)]
    [DataRow("not json", OciPushContent.DefaultManifestMediaType)]
    public void MediaTypeOf_Manifest_ReadsItsMediaTypeOrTheOciDefault(string manifest, string expected) =>
        Assert.AreEqual(expected, OciPushContent.MediaTypeOf(Encoding.UTF8.GetBytes(manifest)));

    [TestMethod]
    public void VerifyDigest_MatchingDigest_ReturnsIt()
    {
        const string EmptyDigest = "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        Assert.AreEqual(EmptyDigest, OciPushContent.VerifyDigest(EmptyDigest, []));
    }
}
