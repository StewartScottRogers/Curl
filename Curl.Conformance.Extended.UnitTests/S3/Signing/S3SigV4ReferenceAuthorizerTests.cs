using System.Text;
using Curl.Conformance.Extended.S3;
using Curl.Conformance.Extended.S3.Signing;

namespace Curl.Conformance.Extended.UnitTests.S3.Signing;

/// <summary>
/// The Signature Version 4 verifier against the S3 API reference's published single-chunk examples (GET Object with a range, PUT
/// Object, ListObjects), each signature reproduced byte for byte, and each way a request can fail it.
/// </summary>
[TestClass]
public sealed class S3SigV4ReferenceAuthorizerTests
{
    internal const string ExampleAccessKeyId = "AKIAIOSFODNN7EXAMPLE";

    internal const string ExampleSecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    internal const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private const string ExampleHost = "examplebucket.s3.amazonaws.com";

    private const string ExampleScope = ExampleAccessKeyId + "/20130524/us-east-1/s3/aws4_request";

    private const string GetSignature = "f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41";

    private const string ListSignature = "34b48302e7b5fa45bde8084f4b7868a86f0a534bc59db6670ed5711ef69dc6f7";

    private static readonly S3SigV4ReferenceAuthorizer Verifier = new(ExampleAccessKeyId, ExampleSecretAccessKey, "us-east-1", new S3FixedClock(S3FixedClock.ExampleTime));

    private static S3ReferenceRequest Request(string method, string target, Dictionary<string, string> query, Dictionary<string, string> headers, byte[]? body = null) =>
        new(method, target, query, new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase), body ?? [], "examplebucket", string.Empty, true);

    private static Dictionary<string, string> GetHeaders(string signature = GetSignature) => new()
    {
        ["Host"] = ExampleHost,
        ["Range"] = "bytes=0-9",
        ["x-amz-content-sha256"] = EmptyHash,
        ["x-amz-date"] = "20130524T000000Z",
        ["Authorization"] = $"AWS4-HMAC-SHA256 Credential={ExampleScope},SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature={signature}",
    };

    private static S3ReferenceRequest GetExample(Action<Dictionary<string, string>>? change = null)
    {
        Dictionary<string, string> headers = GetHeaders();
        change?.Invoke(headers);
        return Request("GET", "/test.txt", [], headers);
    }

    [TestMethod]
    public void SignatureOf_TheGetObjectExample_IsTheReferencesSignature() =>
        Assert.AreEqual(GetSignature, Verifier.SignatureOf(GetExample(), "host;range;x-amz-content-sha256;x-amz-date"));

    [TestMethod]
    public void FaultOf_TheGetObjectExample_IsNone() =>
        Assert.IsNull(Verifier.FaultOf(GetExample()));

    [TestMethod]
    public void Authorize_ThePutObjectExample_ServesItsSignature()
    {
        const string Signature = "98ad721746da40c64f1a55b78f14c238d841ea1380cd77a1b5971af0ece108bd";
        S3ReferenceRequest put = Request(
            "PUT",
            "/test$file.text",
            [],
            new()
            {
                ["Host"] = ExampleHost,
                ["Date"] = "Fri, 24 May 2013 00:00:00 GMT",
                ["x-amz-date"] = "20130524T000000Z",
                ["x-amz-storage-class"] = "REDUCED_REDUNDANCY",
                ["x-amz-content-sha256"] = "44ce7dd67c959e0d3524ffac1771dfbba87d2b6b4b4e99e42034a8b803f8b072",
                ["Authorization"] = $"AWS4-HMAC-SHA256 Credential={ExampleScope},SignedHeaders=date;host;x-amz-content-sha256;x-amz-date;x-amz-storage-class,Signature={Signature}",
            },
            Encoding.ASCII.GetBytes("Welcome to Amazon S3."));

        Assert.AreEqual(Signature, Verifier.SignatureOf(put, "date;host;x-amz-content-sha256;x-amz-date;x-amz-storage-class"));
        Assert.IsNull(Verifier.Authorize(put));
    }

    [TestMethod]
    public void Authorize_TheListObjectsExample_ServesItsSignature()
    {
        S3ReferenceRequest list = Request(
            "GET",
            "/?max-keys=2&prefix=J",
            new() { ["max-keys"] = "2", ["prefix"] = "J" },
            new()
            {
                ["Host"] = ExampleHost,
                ["x-amz-date"] = "20130524T000000Z",
                ["x-amz-content-sha256"] = EmptyHash,
                ["Authorization"] = $"AWS4-HMAC-SHA256 Credential={ExampleScope},SignedHeaders=host;x-amz-content-sha256;x-amz-date,Signature={ListSignature}",
            });

        Assert.AreEqual(ListSignature, Verifier.SignatureOf(list, "host;x-amz-content-sha256;x-amz-date"));
        Assert.IsNull(Verifier.Authorize(list));
    }

    [TestMethod]
    public void UriEncode_ReservedSpaceAndNonAscii_AreUpperCaseHexOfTheirUtf8Bytes()
    {
        Assert.AreEqual("caf%C3%A9%20%26/a%2Bb~", S3SigV4CanonicalRequest.UriEncode("café &/a+b~", keepSlash: true));
        Assert.AreEqual("a%2Fb", S3SigV4CanonicalRequest.UriEncode("a/b", keepSlash: false));
    }

    [TestMethod]
    public void Of_ASignedHeaderNotSent_SignsItAsEmptyAndCollapsesWhitespace()
    {
        string canonical = S3SigV4CanonicalRequest.Of(GetExample(headers => headers["Range"] = "  bytes=0-9 \t x "), "host;range;x-other", EmptyHash);

        StringAssert.Contains(canonical, "range:bytes=0-9 x\nx-other:\n");
    }

    [TestMethod]
    public void FaultOf_UnsignedPayload_IsAcceptedWhenSigned()
    {
        S3ReferenceRequest unsigned = GetExample(headers => headers["x-amz-content-sha256"] = S3SigV4ReferenceAuthorizer.UnsignedPayload);
        string signature = Verifier.SignatureOf(unsigned, "host;range;x-amz-content-sha256;x-amz-date");

        Assert.IsNull(Verifier.FaultOf(GetExample(headers =>
        {
            headers["x-amz-content-sha256"] = S3SigV4ReferenceAuthorizer.UnsignedPayload;
            headers["Authorization"] = headers["Authorization"].Replace(GetSignature, signature, StringComparison.Ordinal);
        })));
    }

    [TestMethod]
    public void FaultOf_TheSessionTokenItExpects_IsChecked()
    {
        S3SigV4ReferenceAuthorizer withToken = new(ExampleAccessKeyId, ExampleSecretAccessKey, "us-east-1", new S3FixedClock(S3FixedClock.ExampleTime)) { SessionToken = "token" };

        StringAssert.Contains(withToken.FaultOf(GetExample()), "x-amz-security-token is not the session token");
    }

    [TestMethod]
    public void Authorize_AFault_IsSignatureDoesNotMatch403() =>
        Assert.AreEqual(
            new S3ReferenceError(403, "SignatureDoesNotMatch", S3SigV4ReferenceAuthorizer.RefusalMessage),
            Verifier.Authorize(GetExample(headers => headers.Remove("Authorization"))));

    [TestMethod]
    [DataRow("Authorization", "AWS4-HMAC-SHA256", "no AWS4-HMAC-SHA256 Authorization header")]
    [DataRow("Authorization", "AWS4-HMAC-SHA256 Credential=x,SignedHeaders=host", "no AWS4-HMAC-SHA256 Authorization header")]
    [DataRow("Authorization", "AWS4-HMAC-SHA256 Credential,SignedHeaders=host,Signature=s", "no AWS4-HMAC-SHA256 Authorization header")]
    [DataRow("x-amz-date", "2013-05-24", "no x-amz-date")]
    [DataRow("x-amz-date", "20130524T001600Z", "more than 15 minutes")]
    [DataRow("x-amz-content-sha256", "00", "is not the body's SHA-256")]
    [DataRow("x-amz-security-token", "token", "x-amz-security-token is not the session token")]
    [DataRow("Range", "bytes=0-8", "The signature does not match")]
    public void FaultOf_AChangedHeader_SaysWhatIsWrong(string name, string value, string fault) =>
        StringAssert.Contains(Verifier.FaultOf(GetExample(headers => headers[name] = value)), fault);

    [TestMethod]
    [DataRow("Credential=" + ExampleScope, "Credential=" + ExampleAccessKeyId + "/20130524/eu-west-1/s3/aws4_request", "The credential is not")]
    [DataRow("SignedHeaders=host;range;", "SignedHeaders=range;host;", "is not lower case, sorted")]
    [DataRow("SignedHeaders=host;range;", "SignedHeaders=Host;range;", "is not lower case, sorted")]
    [DataRow("SignedHeaders=host;range;", "SignedHeaders=host;host;range;", "is not lower case, sorted")]
    [DataRow("SignedHeaders=host;range;", "SignedHeaders=range;", "leaves out a sent host")]
    [DataRow("x-amz-date,Signature", "x-amz-date;x-other,Signature", "names one not sent: x-other")]
    public void FaultOf_AChangedAuthorization_SaysWhatIsWrong(string from, string to, string fault) =>
        StringAssert.Contains(Verifier.FaultOf(GetExample(headers => headers["Authorization"] = headers["Authorization"].Replace(from, to, StringComparison.Ordinal))), fault);

    [TestMethod]
    public void FaultOf_AnUnsignedAmazonHeader_IsLeftOut() =>
        StringAssert.Contains(Verifier.FaultOf(GetExample(headers => headers["x-amz-meta-a"] = "1")), "leaves out a sent host or x-amz-* header, or names one not sent: x-amz-meta-a");
}
