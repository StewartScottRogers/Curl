namespace Curl.HttpMessageSignatures;

/// <summary>
/// Pins RFC 9421 appendix B.2.5 (hmac-sha256) and B.2.6 (ed25519) byte for byte: the signature
/// base, the <c>Signature-Input</c> and the <c>Signature</c>, built from the appendix's
/// test-request through this library's own parts.
/// </summary>
[TestClass]
public sealed class Rfc9421TestVectorTests
{
    private const long Created = 1618884473;

    // RFC 9421 appendix B.1.5, test-shared-secret.
    private const string SharedSecret = "uzvJfB4u3N0Jy4T7NZ75MDVcr8zSTInedJtkgcu46YW4XByzNJjxBdtjUkdJPBtbmHhIDi6pcl8jsasjlTMtDQ==";

    // RFC 9421 appendix B.1.4, test-key-ed25519's JWK "d" (base64url, padding restored).
    private const string Ed25519PrivateKey = "n4Ni+HpISpVObnQMW0wOhCKROaIKqKtW/2ZYb2p9KcU=";

    // RFC 9421 appendix B.2, test-request's header fields.
    private static readonly HttpSignatureRequest TestRequest = new(
        "POST",
        "example.com",
        "/foo",
        "param=Value&Pet=dog",
        [
            "Host: example.com",
            "Date: Tue, 20 Apr 2021 02:07:55 GMT",
            "Content-Type: application/json",
            "Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:",
            "Content-Length: 18",
        ]);

    [TestMethod]
    public void Sign_HmacSha256OverAppendixB25Components_MatchesAppendixB25BytesExactly()
    {
        var headers = Sign(HttpSignatureAlgorithm.HmacSha256, Convert.FromBase64String(SharedSecret), "sig-b25", "test-shared-secret", ["date", "@authority", "content-type"]);

        Assert.AreEqual(
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n"
            + "\"@authority\": example.com\n"
            + "\"content-type\": application/json\n"
            + "\"@signature-params\": (\"date\" \"@authority\" \"content-type\");created=1618884473;keyid=\"test-shared-secret\"",
            headers.SignatureBase);
        Assert.AreEqual("sig-b25=(\"date\" \"@authority\" \"content-type\");created=1618884473;keyid=\"test-shared-secret\"", headers.SignatureInput);
        Assert.AreEqual("sig-b25=:pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=:", headers.Signature);
    }

    [TestMethod]
    public void Sign_Ed25519OverAppendixB26Components_MatchesAppendixB26BytesExactly()
    {
        var headers = Sign(HttpSignatureAlgorithm.Ed25519, Convert.FromBase64String(Ed25519PrivateKey), "sig-b26", "test-key-ed25519", ["date", "@method", "@path", "@authority", "content-type", "content-length"]);

        Assert.AreEqual(
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n"
            + "\"@method\": POST\n"
            + "\"@path\": /foo\n"
            + "\"@authority\": example.com\n"
            + "\"content-type\": application/json\n"
            + "\"content-length\": 18\n"
            + "\"@signature-params\": (\"date\" \"@method\" \"@path\" \"@authority\" \"content-type\" \"content-length\");created=1618884473;keyid=\"test-key-ed25519\"",
            headers.SignatureBase);
        Assert.AreEqual("sig-b26=(\"date\" \"@method\" \"@path\" \"@authority\" \"content-type\" \"content-length\");created=1618884473;keyid=\"test-key-ed25519\"", headers.SignatureInput);
        Assert.AreEqual("sig-b26=:wqcAqbmYJ2ji2glfAMaRy4gruYYnx2nEFN2HN6jrnDnQCK1u02Gb04v9EDgwUPiu4A0w6vuQv5lIp5WPpBKRCw==:", headers.Signature);
    }

    private static HttpSignatureHeaders Sign(HttpSignatureAlgorithm algorithm, byte[] key, string label, string keyId, string[] components)
    {
        var parameters = HttpSignatureBase.FormatParameters(components, Created, keyId, algorithmName: null);
        var values = components.Select(component => KeyValuePair.Create(component, Value(component))).ToList();
        var signatureBase = HttpSignatureBase.Build(values, parameters);
        return HttpSignatureHeaders.Create(label, parameters, signatureBase, HttpMessageSigner.Sign(algorithm, key, signatureBase));
    }

    private static string Value(string component) => component switch
    {
        "@method" => TestRequest.Method,
        "@path" => TestRequest.SignedPath,
        "@authority" => TestRequest.Authority,
        _ => TestRequest.HeaderValue(component),
    };
}
