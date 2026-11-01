using System.Xml.Linq;

namespace Curl.Extended.Protocol.S3.Xml;

/// <summary>
/// Reads the <c>ListBucketResult</c> body of the Amazon S3 API reference "ListObjectsV2": its
/// <c>Contents</c> (<c>Key</c>, <c>Size</c>, <c>LastModified</c>, <c>ETag</c>),
/// <c>CommonPrefixes/Prefix</c>, <c>IsTruncated</c> and <c>NextContinuationToken</c>.
/// </summary>
public static class S3ListBucketResultParser
{
    /// <summary>Reads <paramref name="xml"/> as one listing page.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The page, or why the body could not be read.</returns>
    public static S3XmlParseResult<S3ObjectListing> Parse(string xml) => S3XmlBody.Parse(xml, "ListBucketResult", ReadListing);

    private static S3ObjectListing ReadListing(XElement root) => new(
        [.. S3XmlBody.Children(root, "Contents").Select(ReadObject)],
        [.. S3XmlBody.Children(root, "CommonPrefixes").Select(prefixes => S3XmlBody.RequiredText(prefixes, "Prefix"))],
        S3XmlBody.RequiredBoolean(root, "IsTruncated"),
        S3XmlBody.OptionalText(root, "NextContinuationToken"));

    private static S3ListedObject ReadObject(XElement contents) => new(
        S3XmlBody.RequiredText(contents, "Key"),
        S3XmlBody.RequiredInteger(contents, "Size"),
        S3XmlBody.RequiredTime(contents, "LastModified"),
        S3XmlBody.RequiredText(contents, "ETag"));
}
