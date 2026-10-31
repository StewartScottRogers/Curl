namespace Curl.Extension.Protocol.GoogleCloudStorage.Xml;

/// <summary>One <c>Contents</c> entry of a <c>ListBucketResult</c>: an object the listing holds.</summary>
/// <param name="Key">The object's name.</param>
/// <param name="Size">The object's size in bytes.</param>
/// <param name="LastModified">When the object was last written.</param>
/// <param name="ETag">The object's entity tag, quotes included as Cloud Storage sends it.</param>
public sealed record GcsListedObject(string Key, long Size, DateTimeOffset LastModified, string ETag);
