namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// Writes the access token requests of the three flows and of a refresh (RFC 6749, RFC 7636, RFC 8628),
/// and the device authorization request (RFC 8628 §3.1).
/// </summary>
public static class OAuth2TokenRequestWriter
{
    /// <summary>The <c>grant_type</c> of a device access token request (RFC 8628 §3.4).</summary>
    public const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    /// <summary>Writes a client credentials grant request (RFC 6749 §4.4.2).</summary>
    /// <param name="client">The client.</param>
    /// <param name="scope">The scope asked for, or <see langword="null"/> to leave it out.</param>
    /// <returns>The request.</returns>
    public static OAuth2FormRequest WriteClientCredentials(OAuth2Client client, string? scope)
    {
        var fields = GrantFields("client_credentials");
        AddIfPresent(fields, "scope", scope);
        return OAuth2FormRequest.ForClient(client, fields);
    }

    /// <summary>Writes an authorization code grant request with its PKCE verifier (RFC 6749 §4.1.3, RFC 7636 §4.5).</summary>
    /// <param name="client">The client.</param>
    /// <param name="code">The authorization code.</param>
    /// <param name="redirectUri">The redirection URI the authorization request sent, or <see langword="null"/> when it sent none.</param>
    /// <param name="codeVerifier">The code verifier whose challenge the authorization request sent.</param>
    /// <returns>The request.</returns>
    public static OAuth2FormRequest WriteAuthorizationCode(OAuth2Client client, string code, string? redirectUri, string codeVerifier)
    {
        var fields = GrantFields("authorization_code");
        fields.Add(new("code", code));
        AddIfPresent(fields, "redirect_uri", redirectUri);
        fields.Add(new("code_verifier", codeVerifier));
        return OAuth2FormRequest.ForClient(client, fields);
    }

    /// <summary>Writes a refresh request (RFC 6749 §6).</summary>
    /// <param name="client">The client.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="scope">The scope asked for, or <see langword="null"/> to keep the one first granted.</param>
    /// <returns>The request.</returns>
    public static OAuth2FormRequest WriteRefreshToken(OAuth2Client client, string refreshToken, string? scope)
    {
        var fields = GrantFields("refresh_token");
        fields.Add(new("refresh_token", refreshToken));
        AddIfPresent(fields, "scope", scope);
        return OAuth2FormRequest.ForClient(client, fields);
    }

    /// <summary>Writes a device access token request (RFC 8628 §3.4).</summary>
    /// <param name="client">The client.</param>
    /// <param name="deviceCode">The device verification code.</param>
    /// <returns>The request.</returns>
    public static OAuth2FormRequest WriteDeviceCode(OAuth2Client client, string deviceCode)
    {
        var fields = GrantFields(DeviceCodeGrantType);
        fields.Add(new("device_code", deviceCode));
        return OAuth2FormRequest.ForClient(client, fields);
    }

    /// <summary>Writes a device authorization request (RFC 8628 §3.1).</summary>
    /// <param name="client">The client.</param>
    /// <param name="scope">The scope asked for, or <see langword="null"/> to leave it out.</param>
    /// <returns>The request.</returns>
    public static OAuth2FormRequest WriteDeviceAuthorization(OAuth2Client client, string? scope)
    {
        List<KeyValuePair<string, string>> fields = [];
        AddIfPresent(fields, "scope", scope);
        return OAuth2FormRequest.ForClient(client, fields);
    }

    private static List<KeyValuePair<string, string>> GrantFields(string grantType) => [new("grant_type", grantType)];

    private static void AddIfPresent(List<KeyValuePair<string, string>> fields, string name, string? value)
    {
        if (value is not null)
        {
            fields.Add(new(name, value));
        }
    }
}
