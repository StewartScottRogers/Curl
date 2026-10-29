namespace Curl.Extension.OAuth2.Wire;

/// <summary>Reads the device authorization response body (RFC 8628 §3.2).</summary>
public static class OAuth2DeviceAuthorizationResponseParser
{
    /// <summary>The polling interval in seconds when the response has none (§3.2).</summary>
    public const long DefaultInterval = 5;

    /// <summary>Reads the body.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>The response, or why the body could not be read.</returns>
    public static OAuth2ParseResult<OAuth2DeviceAuthorization> Parse(string body) =>
        OAuth2JsonBody.Read(body, response => new OAuth2DeviceAuthorization(
            OAuth2JsonBody.ReadRequiredString(response, "device_code"),
            OAuth2JsonBody.ReadRequiredString(response, "user_code"),
            OAuth2JsonBody.ReadRequiredString(response, "verification_uri"),
            OAuth2JsonBody.ReadOptionalString(response, "verification_uri_complete"),
            OAuth2JsonBody.ReadRequiredInteger(response, "expires_in"),
            OAuth2JsonBody.ReadOptionalInteger(response, "interval") ?? DefaultInterval));
}
