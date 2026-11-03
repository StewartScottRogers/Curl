namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>
/// Decides whether the Cloud Storage XML API reference endpoint serves a request, before it reads the bucket. Anonymous requests
/// (<see cref="GcsAnonymousReferenceAuthorizer" />) and OAuth 2.0 bearer tokens (<see cref="GcsBearerReferenceAuthorizer" />) are here;
/// V4 HMAC signatures (BL-4476) add their own in <c>Signing/</c>.
/// </summary>
public interface IGcsReferenceAuthorizer
{
    /// <summary>Gives the response that refuses <paramref name="request" />, or <see langword="null" /> to serve it.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The refusal, or <see langword="null" />.</returns>
    GcsReferenceResponse? Refuse(GcsReferenceRequest request);
}
