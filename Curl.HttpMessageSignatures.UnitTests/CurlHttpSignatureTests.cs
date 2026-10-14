using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;

namespace Curl.HttpMessageSignatures;

/// <summary>
/// Checks the whole signing step against curl 8.22.0's <c>Curl_output_httpsig</c>: the <c>sig1</c>
/// header values for a fixed <c>created</c>, the default components, and every refusal's message.
/// </summary>
[TestClass]
public sealed class CurlHttpSignatureTests
{
    private const long Created = 1760000000;

    // A 32-byte Ed25519 private seed, as the hex --httpsig-key takes.
    private const string Ed25519HexKey = "9f8362f87a484a954e6e740c5b4c0e84229139a20aa8ab56ff66586f6a7d29c5";

    private const string HmacHexKey = "00112233445566778899aabbccddeeff";

    private static readonly HttpSignatureRequest PlainRequest = new("GET", "example.com:8080", "/data", null, ["Content-Type: text/plain"]);

    [TestMethod]
    public void CreateHeaders_Ed25519DefaultComponents_SignsMethodAuthorityPathWithSig1()
    {
        var headers = CurlHttpSignature.CreateHeaders(HttpSignatureAlgorithm.Ed25519, Ed25519HexKey, "my-key", null, PlainRequest, Created)!;

        const string parameters = "(\"@method\" \"@authority\" \"@path\");created=1760000000;keyid=\"my-key\";alg=\"ed25519\"";
        Assert.AreEqual(parameters, headers.SignatureParameters);
        Assert.AreEqual("sig1=" + parameters, headers.SignatureInput);
        Assert.AreEqual("\"@method\": GET\n\"@authority\": example.com:8080\n\"@path\": /data\n\"@signature-params\": " + parameters, headers.SignatureBase);
        Assert.IsTrue(Ed25519Verifies(headers));
    }

    [TestMethod]
    public void CreateHeaders_HmacWithQueryAndHeaderComponents_SignsThemInTheGivenOrder()
    {
        var request = new HttpSignatureRequest("POST", "example.com", "", "a=1", ["Content-Type: text/plain"]);

        var headers = CurlHttpSignature.CreateHeaders(HttpSignatureAlgorithm.HmacSha256, HmacHexKey, "shared", "content-type: query path", request, Created)!;

        const string parameters = "(\"content-type\" \"@query\" \"@path\");created=1760000000;keyid=\"shared\";alg=\"hmac-sha256\"";
        var signatureBase = "\"content-type\": text/plain\n\"@query\": ?a=1\n\"@path\": /\n\"@signature-params\": " + parameters;
        Assert.AreEqual(signatureBase, headers.SignatureBase);
        Assert.AreEqual("sig1=" + parameters, headers.SignatureInput);
        var mac = HMACSHA256.HashData(Convert.FromHexString(HmacHexKey), Encoding.UTF8.GetBytes(signatureBase));
        Assert.AreEqual("sig1=:" + Convert.ToBase64String(mac) + ":", headers.Signature);
    }

    [TestMethod]
    public void CreateHeaders_QueryInUrlAndNoComponentsText_AddsQuery()
    {
        var request = new HttpSignatureRequest("GET", "h", "/", "", []);

        var headers = CurlHttpSignature.CreateHeaders(HttpSignatureAlgorithm.HmacSha256, HmacHexKey, "k", null, request, Created)!;

        Assert.StartsWith("sig1=(\"@method\" \"@authority\" \"@path\" \"@query\");", headers.SignatureInput);
        Assert.Contains("\"@query\": ?\n", headers.SignatureBase);
    }

    [TestMethod]
    [DataRow("Signature: sig1=:AA==:")]
    [DataRow("signature-input;")]
    public void CreateHeaders_UserSetsASignatureHeader_ReturnsNull(string line)
    {
        var request = new HttpSignatureRequest("GET", "h", "/", null, [line]);

        var headers = CurlHttpSignature.CreateHeaders(HttpSignatureAlgorithm.Ed25519, "not hex", "k", "bogus", request, Created);

        Assert.IsNull(headers);
    }

    [TestMethod]
    [DataRow(null, "k", "httpsig: CURLOPT_HTTPSIG_KEY is required")]
    [DataRow("", "k", "httpsig: CURLOPT_HTTPSIG_KEY is required")]
    [DataRow(HmacHexKey, null, "httpsig: CURLOPT_HTTPSIG_KEYID is required")]
    [DataRow(HmacHexKey, "", "httpsig: CURLOPT_HTTPSIG_KEYID is required")]
    [DataRow("0", "k", "httpsig: invalid hex key (length 1)")]
    [DataRow("0011", "k", "httpsig: ed25519 requires a 32-byte key (got 2)")]
    public void CreateHeaders_MissingOrBadKey_ThrowsCurlsMessage(string? hexKey, string? keyId, string message)
    {
        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => CurlHttpSignature.CreateHeaders(HttpSignatureAlgorithm.Ed25519, hexKey, keyId, null, PlainRequest, Created));

        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    public void CreateHeaders_HeaderComponentNotSent_ThrowsNotFound()
    {
        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => CurlHttpSignature.CreateHeaders(HttpSignatureAlgorithm.Ed25519, Ed25519HexKey, "k", "method date:", PlainRequest, Created));

        Assert.AreEqual("httpsig: header 'date' not found in request", exception.Message);
    }

    [TestMethod]
    public void CreateHeaders_NullRequest_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlHttpSignature.CreateHeaders(HttpSignatureAlgorithm.Ed25519, Ed25519HexKey, "k", null, null!, Created));
    }

    private static bool Ed25519Verifies(HttpSignatureHeaders headers)
    {
        var publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.ComputePublicKey(Convert.FromHexString(Ed25519HexKey), publicKey);
        var signature = Convert.FromBase64String(headers.Signature["sig1=:".Length..^1]);
        return Ed25519.Verify(publicKey, Encoding.UTF8.GetBytes(headers.SignatureBase), signature);
    }
}
