namespace Curl.Extension.OAuth2.Wire;

/// <summary>Pins the reading of the error response against RFC 6749 §5.2 and RFC 8628 §3.5.</summary>
[TestClass]
public sealed class OAuth2ErrorResponseParserTests
{
    [TestMethod]
    public void Parse_Section52Example_ReadsTheError()
    {
        var result = OAuth2ErrorResponseParser.Parse("""{"error":"invalid_request"}""");

        Assert.AreEqual(new OAuth2ErrorResponse("invalid_request", OAuth2ErrorCode.InvalidRequest, null, null), result.Value);
    }

    [TestMethod]
    [DataRow("invalid_client", OAuth2ErrorCode.InvalidClient)]
    [DataRow("invalid_grant", OAuth2ErrorCode.InvalidGrant)]
    [DataRow("unauthorized_client", OAuth2ErrorCode.UnauthorizedClient)]
    [DataRow("unsupported_grant_type", OAuth2ErrorCode.UnsupportedGrantType)]
    [DataRow("invalid_scope", OAuth2ErrorCode.InvalidScope)]
    [DataRow("authorization_pending", OAuth2ErrorCode.AuthorizationPending)]
    [DataRow("slow_down", OAuth2ErrorCode.SlowDown)]
    [DataRow("access_denied", OAuth2ErrorCode.AccessDenied)]
    [DataRow("expired_token", OAuth2ErrorCode.ExpiredToken)]
    [DataRow("something_else", OAuth2ErrorCode.Unrecognized)]
    public void Parse_EachRfcErrorCode_ReadsItsCode(string error, OAuth2ErrorCode code)
    {
        var result = OAuth2ErrorResponseParser.Parse($$"""{"error":"{{error}}","error_description":"d","error_uri":"https://e/x"}""");

        Assert.AreEqual(new OAuth2ErrorResponse(error, code, "d", "https://e/x"), result.Value);
    }

    [TestMethod]
    public void Parse_NoError_FailsWithTheReason()
    {
        Assert.AreEqual("The body has no \"error\".", OAuth2ErrorResponseParser.Parse("{}").Failure);
    }
}
