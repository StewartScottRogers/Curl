namespace Curl.Extension.OAuth2.Wire;

/// <summary>Pins the authorization request URL against RFC 6749 §3.1 and §4.1.1 and RFC 7636 §4.3.</summary>
[TestClass]
public sealed class OAuth2AuthorizationRequestTests
{
    [TestMethod]
    public void BuildUrl_Section411ExampleWithChallenge_AddsEveryParameterToTheQuery()
    {
        var request = new OAuth2AuthorizationRequest(
            "https://server.example.com/authorize",
            "s6BhdRkqt3",
            "https://client.example.com/cb",
            "read write",
            "xyz",
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");

        Assert.AreEqual(
            "https://server.example.com/authorize?response_type=code&client_id=s6BhdRkqt3"
                + "&redirect_uri=https%3A%2F%2Fclient.example.com%2Fcb&scope=read+write&state=xyz"
                + "&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256",
            request.BuildUrl());
    }

    [TestMethod]
    public void BuildUrl_EndpointWithQueryAndNoRedirectOrScope_KeepsTheQuery()
    {
        var request = new OAuth2AuthorizationRequest("https://server.example.com/authorize?tenant=a", "c", null, null, "s", "x");

        Assert.AreEqual(
            "https://server.example.com/authorize?tenant=a&response_type=code&client_id=c&state=s&code_challenge=x&code_challenge_method=S256",
            request.BuildUrl());
    }
}
