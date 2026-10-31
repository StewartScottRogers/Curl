namespace Curl.Extension.Protocol.S3.Xml;

/// <summary>One page of a ListObjectsV2 listing, read from its <c>ListBucketResult</c> body.</summary>
/// <param name="Objects">The <c>Contents</c> entries, in the order S3 sent them.</param>
/// <param name="CommonPrefixes">The <c>CommonPrefixes/Prefix</c> values, in the order S3 sent them.</param>
/// <param name="IsTruncated">Whether more keys follow this page.</param>
/// <param name="NextContinuationToken">The token that asks for the next page, or <see langword="null"/> when S3 sent none.</param>
public sealed record S3ObjectListing(
    IReadOnlyList<S3ListedObject> Objects,
    IReadOnlyList<string> CommonPrefixes,
    bool IsTruncated,
    string? NextContinuationToken);
