namespace Curl.Extension.OAuth2.Wire;

/// <summary>Reads an error response body (RFC 6749 §5.2, RFC 8628 §3.5).</summary>
public static class OAuth2ErrorResponseParser
{
    private static readonly Dictionary<string, OAuth2ErrorCode> Codes = new(StringComparer.Ordinal)
    {
        ["invalid_request"] = OAuth2ErrorCode.InvalidRequest,
        ["invalid_client"] = OAuth2ErrorCode.InvalidClient,
        ["invalid_grant"] = OAuth2ErrorCode.InvalidGrant,
        ["unauthorized_client"] = OAuth2ErrorCode.UnauthorizedClient,
        ["unsupported_grant_type"] = OAuth2ErrorCode.UnsupportedGrantType,
        ["invalid_scope"] = OAuth2ErrorCode.InvalidScope,
        ["authorization_pending"] = OAuth2ErrorCode.AuthorizationPending,
        ["slow_down"] = OAuth2ErrorCode.SlowDown,
        ["access_denied"] = OAuth2ErrorCode.AccessDenied,
        ["expired_token"] = OAuth2ErrorCode.ExpiredToken,
    };

    /// <summary>Reads the body.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>The response, or why the body could not be read.</returns>
    public static OAuth2ParseResult<OAuth2ErrorResponse> Parse(string body) =>
        OAuth2JsonBody.Read(body, response =>
        {
            var error = OAuth2JsonBody.ReadRequiredString(response, "error");
            return new OAuth2ErrorResponse(
                error,
                Codes.GetValueOrDefault(error, OAuth2ErrorCode.Unrecognized),
                OAuth2JsonBody.ReadOptionalString(response, "error_description"),
                OAuth2JsonBody.ReadOptionalString(response, "error_uri"));
        });
}
