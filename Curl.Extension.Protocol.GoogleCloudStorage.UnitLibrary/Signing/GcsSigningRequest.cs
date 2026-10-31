namespace Curl.Extension.Protocol.GoogleCloudStorage.Signing;

/// <summary>The parts of a Cloud Storage XML API request that the V4 signing process signs.</summary>
/// <param name="Method">The HTTP method, such as <c>GET</c> or <c>PUT</c>.</param>
/// <param name="Path">The absolute path before percent-encoding, such as <c>/example-bucket/cat pics/tabby.jpeg</c>.</param>
/// <param name="QueryParameters">The query parameters before percent-encoding, in any order; a parameter with no value has an empty value.</param>
/// <param name="Headers">The headers to sign, which must include <c>host</c>; <c>x-goog-date</c> and <c>x-goog-content-sha256</c> are added by the signer.</param>
/// <param name="PayloadHash">The lower-case hexadecimal SHA-256 of the payload, or <see cref="GcsRequestSigner.UnsignedPayload"/>.</param>
public sealed record GcsSigningRequest(
    string Method,
    string Path,
    IReadOnlyList<KeyValuePair<string, string>> QueryParameters,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string PayloadHash);
