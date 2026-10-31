using System.Xml.Linq;

namespace Curl.Extension.AzureStorage.Bodies;

/// <summary>
/// Reads the <c>EnumerationResults</c> body of the Blob service REST API "List Blobs": its
/// <c>Blobs/Blob</c> entries (<c>Name</c>, <c>Properties/Content-Length</c>, <c>Last-Modified</c>,
/// <c>Etag</c>), its <c>Blobs/BlobPrefix/Name</c> entries and its <c>NextMarker</c>.
/// </summary>
public static class AzureBlobListingParser
{
    /// <summary>Reads <paramref name="xml"/> as one listing page.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The page, or why the body could not be read.</returns>
    public static AzureStorageBodyParseResult<AzureBlobListing> Parse(string xml) =>
        AzureStorageXmlBody.Parse(xml, "EnumerationResults", ReadListing);

    private static AzureBlobListing ReadListing(XElement root)
    {
        var blobs = AzureStorageXmlBody.RequiredChild(root, "Blobs");
        return new(
            [.. AzureStorageXmlBody.Children(blobs, "Blob").Select(ReadBlob)],
            [.. AzureStorageXmlBody.Children(blobs, "BlobPrefix").Select(prefix => AzureStorageXmlBody.RequiredText(prefix, "Name"))],
            AzureStorageXmlBody.OptionalText(root, "NextMarker"));
    }

    private static AzureListedBlob ReadBlob(XElement blob)
    {
        var properties = AzureStorageXmlBody.RequiredChild(blob, "Properties");
        return new(
            AzureStorageXmlBody.RequiredText(blob, "Name"),
            AzureStorageXmlBody.RequiredInteger(properties, "Content-Length"),
            AzureStorageXmlBody.RequiredHttpTime(properties, "Last-Modified"),
            AzureStorageXmlBody.RequiredText(properties, "Etag"));
    }
}
