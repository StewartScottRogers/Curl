namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// Pins the reading of the token response against RFC 6749 §5.1, and that a malformed body is a failure
/// with its reason.
/// </summary>
[TestClass]
public sealed class OAuth2TokenResponseParserTests
{
    [TestMethod]
    public void Parse_Section51Example_ReadsEveryMember()
    {
        const string Body = """
            {
              "access_token":"2YotnFZFEjr1zCsicMWpAA",
              "token_type":"example",
              "expires_in":3600,
              "refresh_token":"tGzv3JOkF0XG5Qx2TlKWIA",
              "example_parameter":"example_value"
            }
            """;

        var result = OAuth2TokenResponseParser.Parse(Body);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Failure);
        Assert.AreEqual(new OAuth2TokenResponse("2YotnFZFEjr1zCsicMWpAA", "example", 3600, "tGzv3JOkF0XG5Qx2TlKWIA", null), result.Value);
    }

    [TestMethod]
    public void Parse_OnlyRequiredMembersAndNulls_ReadsTheOptionalOnesAsLeftOut()
    {
        var result = OAuth2TokenResponseParser.Parse("""{"access_token":"a","token_type":"Bearer","expires_in":null,"scope":"read"}""");

        Assert.AreEqual(new OAuth2TokenResponse("a", "Bearer", null, null, "read"), result.Value);
    }

    [TestMethod]
    [DataRow("not json", "The body is not valid JSON.")]
    [DataRow("[]", "The body is not a JSON object.")]
    [DataRow("""{"token_type":"Bearer"}""", "The body has no \"access_token\".")]
    [DataRow("""{"access_token":1,"token_type":"Bearer"}""", "The body's \"access_token\" is not a string.")]
    [DataRow("""{"access_token":"a","token_type":"Bearer","expires_in":"3600"}""", "The body's \"expires_in\" is not an integer.")]
    [DataRow("""{"access_token":"a","token_type":"Bearer","expires_in":1.5}""", "The body's \"expires_in\" is not an integer.")]
    public void Parse_MalformedBody_FailsWithTheReason(string body, string failure)
    {
        var result = OAuth2TokenResponseParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Value);
        Assert.AreEqual(failure, result.Failure);
    }
}
