using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.GoogleCloudStorage;
using Curl.Conformance.Extended.GoogleCloudStorage.Signing;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.GoogleCloudStorage.Signing;

/// <summary>
/// Cloud Storage's V4 signing process with an HMAC key (<c>--user ACCESS_ID:SECRET</c>, <c>Authorization</c> header form) through the
/// curl command line, each request checked by <see cref="GcsV4HmacReferenceAuthorizer" />, written from the documentation alone.
/// </summary>
[TestClass]
public sealed class GcsV4SigningCases
{
    private const string AccessId = "GOOG1EXAMPLE";

    private const string Secret = "s3cr3t";

    private const string AutoScope = AccessId + "/20261102/auto/storage/goog4_request";

    private const string SignedHeaders = "host;x-goog-content-sha256;x-goog-date";

    private const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly X509Certificate2 Certificate = ReferenceCertificate.CreateSelfSigned("storage.googleapis.com");

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly GcsFixedClock Clock = new(GcsFixedClock.CaseTime);

    private static GcsV4HmacReferenceAuthorizer Verifier(string region = "auto", string secret = Secret) => new(AccessId, secret, region, Clock);

    private static Task<CurlRunResult> RunSignedAsync(GcsReferenceServer server, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tls(443, server, Certificate)], ["-sS", "-k", "--user", AccessId + ":" + Secret, .. arguments], timeProvider: Clock);

    private static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static string Authorization(GcsV4HmacReferenceAuthorizer verifier, GcsReferenceRequest request, string scope = AutoScope) =>
        $"GOOG4-HMAC-SHA256 Credential={scope}, SignedHeaders={SignedHeaders}, Signature={verifier.SignatureOf(request, SignedHeaders)}";

    private static void AssertSignedHeaders(GcsV4HmacReferenceAuthorizer verifier, GcsReferenceRequest request, string payloadHash)
    {
        Assert.AreEqual("20261102T103000Z", request.Headers["x-goog-date"]);
        Assert.AreEqual(payloadHash, request.Headers["x-goog-content-sha256"]);
        Assert.AreEqual(Authorization(verifier, request), request.Headers["Authorization"]);
        Assert.IsNull(verifier.FaultOf(request));
    }

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:V4Signing-Algorithm-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-Authorization-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-Date-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-Scope-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-CanonicalRequest-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-CanonicalHeaders-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-SignedHeaders-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-StringToSign-1")]
    [SpecRequirement("GoogleCloudStorage:V4Signing-SigningKey-1")]
    public Task Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256() =>
        Known.RunCaseAsync(
            ["GoogleCloudStorage:V4Signing-Algorithm-1", "GoogleCloudStorage:V4Signing-Authorization-1", "GoogleCloudStorage:V4Signing-Date-1", "GoogleCloudStorage:V4Signing-Scope-1", "GoogleCloudStorage:V4Signing-CanonicalRequest-1", "GoogleCloudStorage:V4Signing-CanonicalHeaders-1", "GoogleCloudStorage:V4Signing-SignedHeaders-1", "GoogleCloudStorage:V4Signing-StringToSign-1", "GoogleCloudStorage:V4Signing-SigningKey-1"],
            async () =>
            {
                GcsV4HmacReferenceAuthorizer verifier = Verifier();
                GcsReferenceServer server = new(verifier);
                server.PutObject("private", "secret.txt", "classified"u8.ToArray());

                CurlRunResult result = await RunSignedAsync(server, "gs://private/secret.txt");

                Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
                Assert.AreEqual("classified", Text(result.StandardOutput));
                Assert.AreEqual(string.Empty, Text(result.StandardError));
                AssertSignedHeaders(verifier, server.Requests.Single(), EmptyHash);
            });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:V4Signing-Payload-1")]
    public Task Upload_WithHmacKey_SignsTheBodysSha256() =>
        Known.RunCaseAsync(["GoogleCloudStorage:V4Signing-Payload-1"], async () =>
        {
            string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllTextAsync(file, "hello gcs");
            try
            {
                GcsV4HmacReferenceAuthorizer verifier = Verifier();
                GcsReferenceServer server = new(verifier);
                server.PutObject("drop", "keep", []);

                CurlRunResult result = await RunSignedAsync(server, "-T", file, "gs://drop/hello.txt");

                Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
                Assert.AreEqual(string.Empty, Text(result.StandardOutput));
                Assert.AreEqual(string.Empty, Text(result.StandardError));
                AssertSignedHeaders(verifier, server.Requests.Single(), "e9a788a8050dc96ec5da22a3086c3ff337d83a1bf6271d9e44487dae28d95dca");
                Assert.AreEqual("hello gcs", Text(server.GetObject("drop", "hello.txt")!));
            }
            finally
            {
                File.Delete(file);
            }
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:V4Signing-CanonicalQuery-1")]
    public Task List_WithHmacKey_SignsTheQuerySortedByName() =>
        Known.RunCaseAsync(["GoogleCloudStorage:V4Signing-CanonicalQuery-1"], async () =>
        {
            GcsV4HmacReferenceAuthorizer verifier = Verifier();
            GcsReferenceServer server = new(verifier);
            server.PutObject("site", "logs/a.txt", []);
            server.PutObject("site", "logs/deep/b.txt", []);

            CurlRunResult result = await RunSignedAsync(server, "gs://site/logs/");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("logs/deep/\nlogs/a.txt\n", Text(result.StandardOutput));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            GcsReferenceRequest request = server.Requests.Single();
            Assert.HasCount(3, request.Query);
            AssertSignedHeaders(verifier, request, EmptyHash);
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:V4Signing-CanonicalURI-1")]
    public Task Download_WithHmacKey_SignsTheReservedAndNonAsciiNamePercentEncoded() =>
        Known.RunCaseAsync(["GoogleCloudStorage:V4Signing-CanonicalURI-1"], async () =>
        {
            GcsV4HmacReferenceAuthorizer verifier = Verifier();
            GcsReferenceServer server = new(verifier);
            server.PutObject("docs", "café a+b&c=d~.txt", "accented"u8.ToArray());

            CurlRunResult result = await RunSignedAsync(server, "gs://docs/caf%C3%A9%20a%2Bb%26c%3Dd~.txt");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("accented", Text(result.StandardOutput));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            GcsReferenceRequest request = server.Requests.Single();
            Assert.AreEqual("/docs/caf%C3%A9%20a%2Bb%26c%3Dd~.txt", request.Target);
            AssertSignedHeaders(verifier, request, EmptyHash);
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:V4Signing-Scope-2")]
    public Task Download_WithHmacKeyAndARegion_SignsWithThatRegionsScope() =>
        Known.RunCaseAsync(["GoogleCloudStorage:V4Signing-Scope-2"], async () =>
        {
            GcsV4HmacReferenceAuthorizer verifier = Verifier("us-east1");
            GcsReferenceServer server = new(verifier);
            server.PutObject("east", "a.txt", "regional"u8.ToArray());

            CurlRunResult result = await RunSignedAsync(server, "--aws-sigv4", "goog4:goog:us-east1:storage", "gs://east/a.txt");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("regional", Text(result.StandardOutput));
            Assert.AreEqual(string.Empty, Text(result.StandardError));
            GcsReferenceRequest request = server.Requests.Single();
            Assert.AreEqual(Authorization(verifier, request, AccessId + "/20261102/us-east1/storage/goog4_request"), request.Headers["Authorization"]);
        });

    [TestMethod]
    [SpecRequirement("GoogleCloudStorage:V4Signing-Signature-1")]
    public Task Download_WithTheWrongSecret_IsRefusedAndExitsWith22() =>
        Known.RunCaseAsync(["GoogleCloudStorage:V4Signing-Signature-1"], async () =>
        {
            GcsReferenceServer server = new(Verifier(secret: "not-the-secret"));
            server.PutObject("private", "secret.txt", "classified"u8.ToArray());

            CurlRunResult result = await RunSignedAsync(server, "gs://private/secret.txt");

            Assert.AreEqual(22, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual("curl: (22) Cloud Storage returned error 403: SignatureDoesNotMatch: " + GcsV4HmacReferenceAuthorizer.RefusalMessage + Environment.NewLine, Text(result.StandardError));
            StringAssert.StartsWith(Text(result.Exchanges.Single().ServerBytes), "HTTP/1.1 403 Forbidden\r\n");
        });
}
