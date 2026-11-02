using System.Text;
using Curl.Conformance.Extended.GoogleCloudStorage;
using Curl.Conformance.Extended.GoogleCloudStorage.Signing;

namespace Curl.Conformance.Extended.UnitTests.GoogleCloudStorage.Signing;

/// <summary>
/// The V4 signature verifier against a fixed vector computed by hand from the documentation's steps (a PowerShell run of the canonical
/// request, string to sign and HMAC chain, BL-4476), and each way a request can fail it.
/// </summary>
[TestClass]
public sealed class GcsV4HmacReferenceAuthorizerTests
{
    private const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private const string VectorSignature = "a488c22c8c105c3f3fcfdd710cef1a865ff9a907a6b5cacba349efefaa92bff8";

    private const string SignedHead =
        "GET /photos/caf%C3%A9%20%26.txt?prefix=a%2Fb&list-type=2 HTTP/1.1\r\n" +
        "Host: storage.googleapis.com\r\n" +
        "x-goog-date: 20261102T103000Z\r\n" +
        "x-goog-content-sha256: " + EmptyHash + "\r\n" +
        "Authorization: GOOG4-HMAC-SHA256 Credential=GOOG1EXAMPLE/20261102/auto/storage/goog4_request, SignedHeaders=host;x-goog-content-sha256;x-goog-date, Signature=" + VectorSignature;

    private static readonly GcsV4HmacReferenceAuthorizer Authorizer = new("GOOG1EXAMPLE", "s3cr3t", "auto", new GcsFixedClock(GcsFixedClock.CaseTime));

    [TestMethod]
    public void SignatureOf_TheFixedVector_IsTheHandComputedSignature() =>
        Assert.AreEqual(VectorSignature, Authorizer.SignatureOf(GcsReferenceRequest.ParseHead(SignedHead), "host;x-goog-content-sha256;x-goog-date"));

    [TestMethod]
    public void Refuse_TheFixedVector_ServesIt() =>
        Assert.IsNull(Authorizer.Refuse(GcsReferenceRequest.ParseHead(SignedHead)));

    [TestMethod]
    public void Refuse_UnsignedPayloadWithoutTheHashHeader_ServesItWhenSigned()
    {
        string head = SignedHead.Replace("x-goog-content-sha256: " + EmptyHash + "\r\n", string.Empty, StringComparison.Ordinal).Replace("host;x-goog-content-sha256;x-goog-date", "host;x-goog-date", StringComparison.Ordinal);
        string signature = Authorizer.SignatureOf(GcsReferenceRequest.ParseHead(head), "host;x-goog-date");

        Assert.IsNull(Authorizer.Refuse(GcsReferenceRequest.ParseHead(head.Replace(VectorSignature, signature, StringComparison.Ordinal))));
    }

    [TestMethod]
    public void SignatureOf_ASignedHeaderNotSent_SignsItAsEmpty() =>
        Assert.AreNotEqual(VectorSignature, Authorizer.SignatureOf(GcsReferenceRequest.ParseHead(SignedHead), "host;range;x-goog-content-sha256;x-goog-date"));

    [TestMethod]
    [DataRow("Authorization: GOOG4-HMAC-SHA256", "X-Other: GOOG4-HMAC-SHA256", "no GOOG4-HMAC-SHA256 Authorization header")]
    [DataRow("Authorization: GOOG4-HMAC-SHA256", "Authorization: AWS4-HMAC-SHA256", "no GOOG4-HMAC-SHA256 Authorization header")]
    [DataRow("Credential=", "junk, Credentials=", "no GOOG4-HMAC-SHA256 Authorization header")]
    [DataRow("SignedHeaders=", "Signed=", "no GOOG4-HMAC-SHA256 Authorization header")]
    [DataRow("Signature=", "Sig=", "no GOOG4-HMAC-SHA256 Authorization header")]
    [DataRow("x-goog-date: 20261102T103000Z", "x-goog-date: 2026-11-02", "no x-goog-date")]
    [DataRow("x-goog-date: 20261102T103000Z", "x-goog-date: 20261102T105100Z", "more than 15 minutes")]
    [DataRow("Credential=GOOG1EXAMPLE/", "Credential=GOOG1OTHER/", "The credential is not GOOG1EXAMPLE/20261102/auto/storage/goog4_request.")]
    [DataRow("/auto/storage", "/us-east1/storage", "The credential is not")]
    [DataRow("SignedHeaders=host;x-goog-content-sha256;x-goog-date", "SignedHeaders=x-goog-date;host;x-goog-content-sha256", "not lower case, sorted")]
    [DataRow("SignedHeaders=host;x-goog-content-sha256;x-goog-date", "SignedHeaders=Host;x-goog-content-sha256;x-goog-date", "not lower case, sorted")]
    [DataRow("SignedHeaders=host;x-goog-content-sha256;x-goog-date", "SignedHeaders=host;x-goog-date", "leaves out a sent host or x-goog-* header, or names one not sent: x-goog-content-sha256.")]
    [DataRow("SignedHeaders=host;x-goog-content-sha256;x-goog-date", "SignedHeaders=host;range;x-goog-content-sha256;x-goog-date", "names one not sent: range.")]
    [DataRow("x-goog-content-sha256: e3b0", "x-goog-content-sha256: 00e3b0", "is not the body's SHA-256")]
    [DataRow("Signature=a488", "Signature=b488", "The signature does not match")]
    public void Refuse_AFaultySignature_Gives403SignatureDoesNotMatchSayingWhy(string signedText, string faultyText, string fault)
    {
        GcsReferenceResponse? refusal = Authorizer.Refuse(GcsReferenceRequest.ParseHead(SignedHead.Replace(signedText, faultyText, StringComparison.Ordinal)));

        Assert.IsNotNull(refusal);
        Assert.AreEqual(403, refusal.StatusCode);
        string body = Encoding.UTF8.GetString(refusal.Body);
        StringAssert.Contains(body, "<Error><Code>SignatureDoesNotMatch</Code><Message>" + GcsV4HmacReferenceAuthorizer.RefusalMessage + "</Message>");
        StringAssert.Contains(body, fault);
    }
}
