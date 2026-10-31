namespace Curl.Extension.Protocol.OciRegistry.Auth;

/// <summary>Pins the token request against the Docker/Distribution token authentication specification, "Requesting a Token".</summary>
[TestClass]
public sealed class OciTokenRequestBuilderTests
{
    private static readonly OciBearerChallenge SpecificationChallenge =
        new("https://auth.docker.io/token", "registry.docker.io", "repository:samalba/my-app:pull,push");

    [TestMethod]
    public void Build_SpecificationExampleAnonymous_GetsTheRealmWithServiceAndScope()
    {
        var request = OciTokenRequestBuilder.Build(SpecificationChallenge, null);

        Assert.AreEqual("GET", request?.Method);
        Assert.AreEqual(
            "https://auth.docker.io/token?service=registry.docker.io&scope=repository%3Asamalba%2Fmy-app%3Apull%2Cpush",
            request?.Url.AbsoluteUri);
        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("Accept", "application/json") }, request!.Headers.ToArray());
        Assert.IsNull(request.Body);
    }

    [TestMethod]
    public void Build_WithUserAndPassword_SendsBasicCredentials()
    {
        var request = OciTokenRequestBuilder.Build(SpecificationChallenge, "Aladdin:open sesame");

        Assert.AreEqual(new KeyValuePair<string, string>("Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ=="), request?.Headers[1]);
    }

    [TestMethod]
    public void Build_RealmWithQueryAndNoServiceOrScope_KeepsTheRealmsQuery()
    {
        var withQuery = OciTokenRequestBuilder.Build(new OciBearerChallenge("http://r.example:8080/token?tenant=a", null, "x y"), null);
        var bare = OciTokenRequestBuilder.Build(new OciBearerChallenge("https://r.example/token", null, null), null);

        Assert.AreEqual("http://r.example:8080/token?tenant=a&scope=x%20y", withQuery?.Url.AbsoluteUri);
        Assert.AreEqual("https://r.example/token", bare?.Url.AbsoluteUri);
    }

    [TestMethod]
    public void Build_RelativeRealm_ReturnsNull()
    {
        var request = OciTokenRequestBuilder.Build(new OciBearerChallenge("/token", null, null), null);

        Assert.IsNull(request);
    }

    [TestMethod]
    public void Build_AbsoluteRealmThatIsNotHttp_ReturnsNull()
    {
        var request = OciTokenRequestBuilder.Build(new OciBearerChallenge("file:///token", null, null), null);

        Assert.IsNull(request);
    }

    [TestMethod]
    public void Build_NullChallenge_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => OciTokenRequestBuilder.Build(null!, null));
    }
}
