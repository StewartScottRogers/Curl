using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;
using Curl.Conformance.Extended.S3.Signing;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.S3.Signing;

/// <summary>
/// AWS Signature Version 4 header signing of <c>s3://</c> requests through the curl command line, at the API reference examples'
/// time and with their access key, each request checked by <see cref="S3SigV4ReferenceAuthorizer" />, written from the documentation alone.
/// </summary>
[TestClass]
public sealed class S3SigV4SigningCases
{
    private const string AccessKeyId = S3SigV4ReferenceAuthorizerTests.ExampleAccessKeyId;

    private const string SecretAccessKey = S3SigV4ReferenceAuthorizerTests.ExampleSecretAccessKey;

    private const string SignedHeaders = "host;x-amz-content-sha256;x-amz-date";

    private static readonly X509Certificate2 Certificate = ReferenceCertificate.CreateSelfSigned("s3.us-east-1.amazonaws.com");

    private static readonly S3FixedClock Clock = new(S3FixedClock.ExampleTime);

    private static (S3ReferenceEndpoint Endpoint, S3SigV4ReferenceAuthorizer Verifier) EndpointFor(string region = "us-east-1", string? sessionToken = null)
    {
        S3SigV4ReferenceAuthorizer verifier = new(AccessKeyId, SecretAccessKey, region, Clock) { SessionToken = sessionToken };
        S3ReferenceEndpoint endpoint = new($"s3.{region}.amazonaws.com", verifier);
        endpoint.CreateBucket("examplebucket");
        return (endpoint, verifier);
    }

    private static Task<CurlRunResult> RunAsync(S3ReferenceEndpoint endpoint, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Tls(443, endpoint, Certificate)], ["-sS", "-k", "-m", "60", .. arguments], environment, Clock);

    private static Task<CurlRunResult> RunSignedAsync(S3ReferenceEndpoint endpoint, params string[] arguments) =>
        RunAsync(endpoint, ["--user", AccessKeyId + ":" + SecretAccessKey, .. arguments]);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static void AssertSucceeded(CurlRunResult result, string standardOutput)
    {
        Assert.AreEqual(string.Empty, Text(result.StandardError));
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(standardOutput, Text(result.StandardOutput));
    }

    private static void AssertSigned(S3SigV4ReferenceAuthorizer verifier, S3ReferenceRequest request, string payloadHash, string region = "us-east-1", string signedHeaders = SignedHeaders)
    {
        Assert.AreEqual("20130524T000000Z", request.Headers["x-amz-date"]);
        Assert.AreEqual(payloadHash, request.Headers["x-amz-content-sha256"]);
        Assert.AreEqual(
            $"AWS4-HMAC-SHA256 Credential={AccessKeyId}/20130524/{region}/s3/aws4_request,SignedHeaders={signedHeaders},Signature={verifier.SignatureOf(request, signedHeaders)}",
            request.Headers["Authorization"]);
        Assert.IsNull(verifier.FaultOf(request));
    }

    [TestMethod]
    [SpecRequirement("S3:SigV4-Authorization-1")]
    [SpecRequirement("S3:SigV4-Authorization-2")]
    [SpecRequirement("S3:SigV4-Date-1")]
    [SpecRequirement("S3:SigV4-Scope-1")]
    [SpecRequirement("S3:SigV4-CanonicalRequest-1")]
    [SpecRequirement("S3:SigV4-CanonicalHeaders-1")]
    [SpecRequirement("S3:SigV4-SignedHeaders-1")]
    [SpecRequirement("S3:SigV4-Payload-1")]
    [SpecRequirement("S3:SigV4-StringToSign-1")]
    [SpecRequirement("S3:SigV4-SigningKey-1")]
    public async Task Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime()
    {
        (S3ReferenceEndpoint endpoint, S3SigV4ReferenceAuthorizer verifier) = EndpointFor();
        endpoint.PutObject("examplebucket", "test.txt", "Welcome to Amazon S3."u8.ToArray());

        CurlRunResult result = await RunSignedAsync(endpoint, "s3://examplebucket/test.txt");

        AssertSucceeded(result, "Welcome to Amazon S3.");
        S3ReferenceRequest request = endpoint.Requests.Single();
        Assert.AreEqual("/test.txt", request.Target);
        AssertSigned(verifier, request, S3SigV4ReferenceAuthorizerTests.EmptyHash);
    }

    [TestMethod]
    [SpecRequirement("S3:SigV4-Payload-2")]
    [SpecRequirement("S3:SigV4-CanonicalURI-1")]
    public async Task Upload_ThePutObjectExample_SignsTheBodysSha256()
    {
        (S3ReferenceEndpoint endpoint, S3SigV4ReferenceAuthorizer verifier) = EndpointFor();
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        try
        {
            string file = Path.Combine(directory.FullName, "welcome.txt");
            await File.WriteAllTextAsync(file, "Welcome to Amazon S3.");

            CurlRunResult result = await RunSignedAsync(endpoint, "-T", file, "s3://examplebucket/test$file.text");

            AssertSucceeded(result, string.Empty);
            S3ReferenceRequest request = endpoint.Requests.Single();
            Assert.AreEqual("PUT", request.Method);
            Assert.AreEqual("/test%24file.text", request.Target);
            AssertSigned(verifier, request, "44ce7dd67c959e0d3524ffac1771dfbba87d2b6b4b4e99e42034a8b803f8b072");
            Assert.AreEqual("Welcome to Amazon S3.", Text(endpoint.FindObject("examplebucket", "test$file.text")!));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [TestMethod]
    [SpecRequirement("S3:SigV4-CanonicalQuery-1")]
    [SpecRequirement("S3:SigV4-CanonicalQuery-2")]
    public async Task List_TheListObjectsExample_SignsSeveralParametersSortedWithAnEmptyValue()
    {
        (S3ReferenceEndpoint endpoint, S3SigV4ReferenceAuthorizer verifier) = EndpointFor();
        endpoint.PutObject("examplebucket", "Jan/a.txt", [1]);
        endpoint.PutObject("examplebucket", "Jul.txt", [1]);

        CurlRunResult result = await RunSignedAsync(endpoint, "s3://examplebucket/");

        AssertSucceeded(result, "Jan/\nJul.txt\n");
        S3ReferenceRequest request = endpoint.Requests.Single();
        Assert.AreEqual("/?list-type=2&delimiter=%2F&prefix=", request.Target);
        StringAssert.Contains(S3SigV4CanonicalRequest.Of(request, SignedHeaders, S3SigV4ReferenceAuthorizerTests.EmptyHash), "\ndelimiter=%2F&list-type=2&prefix=\n");
        AssertSigned(verifier, request, S3SigV4ReferenceAuthorizerTests.EmptyHash);
    }

    [TestMethod]
    [SpecRequirement("S3:SigV4-CanonicalURI-2")]
    public async Task Download_KeyWithReservedSpaceAndNonAsciiCharacters_IsSignedEncodedOnce()
    {
        (S3ReferenceEndpoint endpoint, S3SigV4ReferenceAuthorizer verifier) = EndpointFor();
        endpoint.PutObject("examplebucket", "photos/café &+=.txt", "x"u8.ToArray());

        CurlRunResult result = await RunSignedAsync(endpoint, "s3://examplebucket/photos/caf%C3%A9%20%26%2B%3D.txt");

        AssertSucceeded(result, "x");
        S3ReferenceRequest request = endpoint.Requests.Single();
        Assert.AreEqual("/photos/caf%C3%A9%20%26%2B%3D.txt", request.Target);
        AssertSigned(verifier, request, S3SigV4ReferenceAuthorizerTests.EmptyHash);
    }

    [TestMethod]
    [SpecRequirement("S3:SigV4-SessionToken-1")]
    public async Task Download_WithASessionToken_SignsTheSecurityTokenHeader()
    {
        (S3ReferenceEndpoint endpoint, S3SigV4ReferenceAuthorizer verifier) = EndpointFor(sessionToken: "FQoGZXIvYXdzEXAMPLE");
        endpoint.PutObject("examplebucket", "test.txt", "t"u8.ToArray());
        Dictionary<string, string> environment = new()
        {
            ["AWS_ACCESS_KEY_ID"] = AccessKeyId,
            ["AWS_SECRET_ACCESS_KEY"] = SecretAccessKey,
            ["AWS_SESSION_TOKEN"] = "FQoGZXIvYXdzEXAMPLE",
        };

        CurlRunResult result = await RunAsync(endpoint, ["s3://examplebucket/test.txt"], environment);

        AssertSucceeded(result, "t");
        S3ReferenceRequest request = endpoint.Requests.Single();
        Assert.AreEqual("FQoGZXIvYXdzEXAMPLE", request.Headers["x-amz-security-token"]);
        AssertSigned(verifier, request, S3SigV4ReferenceAuthorizerTests.EmptyHash, signedHeaders: "host;x-amz-content-sha256;x-amz-date;x-amz-security-token");
    }

    [TestMethod]
    [SpecRequirement("S3:SigV4-Scope-2")]
    public async Task Download_InAnotherRegion_SignsWithThatRegionsScope()
    {
        (S3ReferenceEndpoint endpoint, S3SigV4ReferenceAuthorizer verifier) = EndpointFor("eu-west-1");
        endpoint.PutObject("examplebucket", "test.txt", "eu"u8.ToArray());

        CurlRunResult result = await RunAsync(endpoint, ["--user", AccessKeyId + ":" + SecretAccessKey, "s3://examplebucket/test.txt"], new Dictionary<string, string> { ["AWS_REGION"] = "eu-west-1" });

        AssertSucceeded(result, "eu");
        S3ReferenceRequest request = endpoint.Requests.Single();
        Assert.AreEqual("examplebucket.s3.eu-west-1.amazonaws.com", request.Headers["Host"]);
        AssertSigned(verifier, request, S3SigV4ReferenceAuthorizerTests.EmptyHash, "eu-west-1");
    }

    [TestMethod]
    [SpecRequirement("S3:SigV4-Signature-1")]
    public async Task Download_WithTheWrongSecret_IsRefusedAndExitsWith22()
    {
        (S3ReferenceEndpoint endpoint, S3SigV4ReferenceAuthorizer verifier) = EndpointFor();
        endpoint.PutObject("examplebucket", "test.txt", "secret"u8.ToArray());

        CurlRunResult result = await RunAsync(endpoint, ["--user", AccessKeyId + ":wrong", "s3://examplebucket/test.txt"]);

        Assert.AreEqual(22, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        StringAssert.Contains(Text(result.StandardError), "S3 returned error 403: SignatureDoesNotMatch: " + S3SigV4ReferenceAuthorizer.RefusalMessage);
        StringAssert.Contains(verifier.FaultOf(endpoint.Requests.Single()), "The signature does not match");
    }
}
