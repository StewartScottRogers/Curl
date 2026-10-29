namespace Curl.Extension.S3.Signing;

/// <summary>The parts of an S3 request that AWS Signature Version 4 signs.</summary>
/// <param name="Method">The HTTP method, such as <c>GET</c> or <c>PUT</c>.</param>
/// <param name="Path">The absolute path before percent-encoding, such as <c>/test$file.text</c>.</param>
/// <param name="QueryParameters">The query parameters before percent-encoding, in any order; a parameter with no value has an empty value.</param>
/// <param name="Headers">The headers to sign, which must include <c>host</c>; <c>x-amz-date</c> and <c>x-amz-content-sha256</c> are added by the signer.</param>
/// <param name="PayloadHash">The lower-case hexadecimal SHA-256 of the payload, or <see cref="S3RequestSigner.UnsignedPayload"/>.</param>
public sealed record S3SigningRequest(
    string Method,
    string Path,
    IReadOnlyList<KeyValuePair<string, string>> QueryParameters,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string PayloadHash);
