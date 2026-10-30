namespace Curl.Extension.OAuth2.Wire;

/// <summary>Reads a successful access token response body (RFC 6749 §5.1).</summary>
public static class OAuth2TokenResponseParser
{
    /// <summary>Reads the body.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>The response, or why the body could not be read.</returns>
    public static OAuth2ParseResult<OAuth2TokenResponse> Parse(string body) =>
        OAuth2JsonBody.Read(body, response => new OAuth2TokenResponse(
            OAuth2JsonBody.ReadRequiredString(response, "access_token"),
            OAuth2JsonBody.ReadRequiredString(response, "token_type"),
            OAuth2JsonBody.ReadOptionalInteger(response, "expires_in"),
            OAuth2JsonBody.ReadOptionalString(response, "refresh_token"),
            OAuth2JsonBody.ReadOptionalString(response, "scope")));
}
