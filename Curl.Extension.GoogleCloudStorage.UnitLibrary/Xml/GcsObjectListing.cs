namespace Curl.Extension.GoogleCloudStorage.Xml;

/// <summary>One page of an XML API "List objects" listing, read from its <c>ListBucketResult</c> body.</summary>
/// <param name="Prefix">The <c>Prefix</c> the listing was asked for, or empty when Cloud Storage sent none.</param>
/// <param name="Delimiter">The <c>Delimiter</c> the listing was asked for, or <see langword="null"/> when Cloud Storage sent none.</param>
/// <param name="Objects">The <c>Contents</c> entries, in the order Cloud Storage sent them.</param>
/// <param name="CommonPrefixes">The <c>CommonPrefixes/Prefix</c> values, in the order Cloud Storage sent them.</param>
/// <param name="IsTruncated">Whether more objects follow this page.</param>
/// <param name="ContinuationValue">
/// The value that asks for the next page - <c>NextContinuationToken</c> when Cloud Storage sent one, else
/// <c>NextMarker</c> - or <see langword="null"/> when it sent neither.
/// </param>
public sealed record GcsObjectListing(
    string Prefix,
    string? Delimiter,
    IReadOnlyList<GcsListedObject> Objects,
    IReadOnlyList<string> CommonPrefixes,
    bool IsTruncated,
    string? ContinuationValue);
