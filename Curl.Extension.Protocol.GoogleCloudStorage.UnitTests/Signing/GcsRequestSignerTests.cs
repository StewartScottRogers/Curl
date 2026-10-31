namespace Curl.Extension.Protocol.GoogleCloudStorage.Signing;

/// <summary>
/// Pins the Cloud Storage "V4 signing process" in the <c>Authorization</c> header form for one
/// request, signed with the fixed HMAC key <c>GOOG1EXAMPLE</c> / <c>EXAMPLEKEY</c>, region
/// <c>auto</c> and time 20191020T000000Z. The expected signature was derived outside this
/// library by the section's steps (signing key from <c>GOOG4</c> + secret, date, region,
/// <c>storage</c>, <c>goog4_request</c>; HMAC-SHA256 of the string to sign).
/// </summary>
[TestClass]
public sealed class GcsRequestSignerTests
{
    private const string Timestamp = "20191020T000000Z";
    private const string Scope = "20191020/auto/storage/goog4_request";
    private const string CanonicalRequest =
        "GET\n/example-bucket/cat%20pics/tabby.jpeg\nacl=&prefix=a%2Fb\nhost:storage.googleapis.com\nx-goog-content-sha256:UNSIGNED-PAYLOAD\nx-goog-date:20191020T000000Z\n\nhost;x-goog-content-sha256;x-goog-date\nUNSIGNED-PAYLOAD";

    private const string StringToSign =
        "GOOG4-HMAC-SHA256\n20191020T000000Z\n20191020/auto/storage/goog4_request\n759a6d02615db0689eaa1ffe6a667aa862ccff0d78c6067ecff132a48aac9231";

    private static readonly GcsHmacKey ExampleKey = new("GOOG1EXAMPLE", "EXAMPLEKEY");

    private static readonly GcsSigningRequest ExampleRequest = new(
        "GET",
        "/example-bucket/cat pics/tabby.jpeg",
        [Header("prefix", "a/b"), Header("acl", string.Empty)],
        [Header("Host", "storage.googleapis.com")],
        GcsRequestSigner.UnsignedPayload);

    [TestMethod]
    public void Build_ExampleRequestWithGoogleHeaders_IsTheCanonicalRequest()
    {
        var request = ExampleRequest with
        {
            Headers = [.. ExampleRequest.Headers, Header("x-goog-date", Timestamp), Header("x-goog-content-sha256", GcsRequestSigner.UnsignedPayload)],
        };

        var (canonicalRequest, signedHeaders) = GcsCanonicalRequest.Build(request);

        Assert.AreEqual(CanonicalRequest, canonicalRequest);
        Assert.AreEqual("host;x-goog-content-sha256;x-goog-date", signedHeaders);
    }

    [TestMethod]
    public void StringToSign_ExampleCanonicalRequest_HashesItUnderTheScope()
    {
        Assert.AreEqual(Scope, GcsRequestSigner.CredentialScope("20191020", GcsHmacKey.AutoRegion));
        Assert.AreEqual(StringToSign, GcsRequestSigner.StringToSign(Timestamp, Scope, CanonicalRequest));
    }

    [TestMethod]
    public void Sign_ExampleRequest_ReturnsTheGoogleHeadersAndTheSignature()
    {
        var headers = new GcsRequestSigner(new FixedTimeProvider()).Sign(ExampleRequest, ExampleKey);

        CollectionAssert.AreEqual(
            new[]
            {
                Header("x-goog-date", Timestamp),
                Header("x-goog-content-sha256", GcsRequestSigner.UnsignedPayload),
                Header(
                    "Authorization",
                    "GOOG4-HMAC-SHA256 Credential=GOOG1EXAMPLE/20191020/auto/storage/goog4_request, SignedHeaders=host;x-goog-content-sha256;x-goog-date, Signature=272883af9ae3a5b6c51fd6e040a34aa0306207aa969235c223d93522c0e27f7d"),
            },
            headers.ToArray());
    }

    [TestMethod]
    public void Sign_KeyWithRegion_PutsTheRegionInTheCredentialScope()
    {
        var headers = new GcsRequestSigner(new FixedTimeProvider()).Sign(ExampleRequest, ExampleKey with { Region = "us-central1" });

        StringAssert.StartsWith(headers[2].Value, "GOOG4-HMAC-SHA256 Credential=GOOG1EXAMPLE/20191020/us-central1/storage/goog4_request, ");
        Assert.AreNotEqual("272883af9ae3a5b6c51fd6e040a34aa0306207aa969235c223d93522c0e27f7d", headers[2].Value[^64..]);
    }

    [TestMethod]
    public void Sign_NullRequestOrKey_Throws()
    {
        var signer = new GcsRequestSigner(new FixedTimeProvider());

        Assert.ThrowsExactly<ArgumentNullException>(() => signer.Sign(null!, ExampleKey));
        Assert.ThrowsExactly<ArgumentNullException>(() => signer.Sign(ExampleRequest, null!));
    }

    private static KeyValuePair<string, string> Header(string name, string value) => new(name, value);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2019, 10, 20, 0, 0, 0, TimeSpan.Zero);
    }
}
