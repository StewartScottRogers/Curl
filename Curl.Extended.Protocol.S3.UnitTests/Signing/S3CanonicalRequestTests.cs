namespace Curl.Extended.Protocol.S3.Signing;

/// <summary>
/// Pins the canonical request rules of the Amazon S3 API reference "Signature Calculations for the
/// Authorization Header: Transferring Payload in a Single Chunk".
/// </summary>
[TestClass]
public sealed class S3CanonicalRequestTests
{
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [TestMethod]
    public void Build_ReferenceExampleGetObject_ReproducesItsCanonicalRequest()
    {
        // "Example: GET Object" prints this canonical request.
        var request = new S3SigningRequest(
            "GET",
            "/test.txt",
            [],
            [Header("x-amz-date", "20130524T000000Z"), Header("Range", "bytes=0-9"), Header("Host", "examplebucket.s3.amazonaws.com"), Header("x-amz-content-sha256", EmptyPayloadHash)],
            EmptyPayloadHash);

        var (canonicalRequest, signedHeaders) = S3CanonicalRequest.Build(request);

        Assert.AreEqual(
            "GET\n/test.txt\n\nhost:examplebucket.s3.amazonaws.com\nrange:bytes=0-9\nx-amz-content-sha256:" + EmptyPayloadHash
            + "\nx-amz-date:20130524T000000Z\n\nhost;range;x-amz-content-sha256;x-amz-date\n" + EmptyPayloadHash,
            canonicalRequest);
        Assert.AreEqual("host;range;x-amz-content-sha256;x-amz-date", signedHeaders);
    }

    [TestMethod]
    public void Build_HeaderValueWithOuterAndSequentialSpaces_TrimsAndCollapsesThem()
    {
        var request = new S3SigningRequest("GET", "/", [], [Header("X-Note", "  a   b c  ")], EmptyPayloadHash);

        var (canonicalRequest, _) = S3CanonicalRequest.Build(request);

        StringAssert.Contains(canonicalRequest, "\nx-note:a b c\n");
    }

    [TestMethod]
    public void Build_HeaderGivenTwice_JoinsItsValuesWithACommaUnderOneName()
    {
        var request = new S3SigningRequest("GET", "/", [], [Header("X-Note", "one"), Header("x-note", "two")], EmptyPayloadHash);

        var (canonicalRequest, signedHeaders) = S3CanonicalRequest.Build(request);

        StringAssert.Contains(canonicalRequest, "\nx-note:one,two\n");
        Assert.AreEqual("x-note", signedHeaders);
    }

    [TestMethod]
    public void Build_QueryParametersSharingAName_SortsThemByEncodedNameThenValue()
    {
        var request = new S3SigningRequest("GET", "/", [Header("b", "2"), Header("a b", "z"), Header("b", "1")], [], EmptyPayloadHash);

        var (canonicalRequest, _) = S3CanonicalRequest.Build(request);

        StringAssert.StartsWith(canonicalRequest, "GET\n/\na%20b=z&b=1&b=2\n");
    }

    [TestMethod]
    public void Build_NullRequest_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => S3CanonicalRequest.Build(null!));
    }

    private static KeyValuePair<string, string> Header(string name, string value) => new(name, value);
}
