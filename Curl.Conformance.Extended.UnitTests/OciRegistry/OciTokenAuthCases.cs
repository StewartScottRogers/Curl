using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.OciRegistry;
using static Curl.Conformance.Extended.UnitTests.OciRegistry.OciRegistryRuns;

namespace Curl.Conformance.Extended.UnitTests.OciRegistry;

/// <summary>Docker's Token Authentication Specification through the curl command line: the Bearer challenge, the token request with and without <c>-u</c>, both token members, and the push scope.</summary>
[TestClass]
public sealed class OciTokenAuthCases
{
    private const string PullTokenRequest = "/token?service=registry.example&scope=repository%3Alib%2Fapp%3Apull";

    private static readonly byte[] Manifest = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json","layers":[]}"""u8.ToArray();

    private static OciReferenceRegistry Registry(string? credentials = null, string tokenField = "token")
    {
        OciReferenceRegistry registry = new() { RequiredToken = "tok-1", TokenCredentials = credentials, TokenField = tokenField };
        registry.PutManifest("lib/app", "1.0", "application/vnd.oci.image.manifest.v1+json", Manifest);
        return registry;
    }

    [TestMethod]
    [SpecRequirement("OciRegistry:TOKEN-Flow-1")]
    [SpecRequirement("OciRegistry:TOKEN-Flow-2")]
    [SpecRequirement("OciRegistry:TOKEN-Response-1")]
    public Task AnonymousPull_AsksTheRealmForATokenAndRetriesWithIt() =>
        Known.RunCaseAsync(["OciRegistry:TOKEN-Flow-1", "OciRegistry:TOKEN-Flow-2", "OciRegistry:TOKEN-Response-1"], async () =>
        {
            CurlRunResult result = await RunAsync(Registry(), "oci://registry.example/lib/app:1.0");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            CollectionAssert.AreEqual(Manifest, result.StandardOutput);
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            CollectionAssert.AreEqual(
                new[]
                {
                    Get("/v2/lib/app/manifests/1.0"),
                    Get(PullTokenRequest, "auth.example:9443", "application/json"),
                    Get("/v2/lib/app/manifests/1.0", authorization: "Bearer tok-1"),
                },
                Requests(result));
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:TOKEN-Request-1")]
    public Task PullWithUser_SendsTheCredentialsToTheTokenEndpointAsBasic() =>
        Known.RunCaseAsync(["OciRegistry:TOKEN-Request-1"], async () =>
        {
            CurlRunResult result = await RunAsync(Registry("alice:secret"), "-u", "alice:secret", "oci://registry.example/lib/app:1.0");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            CollectionAssert.AreEqual(Manifest, result.StandardOutput);
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            Assert.AreEqual(
                $"GET {PullTokenRequest} HTTP/1.1\r\nHost: auth.example:9443\r\nAccept: application/json\r\nAuthorization: Basic YWxpY2U6c2VjcmV0\r\nConnection: close\r\n\r\n",
                Requests(result)[1]);
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:TOKEN-Request-2")]
    public Task PullWithWrongPassword_FailsAsLoginDenied() =>
        Known.RunCaseAsync(["OciRegistry:TOKEN-Request-2"], async () =>
        {
            CurlRunResult result = await RunAsync(Registry("alice:secret"), "-u", "alice:wrong", "oci://registry.example/lib/app:1.0");

            Assert.AreEqual(67, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual("curl: (67) The token endpoint answered 401 without a token." + Environment.NewLine, Text(result.StandardError));
            Assert.HasCount(2, result.Exchanges);
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:TOKEN-Response-2")]
    public Task TokenResponseWithAccessToken_IsUsedAsTheBearerToken() =>
        Known.RunCaseAsync(["OciRegistry:TOKEN-Response-2"], async () =>
        {
            CurlRunResult result = await RunAsync(Registry(tokenField: "access_token"), "oci://registry.example/lib/app:1.0");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            CollectionAssert.AreEqual(Manifest, result.StandardOutput);
            Assert.AreEqual(Get("/v2/lib/app/manifests/1.0", authorization: "Bearer tok-1"), Requests(result)[^1]);
        });

    [TestMethod]
    [SpecRequirement("OciRegistry:TOKEN-Scope-1")]
    public Task ManifestPush_AsksForThePushAndPullScope() =>
        Known.RunCaseAsync(["OciRegistry:TOKEN-Scope-1"], async () =>
        {
            OciReferenceRegistry registry = Registry();

            CurlRunResult result = await PushAsync(registry, Manifest, "oci://registry.example/lib/app:2.0");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(
                Get("/token?service=registry.example&scope=repository%3Alib%2Fapp%3Apush%2Cpull", "auth.example:9443", "application/json"), Requests(result)[1]);
            StringAssert.Contains(Requests(result)[2], "Authorization: Bearer tok-1\r\n");
            CollectionAssert.AreEqual(Manifest, registry.GetManifest("lib/app", "2.0"));
        });
}
