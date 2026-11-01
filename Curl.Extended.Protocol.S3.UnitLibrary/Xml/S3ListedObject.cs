namespace Curl.Extended.Protocol.S3.Xml;

/// <summary>One <c>Contents</c> entry of a <c>ListBucketResult</c>: an object the listing holds.</summary>
/// <param name="Key">The object's key.</param>
/// <param name="Size">The object's size in bytes.</param>
/// <param name="LastModified">When the object was last written.</param>
/// <param name="ETag">The object's entity tag, quotes included as S3 sends it.</param>
public sealed record S3ListedObject(string Key, long Size, DateTimeOffset LastModified, string ETag);
