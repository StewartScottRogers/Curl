using System.Xml.Linq;

namespace Curl.Extension.GoogleCloudStorage.Xml;

/// <summary>
/// Reads the <c>ListBucketResult</c> body of the Cloud Storage XML API "List objects": its
/// <c>Prefix</c>, <c>Delimiter</c>, <c>Contents</c> (<c>Key</c>, <c>Size</c>, <c>LastModified</c>,
/// <c>ETag</c>), <c>CommonPrefixes/Prefix</c>, <c>IsTruncated</c> and the continuation value
/// (<c>NextContinuationToken</c> for <c>list-type=2</c>, else <c>NextMarker</c>).
/// </summary>
public static class GcsListBucketResultParser
{
    /// <summary>Reads <paramref name="xml"/> as one listing page.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The page, or why the body could not be read.</returns>
    public static GcsXmlParseResult<GcsObjectListing> Parse(string xml) => GcsXmlBody.Parse(xml, "ListBucketResult", ReadListing);

    private static GcsObjectListing ReadListing(XElement root) => new(
        GcsXmlBody.OptionalText(root, "Prefix") ?? string.Empty,
        GcsXmlBody.OptionalText(root, "Delimiter"),
        [.. GcsXmlBody.Children(root, "Contents").Select(ReadObject)],
        [.. GcsXmlBody.Children(root, "CommonPrefixes").Select(prefixes => GcsXmlBody.RequiredText(prefixes, "Prefix"))],
        GcsXmlBody.RequiredBoolean(root, "IsTruncated"),
        GcsXmlBody.OptionalText(root, "NextContinuationToken") ?? GcsXmlBody.OptionalText(root, "NextMarker"));

    private static GcsListedObject ReadObject(XElement contents) => new(
        GcsXmlBody.RequiredText(contents, "Key"),
        GcsXmlBody.RequiredInteger(contents, "Size"),
        GcsXmlBody.RequiredTime(contents, "LastModified"),
        GcsXmlBody.RequiredText(contents, "ETag"));
}
