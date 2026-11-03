namespace Curl.Conformance.Extended.S3;

/// <summary>
/// Serves an anonymous request and one whose <c>Authorization</c> header has the Signature Version 4
/// form (<c>AWS4-HMAC-SHA256 Credential=...</c>), without checking the signature; any other
/// <c>Authorization</c> header is <c>400 AuthorizationHeaderMalformed</c>.
/// </summary>
public sealed class S3WellFormedAuthorization : IS3RequestAuthorizer
{
    private const string SignatureVersion4Prefix = "AWS4-HMAC-SHA256 Credential=";

    /// <inheritdoc />
    public S3ReferenceError? Authorize(S3ReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Headers.TryGetValue("Authorization", out string? authorization) && !authorization.StartsWith(SignatureVersion4Prefix, StringComparison.Ordinal)
            ? new S3ReferenceError(400, "AuthorizationHeaderMalformed", "The authorization header is malformed.")
            : null;
    }
}
