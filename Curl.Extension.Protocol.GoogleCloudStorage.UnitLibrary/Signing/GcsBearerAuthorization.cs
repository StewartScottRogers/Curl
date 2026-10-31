namespace Curl.Extension.Protocol.GoogleCloudStorage.Signing;

/// <summary>Builds the <c>Authorization</c> header that sends an OAuth 2.0 access token to the XML API (RFC 6750 section 2.1).</summary>
public static class GcsBearerAuthorization
{
    /// <summary>Returns the <c>Authorization: Bearer</c> header for <paramref name="accessToken"/>.</summary>
    /// <param name="accessToken">The OAuth 2.0 access token, which must not be empty, blank or contain a line break.</param>
    /// <returns>The <c>Authorization</c> header.</returns>
    public static KeyValuePair<string, string> Header(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        if (accessToken.AsSpan().ContainsAny('\r', '\n'))
        {
            throw new ArgumentException("An access token cannot contain a line break.", nameof(accessToken));
        }

        return new KeyValuePair<string, string>("Authorization", "Bearer " + accessToken.Trim());
    }
}
