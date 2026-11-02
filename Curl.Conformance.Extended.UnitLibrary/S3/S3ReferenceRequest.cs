namespace Curl.Conformance.Extended.S3;

/// <summary>One HTTP request curl sent the reference S3 endpoint, with the bucket and key its addressing names.</summary>
/// <param name="Method">The request method.</param>
/// <param name="Target">The request target as sent, percent-encoding and query included.</param>
/// <param name="Query">The query parameters, decoded.</param>
/// <param name="Headers">The headers, by name in any case.</param>
/// <param name="Body">The body; empty when there is none.</param>
/// <param name="Bucket">The bucket: the <c>Host</c> label before the service host (virtual-hosted style), else the path's first segment (path style).</param>
/// <param name="Key">The object key, decoded; empty for the bucket itself.</param>
/// <param name="VirtualHosted">Whether the <c>Host</c> header named the bucket.</param>
public sealed record S3ReferenceRequest(
    string Method,
    string Target,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body,
    string Bucket,
    string Key,
    bool VirtualHosted);
