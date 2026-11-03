namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>
/// Serves only a request whose <c>Authorization</c> header is <c>Bearer</c> and the one OAuth 2.0 access token it was given (the XML API's
/// OAuth 2.0 authentication); any other request gets <c>401 AuthenticationRequired</c>.
/// </summary>
/// <param name="accessToken">The access token it accepts.</param>
public sealed class GcsBearerReferenceAuthorizer(string accessToken) : IGcsReferenceAuthorizer
{
    /// <inheritdoc />
    public GcsReferenceResponse? Refuse(GcsReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Headers.GetValueOrDefault("Authorization") == "Bearer " + accessToken
            ? null
            : GcsReferenceResponse.Error(401, "Unauthorized", "AuthenticationRequired", "Authentication required.", "The request has no valid OAuth 2.0 bearer token.");
    }
}
