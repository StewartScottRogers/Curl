namespace Curl.HttpMessageSignatures;

/// <summary>Checks the RFC 9421 names curl writes in the <c>alg</c> parameter.</summary>
[TestClass]
public sealed class HttpSignatureAlgorithmNamesTests
{
    [TestMethod]
    [DataRow(HttpSignatureAlgorithm.Ed25519, "ed25519")]
    [DataRow(HttpSignatureAlgorithm.HmacSha256, "hmac-sha256")]
    public void ToName_Algorithm_ReturnsRfc9421Name(HttpSignatureAlgorithm algorithm, string expected)
    {
        Assert.AreEqual(expected, HttpSignatureAlgorithmNames.ToName(algorithm));
    }
}
