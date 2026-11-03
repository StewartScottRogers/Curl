using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.OciRegistry;
using static Curl.Conformance.Extended.UnitTests.OciRegistry.OciRegistryRuns;

namespace Curl.Conformance.Extended.UnitTests.OciRegistry;

/// <summary>The Distribution Specification's error codes (code-1 to code-14) in an error body, through the curl command line.</summary>
[TestClass]
public sealed class OciErrorCases
{
    [TestMethod]
    [SpecRequirement("OciRegistry:DIST-ErrorCodes-1")]
    [DataRow("BLOB_UNKNOWN", 22)]
    [DataRow("BLOB_UPLOAD_INVALID", 22)]
    [DataRow("BLOB_UPLOAD_UNKNOWN", 22)]
    [DataRow("DIGEST_INVALID", 22)]
    [DataRow("MANIFEST_BLOB_UNKNOWN", 22)]
    [DataRow("MANIFEST_INVALID", 22)]
    [DataRow("MANIFEST_UNKNOWN", 22)]
    [DataRow("NAME_INVALID", 22)]
    [DataRow("NAME_UNKNOWN", 22)]
    [DataRow("SIZE_INVALID", 22)]
    [DataRow("UNAUTHORIZED", 67)]
    [DataRow("DENIED", 67)]
    [DataRow("UNSUPPORTED", 22)]
    [DataRow("TOOMANYREQUESTS", 22)]
    public Task ErrorBody_FailsWithItsStatusCodeAndMessage(string code, int exitCode) =>
        Known.RunCaseAsync(["OciRegistry:DIST-ErrorCodes-1"], async () =>
        {
            OciReferenceRegistry registry = new() { ForcedError = code };
            (int statusCode, string message) = OciReferenceResponse.ErrorCodes[code];

            CurlRunResult result = await RunAsync(registry, "oci://registry.example/lib/app:1.0");

            Assert.AreEqual(exitCode, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual($"curl: ({exitCode}) The requested URL returned error: {statusCode} ({code}: {message})" + Environment.NewLine, Text(result.StandardError));
            CollectionAssert.AreEqual(new[] { Get("/v2/lib/app/manifests/1.0") }, Requests(result));
        });
}
