namespace Curl.Extension.OciRegistry.Auth;

/// <summary>Pins the challenge parsing against the Docker/Distribution token authentication specification and RFC 9110 §11.</summary>
[TestClass]
public sealed class OciBearerChallengeParserTests
{
    [TestMethod]
    public void Parse_SpecificationExample_ReadsRealmServiceAndScopeWithItsComma()
    {
        var challenge = OciBearerChallengeParser.Parse(
            "Bearer realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:samalba/my-app:pull,push\"");

        Assert.AreEqual(new OciBearerChallenge("https://auth.docker.io/token", "registry.docker.io", "repository:samalba/my-app:pull,push"), challenge);
    }

    [TestMethod]
    public void Parse_TokensSpacesAndAnyCase_ReadsEachParameter()
    {
        var challenge = OciBearerChallengeParser.Parse("  bearer\tREALM = https://r.example/token , Service=reg ,, scope=\"a\\\"b\\\\c\"  ");

        Assert.AreEqual(new OciBearerChallenge("https://r.example/token", "reg", "a\"b\\c"), challenge);
    }

    [TestMethod]
    public void Parse_LastParameterAnUnquotedToken_ReadsItToTheEnd()
    {
        var challenge = OciBearerChallengeParser.Parse("Bearer service=reg,realm=https://r.example/token");

        Assert.AreEqual(new OciBearerChallenge("https://r.example/token", "reg", null), challenge);
    }

    [TestMethod]
    public void Parse_RealmOnlyWithDuplicate_KeepsTheFirstAndLeavesTheRestNull()
    {
        var challenge = OciBearerChallengeParser.Parse("Bearer realm=\"https://a.example/\", realm=\"https://b.example/\", error=\"insufficient_scope\"");

        Assert.AreEqual(new OciBearerChallenge("https://a.example/", null, null), challenge);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No field")]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("Basic realm=\"Registry\"", DisplayName = "Basic scheme")]
    [DataRow("Bearerrealm=\"x\"", DisplayName = "Scheme runs into the parameter")]
    [DataRow("Bearer", DisplayName = "No parameters")]
    [DataRow("Bearer service=\"reg\"", DisplayName = "No realm")]
    [DataRow("Bearer realm", DisplayName = "Parameter without =")]
    [DataRow("Bearer =x", DisplayName = "Parameter without a name")]
    [DataRow("Bearer realm=\"https://r.example/token", DisplayName = "Unclosed quoted string")]
    [DataRow("Bearer realm=\"x\\", DisplayName = "Backslash ends the value")]
    public void Parse_NotABearerChallengeWithRealm_ReturnsNull(string? value)
    {
        var challenge = OciBearerChallengeParser.Parse(value);

        Assert.IsNull(challenge);
    }
}
