namespace Curl.Conformance.Extended.S3;

/// <summary>
/// Decides whether the reference S3 endpoint serves a request, before it looks at the operation:
/// <see cref="S3WellFormedAuthorization" /> checks only the <c>Authorization</c> header's form, and
/// signature verification (BL-4499, <c>S3/Signing/</c>) plugs in here.
/// </summary>
public interface IS3RequestAuthorizer
{
    /// <summary>Checks <paramref name="request" />.</summary>
    /// <param name="request">The request curl sent.</param>
    /// <returns>The error to answer with; <see langword="null" /> to serve the request.</returns>
    S3ReferenceError? Authorize(S3ReferenceRequest request);
}
