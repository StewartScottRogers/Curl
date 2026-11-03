using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.OciRegistry;
using static Curl.Conformance.Extended.UnitTests.OciRegistry.OciRegistryRuns;

namespace Curl.Conformance.Extended.UnitTests.OciRegistry;

/// <summary>The Distribution Specification's push workflow through the curl command line: a monolithic blob upload (POST then PUT) to relative, absolute and query-carrying locations, and a manifest PUT.</summary>
[TestClass]
public sealed class OciPushCases
{
    private static readonly byte[] Layer = "layer bytes\n"u8.ToArray();

    private static readonly string LayerDigest = OciReferenceRegistry.DigestOf(Layer);

    private static readonly string EscapedLayerDigest = Uri.EscapeDataString(LayerDigest);

    private static string Head(string target) => $"HEAD {target} HTTP/1.1\r\nHost: registry.example\r\nConnection: close\r\n\r\n";

    private const string Post = "POST /v2/lib/app/blobs/uploads/ HTTP/1.1\r\nHost: registry.example\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private static string Put(string target, string host = "registry.example") =>
        $"PUT {target} HTTP/1.1\r\nHost: {host}\r\nContent-Type: application/octet-stream\r\nContent-Length: {Layer.Length}\r\nConnection: close\r\n\r\nlayer bytes\n";

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PushBlobs-1")]
    [SpecRequirement("OciRegistry:DIST-PushBlobs-2")]
    [SpecRequirement("OciRegistry:DIST-PushBlobs-3")]
    public Task BlobPush_ToAnAbsoluteLocation_PostsThenPutsTheWholeBlobWithItsDigest() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PushBlobs-1", "OciRegistry:DIST-PushBlobs-2", "OciRegistry:DIST-PushBlobs-3"], async () =>
        {
            OciReferenceRegistry registry = new() { UploadLocationAuthority = "https://uploads.example:8443" };

            CurlRunResult result = await PushAsync(registry, Layer, $"oci://registry.example/lib/app@{LayerDigest}");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.AreEqual(
                new[] { Head($"/v2/lib/app/blobs/{LayerDigest}"), Post, Put($"/v2/lib/app/blobs/uploads/session-1?digest={EscapedLayerDigest}", "uploads.example:8443") },
                Requests(result));
            CollectionAssert.AreEqual(Layer, registry.GetBlob(LayerDigest));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PushBlobs-4")]
    public Task BlobPush_ToARelativeLocation_PutsOnTheRegistry() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PushBlobs-4"], async () =>
        {
            OciReferenceRegistry registry = new();

            CurlRunResult result = await PushAsync(registry, Layer, $"oci://registry.example/lib/app@{LayerDigest}");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            Assert.AreEqual(Put($"/v2/lib/app/blobs/uploads/session-1?digest={EscapedLayerDigest}"), Requests(result)[^1]);
            CollectionAssert.AreEqual(Layer, registry.GetBlob(LayerDigest));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PushBlobs-5")]
    public Task BlobPush_ToALocationWithAQuery_KeepsTheQueryAndAddsTheDigest() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PushBlobs-5"], async () =>
        {
            OciReferenceRegistry registry = new() { UploadLocationQuery = "_state=abc" };

            CurlRunResult result = await PushAsync(registry, Layer, $"oci://registry.example/lib/app@{LayerDigest}");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            Assert.AreEqual(Put($"/v2/lib/app/blobs/uploads/session-1?_state=abc&digest={EscapedLayerDigest}"), Requests(result)[^1]);
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-CheckBlobs-1")]
    public Task BlobPush_OfABlobTheRegistryHas_UploadsNothing() =>
        Known.RunCaseAsync(["OciRegistry:DIST-CheckBlobs-1"], async () =>
        {
            OciReferenceRegistry registry = new();
            registry.PutBlob(LayerDigest, Layer);

            CurlRunResult result = await PushAsync(registry, Layer, $"oci://registry.example/lib/app@{LayerDigest}");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.AreEqual(new[] { Head($"/v2/lib/app/blobs/{LayerDigest}") }, Requests(result));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PushBlobs-6")]
    public Task BlobPush_WhoseContentDoesNotMatchTheUrlDigest_IsRefusedBeforeAnyRequest() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PushBlobs-6"], async () =>
        {
            OciReferenceRegistry registry = new();
            string other = OciReferenceRegistry.DigestOf("other"u8.ToArray());

            CurlRunResult result = await PushAsync(registry, Layer, $"oci://registry.example/lib/app@{other}");

            Assert.AreEqual(25, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual($"curl: (25) The upload's digest {LayerDigest} does not match the URL's {other}." + Environment.NewLine, Text(result.StandardError));
            Assert.IsEmpty(registry.Requests);
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PushManifests-1")]
    public Task ManifestPush_PutsTheManifestWithItsMediaType() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PushManifests-1"], async () =>
        {
            byte[] manifest = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}"""u8.ToArray();
            OciReferenceRegistry registry = new();

            CurlRunResult result = await PushAsync(registry, manifest, "oci://registry.example/lib/app:2.0");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.AreEqual(
                new[] { $"PUT /v2/lib/app/manifests/2.0 HTTP/1.1\r\nHost: registry.example\r\nContent-Type: application/vnd.oci.image.index.v1+json\r\nContent-Length: {manifest.Length}\r\nConnection: close\r\n\r\n" + Text(manifest) },
                Requests(result));
            CollectionAssert.AreEqual(manifest, registry.GetManifest("lib/app", "2.0"));
        });
}
