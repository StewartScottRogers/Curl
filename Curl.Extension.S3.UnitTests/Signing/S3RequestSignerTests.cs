namespace Curl.Extension.S3.Signing;

/// <summary>
/// Pins the four examples of the Amazon S3 API reference "Signature Calculations for the
/// Authorization Header: Transferring Payload in a Single Chunk", each signed with that section's
/// access key, secret key, region <c>us-east-1</c>, bucket <c>examplebucket</c> and timestamp
/// 20130524T000000Z.
/// </summary>
[TestClass]
public sealed class S3RequestSignerTests
{
    private const string Host = "examplebucket.s3.amazonaws.com";
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly S3Credentials ExampleCredentials = new("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "us-east-1");

    [TestMethod]
    public void Sign_ReferenceExampleGetObject_ReproducesItsSignature()
    {
        // "Example: GET Object": the first 10 bytes of test.txt, with a Range header.
        var request = new S3SigningRequest("GET", "/test.txt", [], [Header("Host", Host), Header("Range", "bytes=0-9")], EmptyPayloadHash);

        var headers = Sign(request);

        Assert.AreEqual(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41",
            headers["Authorization"]);
    }

    [TestMethod]
    public void Sign_ReferenceExamplePutObject_ReproducesItsSignature()
    {
        // "Example: PUT Object": "Welcome to Amazon S3." to test$file.text, whose "$" the canonical URI encodes as %24.
        var request = new S3SigningRequest(
            "PUT",
            "/test$file.text",
            [],
            [Header("Host", Host), Header("Date", "Fri, 24 May 2013 00:00:00 GMT"), Header("x-amz-storage-class", "REDUCED_REDUNDANCY")],
            "44ce7dd67c959e0d3524ffac1771dfbba87d2b6b4b4e99e42034a8b803f8b072");

        var headers = Sign(request);

        Assert.AreEqual(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,SignedHeaders=date;host;x-amz-content-sha256;x-amz-date;x-amz-storage-class,Signature=98ad721746da40c64f1a55b78f14c238d841ea1380cd77a1b5971af0ece108bd",
            headers["Authorization"]);
    }

    [TestMethod]
    public void Sign_ReferenceExampleGetBucketLifecycle_ReproducesItsSignature()
    {
        // "Example: GET Bucket Lifecycle": the query "?lifecycle" is the parameter "lifecycle" with an empty value.
        var request = new S3SigningRequest("GET", "/", [Header("lifecycle", string.Empty)], [Header("Host", Host)], EmptyPayloadHash);

        var headers = Sign(request);

        Assert.AreEqual(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,SignedHeaders=host;x-amz-content-sha256;x-amz-date,Signature=fea454ca298b7da1c68078a5d1bdbfbbe0d65c699e0f91ac7a200a0136783543",
            headers["Authorization"]);
    }

    [TestMethod]
    public void Sign_ReferenceExampleGetBucketListObjects_ReproducesItsSignature()
    {
        // "Example: Get Bucket (List Objects)": "?max-keys=2&prefix=J", given here out of order to show the sort.
        var request = new S3SigningRequest("GET", "/", [Header("prefix", "J"), Header("max-keys", "2")], [Header("Host", Host)], EmptyPayloadHash);

        var headers = Sign(request);

        Assert.AreEqual(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,SignedHeaders=host;x-amz-content-sha256;x-amz-date,Signature=34b48302e7b5fa45bde8084f4b7868a86f0a534bc59db6670ed5711ef69dc6f7",
            headers["Authorization"]);
    }

    [TestMethod]
    public void Sign_AnyRequest_ReturnsTheDateThePayloadHashAndTheAuthorizationInOrder()
    {
        var request = new S3SigningRequest("GET", "/", [], [Header("Host", Host)], S3RequestSigner.UnsignedPayload);

        var headers = new S3RequestSigner(new FixedTimeProvider()).Sign(request, ExampleCredentials);

        CollectionAssert.AreEqual(new[] { "x-amz-date", "x-amz-content-sha256", "Authorization" }, headers.Select(header => header.Key).ToArray());
        Assert.AreEqual("20130524T000000Z", headers[0].Value);
        Assert.AreEqual("UNSIGNED-PAYLOAD", headers[1].Value);
    }

    [TestMethod]
    public void Sign_NullRequest_Throws()
    {
        var signer = new S3RequestSigner(new FixedTimeProvider());

        Assert.ThrowsExactly<ArgumentNullException>(() => signer.Sign(null!, ExampleCredentials));
    }

    [TestMethod]
    public void Sign_NullCredentials_Throws()
    {
        var signer = new S3RequestSigner(new FixedTimeProvider());
        var request = new S3SigningRequest("GET", "/", [], [Header("Host", Host)], EmptyPayloadHash);

        Assert.ThrowsExactly<ArgumentNullException>(() => signer.Sign(request, null!));
    }

    private static Dictionary<string, string> Sign(S3SigningRequest request) =>
        new S3RequestSigner(new FixedTimeProvider()).Sign(request, ExampleCredentials).ToDictionary(header => header.Key, header => header.Value);

    private static KeyValuePair<string, string> Header(string name, string value) => new(name, value);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);
    }
}
