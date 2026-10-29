namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// Pins the token and device authorization requests against the examples of RFC 6749 §2.3.1, §4.1.3,
/// §4.4.2 and §6, RFC 7636 §4.5 and RFC 8628 §3.1 and §3.4.
/// </summary>
[TestClass]
public sealed class OAuth2TokenRequestWriterTests
{
    private static readonly OAuth2Client BasicClient = new("s6BhdRkqt3", "gX1fBat3bV", OAuth2ClientAuthentication.Basic);
    private static readonly OAuth2Client PublicClient = new("1406020730", null, OAuth2ClientAuthentication.Basic);

    [TestMethod]
    public void WriteClientCredentials_Section442Example_SendsGrantTypeAndBasicAuthorization()
    {
        var request = OAuth2TokenRequestWriter.WriteClientCredentials(BasicClient, null);

        Assert.AreEqual(new OAuth2FormRequest("grant_type=client_credentials", "Basic czZCaGRSa3F0MzpnWDFmQmF0M2JW"), request);
    }

    [TestMethod]
    public void WriteClientCredentials_WithScope_SendsScope()
    {
        var request = OAuth2TokenRequestWriter.WriteClientCredentials(BasicClient, "read write");

        Assert.AreEqual("grant_type=client_credentials&scope=read+write", request.Body);
    }

    [TestMethod]
    public void WriteAuthorizationCode_Section413ExampleWithVerifier_SendsCodeRedirectUriAndVerifier()
    {
        // §4.1.3's example writes "." as "%2E"; Appendix B leaves "." as it is, and both decode alike.
        var request = OAuth2TokenRequestWriter.WriteAuthorizationCode(
            BasicClient, "SplxlOBeZQQYbYS6WxSbIA", "https://client.example.com/cb", "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");

        Assert.AreEqual(
            new OAuth2FormRequest(
                "grant_type=authorization_code&code=SplxlOBeZQQYbYS6WxSbIA&redirect_uri=https%3A%2F%2Fclient.example.com%2Fcb"
                    + "&code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk",
                "Basic czZCaGRSa3F0MzpnWDFmQmF0M2JW"),
            request);
    }

    [TestMethod]
    public void WriteAuthorizationCode_PublicClientWithoutRedirectUri_SendsClientIdInTheBody()
    {
        var request = OAuth2TokenRequestWriter.WriteAuthorizationCode(PublicClient, "abc", null, "verifier");

        Assert.AreEqual(
            new OAuth2FormRequest("grant_type=authorization_code&code=abc&code_verifier=verifier&client_id=1406020730", null),
            request);
    }

    [TestMethod]
    public void WriteRefreshToken_Section231BodyParametersExample_SendsClientIdAndSecretInTheBody()
    {
        var client = new OAuth2Client("s6BhdRkqt3", "7Fjfp0ZBr1KtDRbnfVdmIw", OAuth2ClientAuthentication.RequestBody);

        var request = OAuth2TokenRequestWriter.WriteRefreshToken(client, "tGzv3JOkF0XG5Qx2TlKWIA", null);

        Assert.AreEqual(
            new OAuth2FormRequest(
                "grant_type=refresh_token&refresh_token=tGzv3JOkF0XG5Qx2TlKWIA&client_id=s6BhdRkqt3&client_secret=7Fjfp0ZBr1KtDRbnfVdmIw",
                null),
            request);
    }

    [TestMethod]
    public void WriteRefreshToken_WithScope_SendsScopeAfterTheToken()
    {
        var request = OAuth2TokenRequestWriter.WriteRefreshToken(BasicClient, "tGzv3JOkF0XG5Qx2TlKWIA", "read");

        Assert.AreEqual("grant_type=refresh_token&refresh_token=tGzv3JOkF0XG5Qx2TlKWIA&scope=read", request.Body);
    }

    [TestMethod]
    public void WriteDeviceCode_Rfc8628Section34Example_SendsDeviceCodeGrant()
    {
        var request = OAuth2TokenRequestWriter.WriteDeviceCode(PublicClient, "GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS");

        Assert.AreEqual(
            new OAuth2FormRequest(
                "grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code"
                    + "&device_code=GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS&client_id=1406020730",
                null),
            request);
    }

    [TestMethod]
    public void WriteDeviceAuthorization_Rfc8628Section31Example_SendsScopeAndClientId()
    {
        // §3.1's example sends client_id first; the order of the parameters does not matter.
        var request = OAuth2TokenRequestWriter.WriteDeviceAuthorization(PublicClient, "example_scope");

        Assert.AreEqual(new OAuth2FormRequest("scope=example_scope&client_id=1406020730", null), request);
    }

    [TestMethod]
    public void WriteDeviceAuthorization_WithoutScope_SendsOnlyTheClient()
    {
        var request = OAuth2TokenRequestWriter.WriteDeviceAuthorization(BasicClient, null);

        Assert.AreEqual(new OAuth2FormRequest(string.Empty, "Basic czZCaGRSa3F0MzpnWDFmQmF0M2JW"), request);
    }

    [TestMethod]
    public void WriteClientCredentials_BasicClientWithReservedCharacters_FormEncodesBeforeBase64()
    {
        // §2.3.1: the identifier and password are form-encoded, then used as Basic's user and password.
        var client = new OAuth2Client("a b", "c:d", OAuth2ClientAuthentication.Basic);

        var request = OAuth2TokenRequestWriter.WriteClientCredentials(client, null);

        Assert.AreEqual("Basic " + Convert.ToBase64String("a+b:c%3Ad"u8.ToArray()), request.Authorization);
    }
}
