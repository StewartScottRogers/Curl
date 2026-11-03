using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.OciRegistry;
using static Curl.Conformance.Extended.UnitTests.OciRegistry.OciRegistryRuns;

namespace Curl.Conformance.Extended.UnitTests.OciRegistry;

/// <summary>The Distribution Specification's pull workflow through the curl command line: manifests by tag and digest, an index, blobs, a redirect to blob storage and digest verification.</summary>
[TestClass]
public sealed class OciPullCases
{
    private const string ManifestType = "application/vnd.oci.image.manifest.v1+json";

    private static readonly byte[] Manifest = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json","layers":[]}"""u8.ToArray();

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PullManifests-1")]
    [SpecRequirement("OciRegistry:DIST-PullManifests-3")]
    public Task ManifestPull_ByTag_GetsTheManifestWithTheMediaTypesItAccepts() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PullManifests-1", "OciRegistry:DIST-PullManifests-3"], async () =>
        {
            OciReferenceRegistry registry = new();
            registry.PutManifest("lib/app", "1.0", ManifestType, Manifest);

            CurlRunResult result = await RunAsync(registry, "oci://registry.example/lib/app:1.0");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            CollectionAssert.AreEqual(Manifest, result.StandardOutput);
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.AreEqual(new[] { Get("/v2/lib/app/manifests/1.0") }, Requests(result));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PullManifests-2")]
    public Task ManifestPull_ByDigest_GetsTheManifestUnderItsDigest() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PullManifests-2"], async () =>
        {
            OciReferenceRegistry registry = new();
            string digest = OciReferenceRegistry.DigestOf(Manifest);
            registry.PutManifest("lib/app", digest, ManifestType, Manifest);

            CurlRunResult result = await RunAsync(registry, $"oci://registry.example/lib/app@{digest}");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            CollectionAssert.AreEqual(Manifest, result.StandardOutput);
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.Contains(Requests(result), Get($"/v2/lib/app/manifests/{digest}"));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:IMAGE-MediaTypes-1")]
    public Task IndexPull_ByTag_GetsTheImageIndex() =>
        Known.RunCaseAsync(["OciRegistry:IMAGE-MediaTypes-1"], async () =>
        {
            byte[] index = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[]}"""u8.ToArray();
            OciReferenceRegistry registry = new();
            registry.PutManifest("lib/app", "multi", "application/vnd.oci.image.index.v1+json", index);

            CurlRunResult result = await RunAsync(registry, "oci://registry.example/lib/app:multi");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            CollectionAssert.AreEqual(index, result.StandardOutput);
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.AreEqual(new[] { Get("/v2/lib/app/manifests/multi") }, Requests(result));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-PullBlobs-1")]
    [SpecRequirement("OciRegistry:DIST-PullBlobs-2")]
    public Task BlobPull_FollowsTheRedirectToBlobStorage() =>
        Known.RunCaseAsync(["OciRegistry:DIST-PullBlobs-1", "OciRegistry:DIST-PullBlobs-2"], async () =>
        {
            byte[] layer = "layer bytes\n"u8.ToArray();
            string digest = OciReferenceRegistry.DigestOf(layer);
            OciReferenceRegistry registry = new() { BlobRedirect = "https://storage.example:8443" };
            registry.PutBlob(digest, layer);

            CurlRunResult result = await RunAsync(registry, $"oci://registry.example/lib/app@{digest}");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            CollectionAssert.AreEqual(layer, result.StandardOutput);
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.AreEqual(
                new[] { Get($"/v2/lib/app/blobs/{digest}", accept: "*/*"), Get($"/blobstore/{digest}", "storage.example:8443", "*/*") }, Requests(result));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:IMAGE-Descriptor-Digests-1")]
    public Task BlobPull_WhoseContentDoesNotMatchItsDigest_IsRefused() =>
        Known.RunCaseAsync(["OciRegistry:IMAGE-Descriptor-Digests-1"], async () =>
        {
            OciReferenceRegistry registry = new();
            string digest = OciReferenceRegistry.DigestOf("expected"u8.ToArray());
            registry.PutBlob(digest, "tampered"u8.ToArray());

            CurlRunResult result = await RunAsync(registry, $"oci://registry.example/lib/app@{digest}");

            Assert.AreNotEqual(0, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            CollectionAssert.AreEqual(new[] { Get($"/v2/lib/app/blobs/{digest}", accept: "*/*") }, Requests(result));
        });
}
