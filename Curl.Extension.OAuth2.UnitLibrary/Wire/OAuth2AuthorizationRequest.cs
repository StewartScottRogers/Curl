namespace Curl.Extension.OAuth2.Wire;

/// <summary>An authorization code request with a PKCE challenge (RFC 6749 §4.1.1, RFC 7636 §4.3).</summary>
/// <param name="AuthorizationEndpoint">The authorization endpoint URI, which may have a query but no fragment.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="RedirectUri">The redirection URI, or <see langword="null"/> to leave it out.</param>
/// <param name="Scope">The scope asked for, or <see langword="null"/> to leave it out.</param>
/// <param name="State">The value the server sends back with the code, to bind it to this request.</param>
/// <param name="CodeChallenge">The <c>S256</c> code challenge.</param>
public sealed record OAuth2AuthorizationRequest(
    string AuthorizationEndpoint,
    string ClientId,
    string? RedirectUri,
    string? Scope,
    string State,
    string CodeChallenge)
{
    /// <summary>
    /// Builds the URL the user's browser is sent to: the endpoint with the request's parameters added
    /// to its query, keeping any query it has (RFC 6749 §3.1).
    /// </summary>
    /// <returns>The URL.</returns>
    public string BuildUrl()
    {
        List<KeyValuePair<string, string>> fields = [new("response_type", "code"), new("client_id", ClientId)];
        if (RedirectUri is not null)
        {
            fields.Add(new("redirect_uri", RedirectUri));
        }

        if (Scope is not null)
        {
            fields.Add(new("scope", Scope));
        }

        fields.Add(new("state", State));
        fields.Add(new("code_challenge", CodeChallenge));
        fields.Add(new("code_challenge_method", OAuth2Pkce.S256Method));
        var separator = AuthorizationEndpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return AuthorizationEndpoint + separator + OAuth2FormEncoder.EncodeFields(fields);
    }
}
