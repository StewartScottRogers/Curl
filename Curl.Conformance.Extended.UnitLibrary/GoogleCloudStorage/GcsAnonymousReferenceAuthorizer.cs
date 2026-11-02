namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>Serves every request, as Cloud Storage serves a bucket open to all users without an <c>Authorization</c> header.</summary>
public sealed class GcsAnonymousReferenceAuthorizer : IGcsReferenceAuthorizer
{
    /// <inheritdoc />
    public GcsReferenceResponse? Refuse(GcsReferenceRequest request) => null;
}
