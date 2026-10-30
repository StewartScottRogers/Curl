namespace Curl.Extension.AzureStorage.Bodies;

/// <summary>One <c>Blob</c> entry of a List Blobs <c>EnumerationResults</c>: a blob the listing holds.</summary>
/// <param name="Name">The blob's name.</param>
/// <param name="ContentLength">The blob's size in bytes (<c>Properties/Content-Length</c>).</param>
/// <param name="LastModified">When the blob was last written (<c>Properties/Last-Modified</c>).</param>
/// <param name="ETag">The blob's entity tag (<c>Properties/Etag</c>), as Azure sends it.</param>
public sealed record AzureListedBlob(string Name, long ContentLength, DateTimeOffset LastModified, string ETag);
