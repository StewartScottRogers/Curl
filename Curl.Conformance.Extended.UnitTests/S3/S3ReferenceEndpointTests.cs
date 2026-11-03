using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.S3;

namespace Curl.Conformance.Extended.UnitTests.S3;

// The endpoint's answers to requests curl's s3:// client never sends, driven with http:// command lines.
[TestClass]
public sealed class S3ReferenceEndpointTests
{
    private const string BucketUrl = "http://localhost:9000/data";

    [TestMethod]
    public async Task ServeAsync_MalformedAuthorization_IsAuthorizationHeaderMalformed()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("data");

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["-H", "Authorization: Basic eDp5", BucketUrl + "/a"]);

        StringAssert.Contains(S3CaseRun.Text(result.StandardOutput), "<Code>AuthorizationHeaderMalformed</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_SignatureVersion4Authorization_IsServed()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("data");
        endpoint.PutObject("data", "a", "ok"u8.ToArray());

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["-H", "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20261102/us-east-1/s3/aws4_request", BucketUrl + "/a"]);

        Assert.AreEqual("ok", S3CaseRun.Text(result.StandardOutput));
    }

    [TestMethod]
    public async Task ServeAsync_Delete_IsMethodNotAllowed()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("data");

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, ["-X", "DELETE", BucketUrl + "/a"]);

        StringAssert.Contains(S3CaseRun.Text(result.StandardOutput), "<Code>MethodNotAllowed</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ListWithoutADelimiter_ListsEveryKey()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("data");
        endpoint.PutObject("data", "dir/x", [1]);

        CurlRunResult result = await S3CaseRun.RunAsync(endpoint, [BucketUrl + "?list-type=2"]);

        StringAssert.Contains(S3CaseRun.Text(result.StandardOutput), "<Contents><Key>dir/x</Key>");
    }

    [TestMethod]
    public async Task ServeAsync_CurlClosingBeforeTheRequestEnds_Throws()
    {
        S3ReferenceEndpoint endpoint = S3CaseRun.EndpointWithBucket("data");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            S3CaseRun.RunAsync(endpoint, ["-m", "1", "gopher://localhost:9000/1data"]));
    }

    [TestMethod]
    public void Authorize_NullRequest_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new S3WellFormedAuthorization().Authorize(null!));

    [TestMethod]
    public async Task ServeAsync_NullConnection_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new S3ReferenceEndpoint("h").ServeAsync(null!, CancellationToken.None));
}
