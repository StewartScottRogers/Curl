namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>One page of a List Blobs response.</summary>
/// <param name="Blobs">The blobs on the page, in the order listed.</param>
/// <param name="BlobPrefixes">The names of the <c>BlobPrefix</c> entries a delimited listing rolls blobs up into.</param>
/// <param name="NextMarker">The marker that asks for the next page, or <see langword="null"/> when this page is the last.</param>
public sealed record AzureBlobListing(IReadOnlyList<AzureListedBlob> Blobs, IReadOnlyList<string> BlobPrefixes, string? NextMarker);
